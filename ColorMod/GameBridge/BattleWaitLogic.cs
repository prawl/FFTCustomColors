namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure logic for BattleWait state detection.
    /// After Move+Act, the game skips the action menu and goes directly to the
    /// facing/direction screen (detected as Battle_Attacking or Battle_Moving).
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
            return screenName == "Battle_Attacking" || screenName == "Battle_Moving";
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
            return screenName == "Battle_MyTurn"
                || screenName == "Battle_Acting"
                || screenName == "Battle_Attacking"
                || screenName == "Battle_Moving";
        }
    }
}
