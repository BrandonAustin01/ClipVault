using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ClipVault.Helpers;

namespace ClipVault.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly string _displayVersion = AppVersionHelper.GetDisplayVersion();
    private bool _hasShownMinimizeBalloon;
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;

    public bool IsVisible
    {
        get => _notifyIcon.Visible;
        set => _notifyIcon.Visible = value;
    }

    public TrayIconService()
    {
        _menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open ClipVault");
        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _menu.Items.Add(openItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = BuildTrayText(),
            Icon = LoadTrayIcon(),
            ContextMenuStrip = _menu,
            Visible = false
        };

        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.Click += (_, _) => { };
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    public void Hide()
    {
        _notifyIcon.Visible = false;
    }

    public void ShowFirstMinimizeBalloon()
    {
        if (_hasShownMinimizeBalloon)
            return;

        _notifyIcon.BalloonTipTitle = "ClipVault";
        _notifyIcon.BalloonTipText = "ClipVault is still running in the system tray.";
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(2500);

        _hasShownMinimizeBalloon = true;
    }

    private string BuildTrayText()
    {
        string text = $"ClipVault v{_displayVersion}";
        return text.Length > 63 ? text[..63] : text;
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            string? executablePath = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
            {
                using var extractedIcon = Icon.ExtractAssociatedIcon(executablePath);
                if (extractedIcon is not null)
                {
                    return (Icon)extractedIcon.Clone();
                }
            }
        }
        catch
        {
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
        }
        catch
        {
            // Never throw during shutdown.
        }
    }
}