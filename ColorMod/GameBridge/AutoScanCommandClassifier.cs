namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decides whether a nav-action command should trigger the
    /// post-completion auto-scan (scan_move piggy-back that populates
    /// ValidMoveTiles / AttackTiles / per-unit abilities in the response).
    ///
    /// Default-permissive: unknown actions auto-scan. The skip list
    /// covers known transition-commit actions where the static battle
    /// array may not yet reflect the new state — e.g. auto_place_units,
    /// which polls for a battle-started screen but returns before the
    /// unit array is populated, causing auto-scan to emit
    /// "No ally found in scan".
    /// </summary>
    public static class AutoScanCommandClassifier
    {
        public static bool ShouldAutoScanAfter(string? action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            return action switch
            {
                // Battle-commence: poll exits on BattleMyTurn but the
                // static battle array is still populating. Skip the
                // immediate scan — the user's next `screen` call will
                // auto-scan via the screen-query path once the array
                // has settled.
                "auto_place_units" => false,
                _ => true,
            };
        }
    }
}
