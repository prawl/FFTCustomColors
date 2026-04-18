using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Hard-coded list of ability names that auto-end the casting unit's turn
    /// in FFT canon. When a battle helper sees one of these in use, it should
    /// treat the turn as ended immediately after Enter — don't wait for, or
    /// prompt Claude to press, Wait.
    ///
    /// Currently: Jump (Dragoon primary) + Self-Destruct (Bomb monster suicide attack).
    /// Extend as other cases surface during live testing. Candidates NOT added without
    /// live confirmation:
    ///
    ///   Wish        — Mediator sacrificial heal; behavior varies by version.
    ///   Blood Price — Dark Knight HP-consuming attacks; uses MP not HP in IC, non-lethal.
    ///   Ultima      — canonical spell, no auto-end despite capstone status.
    ///
    /// These are NOT auto-end even though they may SEEM to end a turn:
    ///
    ///   Counter          — reaction ability; fires on incoming attack but doesn't consume
    ///                      the counter-user's own turn.
    ///   Critical: Quick  — grants an extra turn on crystallize; not a user action.
    ///   Reraise          — passively triggers on KO; doesn't consume a turn.
    ///   Items (consumables) — Hi-Potion etc. leave the turn state intact; user still
    ///                         needs to Wait after.
    ///   Throw            — Ninja's Throw is a one-shot action like Attack, but the unit
    ///                      still has a move + Wait remaining.
    /// </summary>
    public static class AutoEndTurnAbilities
    {
        private static readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase)
        {
            "Jump",
            "Self-Destruct",
        };

        public static bool IsAutoEndTurn(string? abilityName)
        {
            if (string.IsNullOrWhiteSpace(abilityName)) return false;
            return _names.Contains(abilityName.Trim());
        }
    }
}
