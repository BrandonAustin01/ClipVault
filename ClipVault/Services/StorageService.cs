using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClipVault.Models;
using Microsoft.Data.Sqlite;

namespace ClipVault.Services;

public sealed class StorageService
{
    private const int DefaultMaxHistoryItems = 100;

    public string DatabaseFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipVault",
            "Data",
            "clipvault.db");

    public void InitializeDatabase()
    {
        var directory = Path.GetDirectoryName(DatabaseFilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Could not determine ClipVault data directory.");

        Directory.CreateDirectory(directory);

        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS ClipboardItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Category TEXT NOT NULL,
                FullText TEXT NOT NULL,
                IsSnippet INTEGER NOT NULL DEFAULT 0,
                IsPinned INTEGER NOT NULL DEFAULT 0,
                CapturedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;

        command.ExecuteNonQuery();

        EnsureClipboardItemsColumn(connection, "IsSensitive", "INTEGER NOT NULL DEFAULT 0");
        EnsureClipboardItemsColumn(connection, "IsSensitiveManual", "INTEGER NOT NULL DEFAULT 0");

        EnsureDefaultSetting("MaxHistoryItems", DefaultMaxHistoryItems.ToString(CultureInfo.InvariantCulture));
        EnsureDefaultSetting("ClipboardMonitoringEnabled", "1");
        EnsureDefaultSetting("LaunchOnStartup", "0");
        EnsureDefaultSetting("MinimizeToTray", "0");
        EnsureDefaultSetting("CloseToTray", "0");
        EnsureDefaultSetting("CheckForUpdatesOnStartup", "1");
        EnsureDefaultSetting("DetectSensitiveClipboardContent", "1");
        EnsureDefaultSetting("MaskSensitiveClipboardContent", "1");
        EnsureDefaultSetting("ExcludeSensitiveClipboardContent", "0");
        EnsureDefaultSetting("Theme", "Midnight");
        EnsureDefaultSetting("UpdateFeedUrl", string.Empty);
    }

    public List<ClipboardEntry> LoadItems()
    {
        var items = new List<ClipboardEntry>();

        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Title, Category, FullText, IsSnippet, IsPinned, CapturedAt, IsSensitive, IsSensitiveManual
            FROM ClipboardItems
            ORDER BY datetime(CapturedAt) DESC, Id DESC;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string capturedAtRaw = reader.GetString(6);

            items.Add(new ClipboardEntry
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Category = reader.GetString(2),
                FullText = reader.GetString(3),
                IsSnippet = reader.GetInt32(4) == 1,
                IsPinned = reader.GetInt32(5) == 1,
                CapturedAt = DateTime.Parse(
                    capturedAtRaw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                IsSensitive = reader.GetInt32(7) == 1,
                IsSensitiveManual = reader.GetInt32(8) == 1
            });
        }

        return items;
    }

    public void ReplaceAllData(IEnumerable<ClipboardEntry> items, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(settings);

        var itemList = items.ToList();

        using var connection = CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        using (var deleteItemsCommand = connection.CreateCommand())
        {
            deleteItemsCommand.Transaction = transaction;
            deleteItemsCommand.CommandText = "DELETE FROM ClipboardItems;";
            deleteItemsCommand.ExecuteNonQuery();
        }

        using (var deleteSettingsCommand = connection.CreateCommand())
        {
            deleteSettingsCommand.Transaction = transaction;
            deleteSettingsCommand.CommandText = "DELETE FROM AppSettings;";
            deleteSettingsCommand.ExecuteNonQuery();
        }

        using (var insertItemCommand = connection.CreateCommand())
        {
            insertItemCommand.Transaction = transaction;
            insertItemCommand.CommandText =
                """
                INSERT INTO ClipboardItems (Title, Category, FullText, IsSnippet, IsPinned, CapturedAt, IsSensitive, IsSensitiveManual)
                VALUES ($title, $category, $fullText, $isSnippet, $isPinned, $capturedAt, $isSensitive, $isSensitiveManual);
                """;

            var titleParameter = insertItemCommand.Parameters.Add("$title", SqliteType.Text);
            var categoryParameter = insertItemCommand.Parameters.Add("$category", SqliteType.Text);
            var fullTextParameter = insertItemCommand.Parameters.Add("$fullText", SqliteType.Text);
            var isSnippetParameter = insertItemCommand.Parameters.Add("$isSnippet", SqliteType.Integer);
            var isPinnedParameter = insertItemCommand.Parameters.Add("$isPinned", SqliteType.Integer);
            var capturedAtParameter = insertItemCommand.Parameters.Add("$capturedAt", SqliteType.Text);
            var isSensitiveParameter = insertItemCommand.Parameters.Add("$isSensitive", SqliteType.Integer);
            var isSensitiveManualParameter = insertItemCommand.Parameters.Add("$isSensitiveManual", SqliteType.Integer);

            foreach (var item in itemList)
            {
                titleParameter.Value = item.Title;
                categoryParameter.Value = item.Category;
                fullTextParameter.Value = item.FullText;
                isSnippetParameter.Value = item.IsSnippet ? 1 : 0;
                isPinnedParameter.Value = item.IsPinned ? 1 : 0;
                capturedAtParameter.Value = item.CapturedAt.ToString("O", CultureInfo.InvariantCulture);
                isSensitiveParameter.Value = item.IsSensitive ? 1 : 0;
                isSensitiveManualParameter.Value = item.IsSensitiveManual ? 1 : 0;

                insertItemCommand.ExecuteNonQuery();
            }
        }

        SaveSettingInternal(connection, transaction, "LaunchOnStartup", settings.LaunchOnStartup ? "1" : "0");
        SaveSettingInternal(connection, transaction, "ClipboardMonitoringEnabled", settings.ClipboardMonitoringEnabled ? "1" : "0");
        SaveSettingInternal(connection, transaction, "MaxHistoryItems", settings.MaxHistoryItems.ToString(CultureInfo.InvariantCulture));
        SaveSettingInternal(connection, transaction, "MinimizeToTray", settings.MinimizeToTray ? "1" : "0");
        SaveSettingInternal(connection, transaction, "CloseToTray", settings.CloseToTray ? "1" : "0");
        SaveSettingInternal(connection, transaction, "CheckForUpdatesOnStartup", settings.CheckForUpdatesOnStartup ? "1" : "0");
        SaveSettingInternal(connection, transaction, "DetectSensitiveClipboardContent", settings.DetectSensitiveClipboardContent ? "1" : "0");
        SaveSettingInternal(connection, transaction, "MaskSensitiveClipboardContent", settings.MaskSensitiveClipboardContent ? "1" : "0");
        SaveSettingInternal(connection, transaction, "ExcludeSensitiveClipboardContent", settings.ExcludeSensitiveClipboardContent ? "1" : "0");
        SaveSettingInternal(connection, transaction, "Theme", NormalizeThemeValue(settings.Theme));
        SaveSettingInternal(connection, transaction, "UpdateFeedUrl", settings.UpdateFeedUrl ?? string.Empty);

        transaction.Commit();
    }

    private static void SaveSettingInternal(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO AppSettings (Key, Value)
            VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;

        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);

        command.ExecuteNonQuery();
    }

    public int InsertItem(ClipboardEntry item)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ClipboardItems (Title, Category, FullText, IsSnippet, IsPinned, CapturedAt, IsSensitive, IsSensitiveManual)
            VALUES ($title, $category, $fullText, $isSnippet, $isPinned, $capturedAt, $isSensitive, $isSensitiveManual);

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$category", item.Category);
        command.Parameters.AddWithValue("$fullText", item.FullText);
        command.Parameters.AddWithValue("$isSnippet", item.IsSnippet ? 1 : 0);
        command.Parameters.AddWithValue("$isPinned", item.IsPinned ? 1 : 0);
        command.Parameters.AddWithValue("$capturedAt", item.CapturedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$isSensitive", item.IsSensitive ? 1 : 0);
        command.Parameters.AddWithValue("$isSensitiveManual", item.IsSensitiveManual ? 1 : 0);

        object? result = command.ExecuteScalar();
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public void UpdatePinState(int id, bool isPinned)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ClipboardItems
            SET IsPinned = $isPinned
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$isPinned", isPinned ? 1 : 0);

        command.ExecuteNonQuery();
    }

    public void UpdateSensitivityState(int id, bool isSensitive, bool isSensitiveManual)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ClipboardItems
            SET IsSensitive = $isSensitive,
                IsSensitiveManual = $isSensitiveManual
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$isSensitive", isSensitive ? 1 : 0);
        command.Parameters.AddWithValue("$isSensitiveManual", isSensitiveManual ? 1 : 0);

        command.ExecuteNonQuery();
    }

    public void UpdateSnippet(int id, string title, string category, string fullText)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ClipboardItems
            SET Title = $title,
                Category = $category,
                FullText = $fullText
            WHERE Id = $id
              AND IsSnippet = 1;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$fullText", fullText);

        int rowsAffected = command.ExecuteNonQuery();

        if (rowsAffected == 0)
            throw new InvalidOperationException("The snippet could not be found for update.");
    }

    public void DeleteItem(int id)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM ClipboardItems
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public int DeleteNonPinnedHistory()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM ClipboardItems
            WHERE IsSnippet = 0 AND IsPinned = 0;
            """;

        int deletedCount = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);

        using var deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText =
            """
            DELETE FROM ClipboardItems
            WHERE IsSnippet = 0 AND IsPinned = 0;
            """;

        deleteCommand.ExecuteNonQuery();

        return deletedCount;
    }

    public AppSettings LoadAppSettings()
    {
        var settings = new AppSettings
        {
            LaunchOnStartup = GetBoolSetting("LaunchOnStartup", false),
            ClipboardMonitoringEnabled = GetBoolSetting("ClipboardMonitoringEnabled", true),
            MaxHistoryItems = GetIntSetting("MaxHistoryItems", DefaultMaxHistoryItems),
            MinimizeToTray = GetBoolSetting("MinimizeToTray", false),
            CloseToTray = GetBoolSetting("CloseToTray", false),
            CheckForUpdatesOnStartup = GetBoolSetting("CheckForUpdatesOnStartup", true),
            DetectSensitiveClipboardContent = GetBoolSetting("DetectSensitiveClipboardContent", true),
            MaskSensitiveClipboardContent = GetBoolSetting("MaskSensitiveClipboardContent", true),
            ExcludeSensitiveClipboardContent = GetBoolSetting("ExcludeSensitiveClipboardContent", false),
            Theme = NormalizeThemeValue(GetSetting("Theme")),
            UpdateFeedUrl = GetSetting("UpdateFeedUrl") ?? string.Empty
        };

        return settings;
    }

    public void SaveAppSettings(AppSettings settings)
    {
        SaveSetting("LaunchOnStartup", settings.LaunchOnStartup ? "1" : "0");
        SaveSetting("ClipboardMonitoringEnabled", settings.ClipboardMonitoringEnabled ? "1" : "0");
        SaveSetting("MaxHistoryItems", settings.MaxHistoryItems.ToString(CultureInfo.InvariantCulture));
        SaveSetting("MinimizeToTray", settings.MinimizeToTray ? "1" : "0");
        SaveSetting("CloseToTray", settings.CloseToTray ? "1" : "0");
        SaveSetting("CheckForUpdatesOnStartup", settings.CheckForUpdatesOnStartup ? "1" : "0");
        SaveSetting("DetectSensitiveClipboardContent", settings.DetectSensitiveClipboardContent ? "1" : "0");
        SaveSetting("MaskSensitiveClipboardContent", settings.MaskSensitiveClipboardContent ? "1" : "0");
        SaveSetting("ExcludeSensitiveClipboardContent", settings.ExcludeSensitiveClipboardContent ? "1" : "0");
        SaveSetting("Theme", NormalizeThemeValue(settings.Theme));
        SaveSetting("UpdateFeedUrl", settings.UpdateFeedUrl ?? string.Empty);
    }

    public void SaveSetting(string key, string value)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO AppSettings (Key, Value)
            VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;

        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);

        command.ExecuteNonQuery();
    }

    public string? GetSetting(string key)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Value
            FROM AppSettings
            WHERE Key = $key
            LIMIT 1;
            """;

        command.Parameters.AddWithValue("$key", key);

        object? result = command.ExecuteScalar();
        return result?.ToString();
    }

    private bool GetBoolSetting(string key, bool defaultValue)
    {
        string? value = GetSetting(key);

        return value switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue
        };
    }

    private int GetIntSetting(string key, int defaultValue)
    {
        string? value = GetSetting(key);

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0)
            return parsed;

        return defaultValue;
    }

    private static string NormalizeThemeValue(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
            return "Midnight";

        return theme.Trim() switch
        {
            "Dark" => "Midnight",
            "Midnight" => "Midnight",
            "Graphite" => "Graphite",
            "Aurora" => "Aurora",
            _ => "Midnight"
        };
    }

    private void EnsureDefaultSetting(string key, string defaultValue)
    {
        if (GetSetting(key) is null)
        {
            SaveSetting(key, defaultValue);
        }
    }

    private static void EnsureClipboardItemsColumn(SqliteConnection connection, string columnName, string columnDefinition)
    {
        bool columnExists;

        using (var pragmaCommand = connection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA table_info(ClipboardItems);";

            using var reader = pragmaCommand.ExecuteReader();
            columnExists = false;

            while (reader.Read())
            {
                string existingColumn = reader.GetString(1);
                if (string.Equals(existingColumn, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (columnExists)
            return;

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE ClipboardItems ADD COLUMN {columnName} {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={DatabaseFilePath}");
    }
}
