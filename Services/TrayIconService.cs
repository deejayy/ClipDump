using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using ClipDumpRe.Services;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace ClipDumpRe.Services
{
    public class TrayIconService : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private readonly LoggingService _loggingService;
        private readonly Window _parentWindow;
        private readonly WindowsStartupService _startupService;
        private System.Windows.Forms.Timer _enableTimer;
        private System.Windows.Forms.Timer _menuUpdateTimer;
        private DateTime _disableEndTime;
        private bool _isClipDumpEnabled = true;
        private ToolStripMenuItem _disableMenuItem;
        private ToolStripMenuItem _enableMenuItem;
        private string _lastSavedLocation; // Track last saved file or directory

        public bool IsClipDumpEnabled => _isClipDumpEnabled;

        public event EventHandler ProcessingStateChanged;

        internal TrayIconService(Window parentWindow, LoggingService loggingService, WindowsStartupService startupService)
        {
            _parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
            Initialize();
        }

        private void Initialize()
        {
            _notifyIcon = new NotifyIcon();

            UpdateTrayIcon();

            _notifyIcon.Text = "ClipDump";
            _notifyIcon.Visible = true;

            CreateContextMenu();

            // Handle mouse clicks
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;

            _loggingService.LogEvent("TrayIconInitialized", "System tray icon created", "");
        }

        private void UpdateTrayIcon()
        {
            try
            {
                string iconPath = _isClipDumpEnabled ? "clipdump.ico" : "clipdump-disabled.ico";
                var iconUri = new Uri($"pack://application:,,,/Resources/Icon/{iconPath}", UriKind.Absolute);
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
        }

        private void CreateContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            // Open Last Dump menu item
            var openLastDumpMenuItem = new ToolStripMenuItem("Open Last Dump");
            openLastDumpMenuItem.Click += (s, e) => OpenLastDump();
            openLastDumpMenuItem.Enabled = !string.IsNullOrEmpty(_lastSavedLocation);
            contextMenu.Items.Add(openLastDumpMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            // Disable ClipDump menu
            _disableMenuItem = new ToolStripMenuItem("Disable ClipDump");
            var disable1Min = new ToolStripMenuItem("For 1 minute");
            var disable5Min = new ToolStripMenuItem("For 5 minutes");
            var disable60Min = new ToolStripMenuItem("For 60 minutes");
            var disableUntilEnabled = new ToolStripMenuItem("Until I enable it again");

            disable1Min.Click += (s, e) => DisableClipDump(TimeSpan.FromMinutes(1));
            disable5Min.Click += (s, e) => DisableClipDump(TimeSpan.FromMinutes(5));
            disable60Min.Click += (s, e) => DisableClipDump(TimeSpan.FromMinutes(60));
            disableUntilEnabled.Click += (s, e) => DisableClipDump(null);

            _disableMenuItem.DropDownItems.Add(disable1Min);
            _disableMenuItem.DropDownItems.Add(disable5Min);
            _disableMenuItem.DropDownItems.Add(disable60Min);
            _disableMenuItem.DropDownItems.Add(disableUntilEnabled);

            // Enable ClipDump menu
            _enableMenuItem = new ToolStripMenuItem("Enable ClipDump");
            _enableMenuItem.Click += (s, e) => EnableClipDump();
            _enableMenuItem.Enabled = false; // Initially disabled since ClipDump is enabled

            contextMenu.Items.Add(_disableMenuItem);
            contextMenu.Items.Add(_enableMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            var exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void OpenLastDump()
        {
            try
            {
                if (string.IsNullOrEmpty(_lastSavedLocation))
                {
                    _loggingService.LogEvent("OpenLastDumpFailed", "No last saved location available", "");
                    return;
                }

                // Expand environment variables and resolve to absolute path
                string expandedPath = Environment.ExpandEnvironmentVariables(_lastSavedLocation);
                string absolutePath = System.IO.Path.GetFullPath(expandedPath);

                if (Directory.Exists(absolutePath))
                {
                    // Open directory
                    System.Diagnostics.Process.Start("explorer.exe", absolutePath);
                    _loggingService.LogEvent("LastDumpDirectoryOpened", "Last dump directory opened", 
                        $"Path: {absolutePath}");
                }
                else if (File.Exists(absolutePath))
                {
                    // Open directory and select file
                    string directoryPath = System.IO.Path.GetDirectoryName(absolutePath);
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{absolutePath}\"");
                    _loggingService.LogEvent("LastDumpFileOpened", "Last dump file opened and selected", 
                        $"File: {absolutePath}");
                }
                else
                {
                    _loggingService.LogEvent("OpenLastDumpFailed", "Last saved location no longer exists", 
                        $"Path: {absolutePath}");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogEvent("OpenLastDumpError", "Error opening last dump location", 
                    $"Error: {ex.Message}");
            }
        }

        public void UpdateLastSavedLocation(string location)
        {
            _lastSavedLocation = location;
            
            // Update the menu item state
            if (_notifyIcon?.ContextMenuStrip?.Items.Count > 0)
            {
                var openLastDumpMenuItem = _notifyIcon.ContextMenuStrip.Items[0] as ToolStripMenuItem;
                if (openLastDumpMenuItem != null)
                {
                    openLastDumpMenuItem.Enabled = !string.IsNullOrEmpty(_lastSavedLocation);
                }
            }
            
            _loggingService.LogEvent("LastSavedLocationUpdated", "Last saved location updated", 
                $"Location: {location}");
        }

        private void DisableClipDump(TimeSpan? duration)
        {
            _isClipDumpEnabled = false;
            UpdateTrayIcon();

            // Stop any existing timers
            _enableTimer?.Stop();
            _enableTimer?.Dispose();
            _menuUpdateTimer?.Stop();
            _menuUpdateTimer?.Dispose();

            if (duration.HasValue)
            {
                _disableEndTime = DateTime.Now.Add(duration.Value);

                _enableTimer = new System.Windows.Forms.Timer();
                _enableTimer.Interval = (int)duration.Value.TotalMilliseconds;
                _enableTimer.Tick += (s, e) => EnableClipDump();
                _enableTimer.Start();

                // Update menu text every second
                _menuUpdateTimer = new System.Windows.Forms.Timer();
                _menuUpdateTimer.Interval = 1000;
                _menuUpdateTimer.Tick += (s, e) => UpdateMenuText();
                _menuUpdateTimer.Start();

                _loggingService.LogEvent("ClipDumpDisabled", $"ClipDump disabled for {duration.Value.TotalMinutes} minutes", "");
            }
            else
            {
                _loggingService.LogEvent("ClipDumpDisabled", "ClipDump disabled until manually enabled", "");
            }

            UpdateMenuItems();
            ProcessingStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EnableClipDump()
        {
            _enableTimer?.Stop();
            _enableTimer?.Dispose();
            _enableTimer = null;

            _menuUpdateTimer?.Stop();
            _menuUpdateTimer?.Dispose();
            _menuUpdateTimer = null;

            _isClipDumpEnabled = true;
            UpdateTrayIcon();
            UpdateMenuItems();

            _loggingService.LogEvent("ClipDumpEnabled", "ClipDump re-enabled", "");
            ProcessingStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateMenuText()
        {
            if (_enableTimer != null && _enableMenuItem != null)
            {
                var timeRemaining = _disableEndTime - DateTime.Now;
                if (timeRemaining.TotalSeconds > 0)
                {
                    string timeText = timeRemaining.TotalMinutes >= 1
                        ? $"{(int)timeRemaining.TotalMinutes}m left"
                        : $"{(int)timeRemaining.TotalSeconds}s left";
                    _enableMenuItem.Text = $"Enable ClipDump ({timeText})";
                }
            }
        }

        private void UpdateMenuItems()
        {
            _disableMenuItem.Enabled = _isClipDumpEnabled;
            _enableMenuItem.Enabled = !_isClipDumpEnabled;

            if (_isClipDumpEnabled)
            {
                _enableMenuItem.Text = "Enable ClipDump";
            }
            else if (_enableTimer == null)
            {
                _enableMenuItem.Text = "Enable ClipDump";
            }
            else
            {
                UpdateMenuText();
            }
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

        public void SetStartupWithWindows(bool enable)
        {
            _startupService.SetStartupWithWindows(enable);
        }

        public bool IsStartupWithWindowsEnabled()
        {
            return _startupService.IsStartupWithWindowsEnabled();
        }

        public void Dispose()
        {
            _enableTimer?.Stop();
            _enableTimer?.Dispose();
            _menuUpdateTimer?.Stop();
            _menuUpdateTimer?.Dispose();
            _notifyIcon?.Dispose();
        }
    }
}
