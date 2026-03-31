using System;
using System.IO;

namespace ClipVault.Services;

public static class LogService
{
    private static readonly object SyncRoot = new();
    private static string? _currentLogFilePath;

    public static string CurrentLogFilePath
    {
        get
        {
            EnsureLogFilePath();
            return _currentLogFilePath!;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(Exception exception, string? message = null)
    {
        Write("ERROR", message ?? "Unhandled exception.", exception);
    }

    private static void EnsureLogFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_currentLogFilePath))
            return;

        lock (SyncRoot)
        {
            if (!string.IsNullOrWhiteSpace(_currentLogFilePath))
                return;

            try
            {
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClipVault",
                    "Logs");

                Directory.CreateDirectory(logDirectory);

                _currentLogFilePath = Path.Combine(
                    logDirectory,
                    $"cvx_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            }
            catch
            {
                var fallbackDirectory = Path.Combine(Path.GetTempPath(), "ClipVault", "Logs");
                Directory.CreateDirectory(fallbackDirectory);

                _currentLogFilePath = Path.Combine(
                    fallbackDirectory,
                    $"cvx_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            }
        }
    }

    private static void Write(string level, string message, Exception? exception = null)
    {
        try
        {
            EnsureLogFilePath();

            var logBlock =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

            if (exception is not null)
            {
                logBlock += $"{exception}{Environment.NewLine}";
            }

            logBlock += Environment.NewLine;

            lock (SyncRoot)
            {
                File.AppendAllText(CurrentLogFilePath, logBlock);
            }
        }
        catch
        {
            // Never let logging crash the app.
        }
    }
}