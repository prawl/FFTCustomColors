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

    public class SpriteSheetExtractor
    {
        public Dictionary<Direction, Bitmap> ExtractAllDirections(Bitmap spriteSheet)
        {
            var result = new Dictionary<Direction, Bitmap>();

            result[Direction.SW] = ExtractSprite(spriteSheet, Direction.SW);
            result[Direction.NW] = ExtractSprite(spriteSheet, Direction.NW);
            result[Direction.NE] = ExtractSprite(spriteSheet, Direction.NE);
            result[Direction.SE] = ExtractSprite(spriteSheet, Direction.SE);

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
            int spriteWidth = 64;
            int spriteHeight = 80;

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

            // Determine position based on source direction
            int x, y;
            switch (sourceDirection)
            {
                case Direction.SW:
                    x = 64;  // Position 1 = 64 pixels from left
                    y = 0;   // Row 0
                    break;
                case Direction.NW:
                    x = 192; // Position 3 = 192 pixels from left
                    y = 0;   // Row 0
                    break;
                default:
                    x = 64;
                    y = 0;
                    break;
            }

            // Extract the sprite from the sheet
            var sprite = new Bitmap(spriteWidth, spriteHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Define black color (RGB 0,0,0) as the transparent color
            var transparentColor = Color.FromArgb(0, 0, 0);

            for (int sx = 0; sx < spriteWidth; sx++)
            {
                for (int sy = 0; sy < spriteHeight; sy++)
                {
                    if (x + sx < spriteSheet.Width && y + sy < spriteSheet.Height)
                    {
                        var pixel = spriteSheet.GetPixel(x + sx, y + sy);

                        // If the pixel is pure black, make it transparent
                        if (pixel.R == 0 && pixel.G == 0 && pixel.B == 0)
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