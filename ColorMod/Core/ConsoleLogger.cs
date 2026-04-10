using System;
using System.IO;
using FFTColorCustomizer.Interfaces;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Console-based implementation of ILogger.
    /// Also tees all output to claude_bridge/live_log.txt next to the mod DLL,
    /// so Claude can read fresh logs without requiring the user to copy-paste
    /// from the game's console window.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly string _prefix;
        private static readonly object _fileLock = new object();
        private static string? _logFilePath;
        private static bool _logFileInitialized = false;

        public ConsoleLogger(string prefix = null)
        {
            _prefix = prefix ?? ColorModConstants.LogPrefix;
        }

        public LogLevel LogLevel { get; set; } = LogLevel.Debug;

        public void Log(string message)
        {
            if (LogLevel <= LogLevel.Info)
            {
                WriteToConsole($"{_prefix} {message ?? string.Empty}");
            }
        }

        public void LogError(string message)
        {
            if (LogLevel <= LogLevel.Error)
            {
                WriteToConsole($"{_prefix} ERROR: {message ?? string.Empty}");
            }
        }

        public void LogError(string message, Exception exception)
        {
            LogError(message);
            if (exception != null && LogLevel <= LogLevel.Error)
            {
                WriteToConsole($"  {exception.GetType().Name}: {exception.Message}");
                if (LogLevel <= LogLevel.Debug && !string.IsNullOrEmpty(exception.StackTrace))
                {
                    WriteToConsole($"  Stack Trace: {exception.StackTrace}");
                }
            }
        }

        public void LogWarning(string message)
        {
            if (LogLevel <= LogLevel.Warning)
            {
                WriteToConsole($"{_prefix} WARNING: {message ?? string.Empty}");
            }
        }

        public void LogDebug(string message)
        {
            if (LogLevel <= LogLevel.Debug)
            {
                WriteToConsole($"{_prefix} DEBUG: {message ?? string.Empty}");
            }
        }

        private void WriteToConsole(string message)
        {
            try
            {
                Console.WriteLine(message);
            }
            catch (ObjectDisposedException)
            {
                // Console has been disposed (common in tests)
                // Silently ignore
            }
            TeeToFile(message);
        }

        /// <summary>
        /// Tees each log line to claude_bridge/live_log.txt. On first call,
        /// discovers the log path via the executing assembly's directory and
        /// truncates the file so a fresh game launch starts with an empty log.
        /// Silently swallows errors — file-log tee must never break console logging.
        /// </summary>
        private static void TeeToFile(string message)
        {
            try
            {
                lock (_fileLock)
                {
                    if (!_logFileInitialized)
                    {
                        _logFileInitialized = true;
                        var asmLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        var asmDir = Path.GetDirectoryName(asmLocation);
                        if (string.IsNullOrEmpty(asmDir)) return;

                        var bridgeDir = Path.Combine(asmDir, "claude_bridge");
                        if (!Directory.Exists(bridgeDir))
                            Directory.CreateDirectory(bridgeDir);

                        _logFilePath = Path.Combine(bridgeDir, "live_log.txt");
                        // Truncate on first write so each game launch starts fresh.
                        File.WriteAllText(_logFilePath, string.Empty);
                    }

                    if (_logFilePath != null)
                    {
                        File.AppendAllText(_logFilePath, message + Environment.NewLine);
                    }
                }
            }
            catch
            {
                // File tee must never throw — swallow everything.
            }
        }
    }
}
