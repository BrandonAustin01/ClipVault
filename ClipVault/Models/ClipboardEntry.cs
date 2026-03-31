using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClipVault.Models;

public class ClipboardEntry : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _category = "Text";
    private string _fullText = string.Empty;
    private bool _isSnippet;
    private bool _isPinned;
    private DateTime _capturedAt = DateTime.Now;

    public int Id { get; set; }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public string Category
    {
        get => _category;
        set
        {
            if (_category != value)
            {
                _category = value;
                OnPropertyChanged();
            }
        }
    }

    public string FullText
    {
        get => _fullText;
        set
        {
            if (_fullText != value)
            {
                _fullText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    public bool IsSnippet
    {
        get => _isSnippet;
        set
        {
            if (_isSnippet != value)
            {
                _isSnippet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PinStateLabel));
            }
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned != value)
            {
                _isPinned = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PinStateLabel));
                OnPropertyChanged(nameof(PinButtonLabel));
            }
        }
    }

    public DateTime CapturedAt
    {
        get => _capturedAt;
        set
        {
            if (_capturedAt != value)
            {
                _capturedAt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DateLabel));
                OnPropertyChanged(nameof(TimeLabel));
            }
        }
    }

    public string DisplayText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FullText))
                return "(empty)";

            return FullText.Length > 220
                ? FullText[..220] + "..."
                : FullText;
        }
    }

    public string DateLabel => CapturedAt.ToString("MMM d");

    public string TimeLabel => CapturedAt.ToString("h:mm tt");

    public string PinStateLabel =>
        IsPinned ? "Pinned" :
        IsSnippet ? "Snippet" :
        "History";

    public string PinButtonLabel => IsPinned ? "Unpin" : "Pin";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}