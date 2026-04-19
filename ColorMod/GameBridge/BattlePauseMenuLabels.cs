namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Maps the BattlePaused menu cursor row (0..5) to its in-game label.
    /// Layout verified live at Mount Bervenia session 44 2026-04-18:
    ///   0 = Data
    ///   1 = Retry
    ///   2 = Load
    ///   3 = Settings
    ///   4 = Return to World Map
    ///   5 = Return to Title Screen
    /// There is NO Save option on BattlePaused (common misconception —
    /// see TODO history on "SaveSlotPicker from BattlePaused" closures).
    /// </summary>
    public static class BattlePauseMenuLabels
    {
        /// <summary>
        /// Returns the label for the given cursor row, or null if the row
        /// is out of range (cursor byte unread or desynced).
        /// </summary>
        public static string? ForRow(int row)
        {
            return row switch
            {
                0 => "Data",
                1 => "Retry",
                2 => "Load",
                3 => "Settings",
                4 => "Return to World Map",
                5 => "Return to Title Screen",
                _ => null,
            };
        }
    }
}
