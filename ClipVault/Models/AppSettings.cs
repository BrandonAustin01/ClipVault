namespace ClipVault.Models;

public class AppSettings
{
    public bool LaunchOnStartup { get; set; }
    public bool ClipboardMonitoringEnabled { get; set; } = true;
    public int MaxHistoryItems { get; set; } = 100;
    public bool MinimizeToTray { get; set; }
    public bool CloseToTray { get; set; }
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public bool DetectSensitiveClipboardContent { get; set; } = true;
    public bool MaskSensitiveClipboardContent { get; set; } = true;
    public bool ExcludeSensitiveClipboardContent { get; set; }
    public string Theme { get; set; } = "Midnight";
    public string UpdateFeedUrl { get; set; } = string.Empty;
}
