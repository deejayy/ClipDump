using System.IO;
using System.Text;
using System.Text.Json;
using ClipDumpRe.Models;

namespace ClipDumpRe.Services
{
    internal class ConfigurationService
    {
        private const string ConfigFileName = "clipdump.json";
        private readonly string _configFilePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly LoggingService _loggingService;

        public ConfigurationService()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            _loggingService = new LoggingService();
        }

        public async Task<Settings> LoadSettingsAsync()
        {
            if (!File.Exists(_configFilePath))
            {
                await _loggingService.LogEventAsync("ConfigurationFileNotFound", "Configuration file does not exist, creating default settings", _configFilePath);
                var defaultSettings = new Settings();
                await SaveSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var settings = JsonSerializer.Deserialize<Settings>(json, _jsonOptions) ?? new Settings();
                await _loggingService.LogEventAsync("ConfigurationLoaded", "Settings loaded successfully", $"FormatRules count: {settings.FormatRules.Count}");
                return settings;
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("ConfigurationLoadFailed", "Failed to load settings, returning defaults", ex.Message);
                return new Settings();
            }
        }

        public async Task SaveSettingsAsync(Settings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                await File.WriteAllTextAsync(_configFilePath, json);
                await _loggingService.LogEventAsync("ConfigurationSaved", "Settings saved successfully", $"WorkingDirectory: {settings.WorkingDirectory}, MaxFileSizeKB: {settings.MaxFileSizeKB}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("ConfigurationSaveFailed", "Failed to save settings", ex.Message);
            }
        }

        public Settings LoadSettings()
        {
            return LoadSettingsAsync().GetAwaiter().GetResult();
        }

        public void SaveSettings(Settings settings)
        {
            SaveSettingsAsync(settings).GetAwaiter().GetResult();
        }
    }
}
