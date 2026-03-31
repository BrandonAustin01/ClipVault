using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;

namespace ClipVault.Services;

public static class ClipboardService
{
    public static bool TrySetText(string text, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errorMessage = "Clipboard text was empty.";
            return false;
        }

        return RetryClipboardOperation(() => WpfClipboard.SetText(text), out errorMessage);
    }

    public static bool TryGetText(out string? text, out string? errorMessage)
    {
        text = null;
        string? capturedText = null;

        bool success = RetryClipboardOperation(() =>
        {
            if (WpfClipboard.ContainsText())
            {
                capturedText = WpfClipboard.GetText();
            }
        }, out errorMessage);

        if (!success)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(capturedText))
        {
            errorMessage = "Clipboard does not currently contain text.";
            return false;
        }

        text = capturedText;
        errorMessage = null;
        return true;
    }

    private static bool RetryClipboardOperation(Action operation, out string? errorMessage)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                operation();
                errorMessage = null;
                return true;
            }
            catch (Exception ex) when (
                ex is COMException ||
                ex is ExternalException ||
                ex is InvalidOperationException)
            {
                lastException = ex;
                Thread.Sleep(40 * attempt);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        errorMessage = lastException?.Message ?? "Unknown clipboard error.";

        if (lastException is not null)
        {
            LogService.Error(lastException, "Clipboard operation failed.");
        }

        return false;
    }
}