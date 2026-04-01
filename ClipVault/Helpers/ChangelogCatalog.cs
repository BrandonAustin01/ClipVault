using System;
using System.Collections.Generic;
using System.Text;

namespace ClipVault.Helpers
{
    public static class ChangelogCatalog
    {
        private sealed class ChangelogEntry
        {
            public Version Version { get; init; } = new(1, 0, 0);
            public string VersionLabel { get; init; } = "1.0.0";
            public string Notes { get; init; } = string.Empty;
        }

        // Add a new entry here every release.
        private static readonly IReadOnlyList<ChangelogEntry> Entries = new List<ChangelogEntry>
    {
        new()
        {
            Version = new Version(1, 0, 0),
            VersionLabel = "1.0.0",
            Notes =
"""
Highlights
- Clipboard history
- Pinned items
- Reusable snippets
- Built-in log viewer
- Tray support
- Startup option
- Packaged updater support

Included in this release
- Main window UI polish
- Snippet editor polish
- Log viewer
- Dark ComboBox fix in the log viewer
- Stability and UX improvements

Summary
ClipVault 1.0.0 delivers the polished core experience and sets the foundation for future updates.
"""
        }

        // Example for future releases:
        // new()
        // {
        //     Version = new Version(1, 0, 1),
        //     VersionLabel = "1.0.1",
        //     Notes =
        // """
        // Improvements
        // - Added post-update welcome experience
        // - Added built-in changelog viewer
        // - Fixed ...
        // """
        // }
    };

        public static string BuildChangesSince(string? previousVersion, string? currentVersion)
        {
            Version? previous = ParseVersion(previousVersion);
            Version? current = ParseVersion(currentVersion);

            IEnumerable<ChangelogEntry> matchingEntries = Entries;

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
                Environment.NewLine + Environment.NewLine + "────────────────────────" + Environment.NewLine + Environment.NewLine,
                ordered.Select(x => $"v{x.VersionLabel}{Environment.NewLine}{Environment.NewLine}{x.Notes.Trim()}"));
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