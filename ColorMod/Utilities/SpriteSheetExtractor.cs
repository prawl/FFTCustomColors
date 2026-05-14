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

        public FrameLayout(int frameWidth, int frameHeight, int swCol, int nwCol, int row)
        {
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            SwCol = swCol;
            NwCol = nwCol;
            Row = row;
        }

        public static readonly FrameLayout Standard = new FrameLayout(64, 80, 1, 3, 0);

        public static FrameLayout For(string characterName)
        {
            return characterName switch
            {
                "Construct8" => new FrameLayout(96, 96, 1, 3, 0),
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
            int spriteWidth = layout.FrameWidth;
            int spriteHeight = layout.FrameHeight;

            // For NE and SE, we extract the base sprite and then mirror it
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

            // Determine position based on source direction + layout
            int x, y;
            switch (sourceDirection)
            {
                case Direction.SW:
                    x = layout.SwCol * spriteWidth;
                    y = layout.Row * spriteHeight;
                    break;
                case Direction.NW:
                    x = layout.NwCol * spriteWidth;
                    y = layout.Row * spriteHeight;
                    break;
                default:
                    x = layout.SwCol * spriteWidth;
                    y = layout.Row * spriteHeight;
                    break;
            }

            // Extract the sprite from the sheet
            var sprite = new Bitmap(spriteWidth, spriteHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (int sx = 0; sx < spriteWidth; sx++)
            {
                for (int sy = 0; sy < spriteHeight; sy++)
                {
                    if (x + sx < spriteSheet.Width && y + sy < spriteSheet.Height)
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