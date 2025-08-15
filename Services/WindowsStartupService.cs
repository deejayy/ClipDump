using System;
using Microsoft.Win32;
using System.Reflection;
using System.Diagnostics;

namespace ClipDumpRe.Services
{
    public class WindowsStartupService
    {
        private readonly LoggingService _loggingService;
        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "Clipboard Dumper";

        public WindowsStartupService(LoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public void SetStartupWithWindows(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (key == null)
                    {
                        _loggingService.LogEvent("StartupRegistryKeyFailed", "Could not open registry key for startup", REGISTRY_KEY);
                        return;
                    }

                    if (enable)
                    {
                        string executablePath = Assembly.GetExecutingAssembly().Location;
                        if (executablePath.EndsWith(".dll"))
                        {
                            // If running as .NET app, get the actual executable
                            executablePath = Process.GetCurrentProcess().MainModule.FileName;
                        }

                        // Use human-readable description with publisher info
                        string displayName = "Clipboard Dumper by DeeJayy";
                        key.SetValue(APP_NAME, $"\"{executablePath}\" /startup", RegistryValueKind.String);
                        _loggingService.LogEvent("StartupEnabled", $"Application '{displayName}' added to Windows startup", executablePath);
                    }
                    else
                    {
                        key.DeleteValue(APP_NAME, false);
                        _loggingService.LogEvent("StartupDisabled", "Application removed from Windows startup", "");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogEvent("StartupRegistryError", "Error managing Windows startup registry", ex.Message);
            }
        }

        public bool IsStartupWithWindowsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key == null) return false;
                    
                    var value = key.GetValue(APP_NAME);
                    return value != null;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogEvent("StartupRegistryCheckError", "Error checking Windows startup registry", ex.Message);
                return false;
            }
        }
    }
}
