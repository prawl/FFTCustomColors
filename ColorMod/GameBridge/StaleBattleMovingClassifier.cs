namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decides whether a "BattleMoving" screen-detection result should be
    /// overridden to "BattleWaiting" because the caller just sent the Enter
    /// that commits the Wait action. Rationale: both screens use battleMode==2
    /// and are distinguished by menuCursor==2; menuCursor has been observed
    /// lagging the game state for hundreds of ms after the Wait commit,
    /// leaving the detection stuck on BattleMoving even though the facing
    /// screen is visibly active.
    ///
    /// The override is gated on a short time window from the most recent
    /// Wait Enter — outside that window, a genuine BattleMoving tile-select
    /// is possible and must not be rewritten.
    /// </summary>
    public static class StaleBattleMovingClassifier
    {
        public const int DefaultOverrideWindowMs = 500;

        public static bool ShouldOverrideToBattleWaiting(
            string? detectedName,
            int battleMode,
            long msSinceLastWaitEnter,
            int overrideWindowMs = DefaultOverrideWindowMs)
        {
            if (detectedName != "BattleMoving") return false;
            if (battleMode != 2) return false;
            if (msSinceLastWaitEnter < 0) return false;
            if (msSinceLastWaitEnter >= overrideWindowMs) return false;
            return true;
        }
    }
}
