using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using ClipVault.Models;
using ClipVault.Services;
using ClipVault.Helpers;
using Microsoft.Win32;
using System.Collections.Generic;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ClipVault;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string DefaultUpdateFeedUrl = "https://brandonmckinney.dev/clipvault/updates";

    private readonly ClipboardMonitorService _clipboardMonitorService = new();
    private readonly StorageService _storageService = new();
    private readonly StartupService _startupService = new();
    private readonly TrayIconService _trayIconService = new();
    private readonly UpdateService _updateService = new();
    private readonly BackupService _backupService = new();
    private readonly string _displayVersion = AppVersionHelper.GetDisplayVersion();


    private AppSettings _appSettings = new();
    private bool _sourceInitialized;
    private bool _allowRealClose;
    private bool _isUpdateCheckRunning;

    private string _currentSectionTitle = "History";
    private string _currentSectionSubtitle = "Your recent copied items will live here.";
    private string _statusMessage = "Starting ClipVault...";
    private string? _suppressedClipboardTextNormalized;

    private int _totalCount;
    private int _pinnedCount;
    private int _snippetCount;
    
    private DateTime _suppressedClipboardUntilUtc = DateTime.MinValue;
    private LogViewerWindow? _logViewerWindow;

    public ObservableCollection<ClipboardEntry> AllItems { get; } = new();
    public ObservableCollection<ClipboardEntry> FilteredItems { get; } = new();

    public string DatabasePath => _storageService.DatabaseFilePath;

    public string CurrentLogPath => LogService.CurrentLogFilePath;
    public string DisplayVersion => _displayVersion;

    public string CurrentSectionTitle
    {
        get => _currentSectionTitle;
        set
        {
            if (_currentSectionTitle != value)
            {
                _currentSectionTitle = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentSectionSubtitle
    {
        get => _currentSectionSubtitle;
        set
        {
            if (_currentSectionSubtitle != value)
            {
                _currentSectionSubtitle = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        set
        {
            if (_totalCount != value)
            {
                _totalCount = value;
                OnPropertyChanged();
            }
        }
    }

    public int PinnedCount
    {
        get => _pinnedCount;
        set
        {
            if (_pinnedCount != value)
            {
                _pinnedCount = value;
                OnPropertyChanged();
            }
        }
    }

    public int SnippetCount
    {
        get => _snippetCount;
        set
        {
            if (_snippetCount != value)
            {
                _snippetCount = value;
                OnPropertyChanged();
            }
        }
    }

    private void ApplyWindowBranding()
    {
        Title = $"ClipVault v{_displayVersion}";
    }

    public MainWindow()
    {
        InitializeComponent();
        ApplyWindowBranding();
        DataContext = this;

        _clipboardMonitorService.ClipboardTextCaptured += ClipboardMonitorService_ClipboardTextCaptured;
        _trayIconService.OpenRequested += TrayIconService_OpenRequested;
        _trayIconService.ExitRequested += TrayIconService_ExitRequested;

        RunGuarded(() =>
        {
            InitializePersistence();
            LoadPersistedItems();
            ApplySettingsToUi();

            SetSection("History");
            UpdateStats();

            StatusMessage = AllItems.Count == 0
                ? "Ready. No saved items yet."
                : $"Ready. Loaded {AllItems.Count} saved item(s).";

            LogService.Info("Main window initialized successfully.");
        }, "Main window initialization");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _sourceInitialized = true;

        RunGuarded(() =>
        {
            ApplyClipboardMonitoringSetting();
            ApplyTrayVisibility();

            StatusMessage = _appSettings.ClipboardMonitoringEnabled
                ? "Clipboard monitoring active."
                : "Clipboard monitoring disabled in settings.";
        }, "Clipboard monitor startup");
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == WindowState.Minimized && _appSettings.MinimizeToTray)
        {
            HideToTray(showBalloon: true);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowRealClose && _appSettings.CloseToTray)
        {
            e.Cancel = true;
            HideToTray(showBalloon: true);
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            _clipboardMonitorService.ClipboardTextCaptured -= ClipboardMonitorService_ClipboardTextCaptured;
            _trayIconService.OpenRequested -= TrayIconService_OpenRequested;
            _trayIconService.ExitRequested -= TrayIconService_ExitRequested;

            _clipboardMonitorService.Stop();
            _trayIconService.Dispose();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error while shutting down the main window.");
        }

        base.OnClosed(e);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RunGuarded(Action action, string operationName, bool fatal = false)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            StatusMessage = $"{operationName} failed. Check the log.";
            ErrorHandler.Handle(ex, $"{operationName} failed.", fatal);

            if (fatal)
                throw;
        }
    }

    private void InitializePersistence()
    {
        _storageService.InitializeDatabase();
        _appSettings = _storageService.LoadAppSettings();

        _appSettings.LaunchOnStartup = _startupService.IsStartupEnabled();

        LogService.Info($"Persistence initialized. Database path: {_storageService.DatabaseFilePath}");
        LogService.Info($"MaxHistoryItems loaded: {_appSettings.MaxHistoryItems}");
    }

    private void LoadPersistedItems()
    {
        AllItems.Clear();

        var items = _storageService.LoadItems();

        foreach (var item in items)
        {
            AllItems.Add(item);
        }
    }

    private void ApplySettingsToUi()
    {
        ChkLaunchOnStartup.IsChecked = _appSettings.LaunchOnStartup;
        ChkClipboardMonitoring.IsChecked = _appSettings.ClipboardMonitoringEnabled;
        ChkMinimizeToTray.IsChecked = _appSettings.MinimizeToTray;
        ChkCloseToTray.IsChecked = _appSettings.CloseToTray;
        MaxHistoryItemsTextBox.Text = _appSettings.MaxHistoryItems.ToString();
    }

    private void ApplyClipboardMonitoringSetting()
    {
        if (!_sourceInitialized)
            return;

        if (_appSettings.ClipboardMonitoringEnabled)
        {
            if (!_clipboardMonitorService.IsListening)
            {
                _clipboardMonitorService.Start(this);
            }
        }
        else
        {
            if (_clipboardMonitorService.IsListening)
            {
                _clipboardMonitorService.Stop();
            }
        }
    }

    private void ApplyTrayVisibility()
    {
        bool shouldShowTrayIcon = _appSettings.MinimizeToTray || _appSettings.CloseToTray;
        _trayIconService.IsVisible = shouldShowTrayIcon;
    }

    private void HideToTray(bool showBalloon)
    {
        ShowInTaskbar = false;
        Hide();

        _trayIconService.Show();

        if (showBalloon)
        {
            _trayIconService.ShowFirstMinimizeBalloon();
        }

        StatusMessage = "ClipVault is running in the system tray.";
        LogService.Info("ClipVault hidden to tray.");
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();

        ApplyTrayVisibility();

        StatusMessage = "ClipVault restored from tray.";
        LogService.Info("ClipVault restored from tray.");
    }

    private void TrayIconService_OpenRequested(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RunGuarded(RestoreFromTray, "Restore from tray");
        });
    }

    private void TrayIconService_ExitRequested(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RunGuarded(() =>
            {
                _allowRealClose = true;
                _trayIconService.Hide();
                Close();
            }, "Tray exit");
        });
    }

    private void ClipboardMonitorService_ClipboardTextCaptured(object? sender, string text)
    {
        Dispatcher.Invoke(() =>
        {
            RunGuarded(() => CaptureClipboardText(text), "Clipboard capture");
        });
    }

    private void CaptureClipboardText(string rawText)
    {
        if (!ShouldCaptureText(rawText))
            return;

        var entry = new ClipboardEntry
        {
            Title = BuildTitle(rawText),
            Category = InferCategory(rawText),
            FullText = rawText,
            IsSnippet = false,
            IsPinned = false,
            CapturedAt = DateTime.Now
        };

        entry.Id = _storageService.InsertItem(entry);

        AllItems.Add(entry);
        int trimmedCount = TrimHistory();
        ApplyFilter();

        StatusMessage = trimmedCount > 0
            ? $"Captured \"{entry.Title}\". Trimmed {trimmedCount} old item(s) to stay within the history limit."
            : $"Captured \"{entry.Title}\".";

        LogService.Info($"Clipboard text captured: {entry.Title}");
    }

    private bool ShouldCaptureText(string? rawText)
    {
        string normalized = NormalizeClipboardText(rawText);

        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (!string.IsNullOrWhiteSpace(_suppressedClipboardTextNormalized))
        {
            if (DateTime.UtcNow <= _suppressedClipboardUntilUtc &&
                string.Equals(normalized, _suppressedClipboardTextNormalized, StringComparison.Ordinal))
            {
                return false;
            }

            if (DateTime.UtcNow > _suppressedClipboardUntilUtc)
            {
                _suppressedClipboardTextNormalized = null;
                _suppressedClipboardUntilUtc = DateTime.MinValue;
            }
        }

        var latestHistoryItem = AllItems
            .Where(x => !x.IsSnippet)
            .OrderByDescending(x => x.CapturedAt)
            .FirstOrDefault();

        if (latestHistoryItem is not null)
        {
            string latestNormalized = NormalizeClipboardText(latestHistoryItem.FullText);
            if (string.Equals(latestNormalized, normalized, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private int TrimHistory()
    {
        int historyCount = AllItems.Count(x => !x.IsSnippet);

        if (historyCount <= _appSettings.MaxHistoryItems)
            return 0;

        var removableItems = AllItems
            .Where(x => !x.IsSnippet && !x.IsPinned)
            .OrderBy(x => x.CapturedAt)
            .ToList();

        int removedCount = 0;

        while (AllItems.Count(x => !x.IsSnippet) > _appSettings.MaxHistoryItems && removableItems.Count > 0)
        {
            var oldest = removableItems[0];
            removableItems.RemoveAt(0);

            _storageService.DeleteItem(oldest.Id);

            if (AllItems.Remove(oldest))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            LogService.Info($"Trimmed {removedCount} old clipboard item(s).");
        }

        return removedCount;
    }

    private static string NormalizeClipboardText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text
            .Replace("\r\n", "\n")
            .Trim();
    }

    private static string BuildTitle(string text)
    {
        string normalized = NormalizeClipboardText(text);

        if (string.IsNullOrWhiteSpace(normalized))
            return "Clipboard item";

        string firstLine = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? normalized;

        if (firstLine.Length > 42)
            return firstLine[..42] + "...";

        return firstLine;
    }

    private static string InferCategory(string text)
    {
        string normalized = NormalizeClipboardText(text);

        if (Uri.TryCreate(normalized, UriKind.Absolute, out _))
            return "Link";

        if (normalized.Contains('\n'))
            return "Note";

        return "Text";
    }

    private void SetSection(string section)
    {
        CurrentSectionTitle = section;

        CurrentSectionSubtitle = section switch
        {
            "History" => "Your recent copied items will live here.",
            "Pinned" => "Important items you do not want to lose.",
            "Snippets" => "Reusable text and saved quick-copy blocks.",
            "Settings" => "ClipVault settings and local storage.",
            _ => "ClipVault"
        };

        UpdateSidebarSelection();
        UpdateContentVisibility();
        ApplyFilter();

        LogService.Info($"Section changed to {section}.");
    }

    private void UpdateSidebarSelection()
    {
        ResetSidebarButton(BtnHistory);
        ResetSidebarButton(BtnPinned);
        ResetSidebarButton(BtnSnippets);
        ResetSidebarButton(BtnSettings);

        switch (CurrentSectionTitle)
        {
            case "History":
                SelectSidebarButton(BtnHistory);
                break;
            case "Pinned":
                SelectSidebarButton(BtnPinned);
                break;
            case "Snippets":
                SelectSidebarButton(BtnSnippets);
                break;
            case "Settings":
                SelectSidebarButton(BtnSettings);
                break;
        }
    }

    private void ResetSidebarButton(WpfButton button)
    {
        button.Background = WpfBrushes.Transparent;
        button.BorderBrush = WpfBrushes.Transparent;
    }

    private void SelectSidebarButton(WpfButton button)
    {
        button.Background = (WpfBrush)FindResource("SidebarSelectedBrush");
        button.BorderBrush = (WpfBrush)FindResource("CardBorderBrush");
    }

    private void UpdateContentVisibility()
    {
        bool showSettings = CurrentSectionTitle == "Settings";

        SettingsContentPanel.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
        ClipboardContentPanel.Visibility = showSettings ? Visibility.Collapsed : Visibility.Visible;

        SearchActionPanel.Visibility = showSettings ? Visibility.Collapsed : Visibility.Visible;
        StatsPanel.Visibility = showSettings ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyFilter()
    {
        var items = CurrentSectionTitle switch
        {
            "History" => AllItems.Where(x => !x.IsSnippet),
            "Pinned" => AllItems.Where(x => x.IsPinned),
            "Snippets" => AllItems.Where(x => x.IsSnippet),
            "Settings" => Enumerable.Empty<ClipboardEntry>(),
            _ => AllItems.AsEnumerable()
        };

        string query = SearchTextBox?.Text?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(query))
        {
            string lowered = query.ToLowerInvariant();

            items = items.Where(x =>
                x.Title.ToLowerInvariant().Contains(lowered) ||
                x.Category.ToLowerInvariant().Contains(lowered) ||
                x.FullText.ToLowerInvariant().Contains(lowered));
        }

        var orderedItems = items
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CapturedAt)
            .ThenByDescending(x => x.Id)
            .ToList();

        FilteredItems.Clear();

        foreach (var item in orderedItems)
        {
            FilteredItems.Add(item);
        }

        UpdateEmptyState();
        UpdateStats();
    }

    private void UpdateEmptyState()
    {
        bool hasItems = FilteredItems.Count > 0;

        NoItemsText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        ClipboardScroller.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStats()
    {
        TotalCount = AllItems.Count(x => !x.IsSnippet);
        PinnedCount = AllItems.Count(x => x.IsPinned);
        SnippetCount = AllItems.Count(x => x.IsSnippet);
    }

    private ClipboardEntry? FindEntryFromButton(object sender)
    {
        if (sender is not WpfButton button || button.Tag is null)
            return null;

        int id = Convert.ToInt32(button.Tag);
        return AllItems.FirstOrDefault(x => x.Id == id);
    }

    private void OpenLogViewerButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            if (_logViewerWindow is not null)
            {
                if (_logViewerWindow.WindowState == WindowState.Minimized)
                {
                    _logViewerWindow.WindowState = WindowState.Normal;
                }

                _logViewerWindow.Activate();
                StatusMessage = "Log viewer is already open.";
                return;
            }

            _logViewerWindow = new LogViewerWindow(CurrentLogPath)
            {
                Owner = this
            };

            _logViewerWindow.Closed += (_, _) => _logViewerWindow = null;
            _logViewerWindow.Show();

            StatusMessage = "Opened log viewer.";
            LogService.Info("Log viewer opened.");
        }, "Open log viewer");
    }

    private void SectionButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            if (sender is WpfButton button && button.Tag is string section)
            {
                SetSection(section);
                StatusMessage = $"Showing {section}.";
            }
        }, "Section change");
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            ApplyFilter();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Search filtering failed.");
            StatusMessage = "Search failed. Check the log.";
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            SearchTextBox.Clear();
            StatusMessage = "Search cleared.";
        }, "Search clear");
    }

    private void NewSnippetButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var editor = new SnippetEditorWindow
            {
                Owner = this
            };

            bool? result = editor.ShowDialog();
            if (result != true)
            {
                StatusMessage = "Snippet creation canceled.";
                return;
            }

            var newSnippet = new ClipboardEntry
            {
                Title = editor.SnippetTitle,
                Category = editor.SnippetCategory,
                FullText = editor.SnippetContent,
                IsSnippet = true,
                IsPinned = false,
                CapturedAt = DateTime.Now
            };

            newSnippet.Id = _storageService.InsertItem(newSnippet);

            AllItems.Add(newSnippet);
            ApplyFilter();

            StatusMessage = $"Created snippet \"{newSnippet.Title}\".";
            LogService.Info($"Snippet created: {newSnippet.Title}");
        }, "Snippet creation");
    }

    private void EditSnippetButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var item = FindEntryFromButton(sender);
            if (item is null || !item.IsSnippet)
            {
                StatusMessage = "Could not find that snippet.";
                LogService.Warn("Edit requested for a snippet that could not be found.");
                return;
            }

            var editor = new SnippetEditorWindow(item.Title, item.Category, item.FullText)
            {
                Owner = this
            };

            bool? result = editor.ShowDialog();
            if (result != true)
            {
                StatusMessage = "Snippet edit canceled.";
                return;
            }

            _storageService.UpdateSnippet(item.Id, editor.SnippetTitle, editor.SnippetCategory, editor.SnippetContent);

            item.Title = editor.SnippetTitle;
            item.Category = editor.SnippetCategory;
            item.FullText = editor.SnippetContent;

            ApplyFilter();

            StatusMessage = $"Updated snippet \"{item.Title}\".";
            LogService.Info($"Snippet updated: {item.Title}");
        }, "Snippet edit");
    }

    private void CopyItemButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var item = FindEntryFromButton(sender);
            if (item is null)
            {
                StatusMessage = "Could not find that clipboard item.";
                LogService.Warn("Copy requested for an item that could not be found.");
                return;
            }

            _suppressedClipboardTextNormalized = NormalizeClipboardText(item.FullText);
            _suppressedClipboardUntilUtc = DateTime.UtcNow.AddSeconds(2);

            if (!ClipboardService.TrySetText(item.FullText, out var errorMessage))
            {
                _suppressedClipboardTextNormalized = null;
                _suppressedClipboardUntilUtc = DateTime.MinValue;
                StatusMessage = $"Clipboard copy failed: {errorMessage}";
                return;
            }

            StatusMessage = $"Copied \"{item.Title}\" back to the clipboard.";
            LogService.Info($"Clipboard item copied: {item.Title}");
        }, "Clipboard copy");
    }

    private void TogglePinButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var item = FindEntryFromButton(sender);
            if (item is null)
            {
                StatusMessage = "Could not find that clipboard item.";
                LogService.Warn("Pin toggle requested for an item that could not be found.");
                return;
            }

            item.IsPinned = !item.IsPinned;
            _storageService.UpdatePinState(item.Id, item.IsPinned);

            ApplyFilter();

            StatusMessage = item.IsPinned
                ? $"Pinned \"{item.Title}\"."
                : $"Unpinned \"{item.Title}\".";

            LogService.Info($"Pin state changed for {item.Title}. IsPinned={item.IsPinned}");
        }, "Pin toggle");
    }

    private void DeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var item = FindEntryFromButton(sender);
            if (item is null)
            {
                StatusMessage = "Could not find that item.";
                LogService.Warn("Delete requested for an item that could not be found.");
                return;
            }

            string itemType = item.IsSnippet ? "snippet" : "clipboard item";

            var result = DialogService.Show(
                $"Delete this {itemType}?{Environment.NewLine}{Environment.NewLine}\"{item.Title}\"",
                "ClipVault",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                StatusMessage = $"Delete {itemType} canceled.";
                return;
            }

            _storageService.DeleteItem(item.Id);
            AllItems.Remove(item);

            ApplyFilter();

            StatusMessage = item.IsSnippet
                ? $"Deleted snippet \"{item.Title}\"."
                : $"Deleted \"{item.Title}\".";

            LogService.Info($"{itemType} deleted: {item.Title}");
        }, "Item deletion");
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            if (!int.TryParse(MaxHistoryItemsTextBox.Text.Trim(), out int maxHistoryItems) ||
                maxHistoryItems < 1 || maxHistoryItems > 5000)
            {
                StatusMessage = "Max history items must be between 1 and 5000.";
                return;
            }

            _appSettings.LaunchOnStartup = ChkLaunchOnStartup.IsChecked == true;
            _appSettings.ClipboardMonitoringEnabled = ChkClipboardMonitoring.IsChecked == true;
            _appSettings.MinimizeToTray = ChkMinimizeToTray.IsChecked == true;
            _appSettings.CloseToTray = ChkCloseToTray.IsChecked == true;
            _appSettings.MaxHistoryItems = maxHistoryItems;

            _startupService.SetStartupEnabled(_appSettings.LaunchOnStartup);
            _storageService.SaveAppSettings(_appSettings);

            ApplyClipboardMonitoringSetting();
            ApplyTrayVisibility();

            int trimmedCount = TrimHistory();
            ApplyFilter();

            StatusMessage = trimmedCount > 0
                ? $"Settings saved. Trimmed {trimmedCount} old item(s) to match the new history limit."
                : "Settings saved.";

            LogService.Info("Settings saved successfully.");
        }, "Settings save");
    }

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdateCheckRunning)
        {
            StatusMessage = "An update check is already running.";
            return;
        }

        string updateFeedUrl = DefaultUpdateFeedUrl;

        try
        {
            _isUpdateCheckRunning = true;
            StatusMessage = "Checking for updates...";
            LogService.Info($"Checking for updates from {updateFeedUrl}");

            var progress = new Progress<int>(percent =>
            {
                StatusMessage = $"Downloading update... {percent}%";
            });

            var result = await _updateService.CheckForUpdatesAsync(updateFeedUrl, progress);

            switch (result.State)
            {
                case UpdateCheckState.NoFeedConfigured:
                    StatusMessage = "The update feed is not configured in this build.";
                    DialogService.Show(
                        "The update feed is not configured in this build.",
                        "ClipVault",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                case UpdateCheckState.NotInstalled:
                    StatusMessage = "Updates only work from an installed ClipVault build.";
                    DialogService.Show(
                        "ClipVault is not running from a Velopack-installed build yet." + Environment.NewLine + Environment.NewLine +
                        "This is expected if you launched it from Visual Studio, bin\\Release, or a loose publish folder. " +
                        "Install the packaged Setup.exe build first, then update checks will work.",
                        "ClipVault",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                case UpdateCheckState.UpToDate:
                    StatusMessage = string.IsNullOrWhiteSpace(result.CurrentVersion)
                        ? "ClipVault is already up to date."
                        : $"ClipVault is already up to date ({result.CurrentVersion}).";
                    DialogService.Show(
                        StatusMessage,
                        "ClipVault",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                case UpdateCheckState.UpdatePendingRestart:
                case UpdateCheckState.UpdateReadyToApply:
                    StatusMessage = $"Update {result.TargetVersion} is ready to install.";

                    var promptResult = DialogService.Show(
                        $"Update {result.TargetVersion} is ready.{Environment.NewLine}{Environment.NewLine}" +
                        $"Current version: {result.CurrentVersion ?? DisplayVersion}{Environment.NewLine}" +
                        $"New version: {result.TargetVersion}{Environment.NewLine}{Environment.NewLine}" +
                        "Install it now and restart ClipVault?",
                        "ClipVault Update Ready",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (promptResult == MessageBoxResult.Yes)
                    {
                        LogService.Info($"Applying update {result.TargetVersion}.");

                        try
                        {
                            string previousVersion = result.CurrentVersion ?? DisplayVersion;
                            string currentVersion = string.IsNullOrWhiteSpace(result.TargetVersion)
                                ? DisplayVersion
                                : result.TargetVersion;

                            string changelogText = ChangelogCatalog.BuildChangesSince(previousVersion, currentVersion);

                            PostUpdateExperienceService.QueueAnnouncement(
                                previousVersion,
                                currentVersion,
                                changelogText);
                        }
                        catch (Exception ex)
                        {
                            LogService.Error(ex, "Failed to queue the post-update announcement.");
                            // Do not block the update if the announcement fails to save.
                        }

                        result.ApplyAndRestart?.Invoke();
                        return;
                    }

                    StatusMessage = $"Update {result.TargetVersion} downloaded. Restart ClipVault when you are ready to install it.";
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Update check failed.");
            StatusMessage = "Update check failed. Check the log.";

            DialogService.Show(
                $"ClipVault could not finish checking for updates.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "ClipVault Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _isUpdateCheckRunning = false;
        }
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var result = DialogService.Show(
                "Clear all non-pinned clipboard history? Pinned items and snippets will be kept.",
                "ClipVault",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                StatusMessage = "Clear history canceled.";
                return;
            }

            int deletedCount = _storageService.DeleteNonPinnedHistory();

            var itemsToRemove = AllItems
                .Where(x => !x.IsSnippet && !x.IsPinned)
                .ToList();

            foreach (var item in itemsToRemove)
            {
                AllItems.Remove(item);
            }

            ApplyFilter();

            StatusMessage = deletedCount == 0
                ? "No non-pinned history to clear."
                : $"Cleared {deletedCount} non-pinned history item(s).";

            LogService.Info($"Cleared {deletedCount} non-pinned history item(s).");
        }, "Clear history");
    }

    private void ViewCurrentChangelogButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var announcement = new PostUpdateAnnouncement
            {
                PreviousVersion = string.Empty,
                CurrentVersion = DisplayVersion,
                ChangelogText = ChangelogCatalog.BuildChangesSince(null, DisplayVersion)
            };

            var window = new UpdateAnnouncementWindow(announcement)
            {
                Owner = this
            };

            window.ShowDialog();
            StatusMessage = "Opened current changelog.";
        }, "Open current changelog");
    }

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            string? folder = Path.GetDirectoryName(DatabasePath);
            if (string.IsNullOrWhiteSpace(folder))
            {
                StatusMessage = "Could not determine the data folder.";
                return;
            }

            Directory.CreateDirectory(folder);

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });

            StatusMessage = "Opened data folder.";
        }, "Open data folder");
    }

    private void OpenLogsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            string? folder = Path.GetDirectoryName(CurrentLogPath);
            if (string.IsNullOrWhiteSpace(folder))
            {
                StatusMessage = "Could not determine the logs folder.";
                return;
            }

            Directory.CreateDirectory(folder);

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });

            StatusMessage = "Opened logs folder.";
        }, "Open logs folder");
    }

    private void ReloadFromStorage()
    {
        _appSettings = _storageService.LoadAppSettings();
        _appSettings.LaunchOnStartup = _startupService.IsStartupEnabled();

        LoadPersistedItems();
        ApplySettingsToUi();
        ApplyClipboardMonitoringSetting();
        ApplyTrayVisibility();
        ApplyFilter();
        UpdateStats();
    }

    private void ExportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var dialog = new WpfSaveFileDialog
            {
                Title = "Export ClipVault Backup",
                Filter = "ClipVault Backup (*.cvxbackup.json)|*.cvxbackup.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = _backupService.BuildDefaultFileName(DisplayVersion),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AddExtension = true,
                DefaultExt = ".json",
                OverwritePrompt = true
            };

            bool? result = dialog.ShowDialog(this);
            if (result != true)
            {
                StatusMessage = "Backup export canceled.";
                return;
            }

            _backupService.ExportToFile(
                dialog.FileName,
                DisplayVersion,
                _appSettings,
                AllItems);

            StatusMessage = $"Exported backup with {AllItems.Count} item(s).";
            LogService.Info($"Backup exported to {dialog.FileName}");
        }, "Backup export");
    }

    private void ImportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var dialog = new WpfOpenFileDialog
            {
                Title = "Import ClipVault Backup",
                Filter = "ClipVault Backup (*.cvxbackup.json)|*.cvxbackup.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                CheckFileExists = true,
                Multiselect = false
            };

            bool? result = dialog.ShowDialog(this);
            if (result != true)
            {
                StatusMessage = "Backup import canceled.";
                return;
            }

            BackupDocument document;
            AppSettings importedSettings;
            List<ClipboardEntry> importedItems;

            try
            {
                document = _backupService.LoadFromFile(dialog.FileName);
                importedSettings = _backupService.ToAppSettings(document);
                importedItems = _backupService.ToClipboardEntries(document);
            }
            catch (System.Text.Json.JsonException ex)
            {
                LogService.Warn($"Invalid backup JSON selected for import: {dialog.FileName}");
                LogService.Error(ex, "Backup import rejected because the file is not valid JSON.");

                StatusMessage = "That file is not a valid ClipVault backup.";
                DialogService.Show(
                    "That file is not a valid ClipVault backup." + Environment.NewLine + Environment.NewLine +
                    "Please choose a backup file exported from ClipVault.",
                    "ClipVault Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            catch (InvalidOperationException ex)
            {
                LogService.Warn($"Invalid backup file selected for import: {dialog.FileName}");
                LogService.Error(ex, "Backup import rejected because the file failed validation.");

                StatusMessage = "That file could not be imported as a ClipVault backup.";
                DialogService.Show(
                    "That file could not be imported as a ClipVault backup." + Environment.NewLine + Environment.NewLine +
                    ex.Message,
                    "ClipVault Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var confirmResult = DialogService.Show(
                $"Import this ClipVault backup?{Environment.NewLine}{Environment.NewLine}" +
                $"This will replace your current local ClipVault data.{Environment.NewLine}{Environment.NewLine}" +
                $"Items in backup: {importedItems.Count}{Environment.NewLine}" +
                $"Exported from version: {document.ExportedFromVersion}{Environment.NewLine}" +
                $"Exported at: {document.ExportedAtUtc.ToLocalTime():g}",
                "Import ClipVault Backup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
            {
                StatusMessage = "Backup import canceled.";
                return;
            }

            _storageService.ReplaceAllData(importedItems, importedSettings);
            _startupService.SetStartupEnabled(importedSettings.LaunchOnStartup);

            ReloadFromStorage();

            StatusMessage = $"Imported backup with {importedItems.Count} item(s).";
            LogService.Info($"Backup imported from {dialog.FileName}");
        }, "Backup import");
    }
}
