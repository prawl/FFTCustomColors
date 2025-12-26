using Xunit;
using FluentAssertions;
using System.Drawing;
using System.Collections.Generic;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Tests
{
    public class SpriteSheetExtractorTests
    {
        [Fact]
        public void SpriteSheetExtractor_Should_Extract_SW_Sprite_From_Sheet()
        {
            // Arrange
            var extractor = new SpriteSheetExtractor();

            // Create a mock sprite sheet (512x512)
            var spriteSheet = new Bitmap(512, 512);

            // Act
            var swSprite = extractor.ExtractSprite(spriteSheet, Direction.SW);

            // Assert
            swSprite.Should().NotBeNull();
            swSprite.Width.Should().Be(64);
            swSprite.Height.Should().Be(80);
        }

        [Fact]
        public void SpriteSheetExtractor_Should_Extract_SW_Sprite_From_Correct_Position()
        {
            // Arrange
            var extractor = new SpriteSheetExtractor();

            // Create a mock sprite sheet with a test pixel at SW position
            var spriteSheet = new Bitmap(512, 512);
            using (var g = Graphics.FromImage(spriteSheet))
            {
                // Place a red pixel at position (64, 0) - the start of SW sprite
                spriteSheet.SetPixel(64, 0, Color.Red);
            }

            // Act
            var swSprite = extractor.ExtractSprite(spriteSheet, Direction.SW);

            // Assert
            // The red pixel should be at (0, 0) in the extracted sprite
            swSprite.GetPixel(0, 0).R.Should().Be(255);
        }

        [Fact]
        public void SpriteSheetExtractor_Should_Extract_NW_Sprite_From_Correct_Position()
        {
            // Arrange
            var extractor = new SpriteSheetExtractor();

            // Create a mock sprite sheet with a test pixel at NW position
            var spriteSheet = new Bitmap(512, 512);
            using (var g = Graphics.FromImage(spriteSheet))
            {
                // Place a blue pixel at position (192, 0) - the start of NW sprite
                spriteSheet.SetPixel(192, 0, Color.Blue);
            }

            // Act
            var nwSprite = extractor.ExtractSprite(spriteSheet, Direction.NW);

            // Assert
            // The blue pixel should be at (0, 0) in the extracted sprite
            nwSprite.GetPixel(0, 0).B.Should().Be(255);
        }

        [Fact]
        public void SpriteSheetExtractor_Should_Create_NE_By_Mirroring_NW()
        {
            // Arrange
            var extractor = new SpriteSheetExtractor();

            // Create a mock sprite sheet with a test pixel on the left side of NW sprite
            var spriteSheet = new Bitmap(512, 512, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            // Place a green pixel at position (192, 0) - left edge of NW sprite
            spriteSheet.SetPixel(192, 0, Color.FromArgb(255, 0, 255, 0));

            // Act
            var neSprite = extractor.ExtractSprite(spriteSheet, Direction.NE);

            // Assert
            // The green pixel should be mirrored to the right edge (63, 0) in NE sprite
            var mirroredPixel = neSprite.GetPixel(63, 0);
            mirroredPixel.G.Should().Be(255);
            mirroredPixel.A.Should().Be(255);
        }

        [Fact]
        public void SpriteSheetExtractor_Should_Create_SE_By_Mirroring_SW()
        {
            // Arrange
            var extractor = new SpriteSheetExtractor();

            // Create a mock sprite sheet with a test pixel on the left side of SW sprite
            var spriteSheet = new Bitmap(512, 512, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            // Place a yellow pixel at position (64, 0) - left edge of SW sprite
            spriteSheet.SetPixel(64, 0, Color.FromArgb(255, 255, 255, 0));

            // Act
            var seSprite = extractor.ExtractSprite(spriteSheet, Direction.SE);

            // Assert
            // The yellow pixel should be mirrored to the right edge (63, 0) in SE sprite
            var mirroredPixel = seSprite.GetPixel(63, 0);
            mirroredPixel.R.Should().Be(255);
            mirroredPixel.G.Should().Be(255);
            mirroredPixel.B.Should().Be(0);
        }

        [Fact]
        public void SpriteSheetExtractor_Should_Extract_All_Four_Directions()
        {
            // Arrange
            var extractor = new SpriteSheetExtractor();

            // Create a mock sprite sheet
            var spriteSheet = new Bitmap(512, 512, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Act
            var allSprites = extractor.ExtractAllDirections(spriteSheet);

            // Assert
            allSprites.Should().NotBeNull();
            allSprites.Count.Should().Be(4);
            allSprites.Should().ContainKeys(Direction.SW, Direction.NW, Direction.NE, Direction.SE);

            foreach (var sprite in allSprites.Values)
            {
                sprite.Width.Should().Be(64);
                sprite.Height.Should().Be(80);
            }
        }

        [Fact]
        public void SpriteSheetExtractor_Should_Load_And_Extract_From_File()
        {
            // Arrange
            var extractor = new SpriteSheetExtractor();
            var tempPath = System.IO.Path.GetTempFileName() + ".png";

            // Create and save a test sprite sheet
            var testSheet = new Bitmap(512, 512, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            testSheet.Save(tempPath);

            try
            {
                // Act
                var allSprites = extractor.ExtractAllDirectionsFromFile(tempPath);

                // Assert
                allSprites.Should().NotBeNull();
                allSprites.Count.Should().Be(4);
            }
            finally
            {
                // Cleanup
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }
    }
}