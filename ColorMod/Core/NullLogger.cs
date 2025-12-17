using System;
using FFTColorCustomizer.Interfaces;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Null object pattern implementation of ILogger that does nothing.
    /// Useful for testing and scenarios where logging should be disabled.
    /// </summary>
    public class NullLogger : ILogger
    {
        /// <summary>
        /// Gets the singleton instance of NullLogger
        /// </summary>
        public static NullLogger Instance { get; } = new NullLogger();

        public LogLevel LogLevel { get; set; } = LogLevel.None;

        public void Log(string message)
        {
            // Intentionally empty
        }

        public void LogError(string message)
        {
            // Intentionally empty
        }

        public void LogError(string message, Exception exception)
        {
            // Intentionally empty
        }

        public void LogWarning(string message)
        {
            // Intentionally empty
        }

        public void LogDebug(string message)
        {
            // Intentionally empty
        }
    }
}
