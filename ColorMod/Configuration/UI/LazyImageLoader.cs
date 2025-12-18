using System;
using System.Drawing;
using System.Windows.Forms;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Manages lazy loading of images for PreviewCarousel controls
    /// </summary>
    public class LazyImageLoader
    {
        private readonly PreviewCarousel _carousel;
        private readonly ScrollableControl _container;
        private bool _hasLoaded = false;

        public event EventHandler ImageLoadRequested;

        public LazyImageLoader(PreviewCarousel carousel, ScrollableControl container)
        {
            _carousel = carousel ?? throw new ArgumentNullException(nameof(carousel));
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        /// <summary>
        /// Checks if the carousel is visible in the viewport and loads images if needed
        /// </summary>
        public void CheckVisibility()
        {
            if (_hasLoaded || _carousel.HasImagesLoaded) return;

            if (IsInViewport())
            {
                ModLogger.Log($"[LAZY] Loading images for carousel at Y={_carousel.Location.Y}");
                _hasLoaded = true;
                _carousel.HasImagesLoaded = true;
                ImageLoadRequested?.Invoke(this, EventArgs.Empty);

                // Trigger the callback if set
                _carousel.LoadImagesCallback?.Invoke(_carousel);
            }
        }

        /// <summary>
        /// Determines if the carousel is currently visible in the container's viewport
        /// </summary>
        private bool IsInViewport()
        {
            if (_carousel.Parent == null || !_carousel.Visible)
                return false;

            try
            {
                // Get the carousel's position in its parent (usually _mainPanel)
                Rectangle carouselBounds = _carousel.Bounds;

                // Get the container's client rectangle (visible area)
                Rectangle containerRect = _container.ClientRectangle;

                // Adjust carousel bounds based on scroll position
                carouselBounds.Offset(_container.AutoScrollPosition);

                // Add buffer for smoother loading
                int buffer = 150;
                containerRect.Inflate(buffer, buffer);

                // Check if carousel intersects with visible area
                bool isVisible = containerRect.IntersectsWith(carouselBounds);

                // Special check for partially visible items at the top
                if (!isVisible && carouselBounds.Bottom > 0 && carouselBounds.Top < containerRect.Height)
                {
                    isVisible = true;
                }

                return isVisible;
            }
            catch
            {
                // If we can't determine visibility, default to not loading
                return false;
            }
        }

        /// <summary>
        /// Forces loading of images regardless of visibility
        /// </summary>
        public void LoadImmediately()
        {
            if (_hasLoaded || _carousel.HasImagesLoaded) return;

            _hasLoaded = true;
            _carousel.HasImagesLoaded = true;
            ImageLoadRequested?.Invoke(this, EventArgs.Empty);
            _carousel.LoadImagesCallback?.Invoke(_carousel);
        }
    }
}