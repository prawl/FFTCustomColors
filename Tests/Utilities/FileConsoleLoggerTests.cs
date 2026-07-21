using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.Utilities
{
    /// <summary>
    /// The typed two-sink logger (CC-2 stage 1). The contract under test, from the proposal:
    /// the FILE sink gets every line unconditionally (Debug included) with a timestamp and the
    /// verb bracket; the CONSOLE sink is curated by level, drops the verb on Info lines only,
    /// and dedups a repeated (level, verb, message) identity until the next apply-pass edge.
    /// Legacy untyped entry points keep working (timestamp + level, no verb) so the 600-site
    /// migration can proceed sweep by sweep. All captures lock their writes and are asserted
    /// on snapshots (test classes run in parallel).
    /// </summary>
    public class FileConsoleLoggerTests
    {
        private sealed class Capture
        {
            private readonly object _lock = new object();
            private readonly List<string> _lines = new List<string>();
            public void Add(string line) { lock (_lock) { _lines.Add(line); } }
            public List<string> Snapshot() { lock (_lock) { return _lines.ToList(); } }
        }

        private static (FileConsoleLogger logger, Capture console, Capture file) Make(
            LogLevel level = LogLevel.Info)
        {
            var console = new Capture();
            var file = new Capture();
            var logger = new FileConsoleLogger(console.Add, file.Add) { LogLevel = level };
            return (logger, console, file);
        }

        private static readonly Regex LineShape = new Regex(
            @"^\[Color Customizer\] \[\d{2}:\d{2}:\d{2}\.\d{3}\] \[(DEBUG|INFO|WARN|ERROR)\] ");

        // --- The two-sink rule ---

        [Fact]
        public void File_gets_every_line_including_debug_while_console_is_curated_by_level()
        {
            var (logger, console, file) = Make(LogLevel.Info);

            logger.Log(LogVerb.Theme, "The theme was applied.");
            logger.LogDebug(LogVerb.Trace, "theme detail (2 files, 61440 bytes each)");

            Assert.Equal(2, file.Snapshot().Count);
            var consoleLines = console.Snapshot();
            Assert.Single(consoleLines);
            Assert.Contains("The theme was applied.", consoleLines[0]);
        }

        [Fact]
        public void Console_shows_nothing_below_the_configured_level_but_the_file_still_records_it()
        {
            var (logger, console, file) = Make(LogLevel.Error);

            logger.Log(LogVerb.Config, "Configuration saved.");
            logger.LogWarning(LogVerb.Sprite, "A sprite source is missing.");

            Assert.Empty(console.Snapshot());
            Assert.Equal(2, file.Snapshot().Count);
        }

        // --- Line shape: timestamps, level tokens, the verb rendering split ---

        [Fact]
        public void Both_sinks_carry_the_prefix_a_millisecond_timestamp_and_a_level_token()
        {
            var (logger, console, file) = Make(LogLevel.Debug);

            logger.Log(LogVerb.Startup, "Color Customizer is starting.");

            Assert.All(file.Snapshot(), l => Assert.Matches(LineShape, l));
            Assert.All(console.Snapshot(), l => Assert.Matches(LineShape, l));
        }

        [Fact]
        public void File_lines_always_carry_the_verb_bracket_and_info_console_lines_drop_it()
        {
            var (logger, console, file) = Make(LogLevel.Info);

            logger.Log(LogVerb.Theme, "The theme was applied.");

            Assert.Contains("[theme]", file.Snapshot().Single());
            Assert.DoesNotContain("[theme]", console.Snapshot().Single());
        }

        [Fact]
        public void Warning_and_error_console_lines_keep_the_verb_bracket()
        {
            var (logger, console, _) = Make(LogLevel.Info);

            logger.LogWarning(LogVerb.Ramza, "Texture generation fell back.");
            logger.LogError(LogVerb.Ui, "The editor failed to open.");

            var lines = console.Snapshot();
            Assert.Contains("[ramza]", lines[0]);
            Assert.Contains("[WARN]", lines[0]);
            Assert.Contains("[ui]", lines[1]);
            Assert.Contains("[ERROR]", lines[1]);
        }

        [Fact]
        public void A_debug_line_on_a_debug_raised_console_keeps_the_verb_bracket()
        {
            var (logger, console, _) = Make(LogLevel.Debug);

            logger.LogDebug(LogVerb.Sprite, "battle_knight_m_spr.bin copied.");

            Assert.Contains("[sprite]", console.Snapshot().Single());
        }

        [Fact]
        public void An_exception_rides_the_error_as_an_indented_companion_line()
        {
            var (logger, _, file) = Make(LogLevel.Info);

            logger.LogError(LogVerb.Config, "Config.json could not be read.",
                new InvalidOperationException("boom"));

            var lines = file.Snapshot();
            Assert.Equal(2, lines.Count);
            Assert.Contains("InvalidOperationException: boom", lines[1]);
        }

        // --- Console dedup per apply pass ---

        [Fact]
        public void A_repeated_identity_prints_once_per_apply_pass_but_the_file_gets_every_repeat()
        {
            var (logger, console, file) = Make(LogLevel.Info);

            logger.LogWarning(LogVerb.Sprite, "The theme file is missing.");
            logger.LogWarning(LogVerb.Sprite, "The theme file is missing.");
            Assert.Single(console.Snapshot());
            Assert.Equal(2, file.Snapshot().Count);

            logger.NoteApplyPassEdge();
            logger.LogWarning(LogVerb.Sprite, "The theme file is missing.");
            Assert.Equal(2, console.Snapshot().Count);
        }

        [Fact]
        public void Dedup_keys_on_the_semantic_identity_so_different_verbs_both_reach_the_console()
        {
            var (logger, console, _) = Make(LogLevel.Debug);

            logger.LogWarning(LogVerb.Sprite, "A file is missing.");
            logger.LogWarning(LogVerb.Monster, "A file is missing.");

            Assert.Equal(2, console.Snapshot().Count);
        }

        // --- Legacy untyped entry points (alive until the sweeps finish) ---

        [Fact]
        public void Legacy_untyped_lines_carry_timestamp_and_level_but_no_verb_bracket()
        {
            var (logger, console, file) = Make(LogLevel.Info);

            logger.Log("An old-style line.");
            logger.LogWarning("An old-style warning.");

            Assert.All(file.Snapshot(), l => Assert.Matches(LineShape, l));
            Assert.Equal(2, console.Snapshot().Count);
            Assert.DoesNotContain("] [startup]", file.Snapshot()[0]);
            Assert.Contains("[WARN]", file.Snapshot()[1]);
        }

        // --- Rotation, not truncation ---

        [Fact]
        public void The_previous_launch_log_survives_as_prev_and_the_new_log_starts_fresh()
        {
            string dir = Path.Combine(Path.GetTempPath(), "cc_logtest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "live_log.txt"), "previous run evidence\n");

                var logger = new FileConsoleLogger(dir);
                logger.Log(LogVerb.Startup, "Fresh launch.");

                string prev = File.ReadAllText(Path.Combine(dir, "live_log.prev.txt"));
                string current = File.ReadAllText(Path.Combine(dir, "live_log.txt"));
                Assert.Contains("previous run evidence", prev);
                Assert.Contains("Fresh launch.", current);
                Assert.DoesNotContain("previous run evidence", current);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // --- The typed facade path ---

        [Fact]
        public void ModLogger_routes_typed_calls_through_a_typed_instance()
        {
            var console = new Capture();
            var file = new Capture();
            var previous = ModLogger.Instance;
            try
            {
                ModLogger.Instance = new FileConsoleLogger(console.Add, file.Add)
                { LogLevel = LogLevel.Info };
                ModLogger.Log(LogVerb.Theme, "Routed through the facade.");

                Assert.Contains(file.Snapshot(), l => l.Contains("[theme]") && l.Contains("Routed through the facade."));
            }
            finally
            {
                ModLogger.Instance = previous;
            }
        }

        [Fact]
        public void ModLogger_renders_the_verb_into_the_message_for_a_legacy_logger_instance()
        {
            var capture = new Capture();
            var previous = ModLogger.Instance;
            try
            {
                ModLogger.Instance = new CaptureLegacyLogger(capture);
                ModLogger.Log(LogVerb.Ramza, "Fell back to the legacy path.");

                Assert.Contains(capture.Snapshot(), l => l.Contains("[ramza] Fell back to the legacy path."));
            }
            finally
            {
                ModLogger.Instance = previous;
            }
        }

        private sealed class CaptureLegacyLogger : ILogger
        {
            private readonly Capture _capture;
            public CaptureLegacyLogger(Capture capture) { _capture = capture; }
            public LogLevel LogLevel { get; set; } = LogLevel.Debug;
            public void Log(string message) => _capture.Add(message);
            public void LogError(string message) => _capture.Add(message);
            public void LogError(string message, Exception exception) => _capture.Add(message);
            public void LogWarning(string message) => _capture.Add(message);
            public void LogDebug(string message) => _capture.Add(message);
        }
    }
}
