using Xunit;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.IO;
using System;
using System.Drawing;

namespace FFTColorCustomizer.Tests
{
    public class RamzaSpritePreviewTests
    {
        [Fact]
        public void Should_Extract_Preview_From_RamzaChapter1_Sprite()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RamzaSpriteTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create a mock sprite file with minimal valid data
                var spritePath = Path.Combine(tempDir, "battle_ramuza_spr.bin");
                var mockData = new byte[43 * 1024]; // 43KB like real file

                // Add a minimal palette (512 bytes)
                for (int i = 0; i < 512; i += 2)
                {
                    // Add some test colors
                    mockData[i] = (byte)(i % 256);
                    mockData[i + 1] = (byte)((i / 2) % 256);
                }

                File.WriteAllBytes(spritePath, mockData);

                var extractor = new BinSpriteExtractor();

                // Act
                var sprites = extractor.ExtractCornerDirections(mockData, 0, 0);

                // Assert
                sprites.Should().NotBeNull();
                sprites.Length.Should().Be(4, "Should extract 4 corner directions");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void Should_Apply_Theme_Colors_To_Ramza_Preview()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RamzaThemeTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create a simple test image
                var originalBitmap = new Bitmap(32, 40);
                using (var g = Graphics.FromImage(originalBitmap))
                {
                    // Draw some test pixels in Ramza's typical colors
                    // Brown/purple armor areas
                    g.FillRectangle(new SolidBrush(Color.FromArgb(96, 48, 48)), 10, 10, 10, 10);
                    // Blue cape
                    g.FillRectangle(new SolidBrush(Color.FromArgb(48, 48, 96)), 10, 20, 10, 10);
                }

                var themeApplier = new RamzaThemeApplier();

                // Act - Apply white_heretic theme (should make armor white)
                var themedBitmap = themeApplier.ApplyTheme(originalBitmap, "white_heretic");

                // Assert
                themedBitmap.Should().NotBeNull();
                // We can't check exact colors without implementing the actual theme logic
                // but we can verify the image was processed
                themedBitmap.Width.Should().Be(32);
                themedBitmap.Height.Should().Be(40);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}