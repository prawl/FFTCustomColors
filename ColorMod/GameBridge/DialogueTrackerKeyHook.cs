namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decides when a raw keypress should bump the
    /// <see cref="DialogueProgressTracker"/>. The `advance_dialogue`
    /// command and the `execute_action Advance` validPath both bump
    /// explicitly. The `enter` shell helper (raw Enter via the bridge's
    /// key-dispatch path) historically did NOT bump — so the box index
    /// drifted behind whenever the player advanced via raw keys.
    ///
    /// Rule: VK_ENTER pressed while the current screen is one of
    /// Cutscene / BattleDialogue / BattleChoice advances the tracker.
    /// Everything else is a no-op. The caller decides when this fires
    /// (currently CommandWatcher.ExecuteKeyCommand's post-send hook).
    ///
    /// Guard: the advance_dialogue handler bumps BEFORE dispatching its
    /// Enter keypress, and that Enter path goes through NavigationActions
    /// directly — it does NOT go through ExecuteKeyCommand. So placing
    /// the hook in ExecuteKeyCommand cleanly avoids double-bumping.
    /// </summary>
    public static class DialogueTrackerKeyHook
    {
        private const int VK_ENTER = 0x0D;

        /// <summary>
        /// True if pressing <paramref name="vkCode"/> on screen
        /// <paramref name="screenName"/> should advance the tracker.
        /// </summary>
        public static bool ShouldAdvance(int vkCode, string? screenName)
        {
            if (vkCode != VK_ENTER) return false;
            if (string.IsNullOrEmpty(screenName)) return false;
            return screenName == "Cutscene"
                || screenName == "BattleDialogue"
                || screenName == "BattleChoice";
        }

        /// <summary>
        /// Convenience wrapper: calls <see cref="DialogueProgressTracker.Advance"/>
        /// iff the key+screen combination is a dialogue advance and the
        /// eventId is in the real-scene range (1..399, matching the
        /// existing advance_dialogue guard at CommandWatcher:2704).
        /// </summary>
        public static void HandleKeyPress(
            DialogueProgressTracker tracker,
            int vkCode,
            string? screenName,
            int eventId)
        {
            if (tracker == null) return;
            if (!ShouldAdvance(vkCode, screenName)) return;
            if (eventId < 1 || eventId >= 400) return;
            tracker.Advance(eventId);
        }
    }
}
