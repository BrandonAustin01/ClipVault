using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClipVault.Helpers
{
    public static class ChangelogCatalog
    {
        private sealed class ChangelogEntry
        {
            public Version Version { get; init; } = new(0, 0, 0);
            public string VersionLabel { get; init; } = string.Empty;
            public string Notes { get; init; } = string.Empty;
        }

        private const string ChangelogFileName = "CHANGELOG.txt";
        private const string SectionSeparator = "────────────────────────";

        public static string BuildChangesSince(string? previousVersion, string? currentVersion)
        {
            Version? previous = ParseVersion(previousVersion);
            Version? current = ParseVersion(currentVersion);

            var entries = LoadEntries();

            IEnumerable<ChangelogEntry> matchingEntries = entries;

            if (current is not null)
            {
                matchingEntries = matchingEntries.Where(x => x.Version <= current);
            }

            if (previous is not null)
            {
                matchingEntries = matchingEntries.Where(x => x.Version > previous);
            }

            var ordered = matchingEntries
                .OrderByDescending(x => x.Version)
                .ToList();

            if (ordered.Count == 0)
            {
                return string.IsNullOrWhiteSpace(currentVersion)
                    ? "ClipVault was updated successfully."
                    : $"ClipVault was updated to v{currentVersion}.";
            }

            return string.Join(
                Environment.NewLine + Environment.NewLine + SectionSeparator + Environment.NewLine + Environment.NewLine,
                ordered.Select(x => $"v{x.VersionLabel}{Environment.NewLine}{Environment.NewLine}{x.Notes.Trim()}"));
        }

        private static IReadOnlyList<ChangelogEntry> LoadEntries()
        {
            try
            {
                string changelogPath = Path.Combine(AppContext.BaseDirectory, ChangelogFileName);

                if (!File.Exists(changelogPath))
                {
                    return Array.Empty<ChangelogEntry>();
                }

                string raw = File.ReadAllText(changelogPath);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    return Array.Empty<ChangelogEntry>();
                }

                var sections = raw
                    .Split(
                        new[] { SectionSeparator },
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                var entries = new List<ChangelogEntry>();

                foreach (string section in sections)
                {
                    var lines = section
                        .Replace("\r\n", "\n")
                        .Split('\n')
                        .Select(x => x.TrimEnd())
                        .ToList();

                    string? versionLine = lines.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    if (string.IsNullOrWhiteSpace(versionLine))
                    {
                        continue;
                    }

                    string versionLabel = versionLine.Trim().TrimStart('v', 'V');

                    if (!Version.TryParse(versionLabel, out Version? parsedVersion))
                    {
                        continue;
                    }

                    int firstContentIndex = lines.FindIndex(x => !string.IsNullOrWhiteSpace(x));
                    string notes = firstContentIndex >= 0 && firstContentIndex + 1 < lines.Count
                        ? string.Join(Environment.NewLine, lines.Skip(firstContentIndex + 1)).Trim()
                        : string.Empty;

                    entries.Add(new ChangelogEntry
                    {
                        Version = parsedVersion,
                        VersionLabel = versionLabel,
                        Notes = notes
                    });
                }

                return entries;
            }
            catch
            {
                return Array.Empty<ChangelogEntry>();
            }
        }

        private static Version? ParseVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string cleaned = value.Trim().TrimStart('v', 'V');

            int dashIndex = cleaned.IndexOf('-');
            if (dashIndex >= 0)
            {
                cleaned = cleaned[..dashIndex];
            }

            return Version.TryParse(cleaned, out Version? version)
                ? version
                : null;
        }
    }
}