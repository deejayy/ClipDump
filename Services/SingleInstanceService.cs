using System;
using System.IO;
using System.Threading;

namespace ClipDumpRe.Services
{
    internal class SingleInstanceService : IDisposable
    {
        private readonly Mutex _mutex;
        private readonly string _mutexName;
        private bool _isOwner;
        private readonly LoggingService _loggingService;

        public SingleInstanceService()
        {
            _mutexName = "Global\\ClipDumpRe_SingleInstance_Mutex";
            _mutex = new Mutex(true, _mutexName, out _isOwner);
            _loggingService = new LoggingService();
        }

        public bool IsFirstInstance()
        {
            if (_isOwner)
            {
                _loggingService.LogEvent("SingleInstanceCheck", "Application is first instance", "Mutex acquired successfully");
                return true;
            }

            _loggingService.LogEvent("SingleInstanceCheck", "Another instance is already running", "Mutex not acquired");
            return false;
        }

        public void Dispose()
        {
            if (_isOwner)
            {
                _mutex?.ReleaseMutex();
                _loggingService.LogEvent("SingleInstanceRelease", "Mutex released", "Application shutting down");
            }
            _mutex?.Dispose();
        }
    }
}
