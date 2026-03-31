using System;
using Microsoft.Win32;

namespace ClipVault.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppName = "ClipVault";

    public bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        string? value = key?.GetValue(AppName)?.ToString();

        return !string.IsNullOrWhiteSpace(value);
    }

    public void SetStartupEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                       ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (key is null)
            throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (enabled)
        {
            string executablePath = Environment.ProcessPath
                                    ?? throw new InvalidOperationException("Could not determine the ClipVault executable path.");

            key.SetValue(AppName, $"\"{executablePath}\"");
        }
        else
        {
            if (key.GetValue(AppName) is not null)
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
    }
}