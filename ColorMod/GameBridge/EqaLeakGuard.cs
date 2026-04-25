namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Filter spurious EquipmentAndAbilities detections that fire as a
    /// transient frame after key presses on GameOver / Battle* / post-battle
    /// screens. Live-captured 2026-04-25 Siedge Weald GameOver: bridge sent
    /// Enter, first post-key detection returned EquipmentAndAbilities (the
    /// abilities-list ui rendered with the EqA fingerprint), then settled
    /// back to GameOver on the next read.
    ///
    /// EquipmentAndAbilities is reachable in normal flow only from
    /// PartyMenuUnits / CharacterStatus / EquipmentItemList. When the prior
    /// settled state is GameOver / Battle* / Victory / Desertion, an EqA
    /// detection isn't a real transition — hold the prior name.
    /// </summary>
    public static class EqaLeakGuard
    {
        public static string Filter(string? previousDetected, string currentDetected)
        {
            if (currentDetected != "EquipmentAndAbilities")
                return currentDetected;

            if (string.IsNullOrEmpty(previousDetected))
                return currentDetected; // unknown prior — accept, no signal

            // States that can't legitimately transition into EqA in one
            // detection cycle. EqA only opens via the party-menu tree.
            if (previousDetected == "GameOver"
                || previousDetected == "BattleVictory"
                || previousDetected == "BattleDesertion"
                || ScreenNamePredicates.IsBattleState(previousDetected))
            {
                return previousDetected;
            }

            return currentDetected;
        }
    }
}
