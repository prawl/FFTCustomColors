using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// A picture box control with carousel functionality to cycle through multiple preview images
    /// </summary>
    public class PreviewCarousel : PictureBox
    {
        private Image[] _images = new Image[0];
        private readonly Color _arrowColor = Color.FromArgb(200, 255, 255, 255);
        private readonly Color _arrowHoverColor = Color.FromArgb(255, 255, 255, 255);
        private readonly Color _arrowShadow = Color.FromArgb(100, 0, 0, 0);
        private bool _mouseIsOver = false;

        public int CurrentViewIndex { get; private set; } = 0;

        public int ImageCount => _images.Length;

        public bool ShowNavigationArrows { get; private set; } = true;  // Always show arrows

        public bool SupportsNavigation => _images.Length > 1;

        public PreviewCarousel()
        {
            // Track when mouse enters/leaves the control
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
        }

        private void OnMouseEnter(object sender, EventArgs e)
        {
            _mouseIsOver = true;
        }

        private void OnMouseLeave(object sender, EventArgs e)
        {
            _mouseIsOver = false;
        }

        public void SetImages(Image[] images)
        {
            // Store the current orientation before changing images
            var previousIndex = CurrentViewIndex;
            _images = images ?? new Image[0];

            if (_images.Length > 0)
            {
                // Preserve orientation if possible, otherwise reset to 0
                if (previousIndex < _images.Length)
                {
                    // Previous index is still valid, preserve it
                    CurrentViewIndex = previousIndex;
                    this.Image = _images[CurrentViewIndex];
                }
                else
                {
                    // Previous index is out of bounds, reset to 0
                    CurrentViewIndex = 0;
                    this.Image = _images[0];
                }

                // Log for debugging
                System.Diagnostics.Debug.WriteLine($"PreviewCarousel: Set {_images.Length} images. CurrentIndex: {CurrentViewIndex}, SupportsNavigation: {SupportsNavigation}");

                // Check if images are actually different
                for (int i = 0; i < _images.Length; i++)
                {
                    if (_images[i] != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Image[{i}]: Size={_images[i].Size}, Hash={_images[i].GetHashCode()}");
                    }
                }
            }
            else
            {
                // No images, reset index
                CurrentViewIndex = 0;
                this.Image = null;
            }
        }

        public void NextView()
        {
            if (_images.Length > 0)
            {
                var oldIndex = CurrentViewIndex;
                CurrentViewIndex = (CurrentViewIndex + 1) % _images.Length;
                this.Image = _images[CurrentViewIndex];
                Invalidate(); // Force repaint
                System.Diagnostics.Debug.WriteLine($"[CAROUSEL] NextView: {oldIndex} -> {CurrentViewIndex}, Total: {_images.Length}");
            }
        }

        public void PreviousView()
        {
            if (_images.Length > 0)
            {
                var oldIndex = CurrentViewIndex;
                CurrentViewIndex = (CurrentViewIndex - 1 + _images.Length) % _images.Length;
                this.Image = _images[CurrentViewIndex];
                Invalidate(); // Force repaint
                System.Diagnostics.Debug.WriteLine($"[CAROUSEL] PreviousView: {oldIndex} -> {CurrentViewIndex}, Total: {_images.Length}");
            }
        }

        /// <summary>
        /// Shows navigation arrows (compatibility method - arrows are always visible)
        /// </summary>
        public void ShowArrows()
        {
            // Arrows are always visible in current implementation
            // This method exists for backward compatibility with tests
        }

        /// <summary>
        /// Hides navigation arrows (compatibility method - arrows are always visible)
        /// </summary>
        public void HideArrows()
        {
            // Arrows are always visible in current implementation
            // This method exists for backward compatibility with tests
        }

        public void HandleArrowClick(int clickX, int controlWidth)
        {
            System.Diagnostics.Debug.WriteLine($"[CAROUSEL] HandleArrowClick: X={clickX}, Width={controlWidth}, Images={_images.Length}");

            if (_images.Length <= 1)
            {
                System.Diagnostics.Debug.WriteLine($"[CAROUSEL] Not enough images to navigate");
                return;
            }

            // If click is on left third of control, go previous
            // If click is on right third of control, go next
            if (clickX < controlWidth / 3)
            {
                System.Diagnostics.Debug.WriteLine($"[CAROUSEL] Left third clicked");
                PreviousView();
            }
            else if (clickX > controlWidth * 2 / 3)
            {
                System.Diagnostics.Debug.WriteLine($"[CAROUSEL] Right third clicked");
                NextView();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[CAROUSEL] Middle third clicked - no action");
            }
        }

        public void SimulateMouseClick(System.Windows.Forms.MouseEventArgs e)
        {
            OnMouseClick(e);
        }


        protected override void OnMouseClick(System.Windows.Forms.MouseEventArgs e)
        {
            base.OnMouseClick(e);

            // Only respond to left mouse button clicks
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                HandleArrowClick(e.X, this.Width);
            }
        }

        // Mouse wheel scrolling removed - caused conflicts with form scrolling

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Debug output
            System.Diagnostics.Debug.WriteLine($"PreviewCarousel OnPaint: SupportsNavigation={SupportsNavigation}, ShowArrows={ShowNavigationArrows}, Images={_images.Length}");

            // Only draw arrows if we support navigation and should show them
            if (!SupportsNavigation || !ShowNavigationArrows)
            {
                System.Diagnostics.Debug.WriteLine($"PreviewCarousel OnPaint: Not drawing arrows (support={SupportsNavigation}, show={ShowNavigationArrows})");
                return;
            }

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Calculate arrow positions
            int arrowSize = 14;  // Smaller arrows (was 20)
            int margin = 8;
            int arrowY = (this.Height - arrowSize) / 2;

            // Draw left arrow (triangle pointing left)
            DrawLeftArrow(g, margin, arrowY, arrowSize);

            // Draw right arrow (triangle pointing right)
            DrawRightArrow(g, this.Width - margin - arrowSize, arrowY, arrowSize);

            // Removed view indicator dots - no longer needed
        }

        private void DrawLeftArrow(Graphics g, int x, int y, int size)
        {
            Point[] leftArrow = new Point[]
            {
                new Point(x, y + size / 2),          // Left point
                new Point(x + size, y),              // Top right
                new Point(x + size, y + size)        // Bottom right
            };

            // Draw shadow
            using (var shadowBrush = new SolidBrush(_arrowShadow))
            {
                var shadowArrow = new Point[]
                {
                    new Point(leftArrow[0].X + 1, leftArrow[0].Y + 1),
                    new Point(leftArrow[1].X + 1, leftArrow[1].Y + 1),
                    new Point(leftArrow[2].X + 1, leftArrow[2].Y + 1)
                };
                g.FillPolygon(shadowBrush, shadowArrow);
            }

            // Draw arrow
            using (var brush = new SolidBrush(_arrowColor))
            {
                g.FillPolygon(brush, leftArrow);
            }
        }

        private void DrawRightArrow(Graphics g, int x, int y, int size)
        {
            Point[] rightArrow = new Point[]
            {
                new Point(x, y),                     // Top left
                new Point(x + size, y + size / 2),   // Right point
                new Point(x, y + size)               // Bottom left
            };

            // Draw shadow
            using (var shadowBrush = new SolidBrush(_arrowShadow))
            {
                var shadowArrow = new Point[]
                {
                    new Point(rightArrow[0].X + 1, rightArrow[0].Y + 1),
                    new Point(rightArrow[1].X + 1, rightArrow[1].Y + 1),
                    new Point(rightArrow[2].X + 1, rightArrow[2].Y + 1)
                };
                g.FillPolygon(shadowBrush, shadowArrow);
            }

            // Draw arrow
            using (var brush = new SolidBrush(_arrowColor))
            {
                g.FillPolygon(brush, rightArrow);
            }
        }

        // DrawViewIndicator method removed - dots are no longer displayed
        // The carousel now shows only the navigation arrows when hovering
    }
}