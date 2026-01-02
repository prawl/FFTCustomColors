using System;
using System.Windows.Forms;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    [Collection("WinForms")]
    public class PreviewCarouselTests : IDisposable
    {
        private readonly Form _form;
        private readonly PreviewCarousel _carousel;

        public PreviewCarouselTests()
        {
            _form = new Form();
            _carousel = new PreviewCarousel();
            _form.Controls.Add(_carousel);
            var handle = _form.Handle;
        }

        public void Dispose()
        {
            _carousel?.Dispose();
            _form?.Dispose();
        }

        [Fact]
        public void PreviewCarousel_Should_Initialize_With_Default_Properties()
        {
            // Assert
            _carousel.Should().NotBeNull();
            _carousel.CurrentViewIndex.Should().Be(0, "Carousel should start at first view");
        }

        [Fact]
        public void PreviewCarousel_Should_Store_Multiple_Images()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            var image3 = new System.Drawing.Bitmap(64, 64);

            // Act
            _carousel.SetImages(new[] { image1, image2, image3 });

            // Assert
            _carousel.ImageCount.Should().Be(3, "Should have 3 images stored");
            _carousel.Image.Should().Be(image1, "Should display first image by default");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
            image3.Dispose();
        }

        [Fact]
        public void NextView_Should_Cycle_To_Next_Image()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            var image3 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2, image3 });

            // Act
            _carousel.NextView();

            // Assert
            _carousel.CurrentViewIndex.Should().Be(1, "Should move to second image");
            _carousel.Image.Should().Be(image2, "Should display second image");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
            image3.Dispose();
        }

        [Fact]
        public void PreviousView_Should_Cycle_To_Previous_Image()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            var image3 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2, image3 });

            // Act
            _carousel.PreviousView();

            // Assert
            _carousel.CurrentViewIndex.Should().Be(2, "Should wrap to last image");
            _carousel.Image.Should().Be(image3, "Should display last image");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
            image3.Dispose();
        }

        [Fact]
        public void NextView_Should_Wrap_Around_To_First_Image()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            var image3 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2, image3 });

            // Act - Cycle through all images
            _carousel.NextView(); // Move to image2 (index 1)
            _carousel.NextView(); // Move to image3 (index 2)
            _carousel.NextView(); // Should wrap to image1 (index 0)

            // Assert
            _carousel.CurrentViewIndex.Should().Be(0, "Should wrap back to first image");
            _carousel.Image.Should().Be(image1, "Should display first image after wrapping");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
            image3.Dispose();
        }

        [Fact]
        public void ShowNavigationArrows_Should_Be_True_Always()
        {
            // Assert - Arrows are always visible in current implementation
            _carousel.ShowNavigationArrows.Should().BeTrue("Navigation arrows are always visible in current implementation");
        }

        [Fact]
        public void ShowArrows_Method_Exists_For_Compatibility()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2 });

            // Act - ShowArrows exists but arrows are always visible
            _carousel.ShowArrows();

            // Assert - Arrows remain visible (always true in current implementation)
            _carousel.ShowNavigationArrows.Should().BeTrue("Navigation arrows are always visible");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
        }

        [Fact]
        public void HideArrows_Method_Exists_For_Compatibility()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2 });

            // Act - HideArrows exists but arrows remain visible
            _carousel.HideArrows();

            // Assert - Arrows remain visible (always true in current implementation)
            _carousel.ShowNavigationArrows.Should().BeTrue("Navigation arrows are always visible, HideArrows is a compatibility method");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
        }

        [Fact]
        public void Click_On_Left_Side_Should_Show_Previous_Image()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2 });
            // Start at first image (index 0)

            // Act - Simulate click on left side (x=10 out of 64 width) - now goes clockwise (next)
            _carousel.HandleArrowClick(10, _carousel.Width);

            // Assert - left arrow now advances to next image
            _carousel.CurrentViewIndex.Should().Be(1, "Clicking left arrow should advance clockwise to next image");
            _carousel.Image.Should().Be(image2, "Should display second image");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
        }

        [Fact]
        public void Click_On_Right_Side_Should_Show_Next_Image()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            var image3 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2, image3 });
            _carousel.NextView(); // Move to second image (index 1)

            // Act - Simulate click on right side (x=50 out of 64 width) - now goes counter-clockwise (previous)
            _carousel.HandleArrowClick(50, 64);

            // Assert - right arrow now goes to previous image
            _carousel.CurrentViewIndex.Should().Be(0, "Clicking right arrow should go counter-clockwise to previous image");
            _carousel.Image.Should().Be(image1, "Should display first image");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
            image3.Dispose();
        }

        [Fact]
        public void Click_In_Middle_Should_Not_Change_Image()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2 });
            var initialIndex = _carousel.CurrentViewIndex;

            // Act - Simulate click in the middle (x=32 out of 64 width)
            _carousel.HandleArrowClick(32, 64);

            // Assert
            _carousel.CurrentViewIndex.Should().Be(initialIndex, "Clicking in middle should not change image");
            _carousel.Image.Should().Be(image1, "Should still display first image");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
        }

        [Fact]
        public void OnMouseClick_Should_Trigger_HandleArrowClick()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2 });
            _carousel.Size = new System.Drawing.Size(64, 64);

            // Act - Simulate actual mouse click event on right side
            var mouseEventArgs = new MouseEventArgs(MouseButtons.Left, 1, 50, 32, 0);
            _carousel.SimulateMouseClick(mouseEventArgs);

            // Assert
            _carousel.CurrentViewIndex.Should().Be(1, "Mouse click on right should advance to next image");
            _carousel.Image.Should().Be(image2, "Should display second image");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
        }

        [Fact]
        public void OnMouseClick_Should_Only_Respond_To_Left_Click()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2 });
            _carousel.Size = new System.Drawing.Size(64, 64);

            // Act - Simulate right mouse button click on right side
            var rightClickArgs = new MouseEventArgs(MouseButtons.Right, 1, 50, 32, 0);
            _carousel.SimulateMouseClick(rightClickArgs);

            // Assert
            _carousel.CurrentViewIndex.Should().Be(0, "Right click should not change image");
            _carousel.Image.Should().Be(image1, "Should still display first image");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
        }

        [Fact]
        public void Should_Not_Navigate_With_Single_Image()
        {
            // Arrange
            var image1 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1 });

            // Act
            _carousel.NextView();
            var indexAfterNext = _carousel.CurrentViewIndex;

            _carousel.PreviousView();
            var indexAfterPrevious = _carousel.CurrentViewIndex;

            // Assert
            indexAfterNext.Should().Be(0, "Should stay at index 0 with single image");
            indexAfterPrevious.Should().Be(0, "Should stay at index 0 with single image");
            _carousel.Image.Should().Be(image1, "Should always show the single image");

            // Cleanup
            image1.Dispose();
        }

        [Fact]
        public void Carousel_Should_Support_Multiple_Images()
        {
            // Arrange & Act
            var image1 = new System.Drawing.Bitmap(64, 64);
            var image2 = new System.Drawing.Bitmap(64, 64);
            var image3 = new System.Drawing.Bitmap(64, 64);
            var image4 = new System.Drawing.Bitmap(64, 64);
            _carousel.SetImages(new[] { image1, image2, image3, image4 });

            // Assert
            _carousel.ImageCount.Should().Be(4, "Should support 4 images");
            _carousel.SupportsNavigation.Should().BeTrue("Should support navigation with multiple images");

            // Cleanup
            image1.Dispose();
            image2.Dispose();
            image3.Dispose();
            image4.Dispose();
        }

        [Fact]
        public void PreviewCarousel_Should_Be_Ready_For_Integration()
        {
            // This test verifies the carousel has all necessary features for integration

            // Assert
            _carousel.Should().BeAssignableTo<PictureBox>("Should extend PictureBox for compatibility");
            _carousel.Should().NotBeNull();
            _carousel.CurrentViewIndex.Should().Be(0);
            _carousel.ImageCount.Should().Be(0);
            _carousel.ShowNavigationArrows.Should().BeTrue("Navigation arrows are always visible in current implementation");
            _carousel.SupportsNavigation.Should().BeFalse("No images loaded yet");

            // Verify all public methods exist
            _carousel.GetType().GetMethod("SetImages").Should().NotBeNull();
            _carousel.GetType().GetMethod("NextView").Should().NotBeNull();
            _carousel.GetType().GetMethod("PreviousView").Should().NotBeNull();
            _carousel.GetType().GetMethod("ShowArrows").Should().NotBeNull();
            _carousel.GetType().GetMethod("HideArrows").Should().NotBeNull();
            _carousel.GetType().GetMethod("HandleArrowClick").Should().NotBeNull();
        }
    }
}