using System;
using System.IO;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the quarantine policy: when command.json fails to parse, the
    /// bridge must move it aside so the watcher loop doesn't spin on it
    /// forever. Without this, a single malformed write (e.g. a shell
    /// helper that wrote a raw command name like "screen" instead of
    /// JSON) jams the bridge for the rest of the session — every poll
    /// re-reads the bad file and re-logs the parse error.
    /// </summary>
    public class CommandFileQuarantineTests
    {
        [Fact]
        public void Quarantine_RenamesFileWithBadSuffixAndTimestamp()
        {
            var dir = Path.Combine(Path.GetTempPath(), "fft-quarantine-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var src = Path.Combine(dir, "command.json");
                File.WriteAllText(src, "screen");

                var quarantined = CommandFileQuarantine.Quarantine(src, new DateTime(2026, 4, 25, 13, 30, 45, DateTimeKind.Utc));

                Assert.False(File.Exists(src), "Original file should be moved away");
                Assert.True(File.Exists(quarantined), "Quarantined file should exist at returned path");
                Assert.Equal("screen", File.ReadAllText(quarantined));
                Assert.StartsWith("command.json.bad-", Path.GetFileName(quarantined));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Quarantine_TimestampInFilename_IsDeterministic()
        {
            var dir = Path.Combine(Path.GetTempPath(), "fft-quarantine-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var src = Path.Combine(dir, "command.json");
                File.WriteAllText(src, "garbage");

                var ts = new DateTime(2026, 4, 25, 13, 30, 45, DateTimeKind.Utc);
                var quarantined = CommandFileQuarantine.Quarantine(src, ts);

                Assert.Equal("command.json.bad-20260425T133045Z", Path.GetFileName(quarantined));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Quarantine_CollidingTimestamp_AppendsCounter()
        {
            // Two parse failures in the same second (rare but possible
            // under tight retry loops) must not throw or overwrite — the
            // second quarantine appends a counter suffix.
            var dir = Path.Combine(Path.GetTempPath(), "fft-quarantine-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var src = Path.Combine(dir, "command.json");
                var ts = new DateTime(2026, 4, 25, 13, 30, 45, DateTimeKind.Utc);

                File.WriteAllText(src, "first");
                var q1 = CommandFileQuarantine.Quarantine(src, ts);

                File.WriteAllText(src, "second");
                var q2 = CommandFileQuarantine.Quarantine(src, ts);

                Assert.NotEqual(q1, q2);
                Assert.Equal("first", File.ReadAllText(q1));
                Assert.Equal("second", File.ReadAllText(q2));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Quarantine_MissingFile_ReturnsNull()
        {
            // If the file vanished between the parse failure and the
            // quarantine call (e.g. the shell helper rm'd it), don't
            // throw — return null so the caller skips logging.
            var dir = Path.Combine(Path.GetTempPath(), "fft-quarantine-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var src = Path.Combine(dir, "command.json");
                var quarantined = CommandFileQuarantine.Quarantine(src, DateTime.UtcNow);
                Assert.Null(quarantined);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
