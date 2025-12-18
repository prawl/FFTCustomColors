using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;
using Xunit;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    public class PreviewCarouselMouseWheelTests : IDisposable
    {
        private readonly PreviewCarousel _carousel;

        public PreviewCarouselMouseWheelTests()
        {
            _carousel = new PreviewCarousel();
        }

        [Fact]
        public void MouseWheel_ScrollUp_Should_Show_Next_View()
        {
            // Arrange
            var images = new[]
            {
                new Bitmap(64, 64), // SW
                new Bitmap(64, 64), // SE
                new Bitmap(64, 64), // NE
                new Bitmap(64, 64)  // NW
            };
            _carousel.SetImages(images);

            // Act - Scroll up (positive delta)
            var wheelArgs = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120);
            _carousel.SimulateMouseWheel(wheelArgs);

            // Assert
            _carousel.CurrentViewIndex.Should().Be(1, "Scrolling up should move to next view");
            _carousel.Image.Should().Be(images[1]);

            // Cleanup
            foreach (var img in images) img.Dispose();
        }

        [Fact]
        public void MouseWheel_ScrollDown_Should_Show_Previous_View()
        {
            // Arrange
            var images = new[]
            {
                new Bitmap(64, 64), // SW
                new Bitmap(64, 64), // SE
                new Bitmap(64, 64), // NE
                new Bitmap(64, 64)  // NW
            };
            _carousel.SetImages(images);
            _carousel.NextView(); // Start at index 1

            // Act - Scroll down (negative delta)
            var wheelArgs = new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120);
            _carousel.SimulateMouseWheel(wheelArgs);

            // Assert
            _carousel.CurrentViewIndex.Should().Be(0, "Scrolling down should move to previous view");
            _carousel.Image.Should().Be(images[0]);

            // Cleanup
            foreach (var img in images) img.Dispose();
        }

        [Fact]
        public void MouseWheel_Should_Wrap_Around_At_Boundaries()
        {
            // Arrange
            var images = new[]
            {
                new Bitmap(64, 64), // 0
                new Bitmap(64, 64), // 1
                new Bitmap(64, 64)  // 2
            };
            _carousel.SetImages(images);

            // Act & Assert - Scroll down from first image should wrap to last
            var scrollDown = new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120);
            _carousel.SimulateMouseWheel(scrollDown);
            _carousel.CurrentViewIndex.Should().Be(2, "Scrolling down from first should wrap to last");

            // Act & Assert - Scroll up from last image should wrap to first
            var scrollUp = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120);
            _carousel.SimulateMouseWheel(scrollUp);
            _carousel.CurrentViewIndex.Should().Be(0, "Scrolling up from last should wrap to first");

            // Cleanup
            foreach (var img in images) img.Dispose();
        }

        [Fact]
        public void MouseWheel_Should_Not_Navigate_With_Single_Image()
        {
            // Arrange
            var image = new Bitmap(64, 64);
            _carousel.SetImages(new[] { image });

            // Act - Try to scroll
            var wheelArgs = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120);
            _carousel.SimulateMouseWheel(wheelArgs);

            // Assert
            _carousel.CurrentViewIndex.Should().Be(0, "Should not navigate with single image");
            _carousel.Image.Should().Be(image);

            // Cleanup
            image.Dispose();
        }

        [Fact]
        public void MouseWheel_Should_Not_Navigate_With_No_Images()
        {
            // Arrange
            _carousel.SetImages(new Image[0]);

            // Act - Try to scroll
            var wheelArgs = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120);
            _carousel.SimulateMouseWheel(wheelArgs);

            // Assert
            _carousel.CurrentViewIndex.Should().Be(0, "Should remain at 0 with no images");
            _carousel.Image.Should().BeNull();
        }

        [Fact]
        public void MouseWheel_Multiple_Scrolls_Should_Navigate_Multiple_Times()
        {
            // Arrange
            var images = new[]
            {
                new Bitmap(64, 64), // 0
                new Bitmap(64, 64), // 1
                new Bitmap(64, 64), // 2
                new Bitmap(64, 64)  // 3
            };
            _carousel.SetImages(images);

            // Act - Scroll up three times
            var scrollUp = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120);
            _carousel.SimulateMouseWheel(scrollUp); // 0 -> 1
            _carousel.SimulateMouseWheel(scrollUp); // 1 -> 2
            _carousel.SimulateMouseWheel(scrollUp); // 2 -> 3

            // Assert
            _carousel.CurrentViewIndex.Should().Be(3, "Three scrolls up should move to index 3");
            _carousel.Image.Should().Be(images[3]);

            // Cleanup
            foreach (var img in images) img.Dispose();
        }

        [Fact]
        public void MouseWheel_Should_Handle_Large_Delta_Values()
        {
            // Arrange - Some mice have different delta values
            var images = new[]
            {
                new Bitmap(64, 64),
                new Bitmap(64, 64),
                new Bitmap(64, 64)
            };
            _carousel.SetImages(images);

            // Act - Large positive delta (some mice use 240 or more)
            var largeScrollUp = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 240);
            _carousel.SimulateMouseWheel(largeScrollUp);

            // Assert - Should still only move one step
            _carousel.CurrentViewIndex.Should().Be(1, "Large delta should still move only one step");

            // Act - Small positive delta (some mice use smaller values)
            var smallScrollUp = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 60);
            _carousel.SimulateMouseWheel(smallScrollUp);

            // Assert - Should still move if delta is positive
            _carousel.CurrentViewIndex.Should().Be(2, "Small positive delta should still navigate");

            // Cleanup
            foreach (var img in images) img.Dispose();
        }

        [Fact]
        public void OnMouseWheel_Protected_Method_Should_Be_Overridable()
        {
            // This test verifies that the carousel properly overrides OnMouseWheel
            // The method should exist and be callable through SimulateMouseWheel
            var method = typeof(PreviewCarousel).GetMethod("OnMouseWheel",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            method.Should().NotBeNull("OnMouseWheel should be overridden");
        }

        public void Dispose()
        {
            _carousel?.Dispose();
        }
    }
}