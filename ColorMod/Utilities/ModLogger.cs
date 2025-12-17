using System;

namespace FFTColorMod.Utilities
{
    /// <summary>
    /// Centralized logging utility for FFT Color Mod
    /// Provides consistent formatting and log level control
    /// </summary>
    public static class ModLogger
    {
        private const string PREFIX = "[FFT Color Mod]";

        /// <summary>
        /// Gets or sets whether debug logging is enabled
        /// </summary>
        public static bool EnableDebugLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum log level to output
        /// </summary>
        public static LogLevel LogLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// Logs a standard information message
        /// </summary>
        public static void Log(string message)
        {
            if (LogLevel <= LogLevel.Info)
            {
                try
                {
                    Console.WriteLine($"{PREFIX} {message ?? string.Empty}");
                }
                catch (ObjectDisposedException)
                {
                    // Console has been disposed (common in tests)
                    // Silently ignore
                }
            }
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void LogError(string message)
        {
            if (LogLevel <= LogLevel.Error)
            {
                Console.WriteLine($"{PREFIX} ERROR: {message ?? string.Empty}");
            }
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            if (LogLevel <= LogLevel.Warning)
            {
                Console.WriteLine($"{PREFIX} WARNING: {message ?? string.Empty}");
            }
        }

        /// <summary>
        /// Logs a debug message (only if debug logging is enabled)
        /// </summary>
        public static void LogDebug(string message)
        {
            if (EnableDebugLogging && LogLevel <= LogLevel.Debug)
            {
                Console.WriteLine($"{PREFIX} DEBUG: {message ?? string.Empty}");
            }
        }

        /// <summary>
        /// Logs an exception with formatted output
        /// </summary>
        public static void LogException(string message, Exception exception)
        {
            if (LogLevel <= LogLevel.Error)
            {
                Console.WriteLine($"{PREFIX} ERROR: {message ?? string.Empty}");
                if (exception != null)
                {
                    Console.WriteLine($"  {exception.GetType().Name}: {exception.Message}");
                    if (!string.IsNullOrEmpty(exception.StackTrace))
                    {
                        Console.WriteLine($"  Stack Trace: {exception.StackTrace}");
                    }
                }
            }
        }

        /// <summary>
        /// Logs a success message with a checkmark
        /// </summary>
        public static void LogSuccess(string message)
        {
            if (LogLevel <= LogLevel.Info)
            {
                Console.WriteLine($"{PREFIX} ✓ {message ?? string.Empty}");
            }
        }

        /// <summary>
        /// Logs a section header for better organization
        /// </summary>
        public static void LogSection(string sectionName)
        {
            if (LogLevel <= LogLevel.Info)
            {
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine($"  {sectionName}");
                Console.WriteLine("========================================");
            }
        }
    }

    /// <summary>
    /// Defines the available log levels
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }
}