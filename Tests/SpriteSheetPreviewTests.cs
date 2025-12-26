using Xunit;
using FluentAssertions;
using System.Drawing;
using System.IO;
using System;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer.Tests
{
    public class SpriteSheetPreviewTests
    {
        [Fact]
        public void SpriteSheetExtractor_Should_Check_For_SpriteSheet_File()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"SpriteSheetTest_{Guid.NewGuid()}");
            var imagesDir = Path.Combine(tempDir, "Images", "RamzaChapter1", "original");
            Directory.CreateDirectory(imagesDir);

            // Create a test sprite sheet
            var spriteSheetPath = Path.Combine(imagesDir, "sprite_sheet.png");
            var testSheet = new Bitmap(512, 512, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Mark the SW position with a red pixel for testing
            testSheet.SetPixel(64, 0, Color.Red);
            testSheet.Save(spriteSheetPath);

            try
            {
                // Act
                var spriteSheetExists = File.Exists(spriteSheetPath);

                // Assert
                spriteSheetExists.Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SpriteSheetPreviewLoader_Should_Load_Previews_From_SpriteSheet()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"PreviewLoaderTest_{Guid.NewGuid()}");
            var imagesDir = Path.Combine(tempDir, "Images", "RamzaChapter1", "original");
            Directory.CreateDirectory(imagesDir);

            // Create a test sprite sheet with the expected filename for RamzaChapter1
            var spriteSheetPath = Path.Combine(imagesDir, "830_Ramuza_Ch1_test.png");
            var testSheet = new Bitmap(512, 512, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            testSheet.Save(spriteSheetPath);

            try
            {
                var loader = new SpriteSheetPreviewLoader(tempDir);

                // Act
                var previews = loader.LoadPreviews("RamzaChapter1", "original");

                // Assert
                previews.Should().NotBeNull();
                previews.Should().HaveCount(4); // SW, NW, NE, SE
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SpriteSheetPreviewLoader_Should_Return_Sprites_With_Correct_Dimensions()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"DimensionTest_{Guid.NewGuid()}");
            var imagesDir = Path.Combine(tempDir, "Images", "RamzaChapter1", "original");
            Directory.CreateDirectory(imagesDir);

            // Create a test sprite sheet with the expected filename for RamzaChapter1
            var spriteSheetPath = Path.Combine(imagesDir, "830_Ramuza_Ch1_test.png");
            var testSheet = new Bitmap(512, 512, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Add a test pixel to verify extraction
            testSheet.SetPixel(64, 0, Color.Red); // SW sprite position
            testSheet.Save(spriteSheetPath);

            try
            {
                var loader = new SpriteSheetPreviewLoader(tempDir);

                // Act
                var previews = loader.LoadPreviewsWithExtractor("RamzaChapter1", "original");

                // Assert
                previews.Should().NotBeNull();
                previews[0].Width.Should().Be(64);
                previews[0].Height.Should().Be(80);
                previews[0].GetPixel(0, 0).R.Should().Be(255); // Should have the red pixel
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        [Fact]
        public void SpriteSheetPreviewLoader_Should_Load_NonRamza_Character_With_Generic_Name()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"NonRamzaTest_{Guid.NewGuid()}");
            var imagesDir = Path.Combine(tempDir, "Images", "SomeOtherCharacter", "original");
            Directory.CreateDirectory(imagesDir);

            // Create a test sprite sheet with generic name
            var spriteSheetPath = Path.Combine(imagesDir, "sprite_sheet.png");
            var testSheet = new Bitmap(512, 512, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            testSheet.SetPixel(64, 0, Color.Blue);
            testSheet.Save(spriteSheetPath);

            try
            {
                var loader = new SpriteSheetPreviewLoader(tempDir);

                // Act
                var previews = loader.LoadPreviewsWithExtractor("SomeOtherCharacter", "original");

                // Assert
                previews.Should().NotBeNull();
                previews.Should().HaveCount(4);
                previews[0].Width.Should().Be(64);
                previews[0].Height.Should().Be(80);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}