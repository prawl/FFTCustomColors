using Xunit;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Services;
using FluentAssertions;
using System.IO;
using System;
using System.Drawing;

namespace FFTColorCustomizer.Tests
{
    public class CharacterRowBuilderRamzaTests
    {
        [Fact]
        public void CharacterRowBuilder_Should_Transform_Ramza_Preview_For_Theme()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RamzaBuilderTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create the directory structure
                var unitPath = Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                Directory.CreateDirectory(unitPath);

                // Create a mock Ramza sprite file with valid data
                var spritePath = Path.Combine(unitPath, "battle_ramuza_spr.bin");
                var mockData = new byte[43 * 1024]; // 43KB like real file

                // Add a palette with brownish colors that will be transformed (512 bytes)
                // Palette format: 16-bit colors (5 bits each for R,G,B)
                for (int i = 0; i < 16; i++)  // First palette
                {
                    int offset = i * 2;
                    if (i >= 3 && i <= 10)  // Armor color indices
                    {
                        // Brown color (R=80, G=50, B=50) encoded as 16-bit
                        // R=80/8=10, G=50/8=6, B=50/8=6
                        ushort color = (ushort)((6 << 10) | (6 << 5) | 10);
                        mockData[offset] = (byte)(color & 0xFF);
                        mockData[offset + 1] = (byte)(color >> 8);
                    }
                    else
                    {
                        // Other colors
                        mockData[offset] = (byte)i;
                        mockData[offset + 1] = (byte)i;
                    }
                }

                // Add some sprite data (pixels using the palette indices)
                for (int i = 512; i < 2048; i++)
                {
                    // Use palette index 5 (brown armor color) for some pixels
                    mockData[i] = (byte)(i % 2 == 0 ? 0x55 : 0x00); // Two pixels per byte
                }

                File.WriteAllBytes(spritePath, mockData);

                var previewManager = new PreviewImageManager(tempDir);
                var rowBuilder = new CharacterRowBuilder(
                    new System.Windows.Forms.TableLayoutPanel(),
                    previewManager,
                    () => false,
                    new System.Collections.Generic.List<System.Windows.Forms.Control>(),
                    new System.Collections.Generic.List<System.Windows.Forms.Control>()
                );

                // Act - Try to load preview with theme transformation
                var method = typeof(CharacterRowBuilder).GetMethod("TryLoadFromBinFileWithTheme",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // This method should exist and apply theme transformation
                method.Should().NotBeNull("CharacterRowBuilder should have TryLoadFromBinFileWithTheme method");

                if (method != null)
                {
                    var images = method.Invoke(rowBuilder, new object[] { "RamzaChapter1", "white_heretic" }) as Image[];

                    // Assert that transformation was applied
                    images.Should().NotBeNull("Should return transformed images");
                    images.Length.Should().BeGreaterThan(0, "Should have at least one image");

                    // Check that the image has been transformed (should have white colors)
                    if (images[0] is Bitmap bitmap)
                    {
                        // Scan for white/light colors that indicate transformation
                        bool hasWhitePixels = false;
                        for (int x = 0; x < Math.Min(bitmap.Width, 50); x++)
                        {
                            for (int y = 0; y < Math.Min(bitmap.Height, 50); y++)
                            {
                                var pixel = bitmap.GetPixel(x, y);
                                if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200 && pixel.A > 0)
                                {
                                    hasWhitePixels = true;
                                    break;
                                }
                            }
                            if (hasWhitePixels) break;
                        }

                        hasWhitePixels.Should().BeTrue("Transformed Ramza sprite should have white pixels for white_heretic theme");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}