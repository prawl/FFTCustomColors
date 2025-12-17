using System;

namespace FFTColorCustomizer.Interfaces
{
    /// <summary>
    /// Defines logging operations for the application
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Gets or sets the minimum log level to output
        /// </summary>
        LogLevel LogLevel { get; set; }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        void Log(string message);

        /// <summary>
        /// Logs an error message
        /// </summary>
        void LogError(string message);

        /// <summary>
        /// Logs an error message with exception details
        /// </summary>
        void LogError(string message, Exception exception);

        /// <summary>
        /// Logs a warning message
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Logs a debug message
        /// </summary>
        void LogDebug(string message);
    }

    /// <summary>
    /// Defines the verbosity level for logging
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        None = 4
    }
}
