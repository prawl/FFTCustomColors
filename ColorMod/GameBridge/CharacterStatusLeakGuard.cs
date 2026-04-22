namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure helper: filter spurious CharacterStatus / CombatSets detections
    /// that fire during battle_wait animations. TODO §0 session 31 captured
    /// session-tail rows with sourceScreen=CharacterStatus → targetScreen=
    /// BattleMyTurn at 15-23s latencies — detection flickered while unit-
    /// slot/ui bytes transiently matched the CharacterStatus fingerprint
    /// mid-animation.
    ///
    /// The rule: if the PREVIOUS settled screen was in a battle state and
    /// the caller hasn't pressed Enter (SelectUnit) since, a sudden
    /// CharacterStatus detection is a leak — hold the previous battle name.
    ///
    /// Real drill-in from BattleMyTurn → CharacterStatus happens ONLY via
    /// Status in the action menu, which is an explicit key press. Callers
    /// with keysSinceLastBattleScreen &gt; 0 therefore indicate a legitimate
    /// transition.
    /// </summary>
    public static class CharacterStatusLeakGuard
    {
        public static string Filter(
            string? previousDetected,
            string currentDetected,
            int keysSincePreviousDetected)
        {
            if (currentDetected != "CharacterStatus" && currentDetected != "CombatSets")
                return currentDetected;

            if (keysSincePreviousDetected > 0)
                return currentDetected; // real key event could have triggered drill-in

            if (ScreenNamePredicates.IsBattleState(previousDetected))
                return previousDetected!; // leak — keep the battle state

            return currentDetected;
        }
    }
}
