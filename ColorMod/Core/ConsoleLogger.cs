using System;
using FFTColorMod.Interfaces;

namespace FFTColorMod.Core
{
    /// <summary>
    /// Console-based implementation of ILogger
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly string _prefix;

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
        }
    }
}