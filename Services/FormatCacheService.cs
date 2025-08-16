using System.IO;
using System.Text.Json;
using ClipDumpRe.Models;

namespace ClipDumpRe.Services
{
    public class FormatCacheService
    {
        private const string CacheFileName = "clipdump-cache.json";
        private readonly string _cacheFilePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly LoggingService _loggingService;
        private FormatCache _cache;

        public FormatCacheService(LoggingService loggingService)
        {
            _cacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CacheFileName);
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            _loggingService = loggingService;
            LoadCache();
        }

        public FormatCache GetCache() => _cache;

        public async Task AddSeenFormatAsync(string format)
        {
            if (string.IsNullOrWhiteSpace(format)) return;

            var existingFormat = _cache.SeenFormats.FirstOrDefault(f => f.FormatName == format);
            if (existingFormat != null)
            {
                existingFormat.LastSeenDate = DateTime.Now;
                existingFormat.SeenCount++;
            }
            else
            {
                _cache.SeenFormats.Add(new SeenFormat
                {
                    FormatName = format,
                    FirstSeenDate = DateTime.Now,
                    LastSeenDate = DateTime.Now,
                    SeenCount = 1
                });
                await _loggingService.LogEventAsync("NewFormatDetected", "New clipboard format detected", $"Format: {format}");
            }

            await SaveCacheAsync();
        }

        public async Task ClearCacheWithBackupAsync()
        {
            try
            {
                // Create backup if cache file exists
                if (File.Exists(_cacheFilePath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    var backupPath = Path.Combine(
                        Path.GetDirectoryName(_cacheFilePath),
                        $"clipdump-cache-backup-{timestamp}.json"
                    );
                    
                    File.Copy(_cacheFilePath, backupPath);
                    await _loggingService.LogEventAsync("CacheBackupCreated", "Cache backup created before clearing", $"Backup: {backupPath}");
                }

                // Clear the in-memory cache
                _cache = new FormatCache();

                // Save the empty cache (this will overwrite the existing file)
                await SaveCacheAsync();

                await _loggingService.LogEventAsync("CacheCleared", "Format cache cleared successfully", "Cache reset to empty state");
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("CacheClearFailed", "Failed to clear format cache", ex.Message);
                throw;
            }
        }

        private void LoadCache()
        {
            if (!File.Exists(_cacheFilePath))
            {
                _cache = new FormatCache();
                return;
            }

            try
            {
                var json = File.ReadAllText(_cacheFilePath);
                _cache = JsonSerializer.Deserialize<FormatCache>(json, _jsonOptions) ?? new FormatCache();
            }
            catch (Exception ex)
            {
                _loggingService.LogEvent("CacheLoadFailed", "Failed to load format cache, using empty cache", ex.Message);
                _cache = new FormatCache();
            }
        }

        private async Task SaveCacheAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_cache, _jsonOptions);
                await File.WriteAllTextAsync(_cacheFilePath, json);
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("CacheSaveFailed", "Failed to save format cache", ex.Message);
            }
        }
    }
}
