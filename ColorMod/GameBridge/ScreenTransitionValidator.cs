using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Reject screen transitions that are physically impossible given the
    /// last-committed screen. Backstop for the if-else cascade in
    /// <see cref="ScreenDetectionLogic"/>: when memory reads at a transient
    /// produce a fingerprint matching e.g. <c>BattleCrystalMoveConfirm</c>
    /// from <c>BattleAttacking</c> (skipping the required <c>BattleVictory</c>
    /// step), this validator says "no, that can't happen — keep the old
    /// screen and try again on the next poll".
    ///
    /// <para>Default-permit: transitions not in the blacklist are allowed.
    /// We explicitly enumerate KNOWN-IMPOSSIBLE transitions; unknown ones
    /// pass through so legit screens we haven't catalogued aren't broken.
    /// Add to the blacklist as new misfires are observed.</para>
    /// </summary>
    public static class ScreenTransitionValidator
    {
        // Map: target screen → set of allowed predecessor screens. If the
        // target appears here, the FROM screen MUST be in the allowed set
        // (or null/empty/same-as-target — handled in IsValidTransition).
        // If the target is NOT in this dictionary, all transitions to it
        // are allowed.
        private static readonly Dictionary<string, HashSet<string>> RequiredPredecessors = new()
        {
            // Crystal-move reward only follows a Victory.
            ["BattleCrystalMoveConfirm"] = new()
            {
                "BattleVictory",
                "BattleCrystalMoveConfirm",
            },
            // Battle terminals only fire from in-battle states. WorldMap →
            // BattleVictory / BattleDesertion / GameOver is the sticky-flag
            // false-positive shape.
            ["BattleVictory"] = new()
            {
                "BattleMyTurn", "BattleAttacking", "BattleMoving",
                "BattleActing", "BattleAbilities", "BattleCasting",
                "BattleEnemiesTurn", "BattleAlliesTurn",
                "BattlePaused", "BattleDialogue", "BattleChoice",
                "BattleVictory",
                "BattleFormation",
            },
            ["BattleDesertion"] = new()
            {
                "BattleMyTurn", "BattleAttacking", "BattleMoving",
                "BattleActing", "BattleAbilities", "BattleCasting",
                "BattleEnemiesTurn", "BattleAlliesTurn",
                "BattlePaused", "BattleDialogue", "BattleChoice",
                "BattleDesertion",
                "BattleFormation",
            },
            ["GameOver"] = new()
            {
                "BattleMyTurn", "BattleAttacking", "BattleMoving",
                "BattleActing", "BattleAbilities", "BattleCasting",
                "BattleEnemiesTurn", "BattleAlliesTurn",
                "BattlePaused", "BattleDialogue", "BattleChoice",
                "GameOver",
                "BattleFormation",
            },
            // WorldMap from a pre-battle dialogue is the cutscene-during-
            // transition false-positive. Only full Cutscene end or a
            // legit battle-end Advance gets you to WorldMap from a
            // dialogue / battle screen.
            ["WorldMap"] = new()
            {
                "WorldMap", "Cutscene", "BattleVictory", "BattleDesertion",
                "GameOver", "EncounterDialog",
                "TravelList", "LocationMenu", "PartyMenuUnits",
                "PartyMenu", "PartyMenuInventory", "PartyMenuChronicle",
                "PartyMenuOptions", "BattleSequence",
                "TitleScreen", "LoadGame",
                // BattleDialogue / BattleChoice deliberately NOT allowed
                // here — mid-battle dialogue going straight to WorldMap is
                // the cutscene-during-transition false-positive shape we
                // want to suppress (live-flagged 2026-04-26 at Brigands'
                // Den, bridge tagged WorldMap while dialogue still on
                // screen). Battle terminals (Victory/Desertion/GameOver)
                // are the legit path off the battlefield.
            },
        };

        public static bool IsValidTransition(string? fromScreen, string toScreen)
        {
            // First-ever detection has no predecessor — accept anything.
            if (string.IsNullOrEmpty(fromScreen)) return true;
            // Same-screen re-detection always allowed.
            if (fromScreen == toScreen) return true;
            // If the target isn't in our restricted set, transitions are
            // unrestricted — default-permit.
            if (!RequiredPredecessors.TryGetValue(toScreen, out var allowed))
                return true;
            return allowed.Contains(fromScreen);
        }
    }
}
