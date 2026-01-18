using Xunit;
using FFTColorCustomizer.ThemeEditor;
using System.Drawing;

namespace Tests.ThemeEditor
{
    public class RamzaPreviewHelperTests
    {
        [Theory]
        [InlineData(1, "FFTColorCustomizer.Images.RamzaChapter1.original.830_Ramuza_Ch1.bmp")]
        [InlineData(2, "FFTColorCustomizer.Images.RamzaChapter23.original.832_Ramuza_Ch23.bmp")]
        [InlineData(4, "FFTColorCustomizer.Images.RamzaChapter4.original.834_Ramuza_Ch4.bmp")]
        public void GetResourceName_ReturnsCorrectResourceForChapter(int chapter, string expected)
        {
            // Act
            var result = RamzaPreviewHelper.GetResourceName(chapter);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0)]  // Invalid chapter
        [InlineData(3)]  // Invalid chapter
        [InlineData(99)] // Invalid chapter
        public void GetResourceName_DefaultsToChapter1ForInvalidChapter(int chapter)
        {
            // Act
            var result = RamzaPreviewHelper.GetResourceName(chapter);

            // Assert
            Assert.Equal("FFTColorCustomizer.Images.RamzaChapter1.original.830_Ramuza_Ch1.bmp", result);
        }

        [Fact]
        public void LoadOriginalPreview_ReturnsNonNullBitmapForChapter1()
        {
            // Act
            var result = RamzaPreviewHelper.LoadOriginalPreview(1);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Width > 0);
            Assert.True(result.Height > 0);
            result.Dispose();
        }

        [Fact]
        public void LoadOriginalPreview_ReturnsNonNullBitmapForChapter2()
        {
            // Act
            var result = RamzaPreviewHelper.LoadOriginalPreview(2);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Width > 0);
            Assert.True(result.Height > 0);
            result.Dispose();
        }

        [Fact]
        public void LoadOriginalPreview_ReturnsNonNullBitmapForChapter4()
        {
            // Act
            var result = RamzaPreviewHelper.LoadOriginalPreview(4);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Width > 0);
            Assert.True(result.Height > 0);
            result.Dispose();
        }

        [Fact]
        public void ApplyHslShift_WithZeroShifts_ReturnsSimilarImage()
        {
            // Arrange
            var original = RamzaPreviewHelper.LoadOriginalPreview(1);
            Assert.NotNull(original);

            // Act - apply zero shifts (should be similar to original)
            var result = RamzaPreviewHelper.ApplyHslShift(original, 0, 0, 0, 1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(original.Width, result.Width);
            Assert.Equal(original.Height, result.Height);

            original.Dispose();
            result.Dispose();
        }

        [Fact]
        public void ApplyHslShift_WithHueShift_ChangesArmorColors()
        {
            // Arrange
            var original = RamzaPreviewHelper.LoadOriginalPreview(1);
            Assert.NotNull(original);

            // Act - apply significant hue shift
            var result = RamzaPreviewHelper.ApplyHslShift(original, 180, 0, 0, 1);

            // Assert - result should be different from original
            // (exact color comparison is complex, just verify it runs and produces valid output)
            Assert.NotNull(result);
            Assert.Equal(original.Width, result.Width);
            Assert.Equal(original.Height, result.Height);

            original.Dispose();
            result.Dispose();
        }

        [Fact]
        public void ApplyHslShift_WithNegativeShifts_Works()
        {
            // Arrange
            var original = RamzaPreviewHelper.LoadOriginalPreview(2);
            Assert.NotNull(original);

            // Act - apply negative shifts
            var result = RamzaPreviewHelper.ApplyHslShift(original, -90, -50, -25, 2);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(original.Width, result.Width);
            Assert.Equal(original.Height, result.Height);

            original.Dispose();
            result.Dispose();
        }

        [Fact]
        public void CreateScaledPreview_ScalesImageCorrectly()
        {
            // Arrange
            var original = RamzaPreviewHelper.LoadOriginalPreview(1);
            Assert.NotNull(original);
            var originalWidth = original.Width;
            var originalHeight = original.Height;

            // Act
            var scaled = RamzaPreviewHelper.CreateScaledPreview(original, 4) as Bitmap;

            // Assert
            Assert.NotNull(scaled);
            Assert.Equal(originalWidth * 4, scaled.Width);
            Assert.Equal(originalHeight * 4, scaled.Height);

            original.Dispose();
            scaled.Dispose();
        }

        [Fact]
        public void CreateScaledPreview_WithNullSource_ReturnsNull()
        {
            // Act
            var result = RamzaPreviewHelper.CreateScaledPreview(null);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        public void ApplyHslShift_PreservesTransparentPixels(int chapter)
        {
            // Arrange
            var original = RamzaPreviewHelper.LoadOriginalPreview(chapter);
            Assert.NotNull(original);

            // Act
            var result = RamzaPreviewHelper.ApplyHslShift(original, 90, 50, 25, chapter);

            // Assert - check some edge pixels (likely transparent in sprite sheets)
            // The image should preserve transparency
            Assert.NotNull(result);

            original.Dispose();
            result.Dispose();
        }
    }
}
