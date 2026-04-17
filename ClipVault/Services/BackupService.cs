using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClipVault.Models;

namespace ClipVault.Services;

public sealed class BackupDocument
{
    public string AppName { get; set; } = "ClipVault";
    public int SchemaVersion { get; set; } = 2;
    public string ExportedFromVersion { get; set; } = string.Empty;
    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    public BackupAppSettings Settings { get; set; } = new();
    public List<BackupClipboardEntry> Items { get; set; } = new();
}

public sealed class BackupAppSettings
{
    public bool LaunchOnStartup { get; set; }
    public bool ClipboardMonitoringEnabled { get; set; } = true;
    public int MaxHistoryItems { get; set; } = 100;
    public bool MinimizeToTray { get; set; }
    public bool CloseToTray { get; set; }
    public string Theme { get; set; } = "Dark";
    public string UpdateFeedUrl { get; set; } = string.Empty;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public bool DetectSensitiveClipboardContent { get; set; } = true;
    public bool MaskSensitiveClipboardContent { get; set; } = true;
    public bool ExcludeSensitiveClipboardContent { get; set; }
}

public sealed class BackupClipboardEntry
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Text";
    public string FullText { get; set; } = string.Empty;
    public bool IsSnippet { get; set; }
    public bool IsPinned { get; set; }
    public bool IsSensitive { get; set; }
    public bool IsSensitiveManual { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.Now;
}

public sealed class BackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string BuildDefaultFileName(string appVersion)
    {
        string versionPart = string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion.Trim();
        return $"ClipVault_Backup_{versionPart}_{DateTime.Now:yyyy-MM-dd_HHmmss}.cvxbackup.json";
    }

    public void ExportToFile(
        string filePath,
        string appVersion,
        AppSettings settings,
        IEnumerable<ClipboardEntry> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(items);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new BackupDocument
        {
            AppName = "ClipVault",
            SchemaVersion = 2,
            ExportedFromVersion = appVersion?.Trim() ?? string.Empty,
            ExportedAtUtc = DateTime.UtcNow,
            Settings = new BackupAppSettings
            {
                CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup,
                LaunchOnStartup = settings.LaunchOnStartup,
                ClipboardMonitoringEnabled = settings.ClipboardMonitoringEnabled,
                MaxHistoryItems = settings.MaxHistoryItems,
                MinimizeToTray = settings.MinimizeToTray,
                CloseToTray = settings.CloseToTray,
                Theme = settings.Theme ?? "Dark",
                UpdateFeedUrl = settings.UpdateFeedUrl ?? string.Empty,
                DetectSensitiveClipboardContent = settings.DetectSensitiveClipboardContent,
                MaskSensitiveClipboardContent = settings.MaskSensitiveClipboardContent,
                ExcludeSensitiveClipboardContent = settings.ExcludeSensitiveClipboardContent
            },
            Items = items.Select(item => new BackupClipboardEntry
            {
                Title = item.Title,
                Category = item.Category,
                FullText = item.FullText,
                IsSnippet = item.IsSnippet,
                IsPinned = item.IsPinned,
                IsSensitive = item.IsSensitive,
                IsSensitiveManual = item.IsSensitiveManual,
                CapturedAt = item.CapturedAt
            }).ToList()
        };

        string json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public BackupDocument LoadFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("The selected backup file could not be found.", filePath);

        string json = File.ReadAllText(filePath);

        var document = JsonSerializer.Deserialize<BackupDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("The backup file could not be read.");

        ValidateDocument(document);
        return document;
    }

    public AppSettings ToAppSettings(BackupDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new AppSettings
        {
            CheckForUpdatesOnStartup = document.Settings.CheckForUpdatesOnStartup,
            LaunchOnStartup = document.Settings.LaunchOnStartup,
            ClipboardMonitoringEnabled = document.Settings.ClipboardMonitoringEnabled,
            MaxHistoryItems = document.Settings.MaxHistoryItems < 1 ? 100 : document.Settings.MaxHistoryItems,
            MinimizeToTray = document.Settings.MinimizeToTray,
            CloseToTray = document.Settings.CloseToTray,
            Theme = string.IsNullOrWhiteSpace(document.Settings.Theme) ? "Dark" : document.Settings.Theme,
            UpdateFeedUrl = document.Settings.UpdateFeedUrl ?? string.Empty,
            DetectSensitiveClipboardContent = document.Settings.DetectSensitiveClipboardContent,
            MaskSensitiveClipboardContent = document.Settings.MaskSensitiveClipboardContent,
            ExcludeSensitiveClipboardContent = document.Settings.ExcludeSensitiveClipboardContent
        };
    }

    public List<ClipboardEntry> ToClipboardEntries(BackupDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.Items.Select(item => new ClipboardEntry
        {
            Title = string.IsNullOrWhiteSpace(item.Title) ? "Clipboard item" : item.Title,
            Category = string.IsNullOrWhiteSpace(item.Category) ? "Text" : item.Category,
            FullText = item.FullText ?? string.Empty,
            IsSnippet = item.IsSnippet,
            IsPinned = item.IsPinned,
            IsSensitive = item.IsSensitive,
            IsSensitiveManual = item.IsSensitiveManual,
            CapturedAt = item.CapturedAt
        }).ToList();
    }

    private static void ValidateDocument(BackupDocument document)
    {
        if (!string.Equals(document.AppName, "ClipVault", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This file is not a ClipVault backup.");

        if (document.SchemaVersion is not 1 and not 2)
            throw new InvalidOperationException($"Unsupported backup schema version: {document.SchemaVersion}.");

        document.Settings ??= new BackupAppSettings();
        document.Items ??= new List<BackupClipboardEntry>();
    }
}
