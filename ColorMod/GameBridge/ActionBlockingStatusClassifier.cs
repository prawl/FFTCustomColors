using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Picks the most-restrictive action-blocking status (if any) from a
    /// unit's status list and returns a short header tag like
    /// <c>DontAct(no act)</c> or <c>Petrify(frozen)</c>.
    ///
    /// <para>Driven by playtest #9 2026-04-26: agent missed Wilham's
    /// <c>DontAct</c> status because it lived in the bottom Units block,
    /// not on the active-unit header. The agent kept retrying menu nav
    /// and accidentally landed on AutoBattle. This classifier surfaces
    /// the blocker on the header where it can't be missed.</para>
    ///
    /// <para>Severity order (most-restrictive first): action AND movement
    /// blockers (Petrify, Stop, Sleep) come before transformations (Frog,
    /// Chicken), then mind-control (Confusion, Charm, Berserk), then
    /// action-only (DontAct), then move-only (DontMove), then animation
    /// states (Performing, Charging, Jump). The first status in the list
    /// that the unit has wins.</para>
    /// </summary>
    public static class ActionBlockingStatusClassifier
    {
        private static readonly (string Status, string Hint)[] BlockingByPriority = new[]
        {
            ("Petrify",     "frozen"),
            ("Stop",        "frozen"),
            ("Sleep",       "sleeping"),
            ("Frog",        "transformed"),
            ("Chicken",     "transformed"),
            ("Confusion",   "confused"),
            ("Charm",       "charmed"),
            ("Berserk",     "auto-attack"),
            ("DontAct",     "no act"),
            ("DontMove",    "no move"),
            ("Performing",  "performing"),
            ("Charging",    "charging"),
            ("Jump",        "airborne"),
        };

        public static string? GetBlockingTag(IEnumerable<string>? statuses)
        {
            if (statuses == null) return null;
            var present = new HashSet<string>(statuses, StringComparer.OrdinalIgnoreCase);
            if (present.Count == 0) return null;
            foreach (var (status, hint) in BlockingByPriority)
            {
                if (present.Contains(status))
                {
                    return $"{status}({hint})";
                }
            }
            return null;
        }
    }
}
