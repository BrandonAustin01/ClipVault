using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClipVault.Services;
using WpfMessageBox = System.Windows.MessageBox;
using WpfClipboard = System.Windows.Clipboard;

namespace ClipVault;

public partial class LogViewerWindow : Window
{
    private readonly string _logFilePath;
    private List<string> _allBlocks = new();

    public LogViewerWindow(string logFilePath)
    {
        InitializeComponent();

        _logFilePath = logFilePath;
        LogPathTextBlock.Text = logFilePath;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        RefreshLogs();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshLogs();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void LevelFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ApplyFilters();
    }

    private void CopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(LogTextBox.Text))
            {
                StatusTextBlock.Text = "Nothing to copy.";
                return;
            }

            if (!ClipboardService.TrySetText(LogTextBox.Text, out var errorMessage))
            {
                StatusTextBlock.Text = $"Copy failed: {errorMessage}";
                return;
            }

            StatusTextBlock.Text = "Copied current log view to clipboard.";
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed to copy log text.");
            WpfMessageBox.Show(
                $"Could not copy the current log view.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "ClipVault",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? folder = Path.GetDirectoryName(_logFilePath);
            if (string.IsNullOrWhiteSpace(folder))
            {
                StatusTextBlock.Text = "Could not determine the logs folder.";
                return;
            }

            Directory.CreateDirectory(folder);

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });

            StatusTextBlock.Text = "Opened logs folder.";
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed to open logs folder from the log viewer.");
            WpfMessageBox.Show(
                $"Could not open the logs folder.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "ClipVault",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RefreshLogs()
    {
        try
        {
            if (!File.Exists(_logFilePath))
            {
                _allBlocks = new List<string>();
                LogTextBox.Text = string.Empty;
                EmptyStateTextBlock.Visibility = Visibility.Visible;
                StatusTextBlock.Text = "No log file was found yet.";
                return;
            }

            string raw = File.ReadAllText(_logFilePath);

            _allBlocks = SplitIntoBlocks(raw);
            ApplyFilters();

            StatusTextBlock.Text = _allBlocks.Count == 0
                ? "Log file is empty."
                : $"Loaded {_allBlocks.Count} log entr{(_allBlocks.Count == 1 ? "y" : "ies")}.";
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed to refresh the log viewer.");
            WpfMessageBox.Show(
                $"Could not load the log file.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "ClipVault",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<string> filtered = _allBlocks;

        string selectedLevel = (LevelFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All entries";
        filtered = selectedLevel switch
        {
            "Info only" => filtered.Where(block => block.Contains("[INFO]", StringComparison.OrdinalIgnoreCase)),
            "Warnings only" => filtered.Where(block => block.Contains("[WARN]", StringComparison.OrdinalIgnoreCase)),
            "Errors only" => filtered.Where(block => block.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase)),
            _ => filtered
        };

        string query = SearchTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(block => block.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var results = filtered.ToList();

        LogTextBox.Text = string.Join(Environment.NewLine + Environment.NewLine, results);
        EmptyStateTextBlock.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (results.Count > 0)
        {
            StatusTextBlock.Text = $"Showing {results.Count} filtered entr{(results.Count == 1 ? "y" : "ies")}.";
        }
        else
        {
            StatusTextBlock.Text = "No log entries match the current filter.";
        }
    }

    private static List<string> SplitIntoBlocks(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw.Replace("\r\n", "\n")
                  .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                  .Select(block => block.Trim())
                  .Where(block => !string.IsNullOrWhiteSpace(block))
                  .ToList();
    }
}