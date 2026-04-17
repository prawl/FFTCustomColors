namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Resolves the "what ability is being targeted" label for BattleAttacking /
    /// BattleCasting screens. Precedence (matches the live code path):
    ///   1. lastAbilityName — set by battle_ability/battle_attack helpers when
    ///      they drive the menu programmatically.
    ///   2. selectedAbility — set by BattleMenuTracker when the player presses
    ///      Enter inside an ability list (Battle_Mettle etc).
    ///   3. selectedItem    — set by BattleMenuTracker when the player presses
    ///      Enter on a top-level skillset (Attack / Items).
    /// </summary>
    public static class TargetingLabelResolver
    {
        public static string? Resolve(string? lastAbilityName, string? selectedAbility, string? selectedItem)
        {
            return lastAbilityName ?? selectedAbility ?? selectedItem;
        }
    }
}
