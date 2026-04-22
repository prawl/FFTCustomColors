using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// One catalogued BattleChoice prompt. Captures the eventId + the
    /// two option labels so `ui=` on the BattleChoice screen can surface
    /// the cursor's current option by text rather than just "option 1"
    /// / "option 2". Labels are live-observed from in-game prompts.
    /// </summary>
    public record BattleChoiceEntry(int EventId, string Location, string OptionOne, string OptionTwo);

    /// <summary>
    /// Canonical catalog of story eventIds that produce a 2-option
    /// BattleChoice prompt (cursor + Confirm/Cancel modal that forks the
    /// battle objective). Built incrementally as events are encountered
    /// in-game. Each entry is a live-observed choice scene, not a guess
    /// based on .mes byte scan.
    ///
    /// The signal-based detection rule at
    /// <see cref="ScreenDetectionLogic.Detect"/> already classifies these
    /// correctly via the <c>eventHasChoice</c> + <c>choiceModalFlag</c>
    /// pair — this catalog is a documentation + regression pin plus a
    /// label-lookup for rendering. If the signals ever prove unreliable,
    /// this set can back a fallback whitelist clause.
    ///
    /// Source: FFTHandsFree/TODO.md §0 session 44 "BattleChoice detection
    /// — use eventId whitelist approach (NEW PLAN)". Add new entries as
    /// live-verified choice scenes are encountered; reference the source
    /// session in a comment on each entry.
    /// </summary>
    public static class BattleChoiceEventIds
    {
        /// <summary>
        /// Catalogued eventIds with their option labels. Only live-verified
        /// entries — do NOT add speculative entries from training data.
        /// </summary>
        public static readonly Dictionary<int, BattleChoiceEntry> Catalog = new()
        {
            // Mandalia Plain — "1. Defeat the Brigade" / "2. Rescue the captive".
            // Session 44 (2026-04-18). Live-verified: eventHasChoice=1,
            // choiceModalFlag=1 → BattleChoice classification.
            [16] = new BattleChoiceEntry(
                EventId: 16,
                Location: "Mandalia Plain",
                OptionOne: "Defeat the Brigade",
                OptionTwo: "Rescue the captive"),
        };

        /// <summary>
        /// Backwards-compatible hash set for callers that only need
        /// existence queries (ScreenDetectionLogic fallback whitelist).
        /// Derived from <see cref="Catalog"/>.
        /// </summary>
        public static readonly HashSet<int> KnownEventIds = BuildKnownSet();

        private static HashSet<int> BuildKnownSet()
        {
            var set = new HashSet<int>();
            foreach (var id in Catalog.Keys) set.Add(id);
            return set;
        }

        public static bool IsKnownChoiceEvent(int eventId) =>
            Catalog.ContainsKey(eventId);

        /// <summary>
        /// Resolve option label by eventId + cursor row (0 or 1). Returns
        /// null for unknown events or invalid cursor. Used by the screen
        /// renderer to surface <c>ui=&lt;label&gt;</c> on BattleChoice.
        /// </summary>
        public static string? OptionLabel(int eventId, int cursorRow)
        {
            if (!Catalog.TryGetValue(eventId, out var entry)) return null;
            return cursorRow switch
            {
                0 => entry.OptionOne,
                1 => entry.OptionTwo,
                _ => null,
            };
        }

        /// <summary>
        /// Resolve option label with a generic fallback when the event is
        /// not yet catalogued. Returns "Option 1" / "Option 2" instead of
        /// null, so scan output can still surface SOMETHING rather than
        /// leaving ui= blank. Callers that want to distinguish catalogued
        /// from fallback should use <see cref="OptionLabel"/> directly.
        /// </summary>
        public static string OptionLabelOrGeneric(int eventId, int cursorRow)
        {
            var label = OptionLabel(eventId, cursorRow);
            if (label != null) return label;
            return cursorRow switch
            {
                0 => "Option 1",
                1 => "Option 2",
                _ => "?",
            };
        }
    }
}
