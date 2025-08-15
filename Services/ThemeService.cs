using System;
using System.Windows;

namespace ClipDumpRe.Services
{
    public enum Theme
    {
        Light,
        Dark
    }

    public class ThemeService
    {
        private readonly LoggingService _loggingService;
        private Theme _currentTheme = Theme.Light;

        public event EventHandler<Theme>? ThemeChanged;

        public ThemeService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public Theme CurrentTheme => _currentTheme;

        public void SetTheme(Theme theme)
        {
            if (_currentTheme == theme) return;

            _currentTheme = theme;
            ApplyTheme(theme);
            ThemeChanged?.Invoke(this, theme);

            _loggingService.LogEvent("ThemeChanged", "Application theme changed", $"New theme: {theme}");
        }

        private void ApplyTheme(Theme theme)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            // Clear existing theme resources
            var resourcesToRemove = new List<string> { "LightTheme", "DarkTheme" };
            foreach (var resource in resourcesToRemove)
            {
                if (app.Resources.MergedDictionaries.Any(d => d.Source?.ToString().Contains(resource) == true))
                {
                    var dictToRemove = app.Resources.MergedDictionaries.First(d => d.Source?.ToString().Contains(resource) == true);
                    app.Resources.MergedDictionaries.Remove(dictToRemove);
                }
            }

            // Apply new theme
            var themeUri = new Uri($"/Themes/{theme}Theme.xaml", UriKind.Relative);
            var themeDict = new ResourceDictionary { Source = themeUri };
            app.Resources.MergedDictionaries.Add(themeDict);
        }

        public void ToggleTheme()
        {
            SetTheme(_currentTheme == Theme.Light ? Theme.Dark : Theme.Light);
        }
    }
}
