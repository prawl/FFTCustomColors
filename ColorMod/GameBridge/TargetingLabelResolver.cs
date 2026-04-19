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

        /// <summary>
        /// Same precedence as Resolve, but falls back to the cursor tile
        /// "(x,y)" when all three ability inputs are null — mirrors
        /// BattleMoving's ui= behavior so targeting screens always surface
        /// *something* actionable even when the player navigates menus
        /// manually (no battle_ability helper + no tracker latch).
        /// </summary>
        public static string ResolveOrCursor(
            string? lastAbilityName, string? selectedAbility, string? selectedItem,
            int cursorX, int cursorY)
        {
            return Resolve(lastAbilityName, selectedAbility, selectedItem)
                ?? BattleCursorFormatter.FormatCursor(cursorX, cursorY);
        }
    }
}
