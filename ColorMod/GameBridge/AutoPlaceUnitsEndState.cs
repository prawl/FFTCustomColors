namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Accept-predicate for <c>auto_place_units</c>'s post-commence poll loop.
    /// The helper sends Enter*8 + Space + Enter and then polls screen detection
    /// until "the battle has visibly started" — which includes pre-battle story
    /// text (<c>BattleDialogue</c>, <c>Cutscene</c>), not just turn-owner states.
    /// Story battles (Dorter, Orbonne, Mandalia) always open with dialogue
    /// before granting the player a turn; without those in the accept-list the
    /// helper hangs its full poll window before falling back.
    /// </summary>
    public static class AutoPlaceUnitsEndState
    {
        public static bool IsBattleStartedState(string? screenName)
        {
            return screenName switch
            {
                "BattleMyTurn" => true,
                "BattleAlliesTurn" => true,
                "BattleEnemiesTurn" => true,
                "BattleActing" => true,
                "BattleDialogue" => true,
                "Cutscene" => true,
                _ => false,
            };
        }
    }
}
