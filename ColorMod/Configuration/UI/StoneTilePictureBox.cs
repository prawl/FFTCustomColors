using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// A PictureBox that paints the embedded stone-tile plate as its background,
    /// so transparent sprite images render as if standing on a stone floor.
    /// </summary>
    public class StoneTilePictureBox : PictureBox
    {
        // Shared, read-only background plate. Loaded once; intentionally never disposed
        // (it outlives any single control and is reused across all instances).
        private static Image _backgroundTile;
        private static bool _backgroundTileLoadAttempted;

        /// <summary>
        /// Loads (once) and returns the embedded stone-tile background plate, or null if unavailable.
        /// </summary>
        public static Image GetBackgroundTile()
        {
            if (_backgroundTile != null) return _backgroundTile;
            if (_backgroundTileLoadAttempted) return null;
            _backgroundTileLoadAttempted = true;

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var resourceName = asm.GetManifestResourceNames()
                    .FirstOrDefault(r => r.EndsWith("Images.Backgrounds.empty_tile.png", StringComparison.OrdinalIgnoreCase));
                if (resourceName != null)
                {
                    using (var stream = asm.GetManifestResourceStream(resourceName))
                    using (var loaded = Image.FromStream(stream))
                    {
                        _backgroundTile = new Bitmap(loaded);
                    }
                }
            }
            catch
            {
                _backgroundTile = null;
            }
            return _backgroundTile;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var tile = GetBackgroundTile();
            if (tile == null || Width <= 0 || Height <= 0)
            {
                base.OnPaintBackground(e);
                return;
            }

            // "Cover" scaling: fill the control, preserve aspect, center-crop the overflow.
            float scale = Math.Max((float)Width / tile.Width, (float)Height / tile.Height);
            int dw = (int)Math.Ceiling(tile.Width * scale);
            int dh = (int)Math.Ceiling(tile.Height * scale);
            int dx = (Width - dw) / 2;
            int dy = (Height - dh) / 2;
            e.Graphics.DrawImage(tile, dx, dy, dw, dh);
        }
    }
}
