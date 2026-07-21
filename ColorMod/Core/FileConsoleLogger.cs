using System;
using System.Collections.Generic;
using System.IO;
using FFTColorCustomizer.Interfaces;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Production <see cref="ILogger"/> with TWO-SINK semantics (CC-2, the model shared with
    /// the sibling FFT mods). The FILE sink (logs/live_log.txt, rotated per launch to
    /// live_log.prev.txt so the run a user is reporting a bug about survives the relaunch)
    /// writes EVERY message, Debug tier included, UNCONDITIONALLY, each line timestamped to
    /// the millisecond. The CONSOLE sink only writes messages at or above
    /// <see cref="LogLevel"/>, and suppresses a typed line whose (level, verb, message)
    /// identity already appeared this apply pass; <see cref="NoteApplyPassEdge"/> resets that
    /// seen-set. The FILE sink is never deduped: the evidence chain is never thinner than the
    /// console.
    ///
    /// RENDERING SPLIT: the FILE line always carries the verb:
    /// <c>[Color Customizer] [HH:mm:ss.fff] [LEVEL] [verb] description</c>. The CONSOLE line
    /// drops the "[verb] " segment at Info tier only (subject-first prose a player reads).
    /// Warning and Error console lines keep the verb (a Nexus bug report is a console paste),
    /// and so does a Debug line reaching a console raised to Debug.
    ///
    /// The legacy untyped ILogger entry points stay alive during the call-site migration and
    /// render with timestamp and level but no verb bracket; the conversion sweeps retire them.
    ///
    /// THREADING: every write locks <see cref="_gate"/>; the WPF configuration form logs from
    /// its UI thread, so sinks must stay cheap (one append, no flushing schemes) and any
    /// failure is swallowed: logging must never take down the form or the game.
    /// </summary>
    public class FileConsoleLogger : ILogger
    {
        private const string Prefix = "[Color Customizer]";

        private readonly Action<string> _consoleSink;
        private readonly Action<string> _fileSink;
        private readonly HashSet<(LogLevel level, LogVerb verb, string message)> _consoleSeenThisApplyPass
            = new HashSet<(LogLevel, LogVerb, string)>();
        private readonly object _gate = new object();

        // Rotation must run once per process PER LOG DIRECTORY: Mod.cs constructs the logger
        // in more than one code path, and a second rotation would shove the fresh launch log
        // into .prev. Tests use unique temp dirs, so each still observes a real rotation.
        private static readonly object _rotateLock = new object();
        private static readonly HashSet<string> _rotatedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        /// <summary>Production ctor: logs into &lt;assembly dir&gt;/logs (the same location and
        /// live_log.txt name the fft-dev helpers already tail), rotating the previous run to
        /// live_log.prev.txt.</summary>
        public FileConsoleLogger() : this(DiscoverLogsDir()) { }

        /// <summary>Logs into the given directory (rotating live_log.txt to live_log.prev.txt
        /// once per process for that directory), console to stdout.</summary>
        public FileConsoleLogger(string logsDir) : this(SafeConsoleWrite, MakeFileSink(logsDir)) { }

        /// <summary>Test seam: inject sinks so tests never touch the real console or
        /// filesystem.</summary>
        public FileConsoleLogger(Action<string> consoleSink, Action<string> fileSink)
        {
            _consoleSink = consoleSink;
            _fileSink = fileSink;
        }

        private static string DiscoverLogsDir()
        {
            try
            {
                var asmDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                return string.IsNullOrEmpty(asmDir) ? null : Path.Combine(asmDir, "logs");
            }
            catch { return null; }
        }

        private static void SafeConsoleWrite(string line)
        {
            try { Console.WriteLine(line); } catch { }
        }

        /// <summary>Rotate any prior launch's live_log.txt to live_log.prev.txt, then return a
        /// closure appending one line per call. Failures (locked file, read-only folder, null
        /// dir) degrade to "no file sink" rather than throwing: console logging must survive a
        /// broken deploy folder.</summary>
        private static Action<string> MakeFileSink(string logsDir)
        {
            if (string.IsNullOrEmpty(logsDir)) return _ => { };
            string file = Path.Combine(logsDir, "live_log.txt");
            try
            {
                Directory.CreateDirectory(logsDir);
                lock (_rotateLock)
                {
                    if (_rotatedDirs.Add(logsDir) && File.Exists(file))
                        File.Move(file, Path.Combine(logsDir, "live_log.prev.txt"), true);
                }
            }
            catch { }
            return line => { try { File.AppendAllText(file, line + Environment.NewLine); } catch { } };
        }

        // --- Typed entry points (the migration target) ---

        public void Log(LogVerb verb, string message) => Write(LogLevel.Info, verb, message);
        public void LogWarning(LogVerb verb, string message) => Write(LogLevel.Warning, verb, message);
        public void LogError(LogVerb verb, string message) => Write(LogLevel.Error, verb, message);

        public void LogError(LogVerb verb, string message, Exception exception)
        {
            Write(LogLevel.Error, verb, message);
            if (exception != null)
                Write(LogLevel.Error, verb, $"  {exception.GetType().Name}: {exception.Message}");
        }

        public void LogDebug(LogVerb verb, string message) => Write(LogLevel.Debug, verb, message);

        /// <summary>Resets the console dedup seen-set. Called at the edges of an apply pass
        /// (the episodic unit of this mod, where Living Weapons used battle edges), so a
        /// recurring warning prints once per pass instead of once per launch or per file.</summary>
        public void NoteApplyPassEdge()
        {
            lock (_gate) { _consoleSeenThisApplyPass.Clear(); }
        }

        // --- Legacy untyped ILogger entry points (alive until the sweeps finish) ---

        public void Log(string message) => WriteLegacy(LogLevel.Info, message);
        public void LogError(string message) => WriteLegacy(LogLevel.Error, message);

        public void LogError(string message, Exception exception)
        {
            WriteLegacy(LogLevel.Error, message);
            if (exception != null)
                WriteLegacy(LogLevel.Error, $"  {exception.GetType().Name}: {exception.Message}");
        }

        public void LogWarning(string message) => WriteLegacy(LogLevel.Warning, message);
        public void LogDebug(string message) => WriteLegacy(LogLevel.Debug, message);

        // --- The two-sink core ---

        private void Write(LogLevel level, LogVerb verb, string message)
        {
            lock (_gate)
            {
                string body = message ?? string.Empty;
                string stamp = Stamp(level);
                string verbBracket = $"[{verb.Token()}] ";

                try { _fileSink($"{stamp}{verbBracket}{body}"); } catch { }

                if (level >= LogLevel && _consoleSeenThisApplyPass.Add((level, verb, body)))
                {
                    bool showVerbOnConsole = level != LogLevel.Info;
                    try { _consoleSink($"{stamp}{(showVerbOnConsole ? verbBracket : "")}{body}"); } catch { }
                }
            }
        }

        /// <summary>Legacy lines: both sinks, no verb bracket, no dedup (a legacy call has no
        /// semantic identity to key on). File unconditional, console curated by level.</summary>
        private void WriteLegacy(LogLevel level, string message)
        {
            lock (_gate)
            {
                string line = $"{Stamp(level)}{message ?? string.Empty}";
                try { _fileSink(line); } catch { }
                if (level >= LogLevel)
                {
                    try { _consoleSink(line); } catch { }
                }
            }
        }

        private static string Stamp(LogLevel level)
            => $"{Prefix} [{DateTime.Now:HH:mm:ss.fff}] [{LevelToken(level)}] ";

        private static string LevelToken(LogLevel level) => level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            _ => level.ToString().ToUpperInvariant(),
        };
    }
}
