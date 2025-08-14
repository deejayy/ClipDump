using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ClipDumpRe.Services;
using ClipDumpRe.Models;
using System.ComponentModel;
using System.Drawing;
using WinForms = System.Windows.Forms;

namespace clipdump_re;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConfigurationService _configurationService;
    private readonly LoggingService _loggingService;
    private Settings _settings;
    private NotifyIcon _notifyIcon;

    public MainWindow()
    {
        InitializeComponent();
        _configurationService = new ConfigurationService();
        _loggingService = new LoggingService();
        
        _loggingService.LogEvent("ApplicationStarted", "MainWindow initialized", "");
        LoadSettings();
        AttachEventHandlers();
        InitializeTrayIcon();
    }

    private async void LoadSettings()
    {
        _loggingService.LogEvent("SettingsLoadStarted", "Loading settings from configuration", "");
        _settings = await _configurationService.LoadSettingsAsync();
        
        // Populate UI controls with loaded settings
        WorkingDirectoryTextBox.Text = _settings.WorkingDirectory;
        MaxFileSizeTextBox.Text = _settings.MaxFileSizeKB.ToString();
        FormatDataGrid.ItemsSource = _settings.FormatRules;
        
        _loggingService.LogEvent("SettingsLoadCompleted", "Settings applied to UI controls", $"Rules count: {_settings.FormatRules.Count}");
    }

    private void AttachEventHandlers()
    {
        WorkingDirectoryTextBox.TextChanged += OnSettingsChanged;
        MaxFileSizeTextBox.TextChanged += OnSettingsChanged;
        FormatDataGrid.CellEditEnding += OnDataGridCellEditEnding;
        
        _loggingService.LogEvent("EventHandlersAttached", "UI event handlers attached", "");
    }

    private async void OnSettingsChanged(object sender, EventArgs e)
    {
        if (_settings == null) return;

        var control = sender as System.Windows.Controls.Control;
        var oldWorkingDir = _settings.WorkingDirectory;
        var oldMaxSize = _settings.MaxFileSizeKB;

        // Update settings from UI controls
        _settings.WorkingDirectory = WorkingDirectoryTextBox.Text;
        
        if (int.TryParse(MaxFileSizeTextBox.Text, out int maxSizeKB))
        {
            _settings.MaxFileSizeKB = maxSizeKB;
        }

        await _configurationService.SaveSettingsAsync(_settings);
        
        _loggingService.LogEvent("SettingsChanged", $"Settings updated via {control?.Name ?? "Unknown"}", 
            $"WorkingDir: {oldWorkingDir} -> {_settings.WorkingDirectory}, MaxSize: {oldMaxSize} -> {_settings.MaxFileSizeKB}");
    }

    private async void OnDataGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (_settings == null) return;

        var item = e.Row.Item as ClipboardFormatRule;
        var columnHeader = e.Column.Header?.ToString() ?? "Unknown";
        
        // Save settings after DataGrid edit
        await Task.Delay(100); // Small delay to ensure the edit is committed
        await _configurationService.SaveSettingsAsync(_settings);
        
        _loggingService.LogEvent("DataGridCellEdited", $"Format rule edited in column '{columnHeader}'", 
            $"Format: {item?.Format ?? "Unknown"}");
    }

    private void InitializeTrayIcon()
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

    private void NotifyIcon_MouseClick(object sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Dispatcher.Invoke(ToggleWindowVisibility);
        }
    }

    private void NotifyIcon_MouseDoubleClick(object sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Dispatcher.Invoke(ToggleWindowVisibility);
        }
    }

    private void ExitApplication()
    {
        Dispatcher.Invoke(() =>
        {
            _loggingService.LogEvent("ApplicationExit", "User requested exit from tray menu", "");
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        });
    }

    private void ToggleWindowVisibility()
    {
        if (WindowState == WindowState.Minimized || !IsVisible)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            _loggingService.LogEvent("WindowRestored", "Window restored from tray", "");
        }
        else
        {
            Hide();
            _loggingService.LogEvent("WindowHidden", "Window hidden to tray", "");
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        _loggingService.LogEvent("WindowClosing", "Window close intercepted, minimized to tray", "");
    }

    protected override void OnClosed(EventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnClosed(e);
    }
}