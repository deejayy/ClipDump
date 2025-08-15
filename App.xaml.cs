using System.Configuration;
using System.Data;
using System.Windows;
using ClipDumpRe.Services;

namespace clipdump_re;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstanceService;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceService = new SingleInstanceService();

        if (!_singleInstanceService.IsFirstInstance())
        {
            System.Windows.MessageBox.Show("ClipDump is already running.", "Application Already Running",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceService?.Dispose();
        base.OnExit(e);
    }
}

