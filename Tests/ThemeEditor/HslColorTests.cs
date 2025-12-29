using System.Drawing;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    public class HslColorTests
    {
        [Fact]
        public void FromRgb_WithPureRed_ReturnsCorrectHsl()
        {
            // Arrange
            var color = Color.FromArgb(255, 0, 0);

            // Act
            var hsl = HslColor.FromRgb(color);

            // Assert - Pure red is H=0, S=1.0, L=0.5
            Assert.Equal(0, hsl.H, 1);
            Assert.Equal(1.0, hsl.S, 2);
            Assert.Equal(0.5, hsl.L, 2);
        }

        [Fact]
        public void ToRgb_WithPureRedHsl_ReturnsCorrectRgb()
        {
            // Arrange
            var hsl = new HslColor(0, 1.0, 0.5);

            // Act
            var color = hsl.ToRgb();

            // Assert
            Assert.Equal(255, color.R);
            Assert.Equal(0, color.G);
            Assert.Equal(0, color.B);
        }

        [Fact]
        public void GenerateShades_FromBaseColor_ReturnsShadowBaseHighlight()
        {
            // Arrange - a medium blue
            var baseColor = Color.FromArgb(0, 100, 200);

            // Act
            var shades = HslColor.GenerateShades(baseColor);

            // Assert - shadow should be darker, highlight should be lighter
            Assert.True(shades.Shadow.GetBrightness() < shades.Base.GetBrightness());
            Assert.True(shades.Base.GetBrightness() < shades.Highlight.GetBrightness());
        }
    }
}
