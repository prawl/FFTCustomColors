using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

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
        /// Loads a themed sprite-sheet BMP whose transparency may have been lost. The export
        /// pipeline flattens some themed sheets (notably Ramza's per-theme BMPs) to a
        /// non-indexed 24bpp format with the transparent background baked to solid black —
        /// which renders as a black box behind the sprite.
        ///
        /// When <paramref name="bmpPath"/> is non-indexed, transparency is recovered from
        /// <paramref name="maskBmpPath"/> — the matching <em>indexed</em> "original" sheet —
        /// by treating every pixel that is palette index 0 in the mask as transparent. The
        /// two sheets share an identical layout, so this restores the alpha channel without
        /// guessing: a pixel the export rendered pure black is dropped only if the indexed
        /// original says it is background, never because of its colour.
        ///
        /// When <paramref name="bmpPath"/> is itself indexed it carries its own index-0
        /// transparency, so this behaves exactly like <see cref="LoadWithOriginalPalette"/>
        /// and the mask is ignored. If the mask is missing or a different size, the themed
        /// BMP is returned as-is — the preview degrades to the black box rather than crashing.
        /// </summary>
        public static Bitmap LoadWithTransparencyMask(string bmpPath, string maskBmpPath)
        {
            var bmp = new Bitmap(bmpPath);

            // Indexed themed sheet: self-describing, same as the plain original-palette load.
            if ((bmp.PixelFormat & PixelFormat.Indexed) != 0)
            {
                NormalizeIndex0Transparency(bmp);
                return bmp;
            }

            // Non-indexed themed sheet: recover transparency from the indexed original.
            bool[,] isBackground = TryReadIndex0Mask(maskBmpPath, bmp.Width, bmp.Height);
            if (isBackground == null)
                return bmp; // no usable mask — degrade gracefully

            var result = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        if (isBackground[x, y])
                        {
                            result.SetPixel(x, y, Color.Transparent);
                        }
                        else
                        {
                            var c = bmp.GetPixel(x, y);
                            result.SetPixel(x, y, Color.FromArgb(255, c.R, c.G, c.B));
                        }
                    }
                }
            }
            finally
            {
                bmp.Dispose();
            }
            return result;
        }

        /// <summary>
        /// Reads <paramref name="maskBmpPath"/> (an indexed bitmap) into a [width, height]
        /// grid that is true wherever the mask pixel is palette index 0. Returns null —
        /// meaning "no usable mask" — if the file is missing, not indexed, a different size
        /// than <paramref name="width"/> x <paramref name="height"/>, or fails to decode.
        /// </summary>
        private static bool[,] TryReadIndex0Mask(string maskBmpPath, int width, int height)
        {
            if (string.IsNullOrEmpty(maskBmpPath) || !File.Exists(maskBmpPath))
                return null;

            try
            {
                using (var mask = new Bitmap(maskBmpPath))
                {
                    if (mask.Width != width || mask.Height != height)
                        return null;
                    if ((mask.PixelFormat & PixelFormat.Indexed) == 0)
                        return null;

                    int bpp = Image.GetPixelFormatSize(mask.PixelFormat);
                    var rect = new Rectangle(0, 0, width, height);
                    var data = mask.LockBits(rect, ImageLockMode.ReadOnly, mask.PixelFormat);
                    try
                    {
                        var buffer = new byte[Math.Abs(data.Stride) * height];
                        Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                        var result = new bool[width, height];
                        for (int y = 0; y < height; y++)
                        {
                            int row = y * data.Stride;
                            for (int x = 0; x < width; x++)
                            {
                                int index;
                                switch (bpp)
                                {
                                    case 4:
                                        byte packed = buffer[row + (x >> 1)];
                                        index = (x & 1) == 0 ? (packed >> 4) : (packed & 0x0F);
                                        break;
                                    case 8:
                                        index = buffer[row + x];
                                        break;
                                    case 1:
                                        byte bits = buffer[row + (x >> 3)];
                                        index = (bits >> (7 - (x & 7))) & 1;
                                        break;
                                    default:
                                        return null; // unexpected indexed depth
                                }
                                result[x, y] = index == 0;
                            }
                        }
                        return result;
                    }
                    finally
                    {
                        mask.UnlockBits(data);
                    }
                }
            }
            catch
            {
                return null; // any decode/lock failure — degrade gracefully
            }
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
