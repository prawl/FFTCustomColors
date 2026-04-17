namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Compares the BFS-computed valid-tile count against the game's own
    /// tileCount byte (at 0x142FEA008 — discovered session 28). BFS has
    /// known gaps (slope-direction height checks, ally traversal edge cases)
    /// so when the two diverge we want a loud warning, not a silent pass.
    /// </summary>
    public static class MoveTileCountValidator
    {
        /// <summary>
        /// Returns null when counts match OR gameCount is unavailable/ambiguous
        /// (null or 0). Returns a pre-formatted warning string when the counts
        /// disagree — caller logs it.
        /// </summary>
        public static string? Compare(int bfsCount, int? gameCount)
        {
            if (gameCount == null) return null;
            if (gameCount == 0) return null;
            if (bfsCount == gameCount) return null;

            int delta = bfsCount - gameCount.Value;
            string direction = delta > 0 ? "OVERCOUNT" : "UNDERCOUNT";
            return $"[BFS {direction}] BFS reported {bfsCount} valid tiles; game memory reports {gameCount} (delta={delta}). BFS tile list may be wrong.";
        }
    }
}
