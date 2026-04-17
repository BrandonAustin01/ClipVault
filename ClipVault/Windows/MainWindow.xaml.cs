using ClipVault.Helpers;
using ClipVault.Models;
using ClipVault.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfWindow = System.Windows.Window;

namespace ClipVault;

public partial class MainWindow : WpfWindow, INotifyPropertyChanged
{
    private const string DefaultUpdateFeedUrl = "https://brandonmckinney.dev/clipvault/updates";

    private readonly ClipboardMonitorService _clipboardMonitorService = new();
    private readonly StorageService _storageService = new();
    private readonly StartupService _startupService = new();
    private readonly TrayIconService _trayIconService = new();
    private readonly UpdateService _updateService = new();
    private readonly BackupService _backupService = new();
    private readonly SensitiveContentDetector _sensitiveContentDetector = new();
    private readonly string _displayVersion = AppVersionHelper.GetDisplayVersion();


    private AppSettings _appSettings = new();
    private bool _sourceInitialized;
    private bool _allowRealClose;
    private bool _isUpdateCheckRunning;
    private bool _hasTriggeredStartupUpdateCheck;

    private string _currentSectionTitle = "History";
    private string _currentSectionSubtitle = "Your recent copied items will live here.";
    private string _statusMessage = "Starting ClipVault...";
    private string? _suppressedClipboardTextNormalized;
    public string DatabasePath => _storageService.DatabaseFilePath;
    public string CurrentLogPath => LogService.CurrentLogFilePath;
    public string DisplayVersion => _displayVersion;

    private int _totalCount;
    private int _pinnedCount;
    private int _snippetCount;

    private DateTime _suppressedClipboardUntilUtc = DateTime.MinValue;
    private LogViewerWindow? _logViewerWindow;

    public ObservableCollection<ClipboardEntry> AllItems { get; } = new();
    public ObservableCollection<ClipboardEntry> FilteredItems { get; } = new();

    private sealed record ThemePalette(
    Dictionary<string, string> Brushes,
    string SidebarHeaderStart,
    string SidebarHeaderEnd,
    string TopGlowStart,
    string TopGlowEnd,
    string BottomGlowStart,
    string BottomGlowEnd);

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

        Loaded += MainWindow_Loaded;

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

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        RunGuarded(() =>
        {
            ApplyTheme(_appSettings.Theme);
        }, "Initial theme apply");

        if (_appSettings.CheckForUpdatesOnStartup)
        {
            Dispatcher.BeginInvoke(
                new Action(async () => await TryRunStartupUpdateCheckAsync()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
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
            ApplySensitiveMaskingToEntry(item);
            AllItems.Add(item);
        }
    }

    private void ApplySettingsToUi()
    {
        ChkLaunchOnStartup.IsChecked = _appSettings.LaunchOnStartup;
        ChkClipboardMonitoring.IsChecked = _appSettings.ClipboardMonitoringEnabled;
        ChkMinimizeToTray.IsChecked = _appSettings.MinimizeToTray;
        ChkCloseToTray.IsChecked = _appSettings.CloseToTray;
        ChkCheckForUpdatesOnStartup.IsChecked = _appSettings.CheckForUpdatesOnStartup;
        ChkDetectSensitiveClipboardContent.IsChecked = _appSettings.DetectSensitiveClipboardContent;
        ChkMaskSensitiveClipboardContent.IsChecked = _appSettings.MaskSensitiveClipboardContent;
        ChkExcludeSensitiveClipboardContent.IsChecked = _appSettings.ExcludeSensitiveClipboardContent;
        UpdateSensitiveSettingsAvailability();
        MaxHistoryItemsTextBox.Text = _appSettings.MaxHistoryItems.ToString();

        if (ThemeComboBox is not null)
        {
            SelectThemeComboBoxItem(NormalizeThemeName(_appSettings.Theme));
        }
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

        SensitiveDetectionResult detection = _appSettings.DetectSensitiveClipboardContent
            ? _sensitiveContentDetector.Evaluate(rawText)
            : default;

        if (detection.IsSensitive && _appSettings.ExcludeSensitiveClipboardContent)
        {
            StatusMessage = "Sensitive clipboard content detected and excluded from history.";
            LogService.Info($"Sensitive clipboard content excluded from history. Reason: {detection.Reason}");
            return;
        }

        var entry = new ClipboardEntry
        {
            Title = BuildTitle(rawText),
            Category = InferCategory(rawText),
            FullText = rawText,
            IsSnippet = false,
            IsPinned = false,
            IsSensitive = detection.IsSensitive,
            IsSensitiveManual = false,
            CapturedAt = DateTime.Now
        };

        ApplySensitiveMaskingToEntry(entry);

        entry.Id = _storageService.InsertItem(entry);

        AllItems.Add(entry);
        int trimmedCount = TrimHistory();
        ApplyFilter();

        StatusMessage = trimmedCount > 0
            ? $"Captured {GetEntryLabelForStatus(entry)}. Trimmed {trimmedCount} old item(s) to stay within the history limit."
            : $"Captured {GetEntryLabelForStatus(entry)}.";

        LogService.Info($"Clipboard text captured: {GetEntryLabelForLog(entry)}");
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

    private void ApplySensitiveMaskingToEntry(ClipboardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.IsPreviewMasked = entry.IsSensitive && _appSettings.MaskSensitiveClipboardContent;
    }

    private void ApplySensitivePreviewMasking()
    {
        foreach (var item in AllItems)
        {
            ApplySensitiveMaskingToEntry(item);
        }
    }

    private void UpdateSensitiveSettingsAvailability()
    {
        bool detectionEnabled = ChkDetectSensitiveClipboardContent?.IsChecked == true;

        if (ChkMaskSensitiveClipboardContent is not null)
        {
            ChkMaskSensitiveClipboardContent.IsEnabled = detectionEnabled;
        }

        if (ChkExcludeSensitiveClipboardContent is not null)
        {
            ChkExcludeSensitiveClipboardContent.IsEnabled = detectionEnabled;
        }
    }

    private static string GetEntryLabelForLog(ClipboardEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsSensitive ? "[sensitive item]" : item.Title;
    }

    private static string GetEntryLabelForStatus(ClipboardEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsSensitive ? "sensitive item" : $"\"{item.Title}\"";
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
        if (!AreFilterVisualsReady())
            return;

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
                (x.Title ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                (x.Category ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                (x.FullText ?? string.Empty).ToLowerInvariant().Contains(lowered));
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
        if (NoItemsText is null || ClipboardScroller is null)
            return;

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

    private bool AreFilterVisualsReady()
    {
        return SearchTextBox is not null &&
               NoItemsText is not null &&
               ClipboardScroller is not null;
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!AreFilterVisualsReady())
            return;

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
        StatusMessage = "Opening snippet editor...";

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
        StatusMessage = "Opening snippet editor...";

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

            StatusMessage = $"Copied {GetEntryLabelForStatus(item)} back to the clipboard.";
            LogService.Info($"Clipboard item copied: {GetEntryLabelForLog(item)}");
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
                ? $"Pinned {GetEntryLabelForStatus(item)}."
                : $"Unpinned {GetEntryLabelForStatus(item)}.";

            LogService.Info($"Pin state changed for {GetEntryLabelForLog(item)}. IsPinned={item.IsPinned}");
        }, "Pin toggle");
    }

    private void ToggleSensitiveButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            var item = FindEntryFromButton(sender);
            if (item is null)
            {
                StatusMessage = "Could not find that clipboard item.";
                LogService.Warn("Sensitive toggle requested for an item that could not be found.");
                return;
            }

            bool wasSensitive = item.IsSensitive;
            bool markSensitive = !item.IsSensitive;

            item.IsSensitive = markSensitive;
            item.IsSensitiveManual = markSensitive;
            ApplySensitiveMaskingToEntry(item);

            _storageService.UpdateSensitivityState(item.Id, item.IsSensitive, item.IsSensitiveManual);

            ApplyFilter();

            StatusMessage = markSensitive
                ? "Marked the item as sensitive."
                : "Removed the sensitive flag from the item.";

            LogService.Info(markSensitive
                ? "Item marked as sensitive."
                : wasSensitive
                    ? "Sensitive flag removed from item."
                    : "Sensitive flag toggle completed.");
        }, "Sensitive toggle");
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

            string deletePrompt = item.IsSensitive
                ? $"Delete this {itemType}?{Environment.NewLine}{Environment.NewLine}This item is marked sensitive."
                : $"Delete this {itemType}?{Environment.NewLine}{Environment.NewLine}\"{item.Title}\"";

            var result = DialogService.Show(
                deletePrompt,
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
                ? $"Deleted snippet {GetEntryLabelForStatus(item)}."
                : $"Deleted {GetEntryLabelForStatus(item)}.";

            LogService.Info($"{itemType} deleted: {GetEntryLabelForLog(item)}");
        }, "Item deletion");
    }

    private void SensitiveDetectionSettingChanged(object sender, RoutedEventArgs e)
    {
        UpdateSensitiveSettingsAvailability();
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
            _appSettings.CheckForUpdatesOnStartup = ChkCheckForUpdatesOnStartup.IsChecked == true;
            _appSettings.DetectSensitiveClipboardContent = ChkDetectSensitiveClipboardContent.IsChecked == true;
            _appSettings.MaskSensitiveClipboardContent = ChkMaskSensitiveClipboardContent.IsChecked == true;
            _appSettings.ExcludeSensitiveClipboardContent = ChkExcludeSensitiveClipboardContent.IsChecked == true;
            _appSettings.MaxHistoryItems = maxHistoryItems;
            string selectedTheme =
                (ThemeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                ?? ThemeComboBox.Text
                ?? string.Empty;

            _appSettings.Theme = NormalizeThemeName(selectedTheme);

            ApplyTheme(_appSettings.Theme);

            _startupService.SetStartupEnabled(_appSettings.LaunchOnStartup);
            _storageService.SaveAppSettings(_appSettings);

            ApplyClipboardMonitoringSetting();
            ApplyTrayVisibility();
            ApplySensitivePreviewMasking();

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
        await CheckForUpdatesAsync(isAutomatic: false);
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
        ApplyTheme(_appSettings.Theme);
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

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is not WpfComboBox comboBox)
            return;

        string selectedTheme =
            (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? comboBox.Text
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(selectedTheme))
            return;

        string normalizedTheme = NormalizeThemeName(selectedTheme);
        ApplyTheme(normalizedTheme);
        StatusMessage = $"Previewing {normalizedTheme} theme. Save settings to keep it.";
    }

    private void ApplyTheme(string? themeName)
    {
        string normalizedTheme = NormalizeThemeName(themeName);
        ThemePalette palette = GetThemePalette(normalizedTheme);

        foreach (var pair in palette.Brushes)
        {
            SetBrushColor(pair.Key, pair.Value);
        }

        SetGradientStops("SidebarHeaderGlowBrush", palette.SidebarHeaderStart, palette.SidebarHeaderEnd);
        SetGradientStops("TopGlowBrush", palette.TopGlowStart, palette.TopGlowEnd);
        SetGradientStops("BottomGlowBrush", palette.BottomGlowStart, palette.BottomGlowEnd);

        Background = (WpfBrush)FindResource("AppBackgroundBrush");
        _appSettings.Theme = normalizedTheme;

        if (ThemeComboBox is not null && !Equals(ThemeComboBox.SelectedValue, normalizedTheme))
        {
            ThemeComboBox.SelectedValue = normalizedTheme;
        }
    }

    private static string NormalizeThemeName(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
            return "Midnight";

        return themeName.Trim() switch
        {
            "Dark" => "Midnight",
            "Midnight" => "Midnight",
            "Graphite" => "Graphite",
            "Aurora" => "Aurora",
            _ => "Midnight"
        };
    }

    private static ThemePalette GetThemePalette(string themeName)
    {
        return themeName switch
        {
            "Graphite" => new ThemePalette(
                new Dictionary<string, string>
                {
                    ["AppBackgroundBrush"] = "#0D1014",
                    ["SidebarBrush"] = "#12161B",
                    ["PanelBrush"] = "#141920",
                    ["CardBrush"] = "#182028",
                    ["CardHoverBrush"] = "#1D2731",
                    ["CardBorderBrush"] = "#2C3947",
                    ["CardBorderHoverBrush"] = "#45576B",
                    ["AccentBrush"] = "#9AB0C9",
                    ["AccentStrongBrush"] = "#C7D4E2",
                    ["AccentSoftBrush"] = "#1E2935",
                    ["AccentSoftHoverBrush"] = "#273545",
                    ["TextPrimaryBrush"] = "#F3F7FB",
                    ["TextMutedBrush"] = "#9AA9BA",
                    ["TextSoftBrush"] = "#C1CCD8",
                    ["SidebarSelectedBrush"] = "#1E2935",
                    ["DangerBrush"] = "#6F2630",
                    ["DangerHoverBrush"] = "#87303A",
                    ["DangerBorderBrush"] = "#A24755",
                    ["InputBackgroundBrush"] = "#11171D",
                    ["InputBorderBrush"] = "#334252",
                    ["InputBorderHoverBrush"] = "#4B5E74",
                    ["PillBackgroundBrush"] = "#1B2632",
                    ["PillBorderBrush"] = "#40556D",
                    ["PinnedCardBrush"] = "#1A2633",
                    ["PinnedBorderBrush"] = "#8FA7C1",
                    ["ScrollTrackBrush"] = "#111821",
                    ["ScrollThumbBrush"] = "#3B4D61",
                    ["ScrollThumbHoverBrush"] = "#4B627B",
                    ["ScrollThumbPressedBrush"] = "#5E7A99",
                    ["SidebarHoverBrush"] = "#1A2430",
                    ["SidebarHoverBorderBrush"] = "#334354",
                    ["SidebarPressedBrush"] = "#223041",
                    ["SidebarPressedBorderBrush"] = "#4C6179",
                    ["ActionButtonBackgroundBrush"] = "#1B2632",
                    ["ActionButtonBorderBrush"] = "#334252",
                    ["ActionButtonHoverBrush"] = "#243243",
                    ["ActionButtonHoverBorderBrush"] = "#4A5E75",
                    ["ActionButtonPressedBrush"] = "#2A3A4D",
                    ["PrimaryButtonBorderBrush"] = "#7E95AD",
                    ["PrimaryButtonPressedBrush"] = "#2B3B4C",
                    ["DangerHoverBorderBrush"] = "#BF5A69",
                    ["DangerPressedBrush"] = "#6C2630",
                    ["SidebarOuterBorderBrush"] = "#202A35",
                    ["SidebarHeaderBorderBrush"] = "#324352",
                    ["StorageBadgeBackgroundBrush"] = "#1C2A3A",
                    ["StorageBadgeBorderBrush"] = "#546B83",
                    ["StorageBadgeShadowBrush"] = "#0B1118",
                    ["StatusIndicatorBrush"] = "#B8C7D8",
                    ["SurfaceShadowBrush"] = "#090C10",
                    ["ComboBoxPopupBrush"] = "#141920",
                    ["ComboBoxPopupBorderBrush"] = "#2C3947"
                },
                "#263544",
                "#12161B",
                "#2C3D52",
                "#002C3D52",
                "#1E2A38",
                "#001E2A38"),

            "Aurora" => new ThemePalette(
                new Dictionary<string, string>
                {
                    ["AppBackgroundBrush"] = "#071216",
                    ["SidebarBrush"] = "#0C171B",
                    ["PanelBrush"] = "#0E1B20",
                    ["CardBrush"] = "#112128",
                    ["CardHoverBrush"] = "#15303A",
                    ["CardBorderBrush"] = "#21444E",
                    ["CardBorderHoverBrush"] = "#2F6775",
                    ["AccentBrush"] = "#4FD7C4",
                    ["AccentStrongBrush"] = "#87EBDD",
                    ["AccentSoftBrush"] = "#13343B",
                    ["AccentSoftHoverBrush"] = "#18434C",
                    ["TextPrimaryBrush"] = "#F3FCFB",
                    ["TextMutedBrush"] = "#95B7BC",
                    ["TextSoftBrush"] = "#C3DADF",
                    ["SidebarSelectedBrush"] = "#13343B",
                    ["DangerBrush"] = "#6F2630",
                    ["DangerHoverBrush"] = "#87303A",
                    ["DangerBorderBrush"] = "#A24755",
                    ["InputBackgroundBrush"] = "#0C171B",
                    ["InputBorderBrush"] = "#2A5863",
                    ["InputBorderHoverBrush"] = "#398091",
                    ["PillBackgroundBrush"] = "#123039",
                    ["PillBorderBrush"] = "#2E6875",
                    ["PinnedCardBrush"] = "#102A31",
                    ["PinnedBorderBrush"] = "#58D7C7",
                    ["ScrollTrackBrush"] = "#0D171C",
                    ["ScrollThumbBrush"] = "#2B5A65",
                    ["ScrollThumbHoverBrush"] = "#377583",
                    ["ScrollThumbPressedBrush"] = "#4592A4",
                    ["SidebarHoverBrush"] = "#11303A",
                    ["SidebarHoverBorderBrush"] = "#24505A",
                    ["SidebarPressedBrush"] = "#16414A",
                    ["SidebarPressedBorderBrush"] = "#327281",
                    ["ActionButtonBackgroundBrush"] = "#123039",
                    ["ActionButtonBorderBrush"] = "#24505A",
                    ["ActionButtonHoverBrush"] = "#18424B",
                    ["ActionButtonHoverBorderBrush"] = "#327281",
                    ["ActionButtonPressedBrush"] = "#1D4E59",
                    ["PrimaryButtonBorderBrush"] = "#4FD7C4",
                    ["PrimaryButtonPressedBrush"] = "#1B4B53",
                    ["DangerHoverBorderBrush"] = "#BF5A69",
                    ["DangerPressedBrush"] = "#6C2630",
                    ["SidebarOuterBorderBrush"] = "#163038",
                    ["SidebarHeaderBorderBrush"] = "#27515C",
                    ["StorageBadgeBackgroundBrush"] = "#11303A",
                    ["StorageBadgeBorderBrush"] = "#3A8090",
                    ["StorageBadgeShadowBrush"] = "#071216",
                    ["StatusIndicatorBrush"] = "#4FD7C4",
                    ["SurfaceShadowBrush"] = "#061015",
                    ["ComboBoxPopupBrush"] = "#0E1B20",
                    ["ComboBoxPopupBorderBrush"] = "#21444E"
                },
                "#184C57",
                "#0C171B",
                "#186873",
                "#00186873",
                "#114952",
                "#00114952"),

            _ => new ThemePalette(
                new Dictionary<string, string>
                {
                    ["AppBackgroundBrush"] = "#0B1118",
                    ["SidebarBrush"] = "#0F1722",
                    ["PanelBrush"] = "#101925",
                    ["CardBrush"] = "#121C28",
                    ["CardHoverBrush"] = "#152233",
                    ["CardBorderBrush"] = "#223040",
                    ["CardBorderHoverBrush"] = "#33506F",
                    ["AccentBrush"] = "#4EA1FF",
                    ["AccentStrongBrush"] = "#6AB2FF",
                    ["AccentSoftBrush"] = "#162437",
                    ["AccentSoftHoverBrush"] = "#1B3047",
                    ["TextPrimaryBrush"] = "#F3F7FB",
                    ["TextMutedBrush"] = "#95A7BC",
                    ["TextSoftBrush"] = "#B9C6D5",
                    ["SidebarSelectedBrush"] = "#162437",
                    ["DangerBrush"] = "#6F2630",
                    ["DangerHoverBrush"] = "#87303A",
                    ["DangerBorderBrush"] = "#A24755",
                    ["InputBackgroundBrush"] = "#0E1620",
                    ["InputBorderBrush"] = "#263547",
                    ["InputBorderHoverBrush"] = "#37506A",
                    ["PillBackgroundBrush"] = "#162334",
                    ["PillBorderBrush"] = "#29435D",
                    ["PinnedCardBrush"] = "#142334",
                    ["PinnedBorderBrush"] = "#4A86C7",
                    ["ScrollTrackBrush"] = "#0F1823",
                    ["ScrollThumbBrush"] = "#2A4159",
                    ["ScrollThumbHoverBrush"] = "#365674",
                    ["ScrollThumbPressedBrush"] = "#46719A",
                    ["SidebarHoverBrush"] = "#142131",
                    ["SidebarHoverBorderBrush"] = "#24384C",
                    ["SidebarPressedBrush"] = "#1A2D42",
                    ["SidebarPressedBorderBrush"] = "#36506D",
                    ["ActionButtonBackgroundBrush"] = "#162131",
                    ["ActionButtonBorderBrush"] = "#243548",
                    ["ActionButtonHoverBrush"] = "#1C2D43",
                    ["ActionButtonHoverBorderBrush"] = "#35506E",
                    ["ActionButtonPressedBrush"] = "#223854",
                    ["PrimaryButtonBorderBrush"] = "#315A87",
                    ["PrimaryButtonPressedBrush"] = "#25425F",
                    ["DangerHoverBorderBrush"] = "#BF5A69",
                    ["DangerPressedBrush"] = "#6C2630",
                    ["SidebarOuterBorderBrush"] = "#182433",
                    ["SidebarHeaderBorderBrush"] = "#263B52",
                    ["StorageBadgeBackgroundBrush"] = "#132338",
                    ["StorageBadgeBorderBrush"] = "#355B84",
                    ["StorageBadgeShadowBrush"] = "#0B1623",
                    ["StatusIndicatorBrush"] = "#4EA1FF",
                    ["SurfaceShadowBrush"] = "#0A1320",
                    ["ComboBoxPopupBrush"] = "#101925",
                    ["ComboBoxPopupBorderBrush"] = "#223040"
                },
                "#1A2B40",
                "#0F1722",
                "#16365B",
                "#0016365B",
                "#11263F",
                "#0011263F")
        };
    }


    private void SetBrushColor(string resourceKey, string hexColor)
    {
        var replacementBrush = new SolidColorBrush(ParseColor(hexColor));

        Resources[resourceKey] = replacementBrush;

        if (WpfApplication.Current is not null)
        {
            WpfApplication.Current.Resources[resourceKey] = replacementBrush;
        }
    }

    private void SetGradientStops(string resourceKey, string firstColor, string secondColor)
    {
        if (TryFindResource(resourceKey) is not GradientBrush existingBrush || existingBrush.GradientStops.Count < 2)
            return;

        GradientBrush replacementBrush = existingBrush switch
        {
            LinearGradientBrush linear => new LinearGradientBrush
            {
                StartPoint = linear.StartPoint,
                EndPoint = linear.EndPoint,
                MappingMode = linear.MappingMode,
                SpreadMethod = linear.SpreadMethod,
                ColorInterpolationMode = linear.ColorInterpolationMode,
                Opacity = linear.Opacity,
                Transform = linear.Transform,
                RelativeTransform = linear.RelativeTransform
            },
            RadialGradientBrush radial => new RadialGradientBrush
            {
                Center = radial.Center,
                GradientOrigin = radial.GradientOrigin,
                RadiusX = radial.RadiusX,
                RadiusY = radial.RadiusY,
                MappingMode = radial.MappingMode,
                SpreadMethod = radial.SpreadMethod,
                ColorInterpolationMode = radial.ColorInterpolationMode,
                Opacity = radial.Opacity,
                Transform = radial.Transform,
                RelativeTransform = radial.RelativeTransform
            },
            _ => existingBrush.CloneCurrentValue()
        };

        replacementBrush.GradientStops.Clear();
        replacementBrush.GradientStops.Add(new GradientStop(ParseColor(firstColor), existingBrush.GradientStops[0].Offset));
        replacementBrush.GradientStops.Add(new GradientStop(ParseColor(secondColor), existingBrush.GradientStops[1].Offset));

        for (int i = 2; i < existingBrush.GradientStops.Count; i++)
        {
            var stop = existingBrush.GradientStops[i];
            replacementBrush.GradientStops.Add(new GradientStop(stop.Color, stop.Offset));
        }

        Resources[resourceKey] = replacementBrush;

        if (WpfApplication.Current is not null)
        {
            WpfApplication.Current.Resources[resourceKey] = replacementBrush;
        }
    }

    private static WpfColor ParseColor(string hexColor)
    {
        return (WpfColor)WpfColorConverter.ConvertFromString(hexColor)!;
    }

    private void OpenGitHubButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/BrandonAustin01/ClipVault",
                UseShellExecute = true
            });

            StatusMessage = "Opened ClipVault GitHub.";
        }, "Open GitHub");
    }

    private void OpenWebsiteButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://brandonmckinney.dev/clipvault",
                UseShellExecute = true
            });

            StatusMessage = "Opened ClipVault website.";
        }, "Open website");
    }

    private void OpenCreditsButton_Click(object sender, RoutedEventArgs e)
    {
        RunGuarded(() =>
        {
            DialogService.Show(
                "ClipVault\n\nCreated by Brandon McKinney.\nBuilt with WPF, SQLite, and Velopack.\n\nSpecial thanks to:\nStackOverflow\nReddit",
                "ClipVault Credits",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            StatusMessage = "Opened credits.";
        }, "Open credits");
    }

    private async Task TryRunStartupUpdateCheckAsync()
    {
        if (_hasTriggeredStartupUpdateCheck || !_appSettings.CheckForUpdatesOnStartup)
            return;

        _hasTriggeredStartupUpdateCheck = true;

        if (PostUpdateExperienceService.HasPendingAnnouncement())
        {
            LogService.Info("Skipped automatic startup update check because a post-update announcement is pending.");
            return;
        }

        await CheckForUpdatesAsync(isAutomatic: true);
    }

    private string GetEffectiveUpdateFeedUrl()
    {
        return string.IsNullOrWhiteSpace(_appSettings.UpdateFeedUrl)
            ? DefaultUpdateFeedUrl
            : _appSettings.UpdateFeedUrl.Trim();
    }

    private async Task CheckForUpdatesAsync(bool isAutomatic)
    {
        if (_isUpdateCheckRunning)
        {
            if (!isAutomatic)
            {
                StatusMessage = "An update check is already running.";
            }

            return;
        }

        string updateFeedUrl = GetEffectiveUpdateFeedUrl();

        try
        {
            _isUpdateCheckRunning = true;

            if (!isAutomatic)
            {
                StatusMessage = "Checking for updates...";
            }

            LogService.Info($"{(isAutomatic ? "Automatic" : "Manual")} update check started from {updateFeedUrl}");

            IProgress<int>? progress = null;

            if (!isAutomatic)
            {
                progress = new Progress<int>(percent =>
                {
                    StatusMessage = $"Downloading update... {percent}%";
                });
            }

            var result = await _updateService.CheckForUpdatesAsync(updateFeedUrl, progress);

            switch (result.State)
            {
                case UpdateCheckState.NoFeedConfigured:
                    if (!isAutomatic)
                    {
                        StatusMessage = "The update feed is not configured in this build.";
                        DialogService.Show(
                            "The update feed is not configured in this build.",
                            "ClipVault",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        LogService.Warn("Automatic update check skipped because no update feed URL is configured.");
                    }
                    break;

                case UpdateCheckState.NotInstalled:
                    if (!isAutomatic)
                    {
                        StatusMessage = "Updates only work from an installed ClipVault build.";
                        DialogService.Show(
                            "ClipVault is not running from a Velopack-installed build yet." + Environment.NewLine + Environment.NewLine +
                            "This is expected if you launched it from Visual Studio, bin\\Release, or a loose publish folder. " +
                            "Install the packaged Setup.exe build first, then update checks will work.",
                            "ClipVault",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        LogService.Info("Automatic update check skipped because ClipVault is not running from an installed build.");
                    }
                    break;

                case UpdateCheckState.UpToDate:
                    if (!isAutomatic)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(result.CurrentVersion)
                            ? "ClipVault is already up to date."
                            : $"ClipVault is already up to date ({result.CurrentVersion}).";

                        DialogService.Show(
                            StatusMessage,
                            "ClipVault",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        LogService.Info(
                            string.IsNullOrWhiteSpace(result.CurrentVersion)
                                ? "Automatic update check found no updates."
                                : $"Automatic update check found no updates. Current version: {result.CurrentVersion}.");
                    }
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

            if (!isAutomatic)
            {
                StatusMessage = "Update check failed. Check the log.";

                DialogService.Show(
                    $"ClipVault could not finish checking for updates.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    "ClipVault Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                LogService.Warn("Automatic startup update check failed silently.");
            }
        }
        finally
        {
            _isUpdateCheckRunning = false;
        }
    }

    private void SelectThemeComboBoxItem(string themeName)
    {
        if (ThemeComboBox is null)
            return;

        string normalizedTheme = NormalizeThemeName(themeName);

        foreach (var item in ThemeComboBox.Items)
        {
            if (item is ComboBoxItem comboBoxItem)
            {
                string itemTheme = NormalizeThemeName(comboBoxItem.Content?.ToString());
                if (string.Equals(itemTheme, normalizedTheme, StringComparison.Ordinal))
                {
                    ThemeComboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }
        }

        ThemeComboBox.Text = normalizedTheme;
    }
}
