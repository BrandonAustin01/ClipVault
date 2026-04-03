using System;
using System.IO;
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

        public static bool HasPendingAnnouncement()
        {
            try
            {
                return File.Exists(PendingAnnouncementPath);
            }
            catch
            {
                return false;
            }
        }

        public static void QueueAnnouncement(
            string? previousVersion,
            string? currentVersion,
            string changelogText)
        {
            try
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

                LogService.Info(
                    $"Queued post-update announcement at '{PendingAnnouncementPath}'. " +
                    $"Previous='{payload.PreviousVersion}', Current='{payload.CurrentVersion}'.");
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Failed to queue the post-update announcement.");
                throw;
            }
        }

        public static void ShowPendingAnnouncement(Window owner)
        {
            if (owner is null)
                throw new ArgumentNullException(nameof(owner));

            LogService.Info($"Checking for pending post-update announcement at '{PendingAnnouncementPath}'.");

            PostUpdateAnnouncement? announcement = TryReadAnnouncement();
            if (announcement is null)
            {
                LogService.Info("No pending post-update announcement was found.");
                return;
            }

            try
            {
                var window = new UpdateAnnouncementWindow(announcement)
                {
                    Owner = owner
                };

                LogService.Info(
                    $"Showing post-update announcement. " +
                    $"Previous='{announcement.PreviousVersion}', Current='{announcement.CurrentVersion}'.");

                window.ShowDialog();

                TryDeletePendingAnnouncement();

                LogService.Info("Post-update announcement displayed and cleared.");
            }
            catch (Exception ex)
            {
                LogService.Error(
                    ex,
                    "Failed while showing the pending post-update announcement. " +
                    "The pending file was left in place so ClipVault can try again next launch.");
            }
        }

        private static PostUpdateAnnouncement? TryReadAnnouncement()
        {
            try
            {
                if (!File.Exists(PendingAnnouncementPath))
                    return null;

                string json = File.ReadAllText(PendingAnnouncementPath);
                var announcement = JsonSerializer.Deserialize<PostUpdateAnnouncement>(json, JsonOptions);

                if (announcement is null)
                {
                    LogService.Warn("Pending post-update announcement file was present but could not be deserialized.");
                    TryDeletePendingAnnouncement();
                    return null;
                }

                LogService.Info($"Loaded pending post-update announcement from '{PendingAnnouncementPath}'.");
                return announcement;
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Failed to read the pending post-update announcement.");
                TryDeletePendingAnnouncement();
                return null;
            }
        }

        private static void TryDeletePendingAnnouncement()
        {
            try
            {
                if (!File.Exists(PendingAnnouncementPath))
                    return;

                File.Delete(PendingAnnouncementPath);
                LogService.Info($"Deleted pending post-update announcement at '{PendingAnnouncementPath}'.");
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Failed to delete the pending post-update announcement.");
            }
        }
    }
}