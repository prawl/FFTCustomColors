namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Resolves the ui= label for the BattleStatus screen — which shows the
    /// active unit's CharacterStatus panel while in battle. The meaningful
    /// label is the active unit's display name; the menuCursor byte at this
    /// screen is stale (it still reports 3 = "Status" from the action-menu
    /// index used to enter the screen).
    /// </summary>
    public static class BattleStatusUiResolver
    {
        /// <summary>
        /// Returns the active unit's name as the BattleStatus ui= label, or
        /// null if no active unit is cached (e.g. first scan of a new battle
        /// before scan_move has populated the cache).
        /// </summary>
        public static string? Resolve(string? activeUnitName)
        {
            return activeUnitName;
        }
    }
}
