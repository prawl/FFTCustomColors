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

        // --- Typed entry points (CC-2): the migration target for every call site. When the
        // installed instance is the typed two-sink logger these flow through it (verb-aware
        // rendering, console dedup); any other ILogger (tests, DI fakes) gets the verb folded
        // into the message so captures still see one line per call. ---

        /// <summary>
        /// Logs an informational message under a verb from the closed glossary
        /// </summary>
        public static void Log(LogVerb verb, string message)
        {
            if (Instance is FileConsoleLogger typed) typed.Log(verb, message);
            else Instance.Log($"[{verb.Token()}] {message}");
        }

        /// <summary>
        /// Logs a warning under a verb from the closed glossary
        /// </summary>
        public static void LogWarning(LogVerb verb, string message)
        {
            if (Instance is FileConsoleLogger typed) typed.LogWarning(verb, message);
            else Instance.LogWarning($"[{verb.Token()}] {message}");
        }

        /// <summary>
        /// Logs an error under a verb from the closed glossary
        /// </summary>
        public static void LogError(LogVerb verb, string message)
        {
            if (Instance is FileConsoleLogger typed) typed.LogError(verb, message);
            else Instance.LogError($"[{verb.Token()}] {message}");
        }

        /// <summary>
        /// Logs an error with exception details under a verb from the closed glossary
        /// </summary>
        public static void LogError(LogVerb verb, string message, Exception exception)
        {
            if (Instance is FileConsoleLogger typed) typed.LogError(verb, message, exception);
            else Instance.LogError($"[{verb.Token()}] {message}", exception);
        }

        /// <summary>
        /// Logs a debug message under a verb from the closed glossary
        /// </summary>
        public static void LogDebug(LogVerb verb, string message)
        {
            if (Instance is FileConsoleLogger typed) typed.LogDebug(verb, message);
            else Instance.LogDebug($"[{verb.Token()}] {message}");
        }

        /// <summary>
        /// Marks the edge of an apply pass, resetting the console dedup so recurring
        /// warnings print once per pass (no-op on a legacy logger instance)
        /// </summary>
        public static void NoteApplyPassEdge()
        {
            if (Instance is FileConsoleLogger typed) typed.NoteApplyPassEdge();
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

        /// <summary>
        /// Disables all logging by setting LogLevel to None
        /// </summary>
        public static void DisableLogging()
        {
            LogLevel = Interfaces.LogLevel.None;
        }

        /// <summary>
        /// Enables logging with the specified level (defaults to Info)
        /// </summary>
        public static void EnableLogging(Interfaces.LogLevel level = Interfaces.LogLevel.Info)
        {
            LogLevel = level;
        }
    }
}
