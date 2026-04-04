using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;
using WpfWindow = System.Windows.Window;

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
        if (WpfApplication.Current?.Dispatcher is Dispatcher dispatcher)
        {
            if (!dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(() => Show(messageBoxText, caption, button, icon, owner));
            }
        }

        try
        {
            owner = ResolveOwner(owner);

            var dialog = new ClipVaultDialogWindow(
                messageBoxText ?? string.Empty,
                string.IsNullOrWhiteSpace(caption) ? "ClipVault" : caption,
                button,
                icon);

            if (owner is not null && !ReferenceEquals(owner, dialog))
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            bool? result = dialog.ShowDialog();
            return result == true ? dialog.Result : NormalizeResult(dialog.Result, button);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "ClipVaultDialogWindow failed to open.");
            return GetDefaultResult(button);
        }
    }

    private static WpfWindow? ResolveOwner(WpfWindow? requestedOwner)
    {
        if (requestedOwner is not null && IsUsableOwner(requestedOwner))
        {
            return requestedOwner;
        }

        if (WpfApplication.Current is null)
        {
            return null;
        }

        var activeWindow = WpfApplication.Current.Windows
            .OfType<WpfWindow>()
            .FirstOrDefault(IsPreferredOwner);

        if (activeWindow is not null)
        {
            return activeWindow;
        }

        var mainWindow = WpfApplication.Current.MainWindow;
        return IsUsableOwner(mainWindow) ? mainWindow : null;
    }

    private static bool IsPreferredOwner(WpfWindow? window)
    {
        return IsUsableOwner(window) && window!.IsActive;
    }

    private static bool IsUsableOwner(WpfWindow? window)
    {
        return window is not null && window.IsLoaded && window.Visibility == Visibility.Visible;
    }

    private static MessageBoxResult NormalizeResult(MessageBoxResult result, MessageBoxButton button)
    {
        return result == MessageBoxResult.None ? GetDefaultResult(button) : result;
    }

    private static MessageBoxResult GetDefaultResult(MessageBoxButton button)
    {
        return button switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.None
        };
    }
}
