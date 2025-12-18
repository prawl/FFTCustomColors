using System.Drawing;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;
using Xunit;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    public class PreviewCarouselOrientationTests : IDisposable
    {
        private readonly PreviewCarousel _carousel;

        public PreviewCarouselOrientationTests()
        {
            _carousel = new PreviewCarousel();
        }

        [Fact]
        public void Carousel_Should_Preserve_Orientation_When_Images_Change()
        {
            // Arrange - Set up first set of images and navigate to NW (index 3)
            var firstSet = new[]
            {
                new Bitmap(64, 64), // SW
                new Bitmap(64, 64), // SE
                new Bitmap(64, 64), // NE
                new Bitmap(64, 64)  // NW
            };

            _carousel.SetImages(firstSet);

            // Navigate to NW position (index 3)
            _carousel.NextView(); // SW -> SE (index 1)
            _carousel.NextView(); // SE -> NE (index 2)
            _carousel.NextView(); // NE -> NW (index 3)

            _carousel.CurrentViewIndex.Should().Be(3, "Should be at NW position");

            // Act - Change to different theme (new set of images)
            var secondSet = new[]
            {
                new Bitmap(64, 64), // SW (new theme)
                new Bitmap(64, 64), // SE (new theme)
                new Bitmap(64, 64), // NE (new theme)
                new Bitmap(64, 64)  // NW (new theme)
            };

            _carousel.SetImages(secondSet);

            // Assert - Should still be at NW position
            _carousel.CurrentViewIndex.Should().Be(3, "Orientation should be preserved when images change");
            _carousel.Image.Should().Be(secondSet[3], "Should display NW image from new set");

            // Cleanup
            foreach (var img in firstSet) img?.Dispose();
            foreach (var img in secondSet) img?.Dispose();
        }

        [Fact]
        public void Carousel_Should_Handle_Orientation_Preservation_With_Different_Image_Counts()
        {
            // Arrange - Start with 4 images, navigate to index 2
            var firstSet = new[]
            {
                new Bitmap(64, 64), // 0
                new Bitmap(64, 64), // 1
                new Bitmap(64, 64), // 2
                new Bitmap(64, 64)  // 3
            };

            _carousel.SetImages(firstSet);
            _carousel.NextView(); // 0 -> 1
            _carousel.NextView(); // 1 -> 2

            _carousel.CurrentViewIndex.Should().Be(2);

            // Act - Change to set with only 2 images
            var secondSet = new[]
            {
                new Bitmap(64, 64), // 0
                new Bitmap(64, 64)  // 1
            };

            _carousel.SetImages(secondSet);

            // Assert - Should clamp to valid range (index 0 since 2 is out of bounds)
            _carousel.CurrentViewIndex.Should().BeLessThanOrEqualTo(1, "Should clamp to valid range");

            // Cleanup
            foreach (var img in firstSet) img?.Dispose();
            foreach (var img in secondSet) img?.Dispose();
        }

        [Fact]
        public void Carousel_Should_Reset_To_First_Image_When_Null_Or_Empty_Array_Provided()
        {
            // Arrange - Set images and navigate to position 2
            var images = new[]
            {
                new Bitmap(64, 64),
                new Bitmap(64, 64),
                new Bitmap(64, 64)
            };

            _carousel.SetImages(images);
            _carousel.NextView(); // 0 -> 1
            _carousel.NextView(); // 1 -> 2

            // Act & Assert - Set to empty array
            _carousel.SetImages(new Image[0]);
            _carousel.CurrentViewIndex.Should().Be(0, "Should reset to 0 with empty array");

            // Act & Assert - Set to null
            _carousel.SetImages(null);
            _carousel.CurrentViewIndex.Should().Be(0, "Should reset to 0 with null");

            // Cleanup
            foreach (var img in images) img?.Dispose();
        }


        public void Dispose()
        {
            _carousel?.Dispose();
        }
    }
}