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
    }
}
