using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Builds walk preview frames from a body cell and the two per beat arm sub sprites.
    /// <see cref="WalkCycle"/> specifies them. Split out of
    /// <see cref="SpriteSheetExtractor"/> (which keeps the standing-pose extraction) along the
    /// natural seam, poses are plain cell crops, walk frames are multi-layer composites with a
    /// per-character rig choice.
    /// </summary>
    public static class WalkFrameCompositor
    {
        /// <summary>
        /// Extracts one beat of the walk cycle for <paramref name="direction"/> at
        /// <paramref name="phase"/> (taken modulo <see cref="WalkCycle.BeatCount"/>),
        /// compositing the body cell and both arm sub-sprites from
        /// <paramref name="walkType"/>'s table, the rig MUST match the sheet
        /// (<see cref="WalkCycleTypes.For"/>), or the arms land dislocated. Returns null when
        /// <paramref name="layout"/> doesn't support the walk cycle (see
        /// <see cref="FrameLayout.SupportsWalkCycle"/>): non-standard sheets (Construct 8,
        /// Chocobo, every monster's explicit-rect layout) have no derivable walk frames.
        /// </summary>
        public static Bitmap ExtractWalkFrame(Bitmap spriteSheet, Direction direction, FrameLayout layout, int phase, WalkCycleType walkType)
        {
            if (!layout.SupportsWalkCycle)
                return null;

            // Same NE/SE mirroring rule as ExtractSprite, only SW and NW are ever sampled from
            // the sheet, the other two corners are their horizontal mirror of the finished frame.
            Direction sourceDirection = direction;
            bool shouldMirror = false;

            switch (direction)
            {
                case Direction.NE:
                    sourceDirection = Direction.NW;
                    shouldMirror = true;
                    break;
                case Direction.SE:
                    sourceDirection = Direction.SW;
                    shouldMirror = true;
                    break;
            }

            int beatIndex = ((phase % WalkCycle.BeatCount) + WalkCycle.BeatCount) % WalkCycle.BeatCount;
            var beat = WalkCycle.For(sourceDirection, walkType)[beatIndex];

            var dest = new Bitmap(layout.FrameWidth, layout.FrameHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // z-order, far arm (behind), body, near arm (in front). Body offsets are always 0
            // for the standard layout, which is the only one that ever reaches here; arm
            // offsets can be slightly negative on the TYPE2 rig (clipped, not an error).
            int bodyX = beat.BodyCell.X * layout.FrameWidth;
            int bodyY = beat.BodyCell.Y * layout.FrameHeight;

            CompositeRegion(spriteSheet, dest, beat.FarArmSource.X, beat.FarArmSource.Y, WalkCycle.ArmBoxSize, WalkCycle.ArmBoxSize, beat.FarArmOffset.X, beat.FarArmOffset.Y);
            CompositeRegion(spriteSheet, dest, bodyX, bodyY, layout.FrameWidth, layout.FrameHeight, 0, 0);
            CompositeRegion(spriteSheet, dest, beat.NearArmSource.X, beat.NearArmSource.Y, WalkCycle.ArmBoxSize, WalkCycle.ArmBoxSize, beat.NearArmOffset.X, beat.NearArmOffset.Y);

            if (!shouldMirror)
                return dest;

            using (dest)
            {
                var mirrored = new Bitmap(layout.FrameWidth, layout.FrameHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                for (int x = 0; x < layout.FrameWidth; x++)
                {
                    for (int y = 0; y < layout.FrameHeight; y++)
                    {
                        mirrored.SetPixel(layout.FrameWidth - 1 - x, y, dest.GetPixel(x, y));
                    }
                }
                return mirrored;
            }
        }

        /// <summary>
        /// Copies a <paramref name="w"/> x <paramref name="h"/> region of <paramref name="sheet"/>
        /// starting at (<paramref name="srcX"/>, <paramref name="srcY"/>) onto <paramref name="dest"/>
        /// at (<paramref name="destX"/>, <paramref name="destY"/>), skipping alpha==0 pixels (the
        /// sheet loaders already key index-0 to alpha 0, so this only draws real content). Bounds
        /// are checked on both the source sheet and the destination bitmap.
        /// </summary>
        private static void CompositeRegion(Bitmap sheet, Bitmap dest, int srcX, int srcY, int w, int h, int destX, int destY)
        {
            for (int x = 0; x < w; x++)
            {
                int sx = srcX + x;
                int dx = destX + x;
                if (sx < 0 || sx >= sheet.Width || dx < 0 || dx >= dest.Width)
                    continue;

                for (int y = 0; y < h; y++)
                {
                    int sy = srcY + y;
                    int dy = destY + y;
                    if (sy < 0 || sy >= sheet.Height || dy < 0 || dy >= dest.Height)
                        continue;

                    var pixel = sheet.GetPixel(sx, sy);
                    if (pixel.A == 0)
                        continue;

                    dest.SetPixel(dx, dy, pixel);
                }
            }
        }
    }
}
