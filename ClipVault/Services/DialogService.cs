using System.Linq;
using WpfApplication = System.Windows.Application;
using WpfWindow = System.Windows.Window;
using System.Windows;

namespace ClipVault.Services;

public static class DialogService
{
    public static MessageBoxResult Show(
        string messageBoxText,
        string caption = "ClipVault",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        WpfWindow? owner = null)
    {
        var dialog = new ClipVaultDialogWindow(messageBoxText, caption, button, icon);

        owner ??= WpfApplication.Current?.Windows
            .OfType<WpfWindow>()
            .FirstOrDefault(x => x.IsActive);

        if (owner is not null && owner != dialog)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }
}