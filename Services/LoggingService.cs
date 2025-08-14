using System;
using System.IO;
using System.Threading.Tasks;

namespace ClipDumpRe.Services
{
    public class LoggingService
    {
        private const string LogFileName = "clipdump-re.log";
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        public LoggingService()
        {
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFileName);
        }

        public void LogEvent(string eventName, string description, string additionalInfo = "")
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var logEntry = $"{timestamp}\t{eventName}\t{description}\t{additionalInfo}";

            lock (_lockObject)
            {
                try
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Silently handle logging errors
                }
            }
        }

        public async Task LogEventAsync(string eventName, string description, string additionalInfo = "")
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var logEntry = $"{timestamp}\t{eventName}\t{description}\t{additionalInfo}";

            try
            {
                await File.AppendAllTextAsync(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Silently handle logging errors
            }
        }
    }
}
