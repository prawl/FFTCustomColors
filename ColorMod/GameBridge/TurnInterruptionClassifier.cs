namespace FFTColorCustomizer.GameBridge
{
    public enum TurnInterruption
    {
        /// <summary>Normal in-battle state — no interruption.</summary>
        Normal,
        /// <summary>Mid-turn story dialogue fired; caller should advance before retrying.</summary>
        DialogueInterrupt,
        /// <summary>Battle ended (Victory / Defeat / Desertion / GameOver).</summary>
        BattleEnded,
        /// <summary>No longer in a battle (WorldMap / PartyMenu / TitleScreen).</summary>
        OutOfBattle,
        /// <summary>Unrecognized screen — detection may have drifted.</summary>
        Unknown,
    }

    /// <summary>
    /// Pure classifier used by <c>execute_turn</c> and battle-action handlers
    /// to decide what to do when the screen state changes mid-sequence. See
    /// <see cref="TurnInterruption"/> for the outcomes.
    /// </summary>
    public static class TurnInterruptionClassifier
    {
        public static TurnInterruption Classify(string? screenName)
        {
            return screenName switch
            {
                null or "" => TurnInterruption.Unknown,
                "BattleMyTurn" or "BattleMoving" or "BattleAttacking" or "BattleCasting"
                    or "BattleAbilities" or "BattleActing" or "BattleWaiting"
                    or "BattleEnemiesTurn" or "BattleAlliesTurn" or "BattlePaused"
                    or "BattleStatus" or "BattleAutoBattle" or "BattleChoice" => TurnInterruption.Normal,
                "BattleDialogue" or "Cutscene" => TurnInterruption.DialogueInterrupt,
                "BattleVictory" or "BattleDesertion" or "GameOver" => TurnInterruption.BattleEnded,
                "WorldMap" or "TitleScreen" or "PartyMenuUnits" or "PartyMenuInventory"
                    or "PartyMenuChronicle" or "PartyMenuOptions" or "TravelList"
                    or "LocationMenu" or "SettlementMenu" or "EncounterDialog"
                    or "BattleFormation" or "CharacterStatus" or "EquipmentAndAbilities"
                    or "JobSelection" or "LoadGame" => TurnInterruption.OutOfBattle,
                _ => TurnInterruption.Unknown,
            };
        }

        /// <summary>
        /// True if the current interruption means the turn sequence should
        /// bail out rather than retry. Covers BattleEnded + OutOfBattle.
        /// Dialogue is recoverable; Normal is not an interruption.
        /// </summary>
        public static bool ShouldAbortTurn(TurnInterruption interruption)
        {
            return interruption == TurnInterruption.BattleEnded
                || interruption == TurnInterruption.OutOfBattle;
        }
    }
}
