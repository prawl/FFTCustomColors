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

        /// <summary>
        /// Loads <paramref name="bmpPath"/> keeping its own embedded palette, normalizing only
        /// the transparency convention: palette index 0 becomes fully transparent and indices
        /// 1-15 fully opaque. Use this for the "original"/un-themed load paths so downstream
        /// extraction can key transparency off the alpha channel — the same contract the
        /// palette-swapping loaders above already satisfy.
        /// </summary>
        public static Bitmap LoadWithOriginalPalette(string bmpPath)
        {
            var bmp = new Bitmap(bmpPath);
            NormalizeIndex0Transparency(bmp);
            return bmp;
        }

        /// <summary>
        /// Forces palette index 0 transparent (alpha 0) and every other palette entry opaque
        /// (alpha 255), leaving all RGB values untouched. No-op for non-indexed bitmaps.
        /// FFT sprites reserve palette index 0 as the transparent "background" color; encoding
        /// that in the alpha channel lets extraction tell a background pixel apart from a pixel
        /// the user deliberately painted pure black.
        /// </summary>
        public static void NormalizeIndex0Transparency(Bitmap bmp)
        {
            if (bmp?.Palette == null || bmp.Palette.Entries.Length < 1)
                return;

            var palette = bmp.Palette;
            for (int i = 0; i < palette.Entries.Length; i++)
            {
                var c = palette.Entries[i];
                int a = i == 0 ? 0 : 255;
                palette.Entries[i] = Color.FromArgb(a, c.R, c.G, c.B);
            }

            // System.Drawing.Bitmap.Palette is copy-on-get; must reassign for the change to take effect.
            bmp.Palette = palette;
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
