using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// S60 narrator Phase 3: surface when a PLAYER unit crosses the critical-HP
    /// threshold (under 1/3 MaxHp) during an enemy turn. Decision aid — tells
    /// Claude to plan a heal on the next turn before the player is at risk of
    /// a KO from the next incoming attack.
    ///
    /// Fires only when a unit was ABOVE 1/3 in the pre-snap and FELL BELOW
    /// 1/3 in the post-snap. Units that stayed in critical range across the
    /// whole window are not re-surfaced (they were critical before too).
    /// </summary>
    public static class CriticalHpInferrer
    {
        /// <summary>
        /// Returns rendered "> ..." lines suitable for appending to narrator output.
        /// Needs postSnaps (or a lookup) to read MaxHp, which isn't on ChangeEvent.
        /// </summary>
        public static List<string> Infer(
            IReadOnlyList<UnitScanDiff.ChangeEvent> events,
            IReadOnlyList<UnitScanDiff.UnitSnap> postSnaps)
        {
            var lines = new List<string>();
            if (events == null || events.Count == 0) return lines;
            if (postSnaps == null || postSnaps.Count == 0) return lines;

            foreach (var e in events)
            {
                if (e.Team != "PLAYER") continue;
                if (e.Kind != "damaged") continue;
                if (!e.OldHp.HasValue || !e.NewHp.HasValue) continue;

                // Find the unit's MaxHp in the post-snap (matching by Name).
                int maxHp = 0;
                foreach (var u in postSnaps)
                {
                    if (u.Name == e.Label) { maxHp = u.MaxHp; break; }
                }
                if (maxHp <= 0) continue;

                int threshold = maxHp / 3;
                bool wasAboveCritical = e.OldHp.Value > threshold;
                bool nowCritical = e.NewHp.Value > 0 && e.NewHp.Value <= threshold;
                if (wasAboveCritical && nowCritical)
                {
                    lines.Add($"> {e.Label} reached critical HP ({e.OldHp.Value}→{e.NewHp.Value}/{maxHp})");
                }
            }
            return lines;
        }
    }
}
