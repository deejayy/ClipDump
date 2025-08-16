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
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;

namespace clipdump_re;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConfigurationService _configurationService;
    private readonly LoggingService _loggingService;
    private readonly ForegroundApplicationService _foregroundApplicationService;
    private readonly FormatCacheService _formatCacheService;
    private readonly ApplicationEnumerationService _applicationEnumerationService;
    private readonly WindowsStartupService _startupService;
    private readonly ClearUrlsService _clearUrlsService;
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
        _foregroundApplicationService = new ForegroundApplicationService(_loggingService);
        _formatCacheService = new FormatCacheService(_loggingService);
        _applicationEnumerationService = new ApplicationEnumerationService(_loggingService);
        _startupService = new WindowsStartupService(_loggingService);
        _clearUrlsService = new ClearUrlsService(_loggingService);

        _loggingService.LogEvent("ApplicationStarted", "MainWindow initialized", "");
        InitializeTrayIcon(); // Initialize tray icon BEFORE loading settings
        LoadSettings();
        AttachEventHandlers();
        LoadSeenFormats();
        InitializeClearUrlsService();
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
                        // Automatically refresh seen formats after clipboard processing
                        LoadSeenFormats();
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

    private async void InitializeClearUrlsService()
    {
        await _clearUrlsService.InitializeAsync();
    }

    private async void LoadSettings()
    {
        _loggingService.LogEvent("SettingsLoadStarted", "Loading settings from configuration", "");
        _settings = await _configurationService.LoadSettingsAsync();

        // Initialize clipboard service with loaded settings and the already-created tray icon service
        _clipboardService = new ClipboardService(_settings, _loggingService, _foregroundApplicationService, _trayIconService, _formatCacheService, _clearUrlsService);

        // Populate UI controls with loaded settings
        WorkingDirectoryTextBox.Text = _settings.WorkingDirectory;
        MaxFileSizeTextBox.Text = _settings.MaxFileSizeKB.ToString();
        MinClipboardDataSizeTextBox.Text = _settings.MinClipboardDataSizeValue.ToString();
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        UseTimestampSubdirectoriesCheckBox.IsChecked = _settings.UseTimestampSubdirectories;
        FormatDataGrid.ItemsSource = _settings.FormatRules;
        ApplicationDataGrid.ItemsSource = _settings.ApplicationRules;

        _loggingService.LogEvent("SettingsLoadCompleted", "Settings applied to UI controls", $"Format rules: {_settings.FormatRules.Count}, Application rules: {_settings.ApplicationRules.Count}");

        // Update button states based on initial tray icon service state
        UpdateProcessingButtonStates();
    }

    private void LoadSeenFormats()
    {
        var cache = _formatCacheService.GetCache();
        SeenFormatsDataGrid.ItemsSource = cache.SeenFormats.OrderByDescending(f => f.LastSeenDate).ToList();
        _loggingService.LogEvent("SeenFormatsLoaded", "Seen formats loaded into UI", $"Count: {cache.SeenFormats.Count}");
    }

    private void AttachEventHandlers()
    {
        WorkingDirectoryTextBox.TextChanged += OnSettingsChanged;
        MaxFileSizeTextBox.TextChanged += OnSettingsChanged;
        MinClipboardDataSizeTextBox.TextChanged += OnSettingsChanged;
        StartWithWindowsCheckBox.Checked += OnStartWithWindowsChanged;
        StartWithWindowsCheckBox.Unchecked += OnStartWithWindowsChanged;
        UseTimestampSubdirectoriesCheckBox.Checked += OnUseTimestampSubdirectoriesChanged;
        UseTimestampSubdirectoriesCheckBox.Unchecked += OnUseTimestampSubdirectoriesChanged;
        FormatDataGrid.CellEditEnding += OnDataGridCellEditEnding;
        ApplicationDataGrid.CellEditEnding += OnApplicationDataGridCellEditEnding;
        SeenFormatsDataGrid.MouseDoubleClick += OnSeenFormatsDataGridDoubleClick;

        _loggingService.LogEvent("EventHandlersAttached", "UI event handlers attached", "");
    }

    private async void OnSettingsChanged(object sender, EventArgs e)
    {
        if (_settings == null) return;

        var control = sender as System.Windows.Controls.Control;
        var oldWorkingDir = _settings.WorkingDirectory;
        var oldMaxSize = _settings.MaxFileSizeKB;
        var oldMinSize = _settings.MinClipboardDataSizeValue;

        // Update settings from UI controls
        _settings.WorkingDirectory = WorkingDirectoryTextBox.Text;

        if (int.TryParse(MaxFileSizeTextBox.Text, out int maxSizeKB))
        {
            _settings.MaxFileSizeKB = maxSizeKB;
        }

        if (int.TryParse(MinClipboardDataSizeTextBox.Text, out int minClipboardDataSizeBytes))
        {
            _settings.MinClipboardDataSizeValue = minClipboardDataSizeBytes;
        }

        await _configurationService.SaveSettingsAsync(_settings);

        _loggingService.LogEvent("SettingsChanged", $"Settings updated via {control?.Name ?? "Unknown"}",
            $"WorkingDir: {oldWorkingDir} -> {_settings.WorkingDirectory}, MaxSize: {oldMaxSize} -> {_settings.MaxFileSizeKB}, MinClipboardDataSize: {oldMinSize} -> {_settings.MinClipboardDataSizeValue}");
    }

    private async void OnStartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;

        bool isChecked = StartWithWindowsCheckBox.IsChecked == true;
        bool oldValue = _settings.StartWithWindows;
        
        _settings.StartWithWindows = isChecked;
        _startupService.SetStartupWithWindows(isChecked);
        
        await _configurationService.SaveSettingsAsync(_settings);

        _loggingService.LogEvent("StartWithWindowsChanged", "Start with Windows setting changed",
            $"Previous: {oldValue}, New: {isChecked}");
    }

    private async void OnUseTimestampSubdirectoriesChanged(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;

        bool isChecked = UseTimestampSubdirectoriesCheckBox.IsChecked == true;
        bool oldValue = _settings.UseTimestampSubdirectories;
        
        _settings.UseTimestampSubdirectories = isChecked;
        
        await _configurationService.SaveSettingsAsync(_settings);

        _loggingService.LogEvent("UseTimestampSubdirectoriesChanged", "Timestamp subdirectories setting changed",
            $"Previous: {oldValue}, New: {isChecked}");
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

    private async void OnApplicationDataGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (_settings == null) return;

        var item = e.Row.Item as ApplicationRule;
        var columnHeader = e.Column.Header?.ToString() ?? "Unknown";

        // Save settings after DataGrid edit
        await Task.Delay(100); // Small delay to ensure the edit is committed
        await _configurationService.SaveSettingsAsync(_settings);

        _loggingService.LogEvent("ApplicationDataGridCellEdited", $"Application rule edited in column '{columnHeader}'",
            $"Executable: {item?.ExecutableFileName ?? "Unknown"}");
    }

    private void InitializeTrayIcon()
    {
        _trayIconService = new TrayIconService(this, _loggingService, _startupService);

        // Subscribe to state changes from tray icon service
        _trayIconService.ProcessingStateChanged += OnProcessingStateChanged;

        // Update button states based on initial tray icon service state
        UpdateProcessingButtonStates();

        _loggingService.LogEvent("TrayIconServiceInitialized", "Tray icon service created and ready", "");
    }

    private void OnProcessingStateChanged(object sender, EventArgs e)
    {
        // Ensure button state updates happen on UI thread
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateProcessingButtonStates();
            _loggingService.LogEvent("ProcessingStateChangedFromTray", "Button states updated from tray icon state change",
                $"IsEnabled: {_trayIconService?.IsClipDumpEnabled}");
        }));
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
        if (_settings == null || FormatDataGrid.SelectedItems.Count == 0) return;

        // Filter out null items (dummy rows for new entries) and cast to actual rules
        var selectedRules = FormatDataGrid.SelectedItems
            .Cast<object>()
            .Where(item => item != null && item is ClipboardFormatRule)
            .Cast<ClipboardFormatRule>()
            .ToList();

        if (selectedRules.Count == 0)
        {
            // User only selected dummy rows, nothing to delete
            _loggingService.LogEvent("FormatRuleRemovalSkipped", "No valid rules selected for removal", "Only dummy rows were selected");
            return;
        }

        int removedCount = 0;
        int firstSelectedIndex = -1;

        // Find the first valid selected index
        for (int i = 0; i < FormatDataGrid.Items.Count; i++)
        {
            var item = FormatDataGrid.Items[i];
            if (item != null && item is ClipboardFormatRule && selectedRules.Contains(item))
            {
                firstSelectedIndex = i;
                break;
            }
        }

        foreach (var selectedRule in selectedRules)
        {
            _settings.FormatRules.Remove(selectedRule);
            removedCount++;
            _loggingService.LogEvent("FormatRuleRemoved", "Format rule removed", $"Format: {selectedRule.Format}");
        }

        if (removedCount > 0)
        {
            await _configurationService.SaveSettingsAsync(_settings);

            // Refresh the DataGrid
            FormatDataGrid.Items.Refresh();

            // Restore selection to closest item for single removal
            if (selectedRules.Count == 1 && FormatDataGrid.Items.Count > 1) // Check > 1 to account for dummy row
            {
                int newIndex = Math.Min(firstSelectedIndex, FormatDataGrid.Items.Count - 2); // -2 to avoid dummy row
                if (newIndex >= 0)
                {
                    FormatDataGrid.SelectedIndex = newIndex;
                    FormatDataGrid.ScrollIntoView(FormatDataGrid.SelectedItem);
                }
            }

            _loggingService.LogEvent("MultipleFormatRulesRemoved", "Multiple format rules removed",
                $"Removed: {removedCount}, Total selected: {selectedRules.Count}");
        }
    }

    private async void AddNewApplicationRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;

        var newRule = new ApplicationRule
        {
            ExecutableFileName = "newapp.exe",
            MaxSizeKB = 0,
            ShouldIgnore = false,
            RelativeDestinationDirectory = ""
        };

        _settings.ApplicationRules.Add(newRule);
        await _configurationService.SaveSettingsAsync(_settings);

        // Refresh the DataGrid
        ApplicationDataGrid.Items.Refresh();

        _loggingService.LogEvent("ApplicationRuleAdded", "New application rule added", $"Executable: {newRule.ExecutableFileName}");
    }

    private async void RemoveApplicationRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null || ApplicationDataGrid.SelectedItems.Count == 0) return;

        // Filter out null items (dummy rows for new entries) and cast to actual rules
        var selectedRules = ApplicationDataGrid.SelectedItems
            .Cast<object>()
            .Where(item => item != null && item is ApplicationRule)
            .Cast<ApplicationRule>()
            .ToList();

        if (selectedRules.Count == 0)
        {
            // User only selected dummy rows, nothing to delete
            _loggingService.LogEvent("ApplicationRuleRemovalSkipped", "No valid rules selected for removal", "Only dummy rows were selected");
            return;
        }

        int removedCount = 0;
        int firstSelectedIndex = -1;

        // Find the first valid selected index
        for (int i = 0; i < ApplicationDataGrid.Items.Count; i++)
        {
            var item = ApplicationDataGrid.Items[i];
            if (item != null && item is ApplicationRule && selectedRules.Contains(item))
            {
                firstSelectedIndex = i;
                break;
            }
        }

        foreach (var selectedRule in selectedRules)
        {
            _settings.ApplicationRules.Remove(selectedRule);
            removedCount++;
            _loggingService.LogEvent("ApplicationRuleRemoved", "Application rule removed", $"Executable: {selectedRule.ExecutableFileName}");
        }

        if (removedCount > 0)
        {
            await _configurationService.SaveSettingsAsync(_settings);

            // Refresh the DataGrid
            ApplicationDataGrid.Items.Refresh();

            // Restore selection to closest item for single removal
            if (selectedRules.Count == 1 && ApplicationDataGrid.Items.Count > 1) // Check > 1 to account for dummy row
            {
                int newIndex = Math.Min(firstSelectedIndex, ApplicationDataGrid.Items.Count - 2); // -2 to avoid dummy row
                if (newIndex >= 0)
                {
                    ApplicationDataGrid.SelectedIndex = newIndex;
                    ApplicationDataGrid.ScrollIntoView(ApplicationDataGrid.SelectedItem);
                }
            }

            _loggingService.LogEvent("MultipleApplicationRulesRemoved", "Multiple application rules removed",
                $"Removed: {removedCount}, Total selected: {selectedRules.Count}");
        }
    }

    private void UpdateProcessingButtonStates()
    {
        if (_trayIconService != null)
        {
            bool isEnabled = _trayIconService.IsClipDumpEnabled;
            DisableProcessingButton.IsEnabled = isEnabled;
            EnableProcessingButton.IsEnabled = !isEnabled;
        }
    }

    private void DisableProcessingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_trayIconService != null)
        {
            // Disable clipboard processing indefinitely
            var disableMethod = typeof(TrayIconService).GetMethod("DisableClipDump",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            disableMethod?.Invoke(_trayIconService, new object[] { null });

            UpdateProcessingButtonStates();
            _loggingService.LogEvent("ProcessingDisabled", "Clipboard processing disabled from main window", "");
        }
    }

    private void EnableProcessingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_trayIconService != null)
        {
            // Enable clipboard processing
            var enableMethod = typeof(TrayIconService).GetMethod("EnableClipDump",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enableMethod?.Invoke(_trayIconService, new object[0]);

            UpdateProcessingButtonStates();
            _loggingService.LogEvent("ProcessingEnabled", "Clipboard processing enabled from main window", "");
        }
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e)
    {
        _loggingService.LogEvent("ApplicationExit", "User requested exit from main window", "");
        System.Windows.Application.Current.Shutdown();
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

        // Unsubscribe from events before disposing
        if (_trayIconService != null)
        {
            _trayIconService.ProcessingStateChanged -= OnProcessingStateChanged;
        }

        _trayIconService?.Dispose();
        base.OnClosed(e);
    }

    private async void AddFormatToRulesButton_Click(object sender, RoutedEventArgs e)
    {
        AddSelectedFormatsToRules();
    }

    private async void AddSeenFormatToRules(SeenFormat selectedFormat)
    {
        // Check if rule already exists
        var existingRule = _settings.FormatRules.FirstOrDefault(r =>
            string.Equals(r.Format, selectedFormat.FormatName, StringComparison.OrdinalIgnoreCase));

        if (existingRule != null)
        {
            // Switch to the Clipboard Format Rules tab
            var mainTabControl = FindName("MainTabControl") as System.Windows.Controls.TabControl;
            if (mainTabControl != null)
            {
                mainTabControl.SelectedIndex = 1; // Index 1 is the "Clipboard Format Rules" tab
            }

            // Focus and select the existing rule
            FormatDataGrid.Focus();
            FormatDataGrid.SelectedItem = existingRule;
            FormatDataGrid.ScrollIntoView(existingRule);

            _loggingService.LogEvent("FormatRuleAlreadyExists", "Existing format rule selected",
                $"Format: {selectedFormat.FormatName}");
            return; // Skip adding since rule already exists
        }

        var newRule = new ClipboardFormatRule
        {
            Format = selectedFormat.FormatName,
            MaxSizeKB = 0, // Default to 0 for rules added from seen formats
            ShouldIgnore = true, // Default to ignore
            RelativeDestinationDirectory = ""
        };

        _settings.FormatRules.Add(newRule);
        await _configurationService.SaveSettingsAsync(_settings);

        // Refresh the DataGrids
        FormatDataGrid.Items.Refresh();

        // Switch to the Clipboard Format Rules tab
        var tabControl = FindName("MainTabControl") as System.Windows.Controls.TabControl;
        if (tabControl != null)
        {
            tabControl.SelectedIndex = 1; // Index 1 is the "Clipboard Format Rules" tab
        }

        _loggingService.LogEvent("FormatRuleAddedFromSeen", "Format rule added from seen formats",
            $"Format: {newRule.Format}, ShouldIgnore: {newRule.ShouldIgnore}, MaxSizeKB: {newRule.MaxSizeKB}");
    }

    private async void AddSelectedFormatsToRules()
    {
        if (_settings == null || SeenFormatsDataGrid.SelectedItems.Count == 0) return;

        int addedCount = 0;
        int skippedCount = 0;
        ClipboardFormatRule firstExistingRule = null;

        foreach (var selectedItem in SeenFormatsDataGrid.SelectedItems)
        {
            var selectedFormat = selectedItem as SeenFormat;
            if (selectedFormat == null) continue;

            // Check if rule already exists
            var existingRule = _settings.FormatRules.FirstOrDefault(r =>
                string.Equals(r.Format, selectedFormat.FormatName, StringComparison.OrdinalIgnoreCase));

            if (existingRule != null)
            {
                if (firstExistingRule == null)
                {
                    firstExistingRule = existingRule; // Remember the first existing rule to focus on
                }
                skippedCount++;
                continue;
            }

            var newRule = new ClipboardFormatRule
            {
                Format = selectedFormat.FormatName,
                MaxSizeKB = 0, // Default to 0 for rules added from seen formats
                ShouldIgnore = true, // Default to ignore
                RelativeDestinationDirectory = ""
            };

            _settings.FormatRules.Add(newRule);
            addedCount++;

            _loggingService.LogEvent("FormatRuleAddedFromSeen", "Format rule added from seen formats",
                $"Format: {newRule.Format}, ShouldIgnore: {newRule.ShouldIgnore}, MaxSizeKB: {newRule.MaxSizeKB}");
        }

        if (addedCount > 0)
        {
            await _configurationService.SaveSettingsAsync(_settings);

            // Refresh the DataGrids
            FormatDataGrid.Items.Refresh();
        }

        // Switch to the Clipboard Format Rules tab
        var tabControl = FindName("MainTabControl") as System.Windows.Controls.TabControl;
        if (tabControl != null)
        {
            tabControl.SelectedIndex = 1; // Index 1 is the "Clipboard Format Rules" tab
        }

        // If there were existing rules and no new rules were added, focus on the first existing rule
        if (addedCount == 0 && firstExistingRule != null)
        {
            FormatDataGrid.Focus();
            FormatDataGrid.SelectedItem = firstExistingRule;
            FormatDataGrid.ScrollIntoView(firstExistingRule);

            _loggingService.LogEvent("ExistingFormatRuleSelected", "Focused on existing format rule",
                $"Format: {firstExistingRule.Format}, Total skipped: {skippedCount}");
        }

        _loggingService.LogEvent("MultipleFormatsAddedFromSeen", "Multiple format rules processed",
            $"Added: {addedCount}, Skipped: {skippedCount}, Total selected: {SeenFormatsDataGrid.SelectedItems.Count}");
    }

    private void OnSeenFormatsDataGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_settings == null || SeenFormatsDataGrid.SelectedItem == null) return;

        var selectedFormat = SeenFormatsDataGrid.SelectedItem as SeenFormat;
        if (selectedFormat != null)
        {
            AddSeenFormatToRules(selectedFormat);
        }
    }

    private async void CleanupUnnecessaryRulesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;

        var rulesToRemove = new List<ClipboardFormatRule>();
        var seenFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int duplicateCount = 0;
        int unnecessaryCount = 0;

        foreach (var rule in _settings.FormatRules)
        {
            // Check for duplicates (keep first occurrence)
            if (seenFormats.Contains(rule.Format))
            {
                rulesToRemove.Add(rule);
                duplicateCount++;
                _loggingService.LogEvent("DuplicateRuleFound", "Duplicate format rule marked for removal",
                    $"Format: {rule.Format}");
                continue;
            }
            seenFormats.Add(rule.Format);

            // Check for unnecessary rules (max size = global max size AND not ignored AND no directory set)
            bool hasGlobalMaxSize = rule.MaxSizeKB == _settings.MaxFileSizeKB;
            bool isNotIgnored = !rule.ShouldIgnore;
            bool hasNoDirectory = string.IsNullOrWhiteSpace(rule.RelativeDestinationDirectory);

            if (hasGlobalMaxSize && isNotIgnored && hasNoDirectory)
            {
                rulesToRemove.Add(rule);
                unnecessaryCount++;
                _loggingService.LogEvent("UnnecessaryRuleFound", "Unnecessary format rule marked for removal",
                    $"Format: {rule.Format}, MaxSizeKB: {rule.MaxSizeKB}, ShouldIgnore: {rule.ShouldIgnore}, Directory: '{rule.RelativeDestinationDirectory}'");
            }
        }

        if (rulesToRemove.Count > 0)
        {
            foreach (var rule in rulesToRemove)
            {
                _settings.FormatRules.Remove(rule);
            }

            await _configurationService.SaveSettingsAsync(_settings);

            // Refresh the DataGrid
            FormatDataGrid.Items.Refresh();

            _loggingService.LogEvent("RulesCleanupCompleted", "Unnecessary rules cleanup completed",
                $"Total removed: {rulesToRemove.Count}, Duplicates: {duplicateCount}, Unnecessary: {unnecessaryCount}");

            System.Windows.MessageBox.Show(
                $"Cleanup completed!\n\nRemoved {rulesToRemove.Count} rules:\n- {duplicateCount} duplicate rules\n- {unnecessaryCount} unnecessary rules",
                "Rules Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            _loggingService.LogEvent("RulesCleanupSkipped", "No unnecessary rules found during cleanup", "");
            System.Windows.MessageBox.Show("No unnecessary rules found. All rules appear to be needed.",
                "Rules Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void CleanupUnnecessaryApplicationRulesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;

        var rulesToRemove = new List<ApplicationRule>();
        var seenExecutables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int duplicateCount = 0;
        int unnecessaryCount = 0;

        foreach (var rule in _settings.ApplicationRules)
        {
            // Check for duplicates (keep first occurrence)
            if (seenExecutables.Contains(rule.ExecutableFileName))
            {
                rulesToRemove.Add(rule);
                duplicateCount++;
                _loggingService.LogEvent("DuplicateApplicationRuleFound", "Duplicate application rule marked for removal",
                    $"Executable: {rule.ExecutableFileName}");
                continue;
            }
            seenExecutables.Add(rule.ExecutableFileName);

            // Check for unnecessary rules (max size = global max size AND not ignored AND no directory set)
            bool hasGlobalMaxSize = rule.MaxSizeKB == _settings.MaxFileSizeKB;
            bool isNotIgnored = !rule.ShouldIgnore;
            bool hasNoDirectory = string.IsNullOrWhiteSpace(rule.RelativeDestinationDirectory);

            if (hasGlobalMaxSize && isNotIgnored && hasNoDirectory)
            {
                rulesToRemove.Add(rule);
                unnecessaryCount++;
                _loggingService.LogEvent("UnnecessaryApplicationRuleFound", "Unnecessary application rule marked for removal",
                    $"Executable: {rule.ExecutableFileName}, MaxSizeKB: {rule.MaxSizeKB}, ShouldIgnore: {rule.ShouldIgnore}, Directory: '{rule.RelativeDestinationDirectory}'");
            }
        }

        if (rulesToRemove.Count > 0)
        {
            foreach (var rule in rulesToRemove)
            {
                _settings.ApplicationRules.Remove(rule);
            }

            await _configurationService.SaveSettingsAsync(_settings);

            // Refresh the DataGrid
            ApplicationDataGrid.Items.Refresh();

            _loggingService.LogEvent("ApplicationRulesCleanupCompleted", "Unnecessary application rules cleanup completed",
                $"Total removed: {rulesToRemove.Count}, Duplicates: {duplicateCount}, Unnecessary: {unnecessaryCount}");

            System.Windows.MessageBox.Show(
                $"Cleanup completed!\n\nRemoved {rulesToRemove.Count} application rules:\n- {duplicateCount} duplicate rules\n- {unnecessaryCount} unnecessary rules",
                "Application Rules Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            _loggingService.LogEvent("ApplicationRulesCleanupSkipped", "No unnecessary application rules found during cleanup", "");
            System.Windows.MessageBox.Show("No unnecessary application rules found. All rules appear to be needed.",
                "Application Rules Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void AddRunningAppButton_Click(object sender, RoutedEventArgs e)
    {
        var runningApps = _applicationEnumerationService.GetFilteredApplications(_settings?.ApplicationRules);
        
        if (runningApps.Count == 0)
        {
            _loggingService.LogEvent("NoRunningAppsFound", "No running applications with windows found", "");
            return;
        }

        // Create context menu
        var contextMenu = new ContextMenu();
        
        foreach (var app in runningApps)
        {
            var menuItem = new MenuItem
            {
                Header = app.DisplayName,
                Tag = app.ExecutableName
            };
            menuItem.Click += async (menuSender, menuArgs) =>
            {
                await AddApplicationRuleFromRunningApp(app.ExecutableName, app.ProcessName);
            };
            contextMenu.Items.Add(menuItem);
        }

        // Show context menu at button location
        var button = sender as System.Windows.Controls.Button;
        contextMenu.PlacementTarget = button;
        contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        contextMenu.IsOpen = true;

        _loggingService.LogEvent("RunningAppsMenuShown", "Running applications menu displayed", 
            $"Found {runningApps.Count} applications with windows");
    }

    private async Task AddApplicationRuleFromRunningApp(string executableName, string processName)
    {
        if (_settings == null) return;

        // Check if rule already exists
        var existingRule = _settings.ApplicationRules.FirstOrDefault(r =>
            string.Equals(r.ExecutableFileName, executableName, StringComparison.OrdinalIgnoreCase));

        if (existingRule != null)
        {
            // Focus and select the existing rule
            ApplicationDataGrid.Focus();
            ApplicationDataGrid.SelectedItem = existingRule;
            ApplicationDataGrid.ScrollIntoView(existingRule);

            _loggingService.LogEvent("ApplicationRuleAlreadyExists", "Existing application rule selected",
                $"Executable: {executableName}");
            return;
        }

        var newRule = new ApplicationRule
        {
            ExecutableFileName = executableName,
            MaxSizeKB = 0,
            ShouldIgnore = false,
            RelativeDestinationDirectory = ""
        };

        _settings.ApplicationRules.Add(newRule);
        await _configurationService.SaveSettingsAsync(_settings);

        // Refresh the DataGrid
        ApplicationDataGrid.Items.Refresh();

        // Select the new rule
        ApplicationDataGrid.SelectedItem = newRule;
        ApplicationDataGrid.ScrollIntoView(newRule);

        _loggingService.LogEvent("ApplicationRuleAddedFromRunning", "Application rule added from running app",
            $"Executable: {executableName}, Process: {processName}");
    }

    private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "This will clear all seen format history and create a backup of the current data. Are you sure you want to continue?",
            "Clear History", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _formatCacheService.ClearCacheWithBackupAsync();
                LoadSeenFormats(); // Refresh the UI
                
                _loggingService.LogEvent("HistoryCleared", "Seen formats history cleared by user", "");
                
                System.Windows.MessageBox.Show(
                    "History cleared successfully! A backup has been created.",
                    "Clear History", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _loggingService.LogEvent("HistoryClearFailed", "Failed to clear history", ex.Message);
                
                System.Windows.MessageBox.Show(
                    $"Failed to clear history: {ex.Message}",
                    "Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
    }

    private void OpenWorkingDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string workingDirectory = WorkingDirectoryTextBox.Text;
            
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                _loggingService.LogEvent("OpenDirectoryFailed", "Working directory is empty", "");
                System.Windows.MessageBox.Show("Working directory is not set.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Expand environment variables
            string expandedPath = Environment.ExpandEnvironmentVariables(workingDirectory);
            
            // Resolve to absolute path (handles relative paths)
            string absolutePath = System.IO.Path.GetFullPath(expandedPath);

            if (!Directory.Exists(absolutePath))
            {
                _loggingService.LogEvent("OpenDirectoryFailed", "Working directory does not exist after resolution", 
                    $"Original: {workingDirectory}, Resolved: {absolutePath}");
                System.Windows.MessageBox.Show($"Directory does not exist: {absolutePath}\n(Original: {workingDirectory})", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start("explorer.exe", absolutePath);
            _loggingService.LogEvent("WorkingDirectoryOpened", "Working directory opened in Explorer", 
                $"Original: {workingDirectory}, Resolved: {absolutePath}");
        }
        catch (Exception ex)
        {
            _loggingService.LogEvent("OpenDirectoryError", "Error opening working directory", $"Error: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to open directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
        
        _loggingService.LogEvent("AboutWindowOpened", "About window opened by user", "");
    }
}