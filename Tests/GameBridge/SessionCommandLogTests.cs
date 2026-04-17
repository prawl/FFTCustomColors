using System.IO;
using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class SessionCommandLogTests
    {
        private static string MakeTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"SessionCmdLogTest_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void Append_WritesOneJsonLinePerCommand()
        {
            var dir = MakeTempDir();
            var log = new SessionCommandLog(dir);

            log.Append(commandId: "c1", action: "execute_action",
                sourceScreen: "WorldMap", targetScreen: "TravelList",
                status: "completed", error: null, latencyMs: 320);
            log.Append(commandId: "c2", action: "execute_action",
                sourceScreen: "TravelList", targetScreen: "WorldMap",
                status: "completed", error: null, latencyMs: 280);

            var lines = File.ReadAllLines(log.LogPath);
            Assert.Equal(2, lines.Length);
            Assert.Contains("\"commandId\":\"c1\"", lines[0]);
            Assert.Contains("\"action\":\"execute_action\"", lines[0]);
            Assert.Contains("\"sourceScreen\":\"WorldMap\"", lines[0]);
            Assert.Contains("\"targetScreen\":\"TravelList\"", lines[0]);
            Assert.Contains("\"status\":\"completed\"", lines[0]);
            Assert.Contains("\"latencyMs\":320", lines[0]);
            Assert.Contains("\"commandId\":\"c2\"", lines[1]);
        }

        [Fact]
        public void Append_IncludesIsoTimestamp()
        {
            var dir = MakeTempDir();
            var log = new SessionCommandLog(dir);

            log.Append("c1", "battle_attack", "BattleMyTurn", "BattleActing",
                "completed", null, 500);

            var line = File.ReadAllLines(log.LogPath).Single();
            // ISO-8601 UTC stamp: 2026-04-17T...Z
            Assert.Matches("\"timestamp\":\"\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}", line);
        }

        [Fact]
        public void Append_EscapesJsonSpecialCharsInError()
        {
            var dir = MakeTempDir();
            var log = new SessionCommandLog(dir);

            log.Append("c1", "execute_action", "WorldMap", "WorldMap",
                "failed", "No path 'Cancel' on \"screen\" with \\ backslashes", 100);

            var line = File.ReadAllLines(log.LogPath).Single();
            // JSON should parse without throwing.
            var doc = System.Text.Json.JsonDocument.Parse(line);
            Assert.Equal("No path 'Cancel' on \"screen\" with \\ backslashes",
                doc.RootElement.GetProperty("error").GetString());
        }

        [Fact]
        public void Append_NullErrorSerializesAsJsonNull()
        {
            var dir = MakeTempDir();
            var log = new SessionCommandLog(dir);
            log.Append("c1", "screen", "WorldMap", "WorldMap", "completed", null, 50);
            var line = File.ReadAllLines(log.LogPath).Single();
            // null → JSON null, not the string "null"
            Assert.Contains("\"error\":null", line);
        }

        [Fact]
        public void LogPath_IsUnderBridgeDirectory()
        {
            var dir = MakeTempDir();
            var log = new SessionCommandLog(dir);
            Assert.StartsWith(dir, log.LogPath);
            Assert.EndsWith(".jsonl", log.LogPath);
        }

        [Fact]
        public void Append_SurvivesCorruptDirectory()
        {
            // If the bridge dir ever goes missing mid-session (user deleted,
            // network drive hiccup), Append must not throw — it's observability,
            // shouldn't take down command processing.
            var dir = MakeTempDir();
            var log = new SessionCommandLog(dir);
            Directory.Delete(dir, recursive: true);
            // Should not throw.
            log.Append("c1", "screen", "WorldMap", null, "completed", null, 0);
        }

        [Fact]
        public void NewInstance_StartsNewFile_DoesNotAppendToOld()
        {
            // Each mod startup = new session. Old sessions' logs should
            // remain on disk (post-hoc review) but a new instance should
            // write to a NEW file, not continue the old one. Otherwise
            // grep'ing "today's session" is painful.
            var dir = MakeTempDir();
            var a = new SessionCommandLog(dir);
            a.Append("c1", "screen", "WorldMap", "WorldMap", "completed", null, 10);
            var aPath = a.LogPath;

            // Simulate time passing / mod reload.
            System.Threading.Thread.Sleep(10);

            var b = new SessionCommandLog(dir);
            b.Append("c2", "screen", "WorldMap", "WorldMap", "completed", null, 10);
            var bPath = b.LogPath;

            Assert.NotEqual(aPath, bPath);
            Assert.Single(File.ReadAllLines(aPath));
            Assert.Single(File.ReadAllLines(bPath));
        }
    }
}
