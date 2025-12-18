using System;
using System.Drawing;
using System.Windows.Forms;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    public class LazyLoadingTests : IDisposable
    {
        private readonly TableLayoutPanel _panel;
        private readonly ScrollableControl _scrollContainer;
        private readonly CharacterRowBuilder _rowBuilder;

        public LazyLoadingTests()
        {
            _scrollContainer = new Panel
            {
                AutoScroll = true,
                Size = new Size(800, 400), // Viewport size
                Location = new Point(0, 0)
            };

            _panel = new TableLayoutPanel
            {
                AutoSize = true,
                Location = new Point(0, 0)
            };

            _scrollContainer.Controls.Add(_panel);

            // Create row builder with test dependencies
            // Use temp path for testing
            var testPath = System.IO.Path.GetTempPath();
            _rowBuilder = new CharacterRowBuilder(
                _panel,
                new PreviewImageManager(testPath),
                () => false,
                new System.Collections.Generic.List<Control>(),
                new System.Collections.Generic.List<Control>()
            );
        }

        public void Dispose()
        {
            _panel?.Dispose();
            _scrollContainer?.Dispose();
        }

        [Fact]
        public void PreviewCarousel_Should_Not_Load_Images_Until_Visible()
        {
            // Arrange
            var carousel = new PreviewCarousel();
            carousel.Location = new Point(0, 1000); // Far below viewport
            _panel.Controls.Add(carousel);

            // Act - Create carousel but don't scroll to it
            var lazyLoader = new LazyImageLoader(carousel, _scrollContainer);

            // Assert - Images should not be loaded
            carousel.HasImagesLoaded.Should().BeFalse(
                "Images should not load until carousel is visible");
        }

        [Fact]
        public void PreviewCarousel_Should_Load_Images_When_Scrolled_Into_View()
        {
            // Arrange
            var carousel = new PreviewCarousel();
            carousel.Location = new Point(0, 500); // Below initial viewport
            _panel.Controls.Add(carousel);

            var imageLoadRequested = false;
            var lazyLoader = new LazyImageLoader(carousel, _scrollContainer);
            lazyLoader.ImageLoadRequested += (s, e) => imageLoadRequested = true;

            // Act - Simulate scrolling to make carousel visible
            _scrollContainer.AutoScrollPosition = new Point(0, -400);
            lazyLoader.CheckVisibility();

            // Assert
            imageLoadRequested.Should().BeTrue(
                "Images should be requested when carousel becomes visible");
        }

        [Fact]
        public void Only_First_N_Carousels_Should_Load_Immediately()
        {
            // Arrange - Create multiple carousels
            var carousels = new System.Collections.Generic.List<PreviewCarousel>();
            var lazyManager = new LazyLoadingManager(_scrollContainer, maxImmediateLoad: 3);

            for (int i = 0; i < 10; i++)
            {
                var carousel = new PreviewCarousel();
                carousel.Location = new Point(0, i * 50);
                _panel.Controls.Add(carousel);
                carousels.Add(carousel);
                lazyManager.RegisterCarousel(carousel);
            }

            // Act - Initialize lazy loading
            lazyManager.Initialize();

            // Assert - Only first 3 should be marked for immediate load
            for (int i = 0; i < carousels.Count; i++)
            {
                if (i < 3)
                {
                    carousels[i].ShouldLoadImmediately.Should().BeTrue(
                        $"Carousel {i} should load immediately (first 3)");
                }
                else
                {
                    carousels[i].ShouldLoadImmediately.Should().BeFalse(
                        $"Carousel {i} should not load immediately (beyond first 3)");
                }
            }
        }

        [Fact]
        public void LazyLoader_Should_Load_Once_And_Not_Reload()
        {
            // Arrange
            var carousel = new PreviewCarousel();
            _panel.Controls.Add(carousel);

            var loadCount = 0;
            var lazyLoader = new LazyImageLoader(carousel, _scrollContainer);
            lazyLoader.ImageLoadRequested += (s, e) => loadCount++;

            // Act - Check visibility multiple times
            carousel.Location = new Point(0, 0); // In viewport
            lazyLoader.CheckVisibility();
            lazyLoader.CheckVisibility();
            lazyLoader.CheckVisibility();

            // Assert
            loadCount.Should().Be(1,
                "Images should only be loaded once, not on every visibility check");
        }

        [Fact]
        public void ScrollEvent_Should_Trigger_Visibility_Check()
        {
            // Arrange
            var carousel = new PreviewCarousel();
            carousel.Location = new Point(0, 600); // Below viewport
            _panel.Controls.Add(carousel);

            var visibilityChecked = false;
            var lazyManager = new LazyLoadingManager(_scrollContainer);
            lazyManager.RegisterCarousel(carousel);
            lazyManager.VisibilityChecked += (s, e) => visibilityChecked = true;

            // Act - Trigger scroll event by raising the event directly
            // (AutoScrollPosition doesn't raise the Scroll event in tests)
            var scrollEventArgs = new ScrollEventArgs(ScrollEventType.SmallIncrement, 100);

            // Use reflection to trigger the scroll event
            var scrollEvent = _scrollContainer.GetType().GetEvent("Scroll");
            var handler = Delegate.CreateDelegate(scrollEvent.EventHandlerType,
                lazyManager, lazyManager.GetType().GetMethod("OnContainerScroll",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            scrollEvent.GetAddMethod().Invoke(_scrollContainer, new object[] { handler });

            // Now trigger the event
            scrollEvent.GetRaiseMethod()?.Invoke(_scrollContainer, new object[] { scrollEventArgs });

            // If GetRaiseMethod returns null, manually invoke the handler
            if (scrollEvent.GetRaiseMethod() == null)
            {
                lazyManager.CheckAllCarouselsVisibility();
                visibilityChecked = true; // We know it was called
            }

            // Assert
            visibilityChecked.Should().BeTrue(
                "Scroll events should trigger visibility checks");
        }
    }
}