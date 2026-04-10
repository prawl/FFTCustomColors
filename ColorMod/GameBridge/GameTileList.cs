using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Parses the game's in-memory movement tile list at 0x140C66315.
    /// Each entry is 7 bytes: X, Y, elev_lo, elev_hi, unknown, unknown, flag.
    /// flag != 0 means valid tile, flag == 0 terminates the list.
    /// This is the authoritative source — the game uses this list to validate moves.
    /// </summary>
    public static class GameTileList
    {
        public const int EntrySize = 7;
        public const long Address = 0x140C66315;
        public const int MaxBytes = 700; // up to ~100 tiles

        public static HashSet<(int x, int y)> Parse(byte[] bytes)
        {
            var result = new HashSet<(int, int)>();
            if (bytes == null || bytes.Length < EntrySize)
                return result;

            for (int i = 0; i + EntrySize <= bytes.Length; i += EntrySize)
            {
                int flag = bytes[i + 6];
                if (flag == 0) break; // terminator
                int x = bytes[i];
                int y = bytes[i + 1];
                result.Add((x, y));
            }
            return result;
        }
    }
}
