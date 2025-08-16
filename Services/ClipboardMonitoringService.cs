using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipDumpRe.Services
{
    public class ClipboardMonitoringService : IDisposable
    {
        private readonly LoggingService _loggingService;
        private ClipboardService _clipboardService;
        private readonly Action _onClipboardChanged;
        private HwndSource _hwndSource;
        private IntPtr _windowHandle;
        private bool _isRegistered;
        private bool _disposed;

        // P/Invoke declarations for clipboard monitoring
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        public ClipboardMonitoringService(LoggingService loggingService, Action onClipboardChanged = null)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _onClipboardChanged = onClipboardChanged;
        }

        public void Initialize(IntPtr windowHandle, ClipboardService clipboardService)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ClipboardMonitoringService));

            _windowHandle = windowHandle;
            _clipboardService = clipboardService;

            // Get HwndSource and add hook
            _hwndSource = HwndSource.FromHwnd(windowHandle);
            _hwndSource?.AddHook(WndProc);

            // Register for clipboard format listener
            if (AddClipboardFormatListener(windowHandle))
            {
                _isRegistered = true;
                _loggingService.LogEvent("ClipboardListenerRegistered", "Successfully registered clipboard format listener", "");
            }
            else
            {
                _loggingService.LogEvent("ClipboardListenerFailed", "Failed to register clipboard format listener", $"Error: {Marshal.GetLastWin32Error()}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                _loggingService.LogEvent("ClipboardChanged", "Clipboard content changed", "");

                // Process clipboard content on UI thread to avoid STA issues
                if (_clipboardService != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            await _clipboardService.DumpClipboardContentAsync();
                            // Notify MainWindow to refresh seen formats
                            _onClipboardChanged?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogEvent("ClipboardProcessingError", "Error processing clipboard on UI thread", $"Error: {ex.Message}");
                        }
                    }));
                }

                handled = true;
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // Remove clipboard listener
            if (_isRegistered && _windowHandle != IntPtr.Zero)
            {
                RemoveClipboardFormatListener(_windowHandle);
                _isRegistered = false;
                _loggingService.LogEvent("ClipboardListenerRemoved", "Clipboard format listener removed", "");
            }

            // Remove window procedure hook
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;

            _disposed = true;
        }
    }
}
