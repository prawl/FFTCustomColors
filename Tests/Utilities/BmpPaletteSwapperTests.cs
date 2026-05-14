using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.Utilities
{
    public class BmpPaletteSwapperTests : IDisposable
    {
        private readonly string _tempDir;

        public BmpPaletteSwapperTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"BmpPaletteSwapperTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch { /* best-effort temp cleanup */ }
        }

        [Fact]
        public void NormalizeIndex0Transparency_MakesIndex0Transparent_AllOthersOpaque()
        {
            // Arrange — a 16-color indexed bitmap whose palette starts fully opaque,
            // including an opaque BLACK at index 5 (a section the user "painted black").
            using var bmp = new Bitmap(8, 8, PixelFormat.Format4bppIndexed);
            var palette = bmp.Palette;
            for (int i = 0; i < palette.Entries.Length; i++)
                palette.Entries[i] = Color.FromArgb(255, 10 + i, 20 + i, 30 + i);
            palette.Entries[0] = Color.FromArgb(255, 0, 0, 0); // background slot, opaque black
            palette.Entries[5] = Color.FromArgb(255, 0, 0, 0); // user-painted black section
            bmp.Palette = palette;

            // Act
            BmpPaletteSwapper.NormalizeIndex0Transparency(bmp);

            // Assert
            var result = bmp.Palette.Entries;
            result[0].A.Should().Be(0, "palette index 0 is the transparent background slot");
            result[1].A.Should().Be(255);
            result[5].A.Should().Be(255, "a non-zero index painted black must stay OPAQUE black");
            result[5].R.Should().Be(0);
            result[5].G.Should().Be(0);
            result[5].B.Should().Be(0);
        }

        [Fact]
        public void NormalizeIndex0Transparency_PreservesRgbOfIndex0()
        {
            // Index 0's RGB is left untouched — only its alpha is forced to 0.
            using var bmp = new Bitmap(8, 8, PixelFormat.Format4bppIndexed);
            var palette = bmp.Palette;
            palette.Entries[0] = Color.FromArgb(255, 12, 34, 56);
            bmp.Palette = palette;

            BmpPaletteSwapper.NormalizeIndex0Transparency(bmp);

            var entry0 = bmp.Palette.Entries[0];
            entry0.A.Should().Be(0);
            entry0.R.Should().Be(12);
            entry0.G.Should().Be(34);
            entry0.B.Should().Be(56);
        }

        [Fact]
        public void NormalizeIndex0Transparency_NonIndexedBitmap_IsNoOp()
        {
            // A 32bpp bitmap has no palette — the call must simply do nothing, not throw.
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);

            Action act = () => BmpPaletteSwapper.NormalizeIndex0Transparency(bmp);

            act.Should().NotThrow();
        }

        // ----- LoadWithTransparencyMask: recovering transparency for flattened themed sheets -----
        // The export pipeline flattens themed Ramza sheets to non-indexed 24bpp with the
        // transparent background baked to solid black — the "black box around Ramza" bug.
        // LoadWithTransparencyMask recovers the alpha channel from the matching indexed
        // "original" sheet, which still carries the index-0 background mask.

        [Fact]
        public void LoadWithTransparencyMask_NonIndexedThemedBmp_MakesMaskIndex0PixelsTransparent()
        {
            var (themedPath, maskPath) = CreateRamzaLikeFixture();

            using var result = BmpPaletteSwapper.LoadWithTransparencyMask(themedPath, maskPath);

            result.GetPixel(0, 0).A.Should().Be(0, "mask index 0 marks the transparent background");
            result.GetPixel(1, 1).A.Should().Be(0);
        }

        [Fact]
        public void LoadWithTransparencyMask_NonIndexedThemedBmp_KeepsNonBackgroundBlackPixelsOpaque()
        {
            // A sprite pixel the export happened to render pure black must NOT be erased —
            // the decision keys off the mask's palette index, not the pixel colour.
            var (themedPath, maskPath) = CreateRamzaLikeFixture();

            using var result = BmpPaletteSwapper.LoadWithTransparencyMask(themedPath, maskPath);

            var spriteBlack = result.GetPixel(6, 6);
            spriteBlack.A.Should().Be(255, "a black pixel outside the index-0 region is real sprite content");
            spriteBlack.R.Should().Be(0);
            spriteBlack.G.Should().Be(0);
            spriteBlack.B.Should().Be(0);
            result.GetPixel(5, 5).A.Should().Be(255, "non-background pixels stay opaque");
        }

        [Fact]
        public void LoadWithTransparencyMask_IndexedThemedBmp_UsesOwnIndex0AndIgnoresMask()
        {
            // An already-indexed themed sheet carries its own index-0 transparency; the mask
            // is unnecessary (and here, deliberately absent).
            Color[] palette = { Color.FromArgb(255, 0, 0, 0), Color.FromArgb(255, 200, 50, 50) };
            var themedPath = WriteIndexed4bppBmp("themed_indexed.bmp", 8, 8,
                (x, y) => (x < 4 && y < 4) ? 0 : 1, palette);

            using var result = BmpPaletteSwapper.LoadWithTransparencyMask(themedPath, maskBmpPath: null);

            result.Palette.Entries[0].A.Should().Be(0, "index 0 is the transparent background slot");
            result.Palette.Entries[1].A.Should().Be(255);
        }

        [Fact]
        public void LoadWithTransparencyMask_MissingMask_ReturnsBitmapWithoutThrowing()
        {
            // No indexed original to transfer from — degrade gracefully rather than crash.
            var themedPath = Write24bppBmp("themed_nomask.bmp", 8, 8, (x, y) => Color.Black);
            var missingMask = Path.Combine(_tempDir, "does_not_exist.bmp");

            Bitmap result = null;
            Action act = () => result = BmpPaletteSwapper.LoadWithTransparencyMask(themedPath, missingMask);

            act.Should().NotThrow();
            result.Should().NotBeNull();
            result.Dispose();
        }

        [Fact]
        public void LoadWithTransparencyMask_MaskDimensionMismatch_ReturnsBitmapWithoutThrowing()
        {
            // A mask of the wrong size can't be aligned pixel-for-pixel — degrade gracefully.
            var themedPath = Write24bppBmp("themed_8x8.bmp", 8, 8, (x, y) => Color.Black);
            Color[] palette = { Color.Black, Color.White };
            var maskPath = WriteIndexed4bppBmp("mask_4x4.bmp", 4, 4, (x, y) => 0, palette);

            Bitmap result = null;
            Action act = () => result = BmpPaletteSwapper.LoadWithTransparencyMask(themedPath, maskPath);

            act.Should().NotThrow();
            result.Should().NotBeNull();
            result.Dispose();
        }

        // ----- fixture helpers -----

        /// <summary>
        /// Builds the pair the bug is about: a 24bpp "themed" sheet with its background baked
        /// to black, and the matching 4bpp indexed "original" sheet whose palette index 0
        /// marks the true background. Background = top-left 4x4 quadrant. Pixel (6,6) is a
        /// deliberate pure-black *sprite* pixel that must survive the transparency transfer.
        /// </summary>
        private (string themedPath, string maskPath) CreateRamzaLikeFixture()
        {
            const int size = 8;

            Color[] palette = { Color.FromArgb(255, 0, 0, 0), Color.FromArgb(255, 180, 120, 60) };
            var maskPath = WriteIndexed4bppBmp("mask.bmp", size, size,
                (x, y) => (x < 4 && y < 4) ? 0 : 1, palette);

            var themedPath = Write24bppBmp("themed.bmp", size, size, (x, y) =>
            {
                if (x < 4 && y < 4) return Color.Black;          // baked-black background
                if (x == 6 && y == 6) return Color.Black;        // genuine black sprite pixel
                return Color.FromArgb(20 + x * 10, 40 + y * 10, 80);
            });

            return (themedPath, maskPath);
        }

        private string Write24bppBmp(string name, int width, int height, Func<int, int, Color> colorAt)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    bmp.SetPixel(x, y, colorAt(x, y));

            var path = Path.Combine(_tempDir, name);
            bmp.Save(path, ImageFormat.Bmp);
            return path;
        }

        private string WriteIndexed4bppBmp(string name, int width, int height,
            Func<int, int, int> indexAt, Color[] palette)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format4bppIndexed);

            var pal = bmp.Palette;
            for (int i = 0; i < palette.Length && i < pal.Entries.Length; i++)
                pal.Entries[i] = palette[i];
            bmp.Palette = pal;

            var rect = new Rectangle(0, 0, width, height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format4bppIndexed);
            try
            {
                var buffer = new byte[data.Stride * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = indexAt(x, y) & 0x0F;
                        int pos = y * data.Stride + (x >> 1);
                        buffer[pos] = (x & 1) == 0
                            ? (byte)((buffer[pos] & 0x0F) | (idx << 4))
                            : (byte)((buffer[pos] & 0xF0) | idx);
                    }
                }
                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            var path = Path.Combine(_tempDir, name);
            bmp.Save(path, ImageFormat.Bmp);
            return path;
        }
    }
}
