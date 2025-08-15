using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ClipDumpRe.Models;

namespace ClipDumpRe.Services
{
    public interface IApplicationEnumerationService
    {
        List<RunningApplication> GetRunningApplicationsWithWindows();
        List<RunningApplication> GetFilteredApplications(IEnumerable<ApplicationRule> existingRules);
    }

    public class ApplicationEnumerationService : IApplicationEnumerationService
    {
        private readonly LoggingService _loggingService;

        // P/Invoke declarations for window enumeration
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private static readonly string[] SystemProcesses = new[]
        {
            "dwm", "winlogon", "csrss", "smss", "wininit", "services", "lsass",
            "explorer", "svchost", "conhost", "audiodg", "spoolsv", "taskhost",
            "taskhostw", "RuntimeBroker", "ApplicationFrameHost"
        };

        public ApplicationEnumerationService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public List<RunningApplication> GetRunningApplicationsWithWindows()
        {
            var processesWithWindows = new Dictionary<uint, string>();
            var shellWindow = GetShellWindow();

            try
            {
                // Enumerate all visible windows to find processes that have them and get window titles
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd) && hWnd != shellWindow)
                    {
                        GetWindowThreadProcessId(hWnd, out uint processId);
                        if (processId != 0)
                        {
                            // Get window title
                            int titleLength = GetWindowTextLength(hWnd);
                            if (titleLength > 0)
                            {
                                StringBuilder windowTitle = new StringBuilder(titleLength + 1);
                                GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                                string title = windowTitle.ToString();

                                // Only add if we have a meaningful title
                                if (!string.IsNullOrWhiteSpace(title))
                                {
                                    processesWithWindows[processId] = title;
                                }
                            }
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                var runningApps = new List<RunningApplication>();
                var seenExecutables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in processesWithWindows)
                {
                    uint processId = kvp.Key;
                    string windowTitle = kvp.Value;

                    try
                    {
                        using (var process = Process.GetProcessById((int)processId))
                        {
                            string processName = process.ProcessName;
                            string executableName = $"{processName}.exe";

                            // Try to get the actual executable filename if possible
                            try
                            {
                                var mainModule = process.MainModule;
                                if (mainModule?.FileName != null)
                                {
                                    executableName = System.IO.Path.GetFileName(mainModule.FileName);
                                }
                            }
                            catch
                            {
                                // Fall back to process name + .exe if we can't access MainModule
                            }

                            // Skip system processes and duplicates
                            if (!string.IsNullOrEmpty(executableName) &&
                                !seenExecutables.Contains(executableName) &&
                                !IsSystemProcess(processName))
                            {
                                runningApps.Add(new RunningApplication
                                {
                                    ExecutableName = executableName,
                                    ProcessName = processName,
                                    WindowTitle = windowTitle,
                                    ProcessId = (int)processId
                                });
                                seenExecutables.Add(executableName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogEvent("ProcessAccessError", 
                            "Failed to access process during enumeration", 
                            $"ProcessId: {processId}, Error: {ex.Message}");
                    }
                }

                _loggingService.LogEvent("ApplicationEnumerationCompleted", 
                    "Successfully enumerated running applications", 
                    $"Found {runningApps.Count} applications with windows");

                return runningApps;
            }
            catch (Exception ex)
            {
                _loggingService.LogEvent("ApplicationEnumerationFailed", 
                    "Failed to enumerate running applications", 
                    $"Error: {ex.Message}");
                return new List<RunningApplication>();
            }
        }

        public List<RunningApplication> GetFilteredApplications(IEnumerable<ApplicationRule> existingRules)
        {
            var runningApps = GetRunningApplicationsWithWindows();
            
            // Get existing application rules to filter out
            var existingRuleNames = new HashSet<string>(
                existingRules?.Select(r => r.ExecutableFileName) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            var filteredApps = runningApps
                .Where(app => !existingRuleNames.Contains(app.ExecutableName))
                .OrderBy(app => app.ExecutableName)
                .ToList();

            _loggingService.LogEvent("ApplicationsFiltered", 
                "Filtered applications against existing rules", 
                $"Total: {runningApps.Count}, Filtered: {filteredApps.Count}, Existing rules: {existingRuleNames.Count}");

            return filteredApps;
        }

        private static bool IsSystemProcess(string processName)
        {
            return SystemProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
