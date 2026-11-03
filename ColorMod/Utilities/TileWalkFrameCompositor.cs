using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Builds the walk preview frames for the monster rigs from per beat tile draws.
    /// <see cref="MonsterWalkCycle"/> specifies them. The humanoid rigs keep
    /// their own compositor (<see cref="WalkFrameCompositor"/>): humanoid beats are a body
    /// cell plus two arm boxes, monster beats are a plain back-to-front tile list, and the
    /// two shapes share nothing but the mirroring rule.
    /// </summary>
    public static class TileWalkFrameCompositor
    {
        /// <summary>
        /// Extracts one beat of <paramref name="rig"/>'s walk cycle for
        /// <paramref name="direction"/> at <paramref name="phase"/> (taken modulo
        /// <see cref="WalkCycle.BeatCount"/>). Same NE/SE rule as the humanoid compositor:
        /// only SW and NW are ever sampled from the sheet, the other two corners are the
        /// horizontal mirror of the finished frame.
        /// </summary>
        public static Bitmap ExtractWalkFrame(Bitmap spriteSheet, Direction direction, int phase, WalkCycleType rig)
        {
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
            var draws = MonsterWalkCycle.For(sourceDirection, rig)[beatIndex];
            var frame = Compose(spriteSheet, draws, MonsterWalkCycle.FrameSize(rig));

            if (!shouldMirror)
                return frame;

            using (frame)
            {
                return MirrorHorizontal(frame);
            }
        }

        /// <summary>
        /// Composites <paramref name="draws"/> (back to front) from <paramref name="sheet"/>
        /// into a fresh <paramref name="frameSize"/> bitmap, skipping alpha==0 pixels (the
        /// sheet loaders key palette index 0 to alpha 0). A draw with <see cref="WalkTileDraw.FlipX"/>
        /// lands mirrored inside its own box. Bounds are checked on both bitmaps.
        /// </summary>
        internal static Bitmap Compose(Bitmap sheet, WalkTileDraw[] draws, Size frameSize)
        {
            var dest = new Bitmap(frameSize.Width, frameSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            foreach (var draw in draws)
            {
                for (int x = 0; x < draw.Source.Width; x++)
                {
                    int sx = draw.Source.X + (draw.FlipX ? draw.Source.Width - 1 - x : x);
                    int dx = draw.Offset.X + x;
                    if (sx < 0 || sx >= sheet.Width || dx < 0 || dx >= dest.Width)
                        continue;

                    for (int y = 0; y < draw.Source.Height; y++)
                    {
                        int sy = draw.Source.Y + y;
                        int dy = draw.Offset.Y + y;
                        if (sy < 0 || sy >= sheet.Height || dy < 0 || dy >= dest.Height)
                            continue;

                        var pixel = sheet.GetPixel(sx, sy);
                        if (pixel.A == 0)
                            continue;

                        dest.SetPixel(dx, dy, pixel);
                    }
                }
            }

            return dest;
        }

        private static Bitmap MirrorHorizontal(Bitmap source)
        {
            var mirrored = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int x = 0; x < source.Width; x++)
            {
                for (int y = 0; y < source.Height; y++)
                {
                    mirrored.SetPixel(source.Width - 1 - x, y, source.GetPixel(x, y));
                }
            }
            return mirrored;
        }
    }
}
