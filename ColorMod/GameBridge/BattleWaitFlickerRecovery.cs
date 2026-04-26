namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// `battle_wait` nav action sees the screen at start of dispatch.
    /// Terminal-state flickers (BattleVictory / GameOver / BattleDesertion)
    /// surface for ~1-3s before resolving — sometimes back to BattleMyTurn
    /// (false positive) and sometimes genuinely terminal. Without a
    /// settle-and-recheck, the wait dies instantly and the caller has to
    /// run a manual `screen` → `battle_wait` recovery dance, costing
    /// 8-15s per occurrence (live-flagged 2026-04-25 playtest, hit 3x in
    /// a row across ranged attacks).
    /// </summary>
    public static class BattleWaitFlickerRecovery
    {
        /// <summary>
        /// True when the screen state is a terminal-flicker that warrants
        /// a settle + recheck before failing the wait. Other states (real
        /// terminal cutscenes, submenus, world map) fail fast.
        /// </summary>
        public static bool IsRecoverableFlicker(string? screenName)
        {
            if (string.IsNullOrEmpty(screenName)) return false;
            return screenName == "BattleVictory"
                || screenName == "GameOver"
                || screenName == "BattleDesertion";
        }
    }
}
