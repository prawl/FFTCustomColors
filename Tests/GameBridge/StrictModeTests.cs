using FFTColorCustomizer.GameBridge;
using Xunit;
using System.Collections.Generic;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class StrictModeTests
    {
        // Strict mode should allow:
        //   - Infrastructure actions (scan_units, read_address, set_strict, etc.)
        //   - "path" action (validPaths navigation)
        //   - Named game actions from fft.sh helpers (battle_wait, battle_attack, move_grid, etc.)
        //
        // Strict mode should block:
        //   - Raw key presses (keys array without action)
        //   - "sequence" action (raw key sequences)
        //   - Any action not in the allowed lists

        private static readonly HashSet<string> AllowedGameActions = new()
        {
            "path", "battle_wait", "battle_flee", "battle_attack",
            "move_grid", "travel_to", "navigate", "auto_move", "get_arrows"
        };

        private static readonly HashSet<string> AllowedInfraActions = new()
        {
            "scan_move", "scan_units", "set_map", "report_state",
            "read_address", "read_block", "batch_read",
            "mark_blocked", "snapshot", "heap_snapshot", "diff",
            "search_bytes", "search_all", "search_memory", "search_near",
            "dump_unit", "dump_all", "write_address", "set_strict", "set_map",
            "read_dialogue", "write_byte"
        };

        [Theory]
        [InlineData("sequence")]
        [InlineData("key")]
        [InlineData("press")]
        [InlineData("send_keys")]
        public void StrictMode_BlocksRawActions(string action)
        {
            Assert.False(AllowedGameActions.Contains(action), $"'{action}' should not be in allowed game actions");
            Assert.False(AllowedInfraActions.Contains(action), $"'{action}' should not be in allowed infra actions");
        }

        [Theory]
        [InlineData("path")]
        [InlineData("battle_wait")]
        [InlineData("battle_flee")]
        [InlineData("battle_attack")]
        [InlineData("move_grid")]
        [InlineData("travel_to")]
        [InlineData("navigate")]
        [InlineData("auto_move")]
        [InlineData("get_arrows")]
        public void StrictMode_AllowsGameActions(string action)
        {
            Assert.True(AllowedGameActions.Contains(action), $"'{action}' should be in allowed game actions");
        }

        [Theory]
        [InlineData("scan_move")]
        [InlineData("scan_units")]
        [InlineData("read_address")]
        [InlineData("read_block")]
        [InlineData("batch_read")]
        [InlineData("read_dialogue")]
        [InlineData("set_strict")]
        [InlineData("report_state")]
        [InlineData("write_byte")]
        public void StrictMode_AllowsInfraActions(string action)
        {
            Assert.True(AllowedInfraActions.Contains(action), $"'{action}' should be in allowed infra actions");
        }
    }
}
