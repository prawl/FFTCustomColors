namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Small null-safe predicates over screen-name strings. Centralizes the
    /// "starts with Battle" (etc.) checks that were previously scattered
    /// across NavigationActions, TurnAutoScanner, and CommandWatcher.
    ///
    /// If the screen-naming convention ever changes (e.g. a non-Battle-prefixed
    /// screen becomes a battle state), only this file needs to update.
    /// </summary>
    public static class ScreenNamePredicates
    {
        /// <summary>
        /// True if the screen name designates an in-battle state. All battle
        /// screens in the detection set currently share the "Battle" prefix,
        /// so this is a simple StartsWith check — but encapsulated so a future
        /// exception can be added here rather than scattered through callers.
        /// </summary>
        public static bool IsBattleState(string? screenName)
        {
            return !string.IsNullOrEmpty(screenName) && screenName!.StartsWith("Battle");
        }

        /// <summary>
        /// True if the screen is one of the four top-level PartyMenu tabs
        /// (Units / Inventory / Chronicle / Options) — NOT drilled-in screens
        /// like CharacterStatus or EquipmentAndAbilities. Use when a helper
        /// only applies to the tabbed view.
        /// </summary>
        public static bool IsPartyMenuTab(string? screenName)
        {
            return screenName == "PartyMenuUnits"
                || screenName == "PartyMenuInventory"
                || screenName == "PartyMenuChronicle"
                || screenName == "PartyMenuOptions";
        }

        /// <summary>
        /// True if the screen is in the roster-view PartyMenu Units tree:
        /// PartyMenuUnits + CharacterStatus + EquipmentAndAbilities. Matches
        /// the scope of the roster-populating block in CommandWatcher. NOT
        /// INCLUDED: JobSelection (different cursor semantics — per-job-grid,
        /// not per-unit-in-roster), Inventory / Chronicle / Options (sibling
        /// tabs, not in the Units tree).
        /// </summary>
        public static bool IsPartyTree(string? screenName)
        {
            return screenName == "PartyMenuUnits"
                || screenName == "CharacterStatus"
                || screenName == "EquipmentAndAbilities";
        }
    }
}
