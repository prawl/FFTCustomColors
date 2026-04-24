using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// S60 enemy-turn narrator: append-only live event log at
    /// `claude_bridge/live_events.log`. `BattleWait` truncates on entry then
    /// appends each batch of narration lines during the poll loop, so the
    /// shell helper can `tail -F` the file and surface events live as they
    /// happen (instead of dumping them all at poll exit).
    ///
    /// Every appended line has the `[NARRATE] ` prefix so shell consumers
    /// can filter confidently.
    /// </summary>
    public static class NarrationEventLog
    {
        private static string? _path;
        private static readonly object _lock = new();

        public const string LinePrefix = "[NARRATE] ";

        private static string? ResolvePath()
        {
            if (_path != null) return _path;
            try
            {
                var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(asmDir)) return null;
                var bridgeDir = Path.Combine(asmDir, "claude_bridge");
                if (!Directory.Exists(bridgeDir)) Directory.CreateDirectory(bridgeDir);
                _path = Path.Combine(bridgeDir, "live_events.log");
                return _path;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reset the event log to empty. Call at the start of `BattleWait` so
        /// the shell's `tail -F` only sees events from this wait window.
        /// </summary>
        public static void Truncate()
        {
            var p = ResolvePath();
            if (p == null) return;
            try
            {
                lock (_lock) { File.WriteAllText(p, string.Empty); }
            }
            catch
            {
                // File I/O must never break the bridge — swallow.
            }
        }

        /// <summary>
        /// Append a batch of narration lines, each prefixed with
        /// `[NARRATE] ` so shell filtering is deterministic.
        /// </summary>
        public static void AppendLines(IEnumerable<string> lines)
        {
            var p = ResolvePath();
            if (p == null) return;
            try
            {
                var sb = new StringBuilder();
                foreach (var line in lines)
                {
                    sb.Append(LinePrefix);
                    sb.Append(line);
                    sb.Append('\n');
                }
                if (sb.Length == 0) return;
                lock (_lock) { File.AppendAllText(p, sb.ToString()); }
            }
            catch
            {
                // swallow
            }
        }
    }
}
