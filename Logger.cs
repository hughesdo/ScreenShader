using System;
using System.IO;

namespace ScreenShader
{
    public static class Logger
    {
        private static bool _enabled = false;
        private static string _logFile = "screenshader.log";
        private static readonly object _lock = new();

        public static bool Enabled => _enabled;

        public static void Initialize(bool enabled)
        {
            _enabled = enabled;
            if (_enabled)
            {
                _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshader.log");
                // Clear old log
                try { File.Delete(_logFile); } catch { }
                Log("=== ScreenShader Log Started ===");
                Log($"Time: {DateTime.Now}");
                Log($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
            }
        }

        public static void Log(string message)
        {
            if (!_enabled) return;

            lock (_lock)
            {
                try
                {
                    string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                    File.AppendAllText(_logFile, line + Environment.NewLine);
                    Console.WriteLine(line);
                }
                catch { }
            }
        }

        public static void LogError(string message)
        {
            Log($"ERROR: {message}");
        }

        public static void LogWarning(string message)
        {
            Log($"WARN: {message}");
        }
    }
}

