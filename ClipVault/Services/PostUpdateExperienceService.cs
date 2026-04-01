using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace ClipVault.Services
{
    public sealed class PostUpdateAnnouncement
    {
        public string PreviousVersion { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string ChangelogText { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    public static class PostUpdateExperienceService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private static string AppDataFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClipVault");

        private static string PendingAnnouncementPath =>
            Path.Combine(AppDataFolder, "pending-update-announcement.json");

        public static void QueueAnnouncement(
            string? previousVersion,
            string? currentVersion,
            string changelogText)
        {
            Directory.CreateDirectory(AppDataFolder);

            var payload = new PostUpdateAnnouncement
            {
                PreviousVersion = previousVersion?.Trim() ?? string.Empty,
                CurrentVersion = currentVersion?.Trim() ?? string.Empty,
                ChangelogText = changelogText?.Trim() ?? string.Empty,
                CreatedUtc = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(payload, JsonOptions);
            File.WriteAllText(PendingAnnouncementPath, json);
        }

        public static void ShowPendingAnnouncement(Window owner)
        {
            if (!TryConsumeAnnouncement(out PostUpdateAnnouncement? announcement) || announcement is null)
                return;

            var window = new UpdateAnnouncementWindow(announcement)
            {
                Owner = owner
            };

            window.ShowDialog();
        }

        private static bool TryConsumeAnnouncement(out PostUpdateAnnouncement? announcement)
        {
            announcement = null;

            try
            {
                if (!File.Exists(PendingAnnouncementPath))
                    return false;

                string json = File.ReadAllText(PendingAnnouncementPath);
                announcement = JsonSerializer.Deserialize<PostUpdateAnnouncement>(json, JsonOptions);

                TryDeletePendingAnnouncement();

                return announcement is not null;
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Failed to load the pending post-update announcement.");
                TryDeletePendingAnnouncement();
                return false;
            }
        }

        private static void TryDeletePendingAnnouncement()
        {
            try
            {
                if (File.Exists(PendingAnnouncementPath))
                {
                    File.Delete(PendingAnnouncementPath);
                }
            }
            catch
            {
                // Never let cleanup break startup.
            }
        }
    }
}