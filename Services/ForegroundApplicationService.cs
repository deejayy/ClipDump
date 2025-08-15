using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ClipDumpRe.Models;

namespace ClipDumpRe.Services
{
    internal class ForegroundApplicationService
    {
        private readonly LoggingService _loggingService;

        // P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        public ForegroundApplicationService(LoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public ForegroundApplicationInfo GetForegroundApplicationInfo()
        {
            try
            {
                // Get the foreground window handle
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    return new ForegroundApplicationInfo
                    {
                        ProcessName = "Unknown",
                        ExecutablePath = "Unknown",
                        WindowTitle = "Unknown",
                        WindowClass = "Unknown"
                    };
                }

                // Get the process ID
                GetWindowThreadProcessId(foregroundWindow, out uint processId);

                // Get window title
                int titleLength = GetWindowTextLength(foregroundWindow);
                StringBuilder windowTitle = new StringBuilder(titleLength + 1);
                GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);

                // Get window class name
                StringBuilder windowClass = new StringBuilder(256);
                GetClassName(foregroundWindow, windowClass, windowClass.Capacity);

                // Get process information
                string processName = "Unknown";
                string executablePath = "Unknown";

                try
                {
                    using (Process process = Process.GetProcessById((int)processId))
                    {
                        processName = process.ProcessName;
                        try
                        {
                            executablePath = process.MainModule?.FileName ?? "Access Denied";
                        }
                        catch (Exception)
                        {
                            // Some system processes may deny access to MainModule
                            executablePath = "Access Denied";
                        }
                    }
                }
                catch (Exception ex)
                {
                    processName = $"Error: {ex.Message}";
                    executablePath = $"Error: {ex.Message}";
                }

                return new ForegroundApplicationInfo
                {
                    ProcessName = processName,
                    ExecutablePath = executablePath,
                    WindowTitle = windowTitle.ToString(),
                    WindowClass = windowClass.ToString()
                };
            }
            catch (Exception ex)
            {
                _loggingService.LogEvent("ForegroundAppDetectionError", "Error detecting foreground application", $"Error: {ex.Message}");
                return new ForegroundApplicationInfo
                {
                    ProcessName = "Error",
                    ExecutablePath = "Error",
                    WindowTitle = "Error",
                    WindowClass = "Error"
                };
            }
        }
    }
}
