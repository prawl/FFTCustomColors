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
    /// live confirmation: Wish (Mediator sacrificial heal — behavior varies by version),
    /// Blood Price (Dark Knight HP-consuming attacks — uses MP not HP in IC, non-lethal),
    /// Ultima (canonical spell, no auto-end).
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
