using System.IO;
using System.Text;
using System.Text.Json;
using ClipDumpRe.Models;

namespace ClipDumpRe.Services
{
    internal class ConfigurationService
    {
        private const string ConfigFileName = "clipdump-re.json";
        private readonly string _configFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigurationService()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<Settings> LoadSettingsAsync()
        {
            if (!File.Exists(_configFilePath))
            {
                var defaultSettings = new Settings();
                await SaveSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                return JsonSerializer.Deserialize<Settings>(json, _jsonOptions) ?? new Settings();
            }
            catch (Exception)
            {
                // If deserialization fails, return default settings
                return new Settings();
            }
        }

        public async Task SaveSettingsAsync(Settings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                await File.WriteAllTextAsync(_configFilePath, json);
            }
            catch (Exception)
            {
                // Handle save errors silently or log as needed
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
