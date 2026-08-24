using System.Collections.Generic;
using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Crops a walk cycle's frames to the single content box they share (CC-46). Monster rig
    /// frame boxes are mostly padding (a 96x96 MON tile around a 60x70 monster), and the
    /// preview panes scale the bitmap to fit, so uncropped frames would shrink the sprite the
    /// moment the animation starts. Cropping every frame by the SAME box (the union of every
    /// frame's opaque pixels) keeps the animation steady: a per-frame tight crop would make
    /// the sprite wobble as its stride widens and narrows.
    /// </summary>
    public static class WalkFrameCrop
    {
        /// <summary>
        /// Returns new bitmaps cropped to the union content box across all
        /// <paramref name="frames"/>, disposing the originals. Frames with no opaque pixels
        /// anywhere (nothing to derive a box from) are returned unchanged.
        /// </summary>
        public static List<Bitmap> ToSharedContentBox(List<Bitmap> frames)
        {
            if (frames == null || frames.Count == 0)
                return frames;

            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var frame in frames)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    for (int y = 0; y < frame.Height; y++)
                    {
                        if (frame.GetPixel(x, y).A == 0)
                            continue;
                        if (x < x0) x0 = x;
                        if (y < y0) y0 = y;
                        if (x > x1) x1 = x;
                        if (y > y1) y1 = y;
                    }
                }
            }

            if (x1 < x0)
                return frames; // fully transparent everywhere: keep the original boxes

            int width = x1 - x0 + 1;
            int height = y1 - y0 + 1;
            var cropped = new List<Bitmap>(frames.Count);
            foreach (var frame in frames)
            {
                var crop = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        crop.SetPixel(x, y, frame.GetPixel(x0 + x, y0 + y));
                    }
                }
                cropped.Add(crop);
                frame.Dispose();
            }
            return cropped;
        }
    }
}
