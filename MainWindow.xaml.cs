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
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace clipdump_re;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConfigurationService _configurationService;
    private readonly LoggingService _loggingService;
    private ClipboardService _clipboardService;
    private TrayIconService _trayIconService;
    private Settings _settings;
    private HwndSource _hwndSource;

    // P/Invoke declarations for clipboard monitoring
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private const int WM_CLIPBOARDUPDATE = 0x031D;

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        
        // Get the window handle
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource.AddHook(WndProc);
        
        // Register for clipboard format listener
        if (AddClipboardFormatListener(new WindowInteropHelper(this).Handle))
        {
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
                Dispatcher.BeginInvoke(new Action(async () => 
                {
                    try
                    {
                        await _clipboardService.DumpClipboardContentAsync();
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

    private async void LoadSettings()
    {
        _loggingService.LogEvent("SettingsLoadStarted", "Loading settings from configuration", "");
        _settings = await _configurationService.LoadSettingsAsync();
        
        // Initialize clipboard service with loaded settings
        _clipboardService = new ClipboardService(_settings, _loggingService);
        
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
        _trayIconService = new TrayIconService(this, _loggingService);
    }

    private async void AddNewRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;

        var newRule = new ClipboardFormatRule
        {
            Format = "NewFormat",
            MaxSizeKB = _settings.MaxFileSizeKB,
            ShouldIgnore = false,
            RelativeDestinationDirectory = ""
        };

        _settings.FormatRules.Add(newRule);
        await _configurationService.SaveSettingsAsync(_settings);
        
        // Refresh the DataGrid
        FormatDataGrid.Items.Refresh();
        
        _loggingService.LogEvent("FormatRuleAdded", "New format rule added", $"Format: {newRule.Format}");
    }

    private async void RemoveRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null || FormatDataGrid.SelectedItem == null) return;

        var selectedRule = FormatDataGrid.SelectedItem as ClipboardFormatRule;
        if (selectedRule != null)
        {
            _settings.FormatRules.Remove(selectedRule);
            await _configurationService.SaveSettingsAsync(_settings);
            
            // Refresh the DataGrid
            FormatDataGrid.Items.Refresh();
            
            _loggingService.LogEvent("FormatRuleRemoved", "Format rule removed", $"Format: {selectedRule.Format}");
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
        // Remove clipboard listener
        if (_hwndSource != null)
        {
            RemoveClipboardFormatListener(new WindowInteropHelper(this).Handle);
            _hwndSource.RemoveHook(WndProc);
            _loggingService.LogEvent("ClipboardListenerRemoved", "Clipboard format listener removed", "");
        }
        
        _trayIconService?.Dispose();
        base.OnClosed(e);
    }
}