using System;
using System.Threading;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace ClipVault.Services;

public static class ErrorHandler
{
    private static int _dialogOpen;

    public static void Handle(Exception exception, string userMessage, bool isFatal = false)
    {
        LogService.Error(exception, userMessage);

        if (Interlocked.Exchange(ref _dialogOpen, 1) == 1)
            return;

        try
        {
            var title = isFatal ? "ClipVault - Fatal Error" : "ClipVault - Error";

            var message =
                $"{userMessage}{Environment.NewLine}{Environment.NewLine}" +
                $"Details: {exception.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"Log file:{Environment.NewLine}{LogService.CurrentLogFilePath}";

            WpfMessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                isFatal ? MessageBoxImage.Error : MessageBoxImage.Warning);
        }
        finally
        {
            Interlocked.Exchange(ref _dialogOpen, 0);
        }
    }
}