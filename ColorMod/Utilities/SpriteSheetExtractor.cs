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
        /// <paramref name="phase"/> (0..<see cref="FrameLayout.WalkPhaseCount"/>-1), sampling
        /// the row-1 stride cells <see cref="FrameLayout.GetWalkCell"/> derives. Returns null
        /// when <paramref name="layout"/> doesn't support the walk cycle (see
        /// <see cref="FrameLayout.SupportsWalkCycle"/>): non-standard sheets (Construct 8,
        /// Chocobo, every monster's explicit-rect layout) have no derivable walk frames.
        /// </summary>
        public Bitmap ExtractWalkFrame(Bitmap spriteSheet, Direction direction, FrameLayout layout, int phase)
        {
            if (!layout.SupportsWalkCycle)
                return null;

            // Same NE/SE mirroring rule as ExtractSprite: only SW and NW are ever sampled from
            // the sheet, the other two corners are their horizontal mirror.
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

            var cell = layout.GetWalkCell(sourceDirection, phase);

            // Offsets are always 0 for the standard layout, which is the only one that ever
            // reaches here (SupportsWalkCycle requires OffsetX == 0 && OffsetY == 0).
            int x = cell.X * layout.FrameWidth;
            int y = cell.Y * layout.FrameHeight;

            return ExtractCell(spriteSheet, x, y, layout.FrameWidth, layout.FrameHeight, shouldMirror);
        }
    }
}