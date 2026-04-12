namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decodes equipped reaction/support/movement abilities from heap unit struct bitfields.
    /// Layout: Reaction 4 bytes at +0x74 (base 166), Support 5 bytes at +0x78 (base 198).
    /// MSB-first: position = id - base, byteIdx = pos/8, bitIdx = 7 - (pos%8).
    /// These bitfields have exactly 1 bit set (the equipped ability) or all zeros (none equipped).
    /// </summary>
    public static class PassiveAbilityDecoder
    {
        private const int ReactionBase = 166;
        private const int ReactionBytes = 4;
        private const int SupportBase = 198;
        private const int SupportBytes = 5;

        public static string? DecodeReaction(byte[] bytes)
        {
            if (bytes == null || bytes.Length < ReactionBytes) return null;
            var id = FindSetBit(bytes, ReactionBytes, ReactionBase);
            if (id < 0) return null;
            return AbilityData.ReactionAbilities.TryGetValue((byte)id, out var info) ? info.Name : null;
        }

        public static string? DecodeSupport(byte[] bytes)
        {
            if (bytes == null || bytes.Length < SupportBytes) return null;
            var id = FindSetBit(bytes, SupportBytes, SupportBase);
            if (id < 0) return null;
            return AbilityData.SupportAbilities.TryGetValue((byte)id, out var info) ? info.Name : null;
        }

        /// <summary>
        /// Scans the bitfield for the first set bit and returns the corresponding ability ID.
        /// Returns -1 if no bits are set.
        /// </summary>
        private static int FindSetBit(byte[] bytes, int length, int baseId)
        {
            for (int byteIdx = 0; byteIdx < length; byteIdx++)
            {
                if (bytes[byteIdx] == 0) continue;
                for (int bitIdx = 7; bitIdx >= 0; bitIdx--)
                {
                    if ((bytes[byteIdx] & (1 << bitIdx)) != 0)
                    {
                        int position = byteIdx * 8 + (7 - bitIdx);
                        return baseId + position;
                    }
                }
            }
            return -1;
        }
    }
}
