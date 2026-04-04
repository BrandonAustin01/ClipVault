using ClipVault.Services;
using System;
using System.Windows;

namespace ClipVault
{
    public partial class UpdateAnnouncementWindow : Window
    {
        private bool _isChangelogVisible;

        public UpdateAnnouncementWindow(PostUpdateAnnouncement announcement)
        {
            ArgumentNullException.ThrowIfNull(announcement);

            InitializeComponent();

            string currentVersion = string.IsNullOrWhiteSpace(announcement.CurrentVersion)
                ? "latest build"
                : $"v{announcement.CurrentVersion}";

            if (string.IsNullOrWhiteSpace(announcement.PreviousVersion))
            {
                VersionTextBlock.Text = $"You are now running {currentVersion}.";
            }
            else
            {
                VersionTextBlock.Text = $"Updated from v{announcement.PreviousVersion} to {currentVersion}.";
            }

            SummaryTextBlock.Text =
                "ClipVault finished updating successfully. " +
                "You can review everything that changed by opening the changelog below.";

            ChangelogTextBox.Text = string.IsNullOrWhiteSpace(announcement.ChangelogText)
                ? "No changelog text was saved for this update."
                : announcement.ChangelogText;

            Loaded += (_, _) =>
            {
                if (ToggleChangelogButton is not null)
                {
                    ToggleChangelogButton.Focus();
                }
            };
        }

        private void ToggleChangelogButton_Click(object sender, RoutedEventArgs e)
        {
            _isChangelogVisible = !_isChangelogVisible;

            ChangelogBorder.Visibility = _isChangelogVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

            Height = _isChangelogVisible ? 640 : 320;

            ToggleChangelogButton.Content = _isChangelogVisible
                ? "Hide Changelog"
                : "View Changelog";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
