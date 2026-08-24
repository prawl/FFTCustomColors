using System.Collections.Generic;
using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    public class SpriteSheetExtractor
    {
        public Dictionary<Direction, Bitmap> ExtractAllDirections(Bitmap spriteSheet)
        {
            return ExtractAllDirections(spriteSheet, FrameLayout.Standard);
        }

        public Dictionary<Direction, Bitmap> ExtractAllDirections(Bitmap spriteSheet, string characterName)
        {
            return ExtractAllDirections(spriteSheet, FrameLayout.For(characterName));
        }

        public Dictionary<Direction, Bitmap> ExtractAllDirections(Bitmap spriteSheet, FrameLayout layout)
        {
            var result = new Dictionary<Direction, Bitmap>();
            result[Direction.SW] = ExtractSprite(spriteSheet, Direction.SW, layout);
            result[Direction.NW] = ExtractSprite(spriteSheet, Direction.NW, layout);
            result[Direction.NE] = ExtractSprite(spriteSheet, Direction.NE, layout);
            result[Direction.SE] = ExtractSprite(spriteSheet, Direction.SE, layout);
            return result;
        }

        public Dictionary<Direction, Bitmap> ExtractAllDirectionsFromFile(string filePath)
        {
            using (var spriteSheet = new Bitmap(filePath))
            {
                return ExtractAllDirections(spriteSheet);
            }
        }

        public Bitmap ExtractSprite(Bitmap spriteSheet, Direction direction)
        {
            return ExtractSprite(spriteSheet, direction, FrameLayout.Standard);
        }

        public Bitmap ExtractSprite(Bitmap spriteSheet, Direction direction, FrameLayout layout)
        {
            // For NE and SE, we extract the base (NW/SW) sprite and then mirror it.
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

            // Resolve the source box for this direction. Explicit-rect layouts carry independent
            // SW/NW boxes (irregular monster sheets); column layouts derive it from cell + col/row.
            int x, y, spriteWidth, spriteHeight;
            if (layout.UsesExplicitRects)
            {
                var r = (sourceDirection == Direction.NW) ? layout.NwRect : layout.SwRect;
                x = r.X; y = r.Y; spriteWidth = r.Width; spriteHeight = r.Height;
            }
            else
            {
                spriteWidth = layout.FrameWidth;
                spriteHeight = layout.FrameHeight;
                int col = (sourceDirection == Direction.NW) ? layout.NwCol : layout.SwCol;
                x = layout.OffsetX + col * spriteWidth;
                y = layout.OffsetY + layout.Row * spriteHeight;
            }

            return ExtractCell(spriteSheet, x, y, spriteWidth, spriteHeight, shouldMirror);
        }

        /// <summary>
        /// Extracts a single <paramref name="spriteWidth"/> x <paramref name="spriteHeight"/>
        /// cell whose top-left corner sits at (<paramref name="x"/>, <paramref name="y"/>) on
        /// <paramref name="spriteSheet"/>, mirroring horizontally when <paramref name="shouldMirror"/>
        /// is set. Shared by <see cref="ExtractSprite(Bitmap, Direction, FrameLayout)"/> (the
        /// standing poses) and <see cref="ExtractWalkFrame"/> (the walk-cycle stride frames).
        /// </summary>
        private Bitmap ExtractCell(Bitmap spriteSheet, int x, int y, int spriteWidth, int spriteHeight, bool shouldMirror)
        {
            // Extract the sprite from the sheet
            var sprite = new Bitmap(spriteWidth, spriteHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (int sx = 0; sx < spriteWidth; sx++)
            {
                for (int sy = 0; sy < spriteHeight; sy++)
                {
                    // Bounds-check BOTH edges. A negative grid origin (offsetX/offsetY < 0, used to
                    // center an off-grid pose) puts some source reads outside the sheet — those
                    // become transparent margin instead of throwing ArgumentOutOfRangeException.
                    if (x + sx >= 0 && y + sy >= 0 && x + sx < spriteSheet.Width && y + sy < spriteSheet.Height)
                    {
                        var pixel = spriteSheet.GetPixel(x + sx, y + sy);

                        // Transparency is keyed off the alpha channel, NOT off "the color is
                        // black". FFT sprites reserve palette index 0 as the transparent
                        // background, and the palette loaders (BmpPaletteSwapper) encode that
                        // as alpha 0 — so only genuine background pixels are dropped. Keying
                        // on RGB would erase any section the user painted pure black (#000000).
                        if (pixel.A == 0)
                        {
                            pixel = Color.Transparent;
                        }

                        // If mirroring, flip the x coordinate
                        int destX = shouldMirror ? (spriteWidth - 1 - sx) : sx;
                        sprite.SetPixel(destX, sy, pixel);
                    }
                }
            }

            return sprite;
        }

        /// <summary>
        /// Extracts one beat of the walk cycle for <paramref name="direction"/> at
        /// <paramref name="phase"/> (taken modulo <see cref="WalkCycle.BeatCount"/>), compositing
        /// the body cell and both arm sub-sprites <see cref="WalkCycle"/> specifies for that
        /// beat. Returns null when <paramref name="layout"/> doesn't support the walk cycle (see
        /// <see cref="FrameLayout.SupportsWalkCycle"/>): non-standard sheets (Construct 8,
        /// Chocobo, every monster's explicit-rect layout) have no derivable walk frames.
        /// </summary>
        public Bitmap ExtractWalkFrame(Bitmap spriteSheet, Direction direction, FrameLayout layout, int phase)
        {
            if (!layout.SupportsWalkCycle)
                return null;

            // Same NE/SE mirroring rule as ExtractSprite: only SW and NW are ever sampled from
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
            var beat = WalkCycle.For(sourceDirection)[beatIndex];

            var dest = new Bitmap(layout.FrameWidth, layout.FrameHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // z-order: far arm (behind), body, near arm (in front). Offsets are always 0 for the
            // standard layout, which is the only one that ever reaches here.
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