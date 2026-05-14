using System;
using System.Drawing;
using System.Drawing.Imaging;
using FluentAssertions;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.Utilities
{
    public class BmpPaletteSwapperTests
    {
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
    }
}
