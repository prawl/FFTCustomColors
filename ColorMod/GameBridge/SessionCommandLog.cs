using System;
using System.IO;
using System.Text.Json;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Append-only JSONL log of every bridge command processed this session.
    /// One file per mod startup (rotated by timestamp so grep'ing a single
    /// session's trail is easy). Captures id, timestamp, action, source and
    /// target screen, status, error, and round-trip latency.
    ///
    /// Purpose: observability for post-hoc review. "Where did Claude get
    /// stuck?" — pinpoint the exact command sequence where a helper failed
    /// or a compound nav drifted. Cheap to write (one short line per
    /// command), high payoff when debugging.
    ///
    /// Thread-safety: writes are not synchronized. CommandWatcher
    /// processes commands serially on one thread, so contention isn't
    /// possible under normal use. Don't call from a background worker.
    ///
    /// Failure-tolerance: Append never throws. Observability code must
    /// not take down command processing. Disk errors are logged and
    /// swallowed.
    /// </summary>
    public class SessionCommandLog
    {
        public string LogPath { get; }

        public SessionCommandLog(string bridgeDirectory)
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            LogPath = Path.Combine(bridgeDirectory, $"session_{stamp}.jsonl");
        }

        public void Append(
            string commandId,
            string action,
            string? sourceScreen,
            string? targetScreen,
            string status,
            string? error,
            long latencyMs)
        {
            try
            {
                var record = new
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    commandId,
                    action,
                    sourceScreen,
                    targetScreen,
                    status,
                    error,
                    latencyMs,
                };
                var line = JsonSerializer.Serialize(record) + "\n";
                File.AppendAllText(LogPath, line);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[SessionCommandLog] Append failed: {ex.Message}");
            }
        }
    }
}
