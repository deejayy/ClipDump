using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using ClipDumpRe.Models;

namespace ClipDumpRe.Services
{
    internal class ClearUrlsService
    {
        private readonly LoggingService _loggingService;
        private ClearUrlsData _clearUrlsData;
        private bool _isInitialized = false;

        public ClearUrlsService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public async Task InitializeAsync()
        {
            try
            {
                string executablePath = AppDomain.CurrentDomain.BaseDirectory;
                string clearUrlsPath = Path.Combine(executablePath, "clearurls.json");

                if (!File.Exists(clearUrlsPath))
                {
                    await _loggingService.LogEventAsync("ClearUrlsFileNotFound", "clearurls.json file not found", 
                        $"Expected path: {clearUrlsPath}");
                    _clearUrlsData = new ClearUrlsData { Providers = new Dictionary<string, ClearUrlsProvider>() };
                    _isInitialized = true;
                    return;
                }

                string jsonContent = await File.ReadAllTextAsync(clearUrlsPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                _clearUrlsData = JsonSerializer.Deserialize<ClearUrlsData>(jsonContent, options);
                _isInitialized = true;

                await _loggingService.LogEventAsync("ClearUrlsLoaded", "ClearURLs rules loaded successfully", 
                    $"Providers loaded: {_clearUrlsData.Providers?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("ClearUrlsLoadError", "Error loading ClearURLs rules", 
                    $"Error: {ex.Message}");
                _clearUrlsData = new ClearUrlsData { Providers = new Dictionary<string, ClearUrlsProvider>() };
                _isInitialized = true;
            }
        }

        public async Task<string> CleanUrlAsync(string url)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            if (string.IsNullOrWhiteSpace(url) || _clearUrlsData?.Providers == null)
            {
                return url;
            }

            try
            {
                string cleanedUrl = url;

                foreach (var provider in _clearUrlsData.Providers.Values)
                {
                    if (await ShouldProcessUrlAsync(cleanedUrl, provider))
                    {
                        cleanedUrl = await ProcessUrlWithProviderAsync(cleanedUrl, provider);
                    }
                }

                if (cleanedUrl != url)
                {
                    await _loggingService.LogEventAsync("UrlCleaned", "URL cleaned using ClearURLs rules", 
                        $"Original: {url}, Cleaned: {cleanedUrl}");
                }

                return cleanedUrl;
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("UrlCleaningError", "Error during URL cleaning", 
                    $"URL: {url}, Error: {ex.Message}");
                return url;
            }
        }

        private async Task<bool> ShouldProcessUrlAsync(string url, ClearUrlsProvider provider)
        {
            try
            {
                // Check if URL matches the provider's pattern
                if (!string.IsNullOrEmpty(provider.UrlPattern))
                {
                    var regex = new Regex(provider.UrlPattern, RegexOptions.IgnoreCase);
                    if (!regex.IsMatch(url))
                    {
                        return false;
                    }
                }

                // Check exceptions
                if (provider.Exceptions != null)
                {
                    foreach (string exception in provider.Exceptions)
                    {
                        var exceptionRegex = new Regex(exception, RegexOptions.IgnoreCase);
                        if (exceptionRegex.IsMatch(url))
                        {
                            await _loggingService.LogEventAsync("UrlExceptionMatched", "URL matches exception rule", 
                                $"URL: {url}, Exception: {exception}");
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("UrlPatternMatchError", "Error matching URL pattern", 
                    $"URL: {url}, Error: {ex.Message}");
                return false;
            }
        }

        private async Task<string> ProcessUrlWithProviderAsync(string url, ClearUrlsProvider provider)
        {
            string processedUrl = url;

            try
            {
                // Handle complete provider (block all matching URLs)
                if (provider.CompleteProvider)
                {
                    await _loggingService.LogEventAsync("UrlBlocked", "URL blocked by complete provider rule", 
                        $"URL: {url}");
                    return processedUrl; // Return as-is since we can't actually block in this context
                }

                // Handle redirections first
                if (provider.Redirections != null)
                {
                    foreach (string redirection in provider.Redirections)
                    {
                        var redirectRegex = new Regex(redirection, RegexOptions.IgnoreCase);
                        var match = redirectRegex.Match(processedUrl);
                        if (match.Success && match.Groups.Count > 1)
                        {
                            string redirectTarget = HttpUtility.UrlDecode(match.Groups[1].Value);
                            await _loggingService.LogEventAsync("UrlRedirected", "URL redirected", 
                                $"Original: {processedUrl}, Target: {redirectTarget}");
                            processedUrl = redirectTarget;
                            break;
                        }
                    }
                }

                // Apply raw rules
                if (provider.RawRules != null)
                {
                    foreach (string rawRule in provider.RawRules)
                    {
                        var rawRegex = new Regex(rawRule, RegexOptions.IgnoreCase);
                        string beforeRaw = processedUrl;
                        processedUrl = rawRegex.Replace(processedUrl, "");
                        if (beforeRaw != processedUrl)
                        {
                            await _loggingService.LogEventAsync("RawRuleApplied", "Raw rule applied to URL", 
                                $"Rule: {rawRule}, Before: {beforeRaw}, After: {processedUrl}");
                        }
                    }
                }

                // Apply standard rules (field removal)
                if (provider.Rules != null)
                {
                    foreach (string rule in provider.Rules)
                    {
                        // Convert rule to regex pattern for field matching
                        string fieldPattern = $@"(?:&|[/?#&])(?:{rule}=[^&]*)";
                        var fieldRegex = new Regex(fieldPattern, RegexOptions.IgnoreCase);
                        string beforeField = processedUrl;
                        processedUrl = fieldRegex.Replace(processedUrl, "");
                        if (beforeField != processedUrl)
                        {
                            await _loggingService.LogEventAsync("FieldRuleApplied", "Field rule applied to URL", 
                                $"Rule: {rule}, Before: {beforeField}, After: {processedUrl}");
                        }
                    }
                }

                // Clean up any resulting URL artifacts
                processedUrl = CleanUrlArtifacts(processedUrl);

                return processedUrl;
            }
            catch (Exception ex)
            {
                await _loggingService.LogEventAsync("UrlProcessingError", "Error processing URL with provider", 
                    $"URL: {url}, Error: {ex.Message}");
                return url;
            }
        }

        private string CleanUrlArtifacts(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            // Remove trailing ? or & characters
            url = url.TrimEnd('?', '&');

            // Fix double separators
            url = Regex.Replace(url, @"[?&]{2,}", "?");
            url = Regex.Replace(url, @"&{2,}", "&");

            // Ensure proper separator after domain
            url = Regex.Replace(url, @"(\.[a-z]{2,})&", "$1?");

            return url;
        }
    }
}
