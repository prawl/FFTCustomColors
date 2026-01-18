using System.Drawing;
using Xunit;
using FFTColorCustomizer.Services;

namespace Tests.Services
{
    public class RamzaPaletteTransformerTests
    {
        [Fact]
        public void GetArmorIndices_ShouldReturnCorrectIndices()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();

            // Act
            var indices = transformer.GetArmorIndices();

            // Assert
            Assert.Equal(new[] { 3, 4, 5, 6 }, indices);
        }

        [Fact]
        public void GetAccessoryIndices_ShouldReturnCorrectIndices()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();

            // Act
            var indices = transformer.GetAccessoryIndices();

            // Assert
            Assert.Equal(new[] { 7, 8 }, indices);
        }

        [Fact]
        public void ApplyHslShift_WithZeroShifts_ShouldReturnSameColors()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(255, 0, 0),   // index 0: red
                Color.FromArgb(0, 255, 0),   // index 1: green
                Color.FromArgb(0, 0, 255),   // index 2: blue
            };

            // Act
            var result = transformer.ApplyHslShift(palette, 0, 0, 0, new[] { 0, 1, 2 });

            // Assert
            Assert.Equal(palette[0].R, result[0].R);
            Assert.Equal(palette[0].G, result[0].G);
            Assert.Equal(palette[0].B, result[0].B);
        }

        [Fact]
        public void ApplyHslShift_WithHueShift180_ShouldShiftRedToCyan()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(255, 0, 0),   // red (hue 0)
            };

            // Act - shift hue by 180 degrees
            var result = transformer.ApplyHslShift(palette, 180, 0, 0, new[] { 0 });

            // Assert - red shifted 180 degrees should be cyan (0, 255, 255)
            Assert.Equal(0, result[0].R);
            Assert.Equal(255, result[0].G);
            Assert.Equal(255, result[0].B);
        }

        [Fact]
        public void ApplyHslShift_ShouldOnlyModifySpecifiedIndices()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(255, 0, 0),   // index 0: red - will be modified
                Color.FromArgb(0, 255, 0),   // index 1: green - should NOT be modified
                Color.FromArgb(0, 0, 255),   // index 2: blue - will be modified
            };

            // Act - only modify indices 0 and 2
            var result = transformer.ApplyHslShift(palette, 180, 0, 0, new[] { 0, 2 });

            // Assert - index 1 should be unchanged
            Assert.Equal(0, result[1].R);
            Assert.Equal(255, result[1].G);
            Assert.Equal(0, result[1].B);
        }

        [Fact]
        public void ApplyHslShift_WithLightnessIncrease_ShouldMakeColorLighter()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(128, 0, 0),   // dark red
            };

            // Act - increase lightness by 50%
            var result = transformer.ApplyHslShift(palette, 0, 0, 50, new[] { 0 });

            // Assert - color should be lighter (higher RGB values)
            Assert.True(result[0].R > 128);
        }

        [Fact]
        public void ApplyHslShift_ShouldHandleOutOfBoundsIndicesGracefully()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(255, 0, 0),
                Color.FromArgb(0, 255, 0),
            };

            // Act - try to modify indices that don't exist
            var result = transformer.ApplyHslShift(palette, 180, 0, 0, new[] { 0, 5, 10 });

            // Assert - should not throw, index 0 modified, others ignored
            Assert.Equal(2, result.Length);
            Assert.Equal(0, result[0].R); // red shifted to cyan
        }

        [Fact]
        public void ApplyHslShift_WithSaturationDecrease_ShouldDesaturateColor()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(255, 0, 0),   // pure red (100% saturation)
            };

            // Act - decrease saturation by 100%
            var result = transformer.ApplyHslShift(palette, 0, -100, 0, new[] { 0 });

            // Assert - fully desaturated red should be gray (R=G=B)
            Assert.Equal(result[0].R, result[0].G);
            Assert.Equal(result[0].G, result[0].B);
        }

        [Fact]
        public void ApplyHslShift_WithEmptyIndices_ShouldReturnUnmodifiedPalette()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(255, 0, 0),
                Color.FromArgb(0, 255, 0),
            };

            // Act - no indices to modify
            var result = transformer.ApplyHslShift(palette, 180, 50, 50, new int[] { });

            // Assert - all colors unchanged
            Assert.Equal(255, result[0].R);
            Assert.Equal(0, result[0].G);
            Assert.Equal(0, result[0].B);
            Assert.Equal(0, result[1].R);
            Assert.Equal(255, result[1].G);
            Assert.Equal(0, result[1].B);
        }

        [Fact]
        public void ApplyHslShift_WithNegativeHueShift_ShouldWrapCorrectly()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(0, 255, 255),   // cyan (hue 180)
            };

            // Act - shift hue by -180 degrees (should wrap to red)
            var result = transformer.ApplyHslShift(palette, -180, 0, 0, new[] { 0 });

            // Assert - cyan shifted -180 degrees should be red (255, 0, 0)
            Assert.Equal(255, result[0].R);
            Assert.Equal(0, result[0].G);
            Assert.Equal(0, result[0].B);
        }

        [Fact]
        public void ApplyHslShift_WithLightnessDecrease_ShouldMakeColorDarker()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(200, 100, 100),   // light red
            };

            // Act - decrease lightness by 50%
            var result = transformer.ApplyHslShift(palette, 0, 0, -50, new[] { 0 });

            // Assert - color should be darker (lower RGB values)
            Assert.True(result[0].R < 200);
        }

        [Fact]
        public void ApplyHslShift_WithSaturationIncrease_ShouldSaturateColor()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(180, 128, 128),   // desaturated red (gray-ish)
            };

            // Act - increase saturation by 50%
            var result = transformer.ApplyHslShift(palette, 0, 50, 0, new[] { 0 });

            // Assert - color should be more saturated (R should increase relative to G/B)
            Assert.True(result[0].R > result[0].G);
        }

        [Fact]
        public void ApplyHslShift_WithNegativeIndices_ShouldIgnoreThem()
        {
            // Arrange
            var transformer = new RamzaPaletteTransformer();
            var palette = new Color[]
            {
                Color.FromArgb(255, 0, 0),
                Color.FromArgb(0, 255, 0),
            };

            // Act - include negative indices
            var result = transformer.ApplyHslShift(palette, 180, 0, 0, new[] { -1, 0, -5 });

            // Assert - only index 0 modified, negative indices ignored
            Assert.Equal(0, result[0].R);    // red shifted to cyan
            Assert.Equal(255, result[0].G);
            Assert.Equal(255, result[0].B);
            Assert.Equal(0, result[1].R);    // green unchanged
            Assert.Equal(255, result[1].G);
            Assert.Equal(0, result[1].B);
        }
    }
}
