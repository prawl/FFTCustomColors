namespace FFTColorCustomizer.GameBridge
{
    public enum BattleLifecycleEvent
    {
        None,
        StartBattle,
        EndBattleVictory,
        EndBattleDefeat,
    }

    // Pure classifier. Watches screen-name transitions and emits
    // start/end events for the BattleStatTracker. The tracker itself
    // is screen-agnostic; this keeps its hooks decoupled from
    // ScreenDetectionLogic's exact state vocabulary.
    //
    // Rules:
    //  - "Start battle" fires on any non-battle (or first-observed)
    //    → BattleMyTurn transition. Mid-battle screens transitioning
    //    back to BattleMyTurn do NOT restart.
    //  - "End battle" fires on the FIRST transition into Victory /
    //    Desertion / GameOver. Repeated polls in that state no-op.
    public static class BattleLifecycleClassifier
    {
        public static BattleLifecycleEvent Classify(string? previous, string? current)
        {
            if (current == "BattleMyTurn" && !IsInsideBattle(previous))
                return BattleLifecycleEvent.StartBattle;

            if (current == "BattleVictory" && previous != "BattleVictory")
                return BattleLifecycleEvent.EndBattleVictory;

            // Don't re-end after Victory. FFT's post-victory sequence can
            // transition through BattleDesertion (unit crystallized) or
            // even a stale GameOver flicker — in both cases the battle
            // already finalized as a win.
            if (previous == "BattleVictory")
                return BattleLifecycleEvent.None;

            if ((current == "BattleDesertion" && previous != "BattleDesertion")
                || (current == "GameOver" && previous != "GameOver"))
                return BattleLifecycleEvent.EndBattleDefeat;

            return BattleLifecycleEvent.None;
        }

        private static bool IsInsideBattle(string? screen)
        {
            return screen is "BattleMyTurn"
                or "BattleMoving"
                or "BattleAttacking"
                or "BattleCasting"
                or "BattleAbilities"
                or "BattleActing"
                or "BattleWaiting"
                or "BattleEnemiesTurn"
                or "BattleAlliesTurn"
                or "BattlePaused"
                or "BattleDialogue"
                or "BattleChoice"
                or "BattleStatus"
                or "BattleAutoBattle";
        }
    }
}
