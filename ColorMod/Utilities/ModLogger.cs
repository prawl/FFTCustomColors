using System;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Static logging facade for FFT Color Mod that maintains backward compatibility
    /// while delegating to the new ILogger interface
    /// </summary>
    public static class ModLogger
    {
        private static ILogger _logger;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets or sets the underlying logger implementation.
        /// Defaults to ConsoleLogger if not set.
        /// </summary>
        public static ILogger Instance
        {
            get
            {
                if (_logger == null)
                {
                    lock (_lock)
                    {
                        if (_logger == null)
                        {
                            _logger = new ConsoleLogger(ColorModConstants.LogPrefix);
                        }
                    }
                }
                return _logger;
            }
            set
            {
                lock (_lock)
                {
                    _logger = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether debug logging is enabled (for backward compatibility)
        /// </summary>
        public static bool EnableDebugLogging
        {
            get => Instance.LogLevel <= Interfaces.LogLevel.Debug;
            set => Instance.LogLevel = value ? Interfaces.LogLevel.Debug : Interfaces.LogLevel.Info;
        }

        /// <summary>
        /// Gets or sets the minimum log level to output
        /// </summary>
        public static Interfaces.LogLevel LogLevel
        {
            get => Instance.LogLevel;
            set => Instance.LogLevel = value;
        }

        /// <summary>
        /// Logs a standard information message
        /// </summary>
        public static void Log(string message)
        {
            Instance.Log(message);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void LogError(string message)
        {
            Instance.LogError(message);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            Instance.LogWarning(message);
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        public static void LogDebug(string message)
        {
            Instance.LogDebug(message);
        }

        /// <summary>
        /// Logs an exception with formatted output
        /// </summary>
        public static void LogException(string message, Exception exception)
        {
            Instance.LogError(message, exception);
        }

        /// <summary>
        /// Logs a success message with a checkmark
        /// </summary>
        public static void LogSuccess(string message)
        {
            Instance.Log($"✓ {message}");
        }

        /// <summary>
        /// Logs a section header for better organization
        /// </summary>
        public static void LogSection(string sectionName)
        {
            Instance.Log(string.Empty);
            Instance.Log("========================================");
            Instance.Log($"  {sectionName}");
            Instance.Log("========================================");
        }

        /// <summary>
        /// Resets the logger to default (useful for testing)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _logger = null;
            }
        }

        /// <summary>
        /// Sets the logger to use a null logger (useful for testing)
        /// </summary>
        public static void UseNullLogger()
        {
            Instance = NullLogger.Instance;
        }
    }
}
