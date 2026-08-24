using System;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Promotes one of a unit bin's unused palette slots into slot 0, which is the slot the game
    /// draws a themed unit from. CC-31: the artists left finished alternate colour schemes in
    /// slots 1-7 of many bins, and the four themes the owner rates best turned out to be verbatim
    /// copies of exactly that. Harvesting one is therefore a BYTE MOVE, never a colour transform:
    /// no quantisation and no shade generation, so the shipped theme is bit-for-bit what the
    /// artists authored. See docs/ADDING_AN_NPC_THEME.md and ledger row CC-31.
    /// </summary>
    public static class PaletteHarvester
    {
        /// <summary>Bytes of palette data at the head of a unit bin: 16 palettes x 16 BGR555.</summary>
        public const int PaletteBlockSize = 512;

        /// <summary>Bytes per palette: 16 entries x 2.</summary>
        public const int PaletteStride = 32;

        /// <summary>Highest palette slot a unit is drawn from; 8-15 are portrait palettes.</summary>
        public const int MaxUnitSlot = 15;

        /// <summary>
        /// Copies <paramref name="sourceSlot"/> verbatim over palette 0, leaving every other
        /// palette and the whole pixel block untouched.
        /// </summary>
        /// <returns>
        /// False when the source slot already matches slot 0, meaning the harvest would produce a
        /// theme identical to vanilla. Callers treat that as a vacuous theme and refuse to ship it.
        /// </returns>
        public static bool PromoteSlotToZero(byte[] binData, int sourceSlot)
        {
            if (binData == null)
                throw new ArgumentNullException(nameof(binData));

            if (sourceSlot < 1 || sourceSlot > MaxUnitSlot)
                throw new ArgumentOutOfRangeException(nameof(sourceSlot), sourceSlot,
                    $"Palette slot must be 1 to {MaxUnitSlot}; slot 0 is the destination.");

            if (binData.Length < PaletteBlockSize)
                throw new ArgumentException(
                    $"Bin is {binData.Length} bytes, too short to hold a {PaletteBlockSize} byte palette block.",
                    nameof(binData));

            int offset = sourceSlot * PaletteStride;

            bool differs = false;
            for (int i = 0; i < PaletteStride; i++)
            {
                if (binData[offset + i] != binData[i])
                {
                    differs = true;
                    break;
                }
            }

            if (!differs)
                return false;

            Buffer.BlockCopy(binData, offset, binData, 0, PaletteStride);
            return true;
        }
    }
}
