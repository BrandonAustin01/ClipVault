using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClipVault.Models;

public class ClipboardEntry : INotifyPropertyChanged
{
    private const string MaskedText = "••••••••••";

    private string _title = string.Empty;
    private string _category = "Text";
    private string _fullText = string.Empty;
    private bool _isSnippet;
    private bool _isPinned;
    private bool _isSensitive;
    private bool _isSensitiveManual;
    private bool _isPreviewMasked;
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
                OnPropertyChanged(nameof(DisplayTitle));
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

    public bool IsSensitive
    {
        get => _isSensitive;
        set
        {
            if (_isSensitive != value)
            {
                _isSensitive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(SensitiveButtonLabel));
                OnPropertyChanged(nameof(SensitiveBadgeText));
                OnPropertyChanged(nameof(SensitiveStateDescription));
            }
        }
    }

    public bool IsSensitiveManual
    {
        get => _isSensitiveManual;
        set
        {
            if (_isSensitiveManual != value)
            {
                _isSensitiveManual = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SensitiveBadgeText));
                OnPropertyChanged(nameof(SensitiveStateDescription));
            }
        }
    }

    public bool IsPreviewMasked
    {
        get => _isPreviewMasked;
        set
        {
            if (_isPreviewMasked != value)
            {
                _isPreviewMasked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(SensitiveStateDescription));
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

    public string DisplayTitle
    {
        get
        {
            if (IsSensitive && IsPreviewMasked)
                return "Sensitive item";

            if (string.IsNullOrWhiteSpace(Title))
                return "Clipboard item";

            return Title;
        }
    }

    public string DisplayText
    {
        get
        {
            if (IsSensitive && IsPreviewMasked)
                return MaskedText;

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

    public string SensitiveButtonLabel => IsSensitive ? "Unmark Sensitive" : "Mark Sensitive";

    public string SensitiveBadgeText => IsSensitiveManual ? "Sensitive • Manual" : "Sensitive";

    public string SensitiveStateDescription
    {
        get
        {
            if (!IsSensitive)
                return string.Empty;

            if (IsPreviewMasked)
                return "Preview hidden in ClipVault. Copy to use the original text.";

            return IsSensitiveManual
                ? "Manually marked sensitive. Visible because masking is off."
                : "Auto-detected as sensitive. Visible because masking is off.";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
