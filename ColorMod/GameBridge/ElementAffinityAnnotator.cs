using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Given an ability's element and a target unit's five per-element affinity
    /// lists, returns a one-word marker for the strongest matching affinity.
    /// Returns null when the ability is non-elemental or the target has no
    /// matching affinity for that element.
    ///
    /// Priority when multiple overlap (rare but possible via gear stacking):
    /// absorb > null > half > weak > strengthen. Absorb and null are the most
    /// decision-altering (skip this target entirely); half is defensive; weak
    /// is offensive upside; strengthen is the target's OUTGOING-damage boost
    /// so it doesn't change whether the ability will hit hard, but it's still
    /// worth noting.
    /// </summary>
    public static class ElementAffinityAnnotator
    {
        public static string? ComputeMarker(
            string? abilityElement,
            List<string>? absorb,
            List<string>? nullList,
            List<string>? half,
            List<string>? weak,
            List<string>? strengthen)
        {
            if (string.IsNullOrEmpty(abilityElement)) return null;
            if (ContainsCI(absorb, abilityElement)) return "absorb";
            if (ContainsCI(nullList, abilityElement)) return "null";
            if (ContainsCI(half, abilityElement)) return "half";
            if (ContainsCI(weak, abilityElement)) return "weak";
            if (ContainsCI(strengthen, abilityElement)) return "strengthen";
            return null;
        }

        private static bool ContainsCI(List<string>? list, string target)
        {
            if (list == null) return false;
            foreach (var e in list)
            {
                if (string.Equals(e, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
