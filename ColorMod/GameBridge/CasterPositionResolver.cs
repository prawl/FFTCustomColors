namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Resolve the caster's actual battle-grid position. Prefers the
    /// active unit's tile from the last scan_move (canonical, identity-
    /// matched) over the live cursor position (which may sit on another
    /// unit after C+Up cycling, hover, or a stale cache). Cursor is the
    /// fallback for first-turn / no-scan situations.
    /// Live-flagged 2026-04-25 playtest: `battle_ability "Shout"` auto-
    /// filled (10,10) (Wilham's tile, where the cursor was) instead of
    /// Ramza's (7,9), and the success summary printed Wilham's HP for
    /// Ramza's self-cast.
    /// </summary>
    public static class CasterPositionResolver
    {
        public static (int x, int y) Resolve(
            int? scannedActiveX, int? scannedActiveY,
            int cursorX, int cursorY)
        {
            if (scannedActiveX.HasValue && scannedActiveY.HasValue)
                return (scannedActiveX.Value, scannedActiveY.Value);
            return (cursorX, cursorY);
        }
    }
}
