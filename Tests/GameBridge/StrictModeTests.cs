using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;
using System.Collections.Generic;
using System.Reflection;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class StrictModeTests
    {
        // Strict mode should allow:
        //   - Infrastructure actions (scan_units, read_address, set_strict, etc.)
        //   - "execute_action" (validPaths navigation, renamed from "path")
        //   - Named game actions from fft.sh helpers (battle_wait, battle_move, etc.)
        //
        // Strict mode should block:
        //   - Raw key presses (keys array without action)
        //   - "sequence" action (raw key sequences)
        //   - Old/renamed actions (path, move_grid, travel_to, navigate)
        //   - Any action not in the allowed lists

        private static readonly HashSet<string> AllowedGameActions = new()
        {
            "execute_action", "battle_wait", "battle_flee", "battle_attack", "battle_ability",
            "battle_move", "world_travel_to", "auto_move", "get_arrows",
            "advance_dialogue", "save", "load",
            "battle_retry", "battle_retry_formation",
            "buy", "sell", "change_job"
        };

        private static readonly HashSet<string> AllowedInfraActions = new()
        {
            "scan_move", "scan_units", "set_map", "report_state",
            "read_address", "read_block", "batch_read",
            "mark_blocked", "snapshot", "heap_snapshot", "diff",
            "search_bytes", "search_all", "search_memory", "search_near",
            "dump_unit", "dump_all", "write_address", "set_strict", "set_map",
            "read_dialogue", "write_byte", "dump_detection_inputs",
            "scrape_shop_items",
            "hold_key",
            "resolve_picker_cursor",
            "resolve_job_cursor",
            "resolve_party_menu_cursor"
        };

        [Theory]
        [InlineData("sequence")]
        [InlineData("key")]
        [InlineData("press")]
        [InlineData("send_keys")]
        [InlineData("path")]
        [InlineData("move_grid")]
        [InlineData("travel_to")]
        [InlineData("navigate")]
        public void StrictMode_BlocksRawAndOldActions(string action)
        {
            Assert.False(AllowedGameActions.Contains(action), $"'{action}' should not be in allowed game actions");
            Assert.False(AllowedInfraActions.Contains(action), $"'{action}' should not be in allowed infra actions");
        }

        [Theory]
        [InlineData("execute_action")]
        [InlineData("battle_wait")]
        [InlineData("battle_flee")]
        [InlineData("battle_attack")]
        [InlineData("battle_move")]
        [InlineData("world_travel_to")]
        [InlineData("auto_move")]
        [InlineData("get_arrows")]
        [InlineData("advance_dialogue")]
        [InlineData("save")]
        [InlineData("load")]
        [InlineData("battle_retry")]
        [InlineData("battle_retry_formation")]
        [InlineData("buy")]
        [InlineData("sell")]
        [InlineData("change_job")]
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

        // Verify the C# CommandWatcher sets match our test contract
        private static HashSet<string> GetPrivateStaticField(string fieldName)
        {
            var field = typeof(CommandWatcher).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            return (HashSet<string>)field!.GetValue(null)!;
        }

        [Fact]
        public void CSharp_AllowedGameActions_MatchesTestContract()
        {
            var actual = GetPrivateStaticField("AllowedGameActions");
            Assert.Equal(AllowedGameActions.Count, actual.Count);
            foreach (var action in AllowedGameActions)
                Assert.Contains(action, actual);
        }

        [Fact]
        public void CSharp_InfrastructureActions_MatchesTestContract()
        {
            var actual = GetPrivateStaticField("InfrastructureActions");
            Assert.Equal(AllowedInfraActions.Count, actual.Count);
            foreach (var action in AllowedInfraActions)
                Assert.Contains(action, actual);
        }
    }
}
