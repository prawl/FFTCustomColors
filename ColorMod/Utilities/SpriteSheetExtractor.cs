using System.Collections.Generic;
using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    public enum Direction
    {
        SW,
        NW,
        NE,
        SE
    }

    /// <summary>
    /// Per-character HD-sheet frame layout. Standard humanoids use 64x80 cells with SW at
    /// cell (1, 0) and NW at cell (3, 0). Non-standard characters (e.g. Construct 8 / tetsu,
    /// whose native frames are 48x48 instead of 32x40) need a different cell size.
    /// </summary>
    public readonly struct FrameLayout
    {
        public int FrameWidth { get; }
        public int FrameHeight { get; }
        public int SwCol { get; }   // SW source column (frame index, not pixel)
        public int NwCol { get; }   // NW source column
        public int Row { get; }     // Row index
        public int OffsetX { get; } // pixel offset of the grid origin (for sheets whose frames aren't grid-aligned)
        public int OffsetY { get; }

        // Explicit-rect mode: irregular monster sheets often place the two chosen poses at
        // positions a single uniform cell size can't isolate (different widths, non-uniform
        // pitch — a wide cell that centers one pose swallows the other's neighbor). In that case
        // the SW and NW source boxes are given outright; NE = mirror(NW), SE = mirror(SW).
        public bool UsesExplicitRects { get; }
        public Rectangle SwRect { get; }
        public Rectangle NwRect { get; }

        public FrameLayout(int frameWidth, int frameHeight, int swCol, int nwCol, int row, int offsetX = 0, int offsetY = 0)
        {
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            SwCol = swCol;
            NwCol = nwCol;
            Row = row;
            OffsetX = offsetX;
            OffsetY = offsetY;
            UsesExplicitRects = false;
            SwRect = Rectangle.Empty;
            NwRect = Rectangle.Empty;
        }

        private FrameLayout(Rectangle swRect, Rectangle nwRect)
        {
            // Frame size = SW box size (SW and NW boxes should match for a uniform carousel).
            FrameWidth = swRect.Width;
            FrameHeight = swRect.Height;
            SwCol = 0; NwCol = 0; Row = 0; OffsetX = 0; OffsetY = 0;
            UsesExplicitRects = true;
            SwRect = swRect;
            NwRect = nwRect;
        }

        /// <summary>Explicit per-pose source boxes (SW and NW); NE/SE are their mirrors.</summary>
        public static FrameLayout Rects(int swX, int swY, int swW, int swH, int nwX, int nwY, int nwW, int nwH)
            => new FrameLayout(new Rectangle(swX, swY, swW, swH), new Rectangle(nwX, nwY, nwW, nwH));

        public static readonly FrameLayout Standard = new FrameLayout(64, 80, 1, 3, 0);

        public static FrameLayout For(string characterName)
        {
            return characterName switch
            {
                "Construct8" => new FrameLayout(96, 96, 1, 3, 0),
                // Chocobo's HD sheet is irregular (poses aren't grid-aligned). The two clean
                // front-facing standing poses (grid cells 6,5 and 7,5) sit at pixel (368,400),
                // 64x64 each — SW = col 0, NW = col 1 from that origin.
                "Chocobo" => new FrameLayout(64, 64, 0, 1, 0, 368, 400),

                // Monster families — explicit per-pose boxes (irregular sheets, see
                // docs/ADDING_A_MONSTER.md). SW = the front-facing standing pose, NW = the
                // away-facing one (editor mirrors SW→SE, NW→NE). Boxes were human-picked off the
                // numbered-pose images and auto-centered by scripts/monster/pick_poses.py.
                "Goblin" => FrameLayout.Rects(76, 1, 46, 78, 205, 4, 46, 78),
                "Bomb" => FrameLayout.Rects(109, 19, 62, 70, 303, 19, 62, 70),
                "Panther" => FrameLayout.Rects(109, 19, 74, 72, 300, 21, 74, 72),
                "Mindflayer" => FrameLayout.Rects(72, 3, 52, 74, 199, 3, 52, 74),
                "Skeleton" => FrameLayout.Rects(75, 1, 50, 80, 202, 2, 50, 80),
                "Ghost" => FrameLayout.Rects(115, 21, 56, 70, 308, 23, 56, 70),
                "Ahriman" => FrameLayout.Rects(123, 13, 50, 76, 315, 16, 50, 76),
                // Explicit per-pose boxes (irregular sheet): SW = 2nd standing bird, NW = 4th.
                "Aevis" => FrameLayout.Rects(126, 110, 44, 76, 318, 109, 44, 76),
                "Pig" => FrameLayout.Rects(127, 17, 36, 66, 319, 17, 36, 66),
                "Treant" => FrameLayout.Rects(105, 1, 82, 92, 296, 2, 82, 92),
                "Minotaur" => FrameLayout.Rects(107, 1, 64, 88, 293, 1, 64, 88),
                "Malboro" => FrameLayout.Rects(105, 8, 74, 86, 299, 7, 74, 86),
                "Behemoth" => FrameLayout.Rects(99, 1, 80, 90, 290, 1, 80, 90),
                "Dragon" => FrameLayout.Rects(98, 0, 88, 92, 291, 1, 88, 92),
                // Hydra/Tiamat (battle_dora2_spr.bin, HD 1098_Tiamat). Same dragon rig as Dragon:
                // SW = pose col1 (front), NW = pose col3 (away), row 0. Detected on 1098_Tiamat_hd.bmp.
                "Hydra" => FrameLayout.Rects(100, 8, 86, 82, 292, 8, 86, 82),

                _ => Standard
            };
        }
    }

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
    }
}