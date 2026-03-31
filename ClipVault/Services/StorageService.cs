using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        EnsureDefaultSetting("MaxHistoryItems", DefaultMaxHistoryItems.ToString(CultureInfo.InvariantCulture));
        EnsureDefaultSetting("ClipboardMonitoringEnabled", "1");
        EnsureDefaultSetting("LaunchOnStartup", "0");
        EnsureDefaultSetting("MinimizeToTray", "0");
        EnsureDefaultSetting("CloseToTray", "0");
        EnsureDefaultSetting("Theme", "Dark");
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
            SELECT Id, Title, Category, FullText, IsSnippet, IsPinned, CapturedAt
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
                    DateTimeStyles.RoundtripKind)
            });
        }

        return items;
    }

    public int InsertItem(ClipboardEntry item)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ClipboardItems (Title, Category, FullText, IsSnippet, IsPinned, CapturedAt)
            VALUES ($title, $category, $fullText, $isSnippet, $isPinned, $capturedAt);

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$category", item.Category);
        command.Parameters.AddWithValue("$fullText", item.FullText);
        command.Parameters.AddWithValue("$isSnippet", item.IsSnippet ? 1 : 0);
        command.Parameters.AddWithValue("$isPinned", item.IsPinned ? 1 : 0);
        command.Parameters.AddWithValue("$capturedAt", item.CapturedAt.ToString("O", CultureInfo.InvariantCulture));

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
            Theme = GetSetting("Theme") ?? "Dark",
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
        SaveSetting("Theme", settings.Theme);
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

    private void EnsureDefaultSetting(string key, string defaultValue)
    {
        if (GetSetting(key) is null)
        {
            SaveSetting(key, defaultValue);
        }
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={DatabaseFilePath}");
    }
}
