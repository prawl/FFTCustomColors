namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decodes the IC-remaster per-unit facing byte stored at offset +0x35 in each
    /// unit's slot of the static battle array (0x140893C00, stride 0x200). The byte
    /// sits immediately after gridY at +0x34.
    ///
    /// Encoding (session 30 live-verified across 4 player units at Siedge Weald):
    ///   0x00 → South
    ///   0x01 → West
    ///   0x02 → North
    ///   0x03 → East
    ///
    /// See memory/project_facing_byte_s30.md for the hunt notes.
    /// </summary>
    public static class FacingByteDecoder
    {
        /// <summary>
        /// Convert a raw facing byte to a cardinal-direction name.
        /// Returns null for values outside 0..3 (shouldn't occur in normal play but
        /// guards against stale / uninitialized slots).
        /// </summary>
        public static string? DecodeName(int rawByte)
        {
            return rawByte switch
            {
                0 => "South",
                1 => "West",
                2 => "North",
                3 => "East",
                _ => null,
            };
        }

        /// <summary>
        /// Convert a raw facing byte to a unit-direction delta (dx, dy) where dy
        /// matches the game's convention (y+ = South when facing byte = 0x00).
        /// Returns null for values outside 0..3.
        /// </summary>
        public static (int dx, int dy)? DecodeDelta(int rawByte)
        {
            return rawByte switch
            {
                0 => (0, 1),   // South
                1 => (-1, 0),  // West
                2 => (0, -1),  // North
                3 => (1, 0),   // East
                _ => null,
            };
        }
    }
}
