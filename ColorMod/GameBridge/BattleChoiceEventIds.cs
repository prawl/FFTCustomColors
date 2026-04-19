using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
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
    /// pair — this catalog is a documentation + regression pin, not a
    /// replacement rule. If the signals ever prove unreliable, this set
    /// can back a fallback whitelist clause.
    ///
    /// Source: FFTHandsFree/TODO.md §0 session 44 "BattleChoice detection
    /// — use eventId whitelist approach (NEW PLAN)". Add new entries as
    /// live-verified choice scenes are encountered; reference the source
    /// session in a comment on each entry.
    /// </summary>
    public static class BattleChoiceEventIds
    {
        public static readonly HashSet<int> KnownEventIds = new()
        {
            // Mandalia Plain — "1. Defeat the Brigade" / "2. Rescue the captive".
            // Session 44 (2026-04-18).
            16,
        };

        public static bool IsKnownChoiceEvent(int eventId) =>
            KnownEventIds.Contains(eventId);
    }
}
