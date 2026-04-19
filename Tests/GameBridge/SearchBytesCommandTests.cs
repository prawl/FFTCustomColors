using FFTColorCustomizer.Utilities;
using System.Text.Json;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the JSON shape of the `search_bytes` command with the new
    /// minAddr/maxAddr params (session 47 — TODO §0 session 45 follow-up).
    ///
    /// Without explicit range params, the heap range (0x4000000000+) can't
    /// be reached because the default 100-match cap gets exhausted by
    /// main-module hits. With minAddr/maxAddr, callers pin a narrow range
    /// (e.g. 0x4000000000..0x4200000000 for unit struct scans). Unblocks
    /// tmp/kill_enemies.sh + other heap-targeted searches.
    /// </summary>
    public class SearchBytesCommandTests
    {
        [Fact]
        public void CommandRequest_Deserializes_MinAddr_AsHexString()
        {
            var json = """{"id":"1","action":"search_bytes","pattern":"AABB","minAddr":"0x4000000000","maxAddr":"0x4200000000"}""";
            var cmd = JsonSerializer.Deserialize<CommandRequest>(json);
            Assert.NotNull(cmd);
            Assert.Equal("search_bytes", cmd!.Action);
            Assert.Equal("AABB", cmd.Pattern);
            Assert.Equal("0x4000000000", cmd.MinAddr);
            Assert.Equal("0x4200000000", cmd.MaxAddr);
        }

        [Fact]
        public void CommandRequest_Deserializes_MinAddr_AsDecimalString()
        {
            // Accepts decimal too — no surprise for callers passing raw
            // long values.
            var json = """{"id":"1","action":"search_bytes","pattern":"AA","minAddr":"274877906944","maxAddr":"281470681743360"}""";
            var cmd = JsonSerializer.Deserialize<CommandRequest>(json);
            Assert.Equal("274877906944", cmd!.MinAddr);
            Assert.Equal("281470681743360", cmd.MaxAddr);
        }

        [Fact]
        public void CommandRequest_MissingAddrs_AreNull_AndOmittedSerializing()
        {
            // Back-compat: existing callers omit MinAddr/MaxAddr and
            // expect the full-memory default.
            var cmd = new CommandRequest { Id = "1", Action = "search_bytes", Pattern = "AA" };
            Assert.Null(cmd.MinAddr);
            Assert.Null(cmd.MaxAddr);

            var json = JsonSerializer.Serialize(cmd);
            // Omitted fields mean default behavior — full memory scan.
            Assert.DoesNotContain("\"minAddr\"", json);
            Assert.DoesNotContain("\"maxAddr\"", json);
        }

        [Theory]
        [InlineData("0x4000000000", 0x4000000000L)]
        [InlineData("4000000000", 0x4000000000L)] // hex without 0x prefix
        [InlineData("0X4000000000", 0x4000000000L)] // uppercase 0X
        public void ParseAddr_HexInput_ReturnsExpectedLong(string input, long expected)
        {
            // The handler is expected to parse hex-prefixed strings as hex,
            // no-prefix strings as hex too (since raw addresses are always
            // hex in this codebase — see other command handlers).
            long parsed = CommandRequest.ParseAddrOrDefault(input, 0L);
            Assert.Equal(expected, parsed);
        }

        [Fact]
        public void ParseAddr_NullOrEmpty_ReturnsDefault()
        {
            Assert.Equal(0L, CommandRequest.ParseAddrOrDefault(null, 0L));
            Assert.Equal(99L, CommandRequest.ParseAddrOrDefault("", 99L));
            Assert.Equal(99L, CommandRequest.ParseAddrOrDefault("   ", 99L));
        }

        [Fact]
        public void ParseAddr_InvalidHex_ReturnsDefault()
        {
            Assert.Equal(42L, CommandRequest.ParseAddrOrDefault("not-a-number", 42L));
            Assert.Equal(42L, CommandRequest.ParseAddrOrDefault("0xZZZZ", 42L));
        }
    }
}
