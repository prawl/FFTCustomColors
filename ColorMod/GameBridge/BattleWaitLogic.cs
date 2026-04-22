namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure logic for BattleWait state detection.
    /// After Move+Act, the game skips the action menu and goes directly to the
    /// facing/direction screen (detected as BattleAttacking or BattleMoving).
    /// BattleWait needs to handle both paths: normal (menu → Wait → facing) and
    /// auto-facing (already on facing screen, just confirm direction).
    /// </summary>
    public static class BattleWaitLogic
    {
        /// <summary>
        /// Returns true if the current screen indicates we're already on the facing
        /// screen (after Move+Act), so BattleWait should skip menu navigation.
        /// </summary>
        public static bool ShouldSkipMenuNavigation(string? screenName)
        {
            return screenName == "BattleAttacking" || screenName == "BattleMoving";
        }

        /// <summary>
        /// Returns true if battle_wait should prompt for confirmation because the
        /// unit hasn't moved or acted this turn. Skipping a turn with no action is
        /// almost always a mistake. The confirmed parameter allows a second call to
        /// bypass the check.
        /// </summary>
        public static bool NeedsConfirmation(bool acted, bool moved, bool confirmed)
        {
            if (confirmed) return false;
            return !acted && !moved;
        }

        /// <summary>
        /// Returns true if BattleWait can start from this screen state.
        /// Accepts normal battle screens AND the auto-facing screens.
        /// </summary>
        public static bool CanStartBattleWait(string? screenName)
        {
            if (string.IsNullOrEmpty(screenName)) return false;
            return screenName == "BattleMyTurn"
                || screenName == "BattleActing"
                || screenName == "BattleAttacking"
                || screenName == "BattleMoving";
        }

        /// <summary>
        /// Decide whether to fire a RETRY nav after the post-nav cursor verify read.
        ///
        /// Context: BattleWait reads the action-menu cursor byte at 0x1407FC620, applies
        /// EffectiveMenuCursor to correct for known stale-reads (after move, byte reads 0
        /// even though the game has advanced to Abilities), fires NavigateMenuCursor to
        /// the target, then re-reads the byte to verify arrival. If the byte hasn't moved
        /// to the target, the old code blindly retried — but if the byte is the KNOWN-stale
        /// value it was before, retrying fires more keys and overshoots (observed S56:
        /// 1-Down nav → byte still 0 → retry fires 2 more Downs → Auto-battle instead of Wait).
        ///
        /// Rule: retry only when the verify read is trustworthy. It's trustworthy iff
        /// either (a) no correction was applied (raw == corrected) and the verify still
        /// didn't reach target — safe retry on a genuinely missed nav, OR (b) a correction
        /// was applied AND the verified byte has MOVED from the initial raw (proving the
        /// byte is tracking reality again). If the verified byte equals the initial raw
        /// AND a correction was applied, the byte is still stale and we should trust the
        /// initial nav. If the verify read failed (-1), don't retry — noise would amplify.
        /// </summary>
        public static bool ShouldRetryVerifyAfterNav(
            int initialRaw, int correctedCursor, int verifiedRaw, int target)
        {
            if (verifiedRaw < 0) return false;          // read failed — trust the nav
            if (verifiedRaw == target) return false;    // arrived — nothing to do
            bool correctionApplied = initialRaw != correctedCursor;
            bool byteMoved = verifiedRaw != initialRaw;
            if (correctionApplied && !byteMoved) return false; // stale byte, same value — trust nav
            return true;                                 // legit miss — retry
        }
    }
}
