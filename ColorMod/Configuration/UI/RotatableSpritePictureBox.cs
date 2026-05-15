using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Sprite preview box that paints rotation arrows as an overlay on the
    /// picture itself (matching the class-preview carousel pattern). Click
    /// the left third to rotate left; click the right third to rotate right;
    /// the middle third is a dead zone so users can click the sprite without
    /// triggering rotation. Inherits the stone-tile background from
    /// <see cref="StoneTilePictureBox"/>.
    /// </summary>
    public class RotatableSpritePictureBox : StoneTilePictureBox
    {
        private readonly Color _arrowColor = Color.FromArgb(200, 255, 255, 255);
        private readonly Color _arrowShadow = Color.FromArgb(100, 0, 0, 0);

        /// <summary>
        /// Raised when the user clicks the left third of the box.
        /// Subscribe and rotate the sprite to the previous direction.
        /// </summary>
        public event EventHandler? RotateLeftRequested;

        /// <summary>
        /// Raised when the user clicks the right third of the box.
        /// Subscribe and rotate the sprite to the next direction.
        /// </summary>
        public event EventHandler? RotateRightRequested;

        /// <summary>
        /// Test seam: dispatch a click at the given (x, y) without needing
        /// to synthesize a real Windows mouse message.
        /// </summary>
        public void SimulateClickAt(int x, int y)
        {
            DispatchClick(x);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            DispatchClick(e.X);
        }

        private void DispatchClick(int x)
        {
            if (Width <= 0) return;

            if (x < Width / 3)
                RotateLeftRequested?.Invoke(this, EventArgs.Empty);
            else if (x > Width * 2 / 3)
                RotateRightRequested?.Invoke(this, EventArgs.Empty);
            // middle third → ignored (dead zone for inspecting the sprite)
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width <= 0 || Height <= 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int arrowSize = 14;
            const int margin = 8;
            int arrowY = (Height - arrowSize) / 2;

            DrawLeftArrow(g, margin, arrowY, arrowSize);
            DrawRightArrow(g, Width - margin - arrowSize, arrowY, arrowSize);
        }

        private void DrawLeftArrow(Graphics g, int x, int y, int size)
        {
            var pts = new[]
            {
                new Point(x, y + size / 2),
                new Point(x + size, y),
                new Point(x + size, y + size)
            };

            using (var shadowBrush = new SolidBrush(_arrowShadow))
            {
                var shadowPts = new[]
                {
                    new Point(pts[0].X + 1, pts[0].Y + 1),
                    new Point(pts[1].X + 1, pts[1].Y + 1),
                    new Point(pts[2].X + 1, pts[2].Y + 1)
                };
                g.FillPolygon(shadowBrush, shadowPts);
            }
            using (var brush = new SolidBrush(_arrowColor))
            {
                g.FillPolygon(brush, pts);
            }
        }

        private void DrawRightArrow(Graphics g, int x, int y, int size)
        {
            var pts = new[]
            {
                new Point(x, y),
                new Point(x + size, y + size / 2),
                new Point(x, y + size)
            };

            using (var shadowBrush = new SolidBrush(_arrowShadow))
            {
                var shadowPts = new[]
                {
                    new Point(pts[0].X + 1, pts[0].Y + 1),
                    new Point(pts[1].X + 1, pts[1].Y + 1),
                    new Point(pts[2].X + 1, pts[2].Y + 1)
                };
                g.FillPolygon(shadowBrush, shadowPts);
            }
            using (var brush = new SolidBrush(_arrowColor))
            {
                g.FillPolygon(brush, pts);
            }
        }
    }
}
