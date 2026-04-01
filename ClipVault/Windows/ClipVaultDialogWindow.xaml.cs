using System;
using System.Windows;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;

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

        Title = string.IsNullOrWhiteSpace(title) ? "ClipVault" : title;
        TitleTextBlock.Text = Title;
        MessageTextBlock.Text = message;

        ApplyIcon(icon);
        BuildButtons(buttons);
    }

    private void ApplyIcon(MessageBoxImage icon)
    {
        var infoBrush = (WpfBrush)FindResource("InfoBrush");
        var warningBrush = (WpfBrush)FindResource("WarningBrush");
        var errorBrush = (WpfBrush)FindResource("ErrorBrush");

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
            Style = (Style)FindResource(primary ? "PrimaryButtonStyle" : "ActionButtonStyle"),
            IsDefault = text == "OK" || text == "Yes",
            IsCancel = text == "Cancel"
        };

        button.Click += (_, _) =>
        {
            Result = result;
            DialogResult = true;
            Close();
        };

        return button;
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