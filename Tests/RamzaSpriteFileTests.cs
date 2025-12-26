using Xunit;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.IO;
using System;
using System.Drawing;

namespace FFTColorCustomizer.Tests
{
    public class RamzaSpriteFileTests
    {
        [Fact]
        public void Should_Transform_Ramza_Colors_For_WhiteHeretic_Theme()
        {
            // Arrange
            var colorTransformer = new RamzaColorTransformer();

            // Ramza Chapter 1's blue armor color (must match IsArmorColor detection)
            var originalArmorColor = Color.FromArgb(60, 80, 140); // Blue armor

            // Act
            var transformedColor = colorTransformer.TransformColor(originalArmorColor, "white_heretic", "RamzaChapter1");

            // Assert
            // For white_heretic theme, armor should become white/gray
            // Based on brightness calculation: (60+80+140)/3 = 93, which maps to medium gray (126)
            transformedColor.R.Should().Be(transformedColor.G, "Gray should have equal RGB values");
            transformedColor.R.Should().Be(transformedColor.B, "Gray should have equal RGB values");
            transformedColor.R.Should().BeGreaterThan(100, "Transformed armor should be gray");
        }

        [Fact]
        public void Should_Apply_Theme_To_Entire_Sprite_Image()
        {
            // Arrange
            var transformer = new RamzaColorTransformer();

            // Create a test bitmap with Ramza-like colors
            var testBitmap = new Bitmap(32, 40);
            using (var g = Graphics.FromImage(testBitmap))
            {
                // Add some blue pixels (Chapter 1 armor color)
                // Blue armor color that will be recognized by IsArmorColor for Chapter 1
                g.FillRectangle(new SolidBrush(Color.FromArgb(60, 80, 140)), 10, 10, 10, 10);
            }

            // Act
            // Use default characterName which is "RamzaChapter1"
            var themedBitmap = transformer.TransformBitmap(testBitmap, "white_heretic");

            // Assert
            themedBitmap.Should().NotBeNull();

            // Check that blue armor pixels were transformed to white/gray
            var pixelColor = themedBitmap.GetPixel(15, 15);
            pixelColor.R.Should().BeGreaterThan(100, "Transformed armor should be white or gray");
            pixelColor.R.Should().Be(pixelColor.G, "White/gray should have equal RGB values");
            pixelColor.R.Should().Be(pixelColor.B, "White/gray should have equal RGB values");
        }
    }
}