using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decides whether a detected-screen transition represents a "fresh
    /// entry" into BattleMyTurn — i.e., a turn-boundary state transition
    /// where the game itself resets the action-menu cursor to Move (0),
    /// and we should mirror that by writing 0 to 0x1407FC620.
    ///
    /// Fresh-entry = prior state implies turn boundary (enemy turn,
    /// pause, formation, dialogue, battle start, world map).
    ///
    /// NOT-fresh = prior state is a submenu or mid-turn action (Moving,
    /// Waiting, Attacking, Casting, Abilities, Acting). The game
    /// preserves cursor on submenu escape; we respect that.
    ///
    /// See <c>PROPOSAL_menucursor_drift.md</c> for the full design.
    /// </summary>
    public static class FreshBattleMyTurnEntryClassifier
    {
        private static readonly HashSet<string> NotFreshPriors = new()
        {
            "BattleMyTurn",        // no-op transition
            "BattleMoving",
            "BattleWaiting",
            "BattleAttacking",
            "BattleCasting",
            "BattleAbilities",
            "BattleActing",
            // Terminal battle states — a real BattleVictory/BattleDefeat/
            // GameOver ends the battle and we never see BattleMyTurn after.
            // If we DO see the transition, the prior detection was a flicker
            // (live-observed at Siedge Weald 2026-04-25 with BattleVictory
            // false-positives after attacks). Treating the transition as
            // fresh would clear _actedThisTurn/_movedThisTurn and let the
            // player re-issue an action they've already used.
            "BattleVictory",
            "BattleDefeat",
            "GameOver",
        };

        public static bool IsFresh(string? prev, string? detected)
        {
            if (detected != "BattleMyTurn") return false;
            if (prev != null && NotFreshPriors.Contains(prev)) return false;
            return true;
        }
    }
}
