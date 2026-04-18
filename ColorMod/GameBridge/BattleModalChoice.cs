using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Scaffolding for two gameplay-critical modal states that block the
    /// battle flow with a 2-option Y/N choice:
    ///
    ///   BattleObjectiveChoice - pre-battle "protect X" vs "defeat all" fork.
    ///     Picking wrong can permanently fail the battle.
    ///   RecruitOffer - post-battle "X wants to join your party" prompt.
    ///     Declining a story-character recruit loses them forever.
    ///
    /// Both present a 2-option modal with identical UX: Up/Down to highlight,
    /// Enter to confirm. Detection discriminator is NOT YET FOUND — memory
    /// scan needed (see TODO §12 "Missing Screen States" and §0 "New state:
    /// BattleChoice"). This module holds the pure option-rendering helper
    /// so detection can be wired in later without a structural refactor.
    /// </summary>
    public static class BattleModalChoice
    {
        /// <summary>
        /// Given a raw cursor index (0 or 1) and the two option labels,
        /// returns the currently-highlighted label. Returns null on invalid
        /// cursor or missing labels — caller falls back to raw "option A / B".
        /// </summary>
        public static string? GetHighlightedLabel(int cursorIndex, string? optionA, string? optionB)
        {
            if (cursorIndex == 0 && !string.IsNullOrEmpty(optionA)) return optionA;
            if (cursorIndex == 1 && !string.IsNullOrEmpty(optionB)) return optionB;
            return null;
        }

        /// <summary>
        /// Returns the list of valid-path names for a modal-choice screen:
        /// always CursorUp, CursorDown, Confirm, Cancel. Used by both
        /// BattleObjectiveChoice and RecruitOffer screens when they land.
        /// </summary>
        public static IReadOnlyList<string> ValidPathNames => new[]
        {
            "CursorUp", "CursorDown", "Confirm", "Cancel"
        };
    }
}
