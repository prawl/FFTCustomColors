using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Swaps the 16-color palette of an indexed (Mode=P) HD sprite-sheet BMP using
    /// the palette baked into a themed sprite .bin file (or an external palette).
    /// The BMP's pixel indices align 1:1 with the bin's 16-color palette, so this is
    /// a pure palette-table swap — pixel data is untouched.
    /// </summary>
    public static class BmpPaletteSwapper
    {
        private const int BinPaletteSize = 32; // 16 colors × 2 bytes (BGR555)

        /// <summary>
        /// Loads <paramref name="bmpPath"/> and swaps its palette using palette 0 of the
        /// themed .bin file at <paramref name="binPath"/>. If either file is missing or the
        /// BMP isn't a 16-color indexed image, returns the BMP as-is.
        /// </summary>
        public static Bitmap LoadWithBinPalette(string bmpPath, string binPath)
        {
            var bmp = new Bitmap(bmpPath);

            if (!File.Exists(binPath))
                return bmp;

            byte[] paletteBytes = new byte[BinPaletteSize];
            using (var fs = File.OpenRead(binPath))
            {
                int read = fs.Read(paletteBytes, 0, BinPaletteSize);
                if (read < BinPaletteSize)
                    return bmp;
            }

            ApplyBgr555Palette(bmp, paletteBytes);
            return bmp;
        }

        /// <summary>
        /// Loads <paramref name="bmpPath"/> and swaps its palette using the first 32 bytes
        /// of an external palette file (user theme palette format, 512 bytes total).
        /// </summary>
        public static Bitmap LoadWithExternalPalette(string bmpPath, byte[] externalPalette)
        {
            var bmp = new Bitmap(bmpPath);
            if (externalPalette == null || externalPalette.Length < BinPaletteSize)
                return bmp;

            ApplyBgr555Palette(bmp, externalPalette);
            return bmp;
        }

        private static void ApplyBgr555Palette(Bitmap bmp, byte[] paletteBytes)
        {
            // Only meaningful for indexed bitmaps with at least 16 palette entries
            if (bmp.Palette == null || bmp.Palette.Entries.Length < 16)
                return;

            var palette = bmp.Palette;
            for (int i = 0; i < 16; i++)
            {
                ushort bgr555 = (ushort)(paletteBytes[i * 2] | (paletteBytes[i * 2 + 1] << 8));

                int r5 = bgr555 & 0x1F;
                int g5 = (bgr555 >> 5) & 0x1F;
                int b5 = (bgr555 >> 10) & 0x1F;

                int r = (r5 * 255) / 31;
                int g = (g5 * 255) / 31;
                int b = (b5 * 255) / 31;

                // Index 0 is transparent (matches the SpriteSheetExtractor's black-as-transparent convention)
                int a = i == 0 ? 0 : 255;
                palette.Entries[i] = Color.FromArgb(a, r, g, b);
            }

            // System.Drawing.Bitmap.Palette is copy-on-get; must reassign for the swap to take effect
            bmp.Palette = palette;
        }
    }
}
