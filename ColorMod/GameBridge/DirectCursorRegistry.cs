using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Registry of memory-byte cursors that we can WRITE to directly,
    /// bypassing key-press simulation. Each entry maps a friendly name
    /// to (address, validRange, screenContext).
    ///
    /// <para>2026-04-26 proto: today the bridge moves cursors by sending
    /// VK_DOWN/UP repeatedly — ~25ms key-hold + 1-2 frames per press +
    /// settle = 100-300ms for a multi-step nav. <see cref="GameMemoryScanner.WriteByte"/>
    /// is a single pointer deref (microseconds). For pure-memory cursors
    /// (game reads the byte and renders accordingly), we can collapse
    /// "Down Down Down + Confirm" into "WriteByte(addr, 3) + Confirm" —
    /// saving 75ms+ per multi-step nav.</para>
    ///
    /// <para>Risks: animation desync (game may animate cursor sprite
    /// from prior position), state machine assuming sequential transitions
    /// (some menus react to KEY events, not cursor-byte changes). Caller
    /// must verify post-write that the byte holds and the screen still
    /// looks right.</para>
    /// </summary>
    public static class DirectCursorRegistry
    {
        public record Entry(
            string Name,
            long Address,
            int MinIndex,
            int MaxIndex,
            string? RequiredScreen,
            string Description);

        private static readonly Dictionary<string, Entry> _entries = new()
        {
            // BattleMyTurn root menu: Move(0) / Abilities(1) / Wait(2) /
            // Status(3) / AutoBattle(4). Address verified — bridge already
            // writes 0 here for cursor resets in 3 places.
            ["battle_menu"] = new Entry(
                Name: "battle_menu",
                Address: 0x1407FC620,
                MinIndex: 0,
                MaxIndex: 4,
                RequiredScreen: "BattleMyTurn",
                Description: "Root action menu (Move/Abilities/Wait/Status/AutoBattle)"),
        };

        public static Entry? Get(string name)
        {
            return _entries.TryGetValue(name, out var e) ? e : null;
        }

        public static IReadOnlyCollection<string> Names => _entries.Keys;
    }
}
