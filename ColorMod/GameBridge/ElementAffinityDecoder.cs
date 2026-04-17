using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decodes a per-unit 8-bit element-affinity mask into the list of element names
    /// whose bits are set. Session 30 live-verified bit layout (same for all 5
    /// affinity fields — Absorb/Cancel/Half/Weak/Strengthen — at static battle
    /// array offsets +0x5A..+0x5E):
    ///
    ///   bit 7 = Fire       bit 3 = Earth
    ///   bit 6 = Lightning  bit 2 = Water
    ///   bit 5 = Ice        bit 1 = Holy
    ///   bit 4 = Wind       bit 0 = Dark
    ///
    /// See memory/project_element_affinity_s30.md for the hunt record and data
    /// points. Wind (bit 4) and Dark (bit 0) were inferred from the consistent
    /// odd/even-bit pattern; the other 6 were live-verified.
    /// </summary>
    public static class ElementAffinityDecoder
    {
        private static readonly (int bit, string name)[] _map =
        {
            (7, "Fire"),
            (6, "Lightning"),
            (5, "Ice"),
            (4, "Wind"),
            (3, "Earth"),
            (2, "Water"),
            (1, "Holy"),
            (0, "Dark"),
        };

        /// <summary>
        /// Returns the list of element names whose bit is set in the mask.
        /// Empty list when mask is 0 (no affinity).
        /// </summary>
        public static List<string> Decode(byte mask)
        {
            var result = new List<string>();
            foreach (var (bit, name) in _map)
            {
                if ((mask & (1 << bit)) != 0) result.Add(name);
            }
            return result;
        }

        /// <summary>
        /// Returns true if the mask has the given element's bit set. Element
        /// name is case-insensitive; unknown names return false.
        /// </summary>
        public static bool Has(byte mask, string element)
        {
            foreach (var (bit, name) in _map)
            {
                if (string.Equals(name, element, System.StringComparison.OrdinalIgnoreCase))
                    return (mask & (1 << bit)) != 0;
            }
            return false;
        }
    }
}
