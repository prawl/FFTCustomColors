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
        ///
        /// Returns null when no ability is latched AND the cursor is
        /// uninitialized (-1 on either axis). The game reports -1 for
        /// cursorX/cursorY during the brief window between entering a
        /// targeting screen and the cursor landing on a real tile —
        /// rendering "(-1,-1)" in that window is nonsense. The caller
        /// (CommandWatcher) will assign the return value to screen.UI
        /// which is JsonIgnoreCondition.WhenWritingNull, so a null result
        /// cleanly omits the ui= field from the response.
        /// </summary>
        public static string? ResolveOrCursor(
            string? lastAbilityName, string? selectedAbility, string? selectedItem,
            int cursorX, int cursorY)
        {
            var abilityLabel = Resolve(lastAbilityName, selectedAbility, selectedItem);
            if (abilityLabel != null) return abilityLabel;
            if (cursorX < 0 || cursorY < 0) return null;
            return BattleCursorFormatter.FormatCursor(cursorX, cursorY);
        }
    }
}
