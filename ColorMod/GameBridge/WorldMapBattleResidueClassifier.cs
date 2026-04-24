namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Rejects a "WorldMap" screen-detection result when the caller was
    /// observably inside a battle within a short recent window. Live
    /// scenario: during certain enemy-turn animations battleMode transiently
    /// flickers to 0 while slot9 stays at the battle sentinel; the
    /// post-battle-stale rule in ScreenDetectionLogic then converts the
    /// frame to WorldMap, misleading callers that poll mid-battle.
    ///
    /// The caller (CommandWatcher) tracks the tick of the most recent
    /// detection that returned a Battle* state. We suppress only when that
    /// tick is recent — real post-battle transitions have no such tick for
    /// several seconds.
    /// </summary>
    public static class WorldMapBattleResidueClassifier
    {
        public const int DefaultSuppressWindowMs = 3000;

        public static bool ShouldSuppress(
            string? detectedName,
            long msSinceLastBattleState,
            int suppressWindowMs = DefaultSuppressWindowMs)
        {
            if (detectedName != "WorldMap") return false;
            if (msSinceLastBattleState < 0) return false;
            if (msSinceLastBattleState >= suppressWindowMs) return false;
            return true;
        }
    }
}
