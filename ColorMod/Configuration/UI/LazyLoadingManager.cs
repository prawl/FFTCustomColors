using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Manages lazy loading for multiple PreviewCarousel controls
    /// </summary>
    public class LazyLoadingManager
    {
        private readonly ScrollableControl _container;
        private readonly int _maxImmediateLoad;
        private readonly List<PreviewCarousel> _carousels = new List<PreviewCarousel>();
        private readonly Dictionary<PreviewCarousel, LazyImageLoader> _loaders = new Dictionary<PreviewCarousel, LazyImageLoader>();

        public event EventHandler VisibilityChecked;

        public LazyLoadingManager(ScrollableControl container, int maxImmediateLoad = 5)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _maxImmediateLoad = maxImmediateLoad;

            // Hook up scroll event
            _container.Scroll += OnContainerScroll;

            // Also hook up mouse wheel event for better scroll detection
            _container.MouseWheel += (s, e) => CheckAllCarouselsVisibility();

            // Hook up paint event as a fallback
            _container.Paint += (s, e) => CheckAllCarouselsVisibility();
        }

        /// <summary>
        /// Registers a carousel for lazy loading management
        /// </summary>
        public void RegisterCarousel(PreviewCarousel carousel)
        {
            if (carousel == null || _carousels.Contains(carousel))
                return;

            _carousels.Add(carousel);

            // Create a lazy loader for this carousel
            var loader = new LazyImageLoader(carousel, _container);
            _loaders[carousel] = loader;

            // Set up the paint event handler for lazy loading
            carousel.Paint += (s, e) =>
            {
                if (!carousel.HasImagesLoaded)
                {
                    loader.CheckVisibility();
                }
            };
        }

        /// <summary>
        /// Initializes the manager and marks first N carousels for immediate loading
        /// </summary>
        public void Initialize()
        {
            ModLogger.Log($"[LAZY] Initializing LazyLoadingManager with {_carousels.Count} carousels, immediate load: {_maxImmediateLoad}");

            for (int i = 0; i < _carousels.Count; i++)
            {
                var carousel = _carousels[i];
                carousel.ShouldLoadImmediately = i < _maxImmediateLoad;

                // Load the first N immediately
                if (carousel.ShouldLoadImmediately && _loaders.TryGetValue(carousel, out var loader))
                {
                    ModLogger.Log($"[LAZY] Loading carousel {i} immediately");
                    loader.LoadImmediately();
                }
            }
        }

        /// <summary>
        /// Handles scroll events to check carousel visibility
        /// </summary>
        private void OnContainerScroll(object sender, ScrollEventArgs e)
        {
            VisibilityChecked?.Invoke(this, EventArgs.Empty);

            // Use a timer to debounce scroll events for better performance
            if (_scrollTimer == null)
            {
                _scrollTimer = new System.Windows.Forms.Timer();
                _scrollTimer.Interval = 100; // Check every 100ms during scrolling
                _scrollTimer.Tick += (s, args) =>
                {
                    CheckAllCarouselsVisibility();
                };
            }

            _scrollTimer.Stop();
            _scrollTimer.Start();

            // Also do an immediate check for responsiveness
            CheckAllCarouselsVisibility();
        }

        private System.Windows.Forms.Timer _scrollTimer;

        /// <summary>
        /// Checks all registered carousels for visibility and loads as needed
        /// </summary>
        public void CheckAllCarouselsVisibility()
        {
            foreach (var carousel in _carousels)
            {
                if (!carousel.HasImagesLoaded && _loaders.TryGetValue(carousel, out var loader))
                {
                    loader.CheckVisibility();
                }
            }
        }

        /// <summary>
        /// Disposes of the manager and unhooks events
        /// </summary>
        public void Dispose()
        {
            if (_container != null)
            {
                _container.Scroll -= OnContainerScroll;
                // MouseWheel is handled by lambda, can't unhook it directly
            }
            if (_scrollTimer != null)
            {
                _scrollTimer.Stop();
                _scrollTimer.Dispose();
                _scrollTimer = null;
            }
            _loaders.Clear();
            _carousels.Clear();
        }
    }
}