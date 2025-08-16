using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ClipDumpRe.Models;
using ClipDumpRe.Utils;
using System.Text.Json;

namespace ClipDumpRe.Services
{
    public class ClipboardService
    {
        private readonly Settings _settings;
        private readonly LoggingService _loggingService;
        private readonly ForegroundApplicationService _foregroundApplicationService;
        private readonly TrayIconService _trayIconService;
        private readonly FormatCacheService _formatCacheService;
        private readonly ClearUrlsService _clearUrlsService;

        public ClipboardService(Settings settings, LoggingService loggingService, ForegroundApplicationService foregroundApplicationService, TrayIconService trayIconService, FormatCacheService formatCacheService, ClearUrlsService clearUrlsService)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _foregroundApplicationService = foregroundApplicationService ?? throw new ArgumentNullException(nameof(foregroundApplicationService));
            _trayIconService = trayIconService; // Should not be null with proper initialization order
            _formatCacheService = formatCacheService ?? throw new ArgumentNullException(nameof(formatCacheService));
            _clearUrlsService = clearUrlsService ?? throw new ArgumentNullException(nameof(clearUrlsService));
        }

        public async Task DumpClipboardContentAsync()
        {
            try
            {
                if (!await ShouldProcessClipboardAsync())
                    return;

                var appInfo = await GetForegroundApplicationInfoAsync();
                var applicationRule = GetApplicationRule(appInfo);
                
                if (await ShouldIgnoreApplicationAsync(applicationRule, appInfo))
                    return;

                var dataObject = await GetClipboardDataObjectAsync();
                if (dataObject == null)
                    return;

                var formats = dataObject.GetFormats();
                await TrackSeenFormatsAsync(formats);

                if (await ShouldExcludeFromMonitoringAsync(formats))
                    return;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
                string baseOutputDir = GetOutputDirectory(applicationRule);

                // Create metadata object
                var metadata = new ClipboardMetadata
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    ForegroundApplication = appInfo
                };

                // Collect metadata for all detected formats
                await CollectDetectedFormatsMetadataAsync(dataObject, formats, metadata);

                int savedCount = await ProcessClipboardFormatsAsync(dataObject, formats, timestamp, baseOutputDir, appInfo, applicationRule, metadata);

                // Only save metadata if we actually saved some formats
                if (savedCount > 0)
                {
                    await SaveMetadataAsync(metadata, timestamp, baseOutputDir);
                }
                else
                {
                    await _loggingService.LogEventAsync("MetadataSkipped", "Metadata file not created because no formats were saved", 
                        $"Total detected formats: {metadata.DetectedFormats.Count}");
                }
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("ClipboardProcessingError", $"Error during clipboard processing", $"Error: {ex.Message}");
                Debug.WriteLine($"Error dumping clipboard content: {ex.Message}");
            }
        }

        private async Task<bool> ShouldProcessClipboardAsync()
        {
            if (!_trayIconService.IsClipDumpEnabled)
            {
                await _loggingService.LogEventAsync("ClipboardProcessingSkipped", "ClipDump is currently disabled", "");
                return false;
            }

            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                await _loggingService.LogEventAsync("ClipboardProcessingError", "Invalid thread apartment state for clipboard access", "Current thread must be STA for clipboard operations");
                return false;
            }

            return true;
        }

        private async Task<ForegroundApplicationInfo> GetForegroundApplicationInfoAsync()
        {
            var appInfo = _foregroundApplicationService.GetForegroundApplicationInfo();
            await _loggingService.LogEventAsync("ForegroundApplicationDetected", "Current foreground application detected",
                $"Process: {appInfo.ProcessName}, Executable: {appInfo.ExecutablePath}, Window: {appInfo.WindowTitle}, Class: {appInfo.WindowClass}");
            return appInfo;
        }

        private ApplicationRule GetApplicationRule(ForegroundApplicationInfo appInfo)
        {
            return _settings.ApplicationRules.FirstOrDefault(r =>
                string.Equals(Path.GetFileName(appInfo.ExecutablePath), r.ExecutableFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals($"{appInfo.ProcessName}.exe", r.ExecutableFileName, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> ShouldIgnoreApplicationAsync(ApplicationRule applicationRule, ForegroundApplicationInfo appInfo)
        {
            if (applicationRule?.ShouldIgnore == true)
            {
                await _loggingService.LogEventAsync("ClipboardProcessingSkipped", "Application is in ignore list",
                    $"Application: {appInfo.ProcessName}, Rule: {applicationRule.ExecutableFileName}");
                return true;
            }
            return false;
        }

        private async Task<System.Windows.IDataObject> GetClipboardDataObjectAsync()
        {
            try
            {
                var dataObject = System.Windows.Clipboard.GetDataObject();
                if (dataObject == null)
                {
                    await _loggingService.LogEventAsync("ClipboardProcessingSkipped", "No clipboard data object available", "");
                }
                return dataObject;
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("ClipboardAccessError", "Failed to access clipboard", $"Error: {ex.Message}");
                return null;
            }
        }

        private async Task TrackSeenFormatsAsync(string[] formats)
        {
            foreach (string format in formats)
            {
                await _formatCacheService.AddSeenFormatAsync(format);
            }
        }

        private async Task<bool> ShouldExcludeFromMonitoringAsync(string[] formats)
        {
            if (formats.Contains("ExcludeClipboardContentFromMonitorProcessing"))
            {
                await _loggingService.LogEventAsync("ClipboardProcessingSkipped", "Content excluded from monitoring", "ExcludeClipboardContentFromMonitorProcessing format detected");
                Debug.WriteLine("Clipboard content excluded from monitoring - skipping save");
                return true;
            }
            return false;
        }

        private string GetOutputDirectory(ApplicationRule applicationRule)
        {
            string workingDir = _settings.WorkingDirectory;
            if (applicationRule != null && !string.IsNullOrWhiteSpace(applicationRule.RelativeDestinationDirectory))
            {
                workingDir = Path.Combine(_settings.WorkingDirectory, applicationRule.RelativeDestinationDirectory);
            }

            string expandedDir = Environment.ExpandEnvironmentVariables(workingDir);
            string baseOutputDir = Path.IsPathRooted(expandedDir)
                ? expandedDir
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, expandedDir);

            if (!Directory.Exists(baseOutputDir))
                Directory.CreateDirectory(baseOutputDir);

            return baseOutputDir;
        }

        private async Task<int> ProcessClipboardFormatsAsync(System.Windows.IDataObject dataObject, string[] formats, string timestamp, string baseOutputDir, ForegroundApplicationInfo appInfo, ApplicationRule applicationRule, ClipboardMetadata metadata)
        {
            await _loggingService.LogEventAsync("ClipboardProcessingStarted", $"Processing {formats.Length} clipboard formats",
                $"Output directory: {baseOutputDir}, Source app: {appInfo.ProcessName}");

            // Filter out ignored formats first
            var validFormats = new List<string>();
            int ignoredCount = 0;
            
            foreach (string format in formats)
            {
                var formatRule = GetFormatRule(format);
                if (await ShouldIgnoreFormatAsync(formatRule, format))
                {
                    ignoredCount++;
                    continue;
                }
                validFormats.Add(format);
            }

            // Deduplicate content across remaining formats
            var deduplicatedFormats = await DeduplicateClipboardFormatsAsync(dataObject, validFormats.ToArray());
            int duplicateCount = validFormats.Count - deduplicatedFormats.Count;

            if (duplicateCount > 0)
            {
                await _loggingService.LogEventAsync("ClipboardContentDeduplicated", 
                    $"Removed {duplicateCount} duplicate content formats",
                    $"Original formats: {validFormats.Count}, After deduplication: {deduplicatedFormats.Count}");
            }

            int savedCount = 0;
            int skippedCount = ignoredCount;

            foreach (string format in deduplicatedFormats)
            {
                var formatRule = GetFormatRule(format);
                var result = await ProcessSingleFormatAsync(dataObject, format, formatRule, applicationRule, timestamp, baseOutputDir, appInfo, metadata);
                if (result.Success)
                    savedCount++;
                else
                    skippedCount++;
            }

            await _loggingService.LogEventAsync("ClipboardProcessingCompleted", $"Clipboard processing finished",
                $"Saved: {savedCount}, Skipped: {skippedCount} (Ignored: {ignoredCount}, Duplicates: {duplicateCount}), Total formats: {formats.Length}, Source app: {appInfo.ProcessName}");

            return savedCount;
        }

        private async Task<List<string>> DeduplicateClipboardFormatsAsync(System.Windows.IDataObject dataObject, string[] formats)
        {
            var contentHashes = new Dictionary<string, string>(); // hash -> format name
            var uniqueFormats = new List<string>();
            var duplicateFormats = new List<string>();

            foreach (string format in formats)
            {
                try
                {
                    var data = dataObject.GetData(format);
                    if (data == null)
                    {
                        uniqueFormats.Add(format);
                        continue;
                    }

                    string contentHash = CalculateContentHash(data);
                    
                    if (contentHashes.ContainsKey(contentHash))
                    {
                        // Found duplicate content
                        string existingFormat = contentHashes[contentHash];
                        duplicateFormats.Add(format);
                        
                        await _loggingService.LogEventAsync("DuplicateContentDetected", 
                            $"Format '{format}' has identical content to '{existingFormat}'",
                            $"Content hash: {contentHash}");
                    }
                    else
                    {
                        // Unique content
                        contentHashes[contentHash] = format;
                        uniqueFormats.Add(format);
                    }
                }
                catch (Exception ex)
                {
                    // If we can't get data or calculate hash, keep the format to be safe
                    uniqueFormats.Add(format);
                    await _loggingService.LogEventAsync("DeduplicationError", 
                        $"Error during deduplication for format '{format}', keeping format",
                        $"Error: {ex.Message}");
                }
            }

            if (duplicateFormats.Count > 0)
            {
                await _loggingService.LogEventAsync("DeduplicationSummary", 
                    $"Deduplication complete: {uniqueFormats.Count} unique, {duplicateFormats.Count} duplicates removed",
                    $"Duplicate formats: {string.Join(", ", duplicateFormats)}");
            }

            return uniqueFormats;
        }

        private string CalculateContentHash(object data)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = null;
                
                if (data is string textData)
                {
                    bytes = System.Text.Encoding.UTF8.GetBytes(textData);
                }
                else if (data is byte[] byteData)
                {
                    bytes = byteData;
                }
                else if (data is System.IO.MemoryStream memoryStream)
                {
                    bytes = memoryStream.ToArray();
                }
                else if (data is System.IO.Stream stream)
                {
                    using (var memStream = new System.IO.MemoryStream())
                    {
                        stream.Position = 0;
                        stream.CopyTo(memStream);
                        bytes = memStream.ToArray();
                    }
                }
                else
                {
                    // For other types, try to serialize to string and then to bytes
                    string serialized = data.ToString() ?? "";
                    bytes = System.Text.Encoding.UTF8.GetBytes(serialized);
                }

                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private ClipboardFormatRule GetFormatRule(string format)
        {
            return _settings.FormatRules.FirstOrDefault(r =>
                string.Equals(r.Format, format, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> ShouldIgnoreFormatAsync(ClipboardFormatRule formatRule, string format)
        {
            if (formatRule?.ShouldIgnore == true)
            {
                await _loggingService.LogEventAsync("ClipboardFormatIgnored", $"Format skipped due to format rule", $"Format: {format}");
                Debug.WriteLine($"Skipping format '{format}' due to format rule ignore setting");
                return true;
            }
            return false;
        }

        private async Task<(bool Success, string Message)> ProcessSingleFormatAsync(System.Windows.IDataObject dataObject, string format, ClipboardFormatRule formatRule, ApplicationRule applicationRule, string timestamp, string baseOutputDir, ForegroundApplicationInfo appInfo, ClipboardMetadata metadata)
        {
            try
            {
                var data = dataObject.GetData(format);
                if (data == null) 
                    return (false, "No data");

                if (!await ValidateDataSizeAsync(data, format, formatRule, applicationRule))
                    return (false, "Size limit exceeded");

                string outputDir = GetFormatOutputDirectory(baseOutputDir, formatRule);
                string filePath = BuildFilePath(outputDir, timestamp, format, data, formatRule, applicationRule);

                long clipboardDataSize = FileUtils.GetDataSize(data);
                string contentHash = CalculateContentHash(data);
                string extension = FileUtils.GetFileExtension(format, data);
                string dataType = GetDataType(data);

                await FileUtils.SaveClipboardDataAsync(filePath, format, data);

                // Check for URL and save cleaned version if applicable
                await ProcessUrlIfApplicableAsync(data, filePath, outputDir, timestamp, format, formatRule, applicationRule);

                // Get file size after saving
                long fileDataSize = new FileInfo(filePath).Length;
                string relativeFilePath = Path.GetRelativePath(baseOutputDir, filePath);

                // Add to metadata
                metadata.SavedFormats.Add(new SavedFormatMetadata
                {
                    FormatName = format,
                    FileExtension = extension,
                    DataType = dataType,
                    ClipboardDataSize = clipboardDataSize,
                    FileDataSize = fileDataSize,
                    SHA256Hash = contentHash,
                    RelativeFilePath = relativeFilePath
                });

                await _loggingService.LogEventAsync("FileSaved", $"Clipboard data saved to file",
                    $"Format: {format}, File: {Path.GetFileName(filePath)}, Size: {clipboardDataSize} bytes, Directory: {Path.GetRelativePath(baseOutputDir, outputDir)}, Source: {appInfo.ProcessName}");
                
                // Update last saved location for tray icon service
                if (_trayIconService != null)
                {
                    // Check if custom directories are set in rules - if so, use prefix naming
                    bool hasCustomDirectory = (formatRule != null && !string.IsNullOrWhiteSpace(formatRule.RelativeDestinationDirectory)) ||
                                            (applicationRule != null && !string.IsNullOrWhiteSpace(applicationRule.RelativeDestinationDirectory));
                    
                    if (_settings.UseTimestampSubdirectories && !hasCustomDirectory)
                    {
                        // For timestamp directories, pass the timestamp subdirectory path
                        string timestampDir = Path.Combine(outputDir, timestamp);
                        _trayIconService.UpdateLastSavedLocation(timestampDir);
                    }
                    else
                    {
                        // For regular saves or custom directories, pass the full file path
                        _trayIconService.UpdateLastSavedLocation(filePath);
                    }
                }

                return (true, "Saved successfully");
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("FileSaveError", $"Error saving clipboard format", $"Format: {format}, Error: {ex.Message}");
                Debug.WriteLine($"Error saving format '{format}': {ex.Message}");
                return (false, ex.Message);
            }
        }

        private bool IsUrl(string text)
        {
            // Quick length check first - URLs longer than 2048 characters are uncommon
            if (string.IsNullOrWhiteSpace(text) || text.Length > 2048)
                return false;

            // Trim whitespace for checking
            text = text.Trim();

            // Check for http:// or https:// at the beginning
            return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> LogAndCleanUrlAsync(string url)
        {
            await _loggingService.LogEventAsync("UrlDetected", "URL found in clipboard data", $"Original URL: {url}");

            try
            {
                // Use ClearUrlsService for comprehensive URL cleaning
                string cleanUrl = await _clearUrlsService.CleanUrlAsync(url);
                
                if (cleanUrl != url)
                {
                    await _loggingService.LogEventAsync("UrlCleanedByClearUrls", "URL cleaned by ClearURLs service", 
                        $"Original: {url}, Clean: {cleanUrl}");
                }
                else
                {
                    // Fallback to basic cleaning if ClearURLs didn't change anything
                    var uri = new Uri(url.Trim());
                    cleanUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
                    
                    await _loggingService.LogEventAsync("UrlCleanedBasic", "URL query parameters removed (basic cleaning)", 
                        $"Original: {url}, Clean: {cleanUrl}");
                }
                
                return cleanUrl;
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("UrlCleaningError", "Error cleaning URL", 
                    $"URL: {url}, Error: {ex.Message}");
                return url; // Return original if cleaning fails
            }
        }

        private async Task ProcessUrlIfApplicableAsync(object data, string originalFilePath, string outputDir, string timestamp, string format, ClipboardFormatRule formatRule, ApplicationRule applicationRule)
        {
            // Only process string data that could contain URLs
            if (!(data is string textData))
                return;

            if (!IsUrl(textData))
                return;

            try
            {
                string cleanUrl = await LogAndCleanUrlAsync(textData);
                
                // Build clean URL file path using the same logic as original file
                string cleanUrlFilePath = BuildCleanUrlFilePath(outputDir, timestamp, format, data, formatRule, applicationRule);

                // Save cleaned URL
                await File.WriteAllTextAsync(cleanUrlFilePath, cleanUrl);

                await _loggingService.LogEventAsync("CleanUrlSaved", "Cleaned URL saved to separate file",
                    $"Original file: {Path.GetFileName(originalFilePath)}, Clean URL file: {Path.GetFileName(cleanUrlFilePath)}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("UrlProcessingError", "Error processing URL data", 
                    $"Format: {format}, Error: {ex.Message}");
            }
        }

        private string BuildCleanUrlFilePath(string outputDir, string timestamp, string format, object data, ClipboardFormatRule formatRule = null, ApplicationRule applicationRule = null)
        {
            string safeFormatName = FileUtils.SanitizeFileName(format);
            string extension = FileUtils.GetFileExtension(format, data);
            
            // Check if custom directories are set in rules - if so, use prefix naming
            bool hasCustomDirectory = (formatRule != null && !string.IsNullOrWhiteSpace(formatRule.RelativeDestinationDirectory)) ||
                                    (applicationRule != null && !string.IsNullOrWhiteSpace(applicationRule.RelativeDestinationDirectory));
            
            if (_settings.UseTimestampSubdirectories && !hasCustomDirectory)
            {
                // Create subdirectory based on timestamp
                string timestampDir = Path.Combine(outputDir, timestamp);
                if (!Directory.Exists(timestampDir))
                    Directory.CreateDirectory(timestampDir);
                
                string fileName = $"{safeFormatName}-cleanurl.{extension}";
                return Path.Combine(timestampDir, fileName);
            }
            else
            {
                // Use timestamp prefix in filename (original behavior)
                string fileName = $"{timestamp}_{safeFormatName}-cleanurl.{extension}";
                return Path.Combine(outputDir, fileName);
            }
        }

        private string GetDataType(object data)
        {
            if (data == null) return "null";
            
            var type = data.GetType();
            if (type == typeof(string)) return "string";
            if (type == typeof(byte[])) return "byte[]";
            if (type == typeof(System.IO.MemoryStream)) return "MemoryStream";
            if (typeof(System.IO.Stream).IsAssignableFrom(type)) return "Stream";
            if (type == typeof(System.Drawing.Bitmap)) return "Bitmap";
            if (type == typeof(System.Drawing.Image)) return "Image";
            if (type.Name.Contains("Metafile")) return "Metafile";
            
            return type.Name;
        }

        private async Task SaveMetadataAsync(ClipboardMetadata metadata, string timestamp, string baseOutputDir)
        {
            try
            {
                string metadataFileName = _settings.UseTimestampSubdirectories 
                    ? Path.Combine(baseOutputDir, timestamp, "metadata.json")
                    : Path.Combine(baseOutputDir, $"{timestamp}_metadata.json");

                // Ensure directory exists
                string metadataDir = Path.GetDirectoryName(metadataFileName);
                if (!Directory.Exists(metadataDir))
                    Directory.CreateDirectory(metadataDir);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string jsonContent = JsonSerializer.Serialize(metadata, options);
                await File.WriteAllTextAsync(metadataFileName, jsonContent);

                await _loggingService.LogEventAsync("MetadataSaved", "Clipboard metadata saved",
                    $"File: {Path.GetFileName(metadataFileName)}, Detected formats: {metadata.DetectedFormats.Count}, Saved formats: {metadata.SavedFormats.Count}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("MetadataSaveError", "Error saving metadata", $"Error: {ex.Message}");
            }
        }

        private async Task CollectDetectedFormatsMetadataAsync(System.Windows.IDataObject dataObject, string[] formats, ClipboardMetadata metadata)
        {
            foreach (string format in formats)
            {
                try
                {
                    var data = dataObject.GetData(format);
                    if (data == null) continue;

                    long dataSize = FileUtils.GetDataSize(data);
                    string contentHash = CalculateContentHash(data);

                    metadata.DetectedFormats.Add(new ClipboardFormatMetadata
                    {
                        FormatName = format,
                        DataSize = dataSize,
                        SHA256Hash = contentHash
                    });
                }
                catch (Exception ex)
                {
                    await _loggingService.LogEventAsync("MetadataCollectionError", 
                        $"Error collecting metadata for format '{format}'", $"Error: {ex.Message}");
                    
                    // Add entry with error information
                    metadata.DetectedFormats.Add(new ClipboardFormatMetadata
                    {
                        FormatName = format,
                        DataSize = -1,
                        SHA256Hash = "ERROR"
                    });
                }
            }
        }

        private async Task<bool> ValidateDataSizeAsync(object data, string format, ClipboardFormatRule formatRule, ApplicationRule applicationRule)
        {
            long dataSize = FileUtils.GetDataSize(data);
            
            // Check minimum clipboard data size
            if (dataSize < _settings.MinClipboardDataSizeBytes)
            {
                await _loggingService.LogEventAsync("ClipboardFormatSkipped", $"Format skipped due to minimum clipboard data size limit",
                    $"Format: {format}, Size: {dataSize} bytes, Minimum: {_settings.MinClipboardDataSizeBytes} bytes");
                Debug.WriteLine($"Skipping format '{format}' - clipboard data size {dataSize} bytes is below minimum limit of {_settings.MinClipboardDataSizeBytes} bytes");
                return false;
            }

            int maxSizeLimit = (formatRule?.MaxSizeKB ?? applicationRule?.MaxSizeKB ?? _settings.MaxFileSizeKB) * 1024;

            if (maxSizeLimit > 0 && dataSize > maxSizeLimit)
            {
                string limitSource = formatRule != null ? "format rule" :
                                   applicationRule != null ? "application rule" : "global setting";
                await _loggingService.LogEventAsync("ClipboardFormatSkipped", $"Format skipped due to size limit ({limitSource})",
                    $"Format: {format}, Size: {dataSize} bytes, Limit: {maxSizeLimit} bytes");
                Debug.WriteLine($"Skipping format '{format}' - size {dataSize} bytes exceeds {limitSource} limit of {maxSizeLimit} bytes");
                return false;
            }

            return true;
        }

        private string GetFormatOutputDirectory(string baseOutputDir, ClipboardFormatRule formatRule)
        {
            string outputDir = baseOutputDir;
            if (formatRule != null && !string.IsNullOrWhiteSpace(formatRule.RelativeDestinationDirectory))
            {
                outputDir = Path.Combine(baseOutputDir, formatRule.RelativeDestinationDirectory);
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
            }
            return outputDir;
        }

        private string BuildFilePath(string outputDir, string timestamp, string format, object data, ClipboardFormatRule formatRule = null, ApplicationRule applicationRule = null)
        {
            string safeFormatName = FileUtils.SanitizeFileName(format);
            string extension = FileUtils.GetFileExtension(format, data);
            
            // Check if custom directories are set in rules - if so, use prefix naming
            bool hasCustomDirectory = (formatRule != null && !string.IsNullOrWhiteSpace(formatRule.RelativeDestinationDirectory)) ||
                                    (applicationRule != null && !string.IsNullOrWhiteSpace(applicationRule.RelativeDestinationDirectory));
            
            if (_settings.UseTimestampSubdirectories && !hasCustomDirectory)
            {
                // Create subdirectory based on timestamp
                string timestampDir = Path.Combine(outputDir, timestamp);
                if (!Directory.Exists(timestampDir))
                    Directory.CreateDirectory(timestampDir);
                
                string fileName = $"{safeFormatName}.{extension}";
                return Path.Combine(timestampDir, fileName);
            }
            else
            {
                // Use timestamp prefix in filename (original behavior)
                string fileName = $"{timestamp}_{safeFormatName}.{extension}";
                return Path.Combine(outputDir, fileName);
            }
        }
    }
}
