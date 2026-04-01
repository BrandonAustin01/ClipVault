using System.Windows;

namespace ClipVault;

public partial class SnippetEditorWindow : Window
{
    public string SnippetTitle => TitleTextBox.Text.Trim();

    public string SnippetCategory =>
        string.IsNullOrWhiteSpace(CategoryTextBox.Text)
            ? "Snippet"
            : CategoryTextBox.Text.Trim();

    public string SnippetContent => ContentTextBox.Text.Trim();

    public SnippetEditorWindow()
    {
        InitializeComponent();
        CategoryTextBox.Text = "Snippet";
    }

    public SnippetEditorWindow(string title, string category, string content) : this()
    {
        TitleTextBox.Text = title;
        CategoryTextBox.Text = string.IsNullOrWhiteSpace(category) ? "Snippet" : category;
        ContentTextBox.Text = content;
    }

    protected override void OnContentRendered(System.EventArgs e)
    {
        base.OnContentRendered(e);
        TitleTextBox.Focus();
        TitleTextBox.SelectAll();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationTextBlock.Visibility = Visibility.Collapsed;
        ValidationTextBlock.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(SnippetTitle))
        {
            ShowValidation("Title is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SnippetContent))
        {
            ShowValidation("Content is required.");
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ShowValidation(string message)
    {
        ValidationTextBlock.Text = message;
        ValidationTextBlock.Visibility = Visibility.Visible;
    }
}