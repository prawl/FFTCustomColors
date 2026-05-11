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
        /// Returns the index in <paramref name="palette"/> of the color closest to <paramref name="pixel"/>
        /// by squared RGB distance. Used to recolor non-indexed (24bpp) portraits by mapping each
        /// pixel to a vanilla palette slot, then swapping in the themed color for that slot.
        /// </summary>
        public static int FindNearestPaletteIndex(Color pixel, Color[] palette)
        {
            int best = 0;
            int bestDist = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int dr = pixel.R - palette[i].R;
                int dg = pixel.G - palette[i].G;
                int db = pixel.B - palette[i].B;
                int dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist) { bestDist = dist; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Decodes a 16-color BGR555 palette (32 bytes) into RGB Color entries.
        /// </summary>
        public static Color[] DecodeBgr555Palette(byte[] paletteBytes)
        {
            var colors = new Color[16];
            for (int i = 0; i < 16; i++)
            {
                ushort v = (ushort)(paletteBytes[i * 2] | (paletteBytes[i * 2 + 1] << 8));
                int r5 = v & 0x1F;
                int g5 = (v >> 5) & 0x1F;
                int b5 = (v >> 10) & 0x1F;
                colors[i] = Color.FromArgb((r5 * 255) / 31, (g5 * 255) / 31, (b5 * 255) / 31);
            }
            return colors;
        }

        /// <summary>
        /// Returns a copy of <paramref name="themed"/> with the entries at <paramref name="preserveFromVanilla"/>
        /// overwritten by the matching entries from <paramref name="vanilla"/>. Used to keep skin tones
        /// (and other "don't theme this" indices) at their original game colors when applying a
        /// themed palette to a portrait.
        /// </summary>
        public static byte[] MergeBgr555Palettes(byte[] themed, byte[] vanilla, int[] preserveFromVanilla)
        {
            var merged = (byte[])themed.Clone();
            if (preserveFromVanilla == null) return merged;

            foreach (var idx in preserveFromVanilla)
            {
                if (idx < 0 || idx >= 16) continue;
                if (vanilla == null || vanilla.Length < (idx * 2) + 2) continue;
                merged[idx * 2] = vanilla[idx * 2];
                merged[idx * 2 + 1] = vanilla[idx * 2 + 1];
            }
            return merged;
        }

        /// <summary>
        /// Like <see cref="LoadWithBinPalette"/> but uses <paramref name="vanillaBinPath"/>'s palette
        /// for the indices listed in <paramref name="preserveFromVanilla"/>. Other indices come from
        /// the themed bin. Used to preserve skin tones on portraits where the section mapping defines
        /// "don't theme this" indices.
        /// </summary>
        public static Bitmap LoadWithBinPalettePreserving(string bmpPath, string themedBinPath, string vanillaBinPath, int[] preserveFromVanilla)
        {
            var bmp = new Bitmap(bmpPath);
            if (!File.Exists(themedBinPath) || !File.Exists(vanillaBinPath))
                return bmp;

            byte[] themedBytes = new byte[BinPaletteSize];
            byte[] vanillaBytes = new byte[BinPaletteSize];
            using (var fs = File.OpenRead(themedBinPath))
            {
                if (fs.Read(themedBytes, 0, BinPaletteSize) < BinPaletteSize) return bmp;
            }
            using (var fs = File.OpenRead(vanillaBinPath))
            {
                if (fs.Read(vanillaBytes, 0, BinPaletteSize) < BinPaletteSize) return bmp;
            }

            var merged = MergeBgr555Palettes(themedBytes, vanillaBytes, preserveFromVanilla);
            ApplyBgr555Palette(bmp, merged);
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
