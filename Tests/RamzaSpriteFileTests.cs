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

            // Ramza's typical brown/purple armor color
            var originalArmorColor = Color.FromArgb(96, 48, 48);

            // Act
            var transformedColor = colorTransformer.TransformColor(originalArmorColor, "white_heretic");

            // Assert
            // For white_heretic theme, armor should become white/light gray
            transformedColor.R.Should().BeGreaterThan(200, "White armor should have high R value");
            transformedColor.G.Should().BeGreaterThan(200, "White armor should have high G value");
            transformedColor.B.Should().BeGreaterThan(200, "White armor should have high B value");
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
                // Add some brownish pixels (armor)
                g.FillRectangle(new SolidBrush(Color.FromArgb(80, 50, 50)), 10, 10, 10, 10);
            }

            // Act
            var themedBitmap = transformer.TransformBitmap(testBitmap, "white_heretic");

            // Assert
            themedBitmap.Should().NotBeNull();

            // Check that brownish pixels were transformed
            var pixelColor = themedBitmap.GetPixel(15, 15);
            pixelColor.R.Should().BeGreaterThan(200, "Transformed armor should be white");
        }
    }
}