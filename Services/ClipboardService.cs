using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ClipDumpRe.Models;
using ClipDumpRe.Utils;

namespace ClipDumpRe.Services
{
    internal class ClipboardService
    {
        private readonly Settings _settings;
        private readonly LoggingService _loggingService;

        public ClipboardService(Settings settings, LoggingService loggingService)
        {
            _settings = settings;
            _loggingService = loggingService;
        }

        public async Task DumpClipboardContentAsync()
        {
            try
            {
                // Ensure we're on an STA thread for clipboard operations
                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                {
                    await _loggingService.LogEventAsync("ClipboardProcessingError", "Invalid thread apartment state for clipboard access", "Current thread must be STA for clipboard operations");
                    return;
                }

                System.Windows.IDataObject dataObject = null;
                try
                {
                    dataObject = System.Windows.Clipboard.GetDataObject();
                }
                catch (Exception ex)
                {
                    await _loggingService.LogEventAsync("ClipboardAccessError", "Failed to access clipboard", $"Error: {ex.Message}");
                    return;
                }

                if (dataObject == null)
                {
                    await _loggingService.LogEventAsync("ClipboardProcessingSkipped", "No clipboard data object available", "");
                    return;
                }

                var formats = dataObject.GetFormats();

                // Check if clipboard content should be excluded from monitoring
                if (formats.Contains("ExcludeClipboardContentFromMonitorProcessing"))
                {
                    await _loggingService.LogEventAsync("ClipboardProcessingSkipped", "Content excluded from monitoring", "ExcludeClipboardContentFromMonitorProcessing format detected");
                    Debug.WriteLine("Clipboard content excluded from monitoring - skipping save");
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");

                // Get working directory from settings
                string workingDir = _settings.WorkingDirectory;

                string expandedDir = Environment.ExpandEnvironmentVariables(workingDir);
                string baseOutputDir = Path.IsPathRooted(expandedDir)
                    ? expandedDir
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, expandedDir);

                if (!Directory.Exists(baseOutputDir))
                    Directory.CreateDirectory(baseOutputDir);

                await _loggingService.LogEventAsync("ClipboardProcessingStarted", $"Processing {formats.Length} clipboard formats", $"Output directory: {baseOutputDir}");

                int savedCount = 0;
                int skippedCount = 0;

                foreach (string format in formats)
                {
                    // Check format-specific rules
                    var formatRule = _settings.FormatRules.FirstOrDefault(r =>
                        string.Equals(r.Format, format, StringComparison.OrdinalIgnoreCase));

                    // If format rule exists and should be ignored, skip
                    if (formatRule?.ShouldIgnore == true)
                    {
                        await _loggingService.LogEventAsync("ClipboardFormatIgnored", $"Format skipped due to format rule", $"Format: {format}");
                        skippedCount++;
                        Debug.WriteLine($"Skipping format '{format}' due to format rule ignore setting");
                        continue;
                    }

                    try
                    {
                        var data = dataObject.GetData(format);
                        if (data == null) continue;

                        // Check data size using format-specific limit or global limit
                        long dataSize = FileUtils.GetDataSize(data);
                        int maxSizeLimit = (formatRule?.MaxSizeKB ?? _settings.MaxFileSizeKB) * 1024;

                        if (maxSizeLimit > 0 && dataSize > maxSizeLimit)
                        {
                            string limitSource = formatRule != null ? "format rule" : "global setting";
                            await _loggingService.LogEventAsync("ClipboardFormatSkipped", $"Format skipped due to size limit ({limitSource})",
                                $"Format: {format}, Size: {dataSize} bytes, Limit: {maxSizeLimit} bytes");
                            skippedCount++;
                            Debug.WriteLine($"Skipping format '{format}' - size {dataSize} bytes exceeds {limitSource} limit of {maxSizeLimit} bytes");
                            continue;
                        }

                        // Determine output directory - use format-specific or default
                        string outputDir = baseOutputDir;
                        if (formatRule != null && !string.IsNullOrWhiteSpace(formatRule.RelativeDestinationDirectory))
                        {
                            outputDir = Path.Combine(baseOutputDir, formatRule.RelativeDestinationDirectory);
                            if (!Directory.Exists(outputDir))
                                Directory.CreateDirectory(outputDir);
                        }

                        string safeFormatName = FileUtils.SanitizeFileName(format);
                        string extension = FileUtils.GetFileExtension(format, data);
                        string fileName = $"{timestamp}_{safeFormatName}.{extension}";
                        string filePath = Path.Combine(outputDir, fileName);

                        await FileUtils.SaveClipboardDataAsync(filePath, format, data);
                        await _loggingService.LogEventAsync("FileSaved", $"Clipboard data saved to file", $"Format: {format}, File: {fileName}, Size: {dataSize} bytes, Directory: {Path.GetRelativePath(baseOutputDir, outputDir)}");
                        savedCount++;
                    }
                    catch (Exception ex)
                    {
                        await _loggingService.LogEventAsync("FileSaveError", $"Error saving clipboard format", $"Format: {format}, Error: {ex.Message}");
                        Debug.WriteLine($"Error saving format '{format}': {ex.Message}");
                    }
                }

                await _loggingService.LogEventAsync("ClipboardProcessingCompleted", $"Clipboard processing finished", $"Saved: {savedCount}, Skipped: {skippedCount}, Total formats: {formats.Length}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("ClipboardProcessingError", $"Error during clipboard processing", $"Error: {ex.Message}");
                Debug.WriteLine($"Error dumping clipboard content: {ex.Message}");
            }
        }
    }
}
