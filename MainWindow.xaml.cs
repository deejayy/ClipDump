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

namespace clipdump_re;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConfigurationService _configurationService;
    private Settings _settings;

    public MainWindow()
    {
        InitializeComponent();
        _configurationService = new ConfigurationService();
        LoadSettings();
    }

    private async void LoadSettings()
    {
        _settings = await _configurationService.LoadSettingsAsync();
        
        // Populate UI controls with loaded settings
        WorkingDirectoryTextBox.Text = _settings.WorkingDirectory;
        MaxFileSizeTextBox.Text = _settings.MaxFileSizeKB.ToString();
        FormatDataGrid.ItemsSource = _settings.FormatRules;
    }
}