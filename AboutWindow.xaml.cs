using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace clipdump_re
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                VersionTextBlock.Text = $"Version {version?.ToString(4) ?? "0.0.0"}";
                
                var currentYear = DateTime.Now.Year;
                CopyrightTextBlock.Text = $"© {currentYear} DeeJayy. All rights reserved.";
            }
            catch
            {
                // Fallback to default values if version info cannot be retrieved
                VersionTextBlock.Text = "Version 0.0.0";
                CopyrightTextBlock.Text = "© 2025 DeeJayy. All rights reserved.";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GitHubHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
                {
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch
            {
                // Silently fail if unable to open the URL
            }
        }
    }
}
