namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Composes a unit's effective (Move, Jump) from (live heap read,
    /// JobBaseStatsTable base). Live values win whenever they're positive;
    /// missing axes fill in from the table. Unknown jobs with both live
    /// values missing collapse to 0 — the honest "BFS returns no tiles"
    /// signal for the caller.
    ///
    /// Used at both the active-unit BFS input path (scan_move) and the
    /// BattleUnitState surfacing path so the fallback shape stays identical.
    /// </summary>
    public static class MoveJumpFallbackResolver
    {
        public static (int move, int jump) Resolve(int liveMove, int liveJump, string? jobName)
        {
            var table = JobBaseStatsTable.TryGet(jobName);
            int m = liveMove > 0 ? liveMove : (table?.move ?? 0);
            int j = liveJump > 0 ? liveJump : (table?.jump ?? 0);
            return (m, j);
        }
    }
}
