namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pre-flight validator for `execute_turn` bundles. Rejects plans that
    /// attempt a move after move is consumed, or an ability after act is
    /// consumed — returning the canonical "Move already used" /
    /// "Act already used" message (matches the battle_attack and
    /// battle_ability entry-reset check, commit 8cf9197).
    ///
    /// Without this pre-flight, a bundle entered on a partially-consumed
    /// turn silently tries the first sub-step, misses, and fails with a
    /// misleading sub-step error (e.g. "Not in Move mode (current:
    /// BattleMyTurn)"). Live-repro 2026-04-24 Siedge Weald.
    /// </summary>
    public static class ExecuteTurnPreflightValidator
    {
        public const string MoveAlreadyUsedMessage =
            "Move already used this turn — only Act or Wait remain.";
        public const string ActAlreadyUsedMessage =
            "Act already used this turn — only Move or Wait remain.";

        /// <summary>
        /// Returns the canonical error message if the plan is unreachable
        /// given the current turn-consumed state, or null if the plan
        /// can be dispatched.
        /// </summary>
        public static string? Validate(TurnPlan plan, bool hasMoved, bool hasActed)
        {
            if (plan == null) return null;
            bool wantsMove = plan.MoveX.HasValue && plan.MoveY.HasValue;
            bool wantsAbility = !string.IsNullOrEmpty(plan.AbilityName);

            // Step ordering matches TurnPlan.ToSteps: move first, ability
            // second. Surface the error for whichever step fails first.
            if (wantsMove && hasMoved) return MoveAlreadyUsedMessage;
            if (wantsAbility && hasActed) return ActAlreadyUsedMessage;
            return null;
        }
    }
}
