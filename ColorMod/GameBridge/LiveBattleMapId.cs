namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Session 48: authoritative current-battle map id byte at
    /// <c>0x14077D83C</c> (u8). Found via snapshot/diff between
    /// Dugeura Pass (map 86 / 0x56) and Beddha Sandwaste (map 82 / 0x52).
    /// Live-verified across 3 maps + survives game restart.
    ///
    /// Reopens only if the byte shifts after a game patch. The locId-
    /// based fallback in <c>NavigationActions</c> Try 1/2 is the next
    /// resort when this read returns an invalid id.
    /// </summary>
    public static class LiveBattleMapId
    {
        public const long Address = 0x14077D83CL;

        /// <summary>
        /// True if the byte value represents a real map id. Map IDs
        /// 1..127 are valid; 0 = uninitialized, 128+ = invalid.
        /// </summary>
        public static bool IsValid(int mapId) => mapId >= 1 && mapId <= 127;
    }
}
