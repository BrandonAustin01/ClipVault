using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipVault.Services;

public sealed class ClipboardMonitorService
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private bool _isListening;

    public bool IsListening => _isListening;

    public event EventHandler<string>? ClipboardTextCaptured;

    public void Start(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (_isListening)
            return;

        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.EnsureHandle();

        _hwndSource = HwndSource.FromHwnd(_windowHandle)
            ?? throw new InvalidOperationException("Could not create an HwndSource for clipboard monitoring.");

        _hwndSource.AddHook(WndProc);

        if (!AddClipboardFormatListener(_windowHandle))
        {
            int errorCode = Marshal.GetLastWin32Error();
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
            _windowHandle = IntPtr.Zero;

            throw new InvalidOperationException(
                $"AddClipboardFormatListener failed with Win32 error {errorCode}.");
        }

        _isListening = true;
        LogService.Info("Clipboard monitoring started.");
    }

    public void Stop()
    {
        if (!_isListening)
            return;

        try
        {
            if (_windowHandle != IntPtr.Zero)
            {
                bool removed = RemoveClipboardFormatListener(_windowHandle);
                if (!removed)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    LogService.Warn($"RemoveClipboardFormatListener failed with Win32 error {errorCode}.");
                }
            }

            if (_hwndSource is not null)
            {
                _hwndSource.RemoveHook(WndProc);
            }

            LogService.Info("Clipboard monitoring stopped.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Failed while stopping clipboard monitoring.");
        }
        finally
        {
            _hwndSource = null;
            _windowHandle = IntPtr.Zero;
            _isListening = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            TryCaptureClipboardText();
        }

        return IntPtr.Zero;
    }

    private void TryCaptureClipboardText()
    {
        try
        {
            if (!ClipboardService.TryGetText(out var text, out var errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage) &&
                    !errorMessage.Contains("does not currently contain text", StringComparison.OrdinalIgnoreCase))
                {
                    LogService.Warn($"Clipboard text read skipped: {errorMessage}");
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                ClipboardTextCaptured?.Invoke(this, text);
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Unexpected clipboard monitor failure.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}