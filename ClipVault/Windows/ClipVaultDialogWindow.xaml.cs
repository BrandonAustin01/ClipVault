using System.Linq;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;


namespace ClipVault;

public partial class ClipVaultDialogWindow : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public ClipVaultDialogWindow(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage icon)
    {
        InitializeComponent();
        ValidateNamedControls();

        Title = string.IsNullOrWhiteSpace(title) ? "ClipVault" : title;
        TitleTextBlock.Text = Title;
        MessageTextBlock.Text = message ?? string.Empty;

        ApplyIcon(icon);
        BuildButtons(buttons);

        Loaded += (_, _) => FocusPrimaryButton();
    }

    private void ValidateNamedControls()
    {
        if (TitleTextBlock is null ||
            MessageTextBlock is null ||
            IconBadge is null ||
            IconGlyphTextBlock is null ||
            ButtonsPanel is null)
        {
            throw new InvalidOperationException("ClipVaultDialogWindow failed to initialize its visual tree.");
        }
    }

    private void ApplyIcon(MessageBoxImage icon)
    {
        var infoBrush = GetBrushOrFallback("InfoBrush", "#355B84");
        var warningBrush = GetBrushOrFallback("WarningBrush", "#B88B2E");
        var errorBrush = GetBrushOrFallback("ErrorBrush", "#A24755");

        switch (icon)
        {
            case MessageBoxImage.Warning:
                IconBadge.Background = warningBrush;
                IconGlyphTextBlock.Text = "!";
                break;

            case MessageBoxImage.Error:
                IconBadge.Background = errorBrush;
                IconGlyphTextBlock.Text = "×";
                break;

            case MessageBoxImage.Question:
                IconBadge.Background = infoBrush;
                IconGlyphTextBlock.Text = "?";
                break;

            default:
                IconBadge.Background = infoBrush;
                IconGlyphTextBlock.Text = "i";
                break;
        }
    }

    private void BuildButtons(MessageBoxButton buttons)
    {
        ButtonsPanel.Children.Clear();

        switch (buttons)
        {
            case MessageBoxButton.OK:
                ButtonsPanel.Children.Add(CreateButton("OK", true, MessageBoxResult.OK));
                break;

            case MessageBoxButton.OKCancel:
                ButtonsPanel.Children.Add(CreateButton("Cancel", false, MessageBoxResult.Cancel));
                ButtonsPanel.Children.Add(CreateButton("OK", true, MessageBoxResult.OK));
                break;

            case MessageBoxButton.YesNo:
                ButtonsPanel.Children.Add(CreateButton("No", false, MessageBoxResult.No));
                ButtonsPanel.Children.Add(CreateButton("Yes", true, MessageBoxResult.Yes));
                break;

            case MessageBoxButton.YesNoCancel:
                ButtonsPanel.Children.Add(CreateButton("Cancel", false, MessageBoxResult.Cancel));
                ButtonsPanel.Children.Add(CreateButton("No", false, MessageBoxResult.No));
                ButtonsPanel.Children.Add(CreateButton("Yes", true, MessageBoxResult.Yes));
                break;

            default:
                ButtonsPanel.Children.Add(CreateButton("OK", true, MessageBoxResult.OK));
                break;
        }
    }

    private WpfButton CreateButton(string text, bool primary, MessageBoxResult result)
    {
        var button = new WpfButton
        {
            Content = text,
            Style = GetButtonStyle(primary),
            IsDefault = text == "OK" || text == "Yes",
            IsCancel = text == "Cancel",
            MinWidth = 108,
            Margin = new Thickness(0, 0, 8, 0)
        };

        button.Click += (_, _) =>
        {
            Result = result;
            DialogResult = true;
            Close();
        };

        return button;
    }

    private Style GetButtonStyle(bool primary)
    {
        string key = primary ? "PrimaryButtonStyle" : "ActionButtonStyle";

        if (TryFindResource(key) is Style style)
        {
            return style;
        }

        throw new InvalidOperationException($"Required dialog button style '{key}' was not found.");
    }

    private WpfBrush GetBrushOrFallback(string resourceKey, string fallbackHex)
    {
        return TryFindResource(resourceKey) as WpfBrush
               ?? new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(fallbackHex)!);
    }

    private void FocusPrimaryButton()
    {
        var primaryButton = ButtonsPanel.Children
            .OfType<WpfButton>()
            .FirstOrDefault(x => x.IsDefault);

        primaryButton?.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (Result == MessageBoxResult.None)
        {
            Result = MessageBoxResult.Cancel;
        }

        base.OnClosed(e);
    }
}
