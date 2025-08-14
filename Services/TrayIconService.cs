using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using ClipDumpRe.Services;

namespace ClipDumpRe.Services
{
    public class TrayIconService : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private readonly LoggingService _loggingService;
        private readonly Window _parentWindow;

        internal TrayIconService(Window parentWindow, LoggingService loggingService)
        {
            _parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            Initialize();
        }

        private void Initialize()
        {
            _notifyIcon = new NotifyIcon();
            
            // Extract icon from current application or use embedded resource
            try
            {
                var iconUri = new Uri("pack://application:,,,/Resources/Icon/clipdump.ico", UriKind.Absolute);
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    _notifyIcon.Icon = new Icon(streamInfo.Stream);
                }
                else
                {
                    // Fallback to system icon
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }
            
            _notifyIcon.Text = "ClipDump-Re";
            _notifyIcon.Visible = true;
            
            // Create context menu
            var contextMenu = new ContextMenuStrip();
            var exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitMenuItem);
            _notifyIcon.ContextMenuStrip = contextMenu;
            
            // Handle mouse clicks
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;
            
            _loggingService.LogEvent("TrayIconInitialized", "System tray icon created", "");
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _parentWindow.Dispatcher.Invoke(ToggleWindowVisibility);
            }
        }

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _parentWindow.Dispatcher.Invoke(ToggleWindowVisibility);
            }
        }

        private void ExitApplication()
        {
            _parentWindow.Dispatcher.Invoke(() =>
            {
                _loggingService.LogEvent("ApplicationExit", "User requested exit from tray menu", "");
                System.Windows.Application.Current.Shutdown();
            });
        }

        private void ToggleWindowVisibility()
        {
            if (_parentWindow.WindowState == WindowState.Minimized || !_parentWindow.IsVisible)
            {
                _parentWindow.Show();
                _parentWindow.WindowState = WindowState.Normal;
                _parentWindow.Activate();
                _loggingService.LogEvent("WindowRestored", "Window restored from tray", "");
            }
            else
            {
                _parentWindow.Hide();
                _loggingService.LogEvent("WindowHidden", "Window hidden to tray", "");
            }
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
        }
    }
}
