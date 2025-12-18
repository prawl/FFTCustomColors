using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Specifies which directional sprites to extract
    /// </summary>
    public enum DirectionMode
    {
        /// <summary>All 8 directions</summary>
        AllEight,
        /// <summary>4 cardinal directions (N, E, S, W)</summary>
        Cardinals,
        /// <summary>4 corner/diagonal directions (NE, SE, SW, NW)</summary>
        Corners
    }

    /// <summary>
    /// Extracts sprites from FFT .bin files
    /// </summary>
    public class BinSpriteExtractor
    {
        // Actual sprite dimensions in the sprite sheet
        private const int SpriteWidth = 32;
        private const int SpriteHeight = 40;

        // The sprite sheet is 256 pixels wide
        private const int SheetWidth = 256;

        // Display size for extracted sprites (scaled up to match PNG preview size)
        private const int DisplayWidth = 64;
        private const int DisplayHeight = 80;  // Maintain aspect ratio: 32x40 -> 64x80

        // Cache for extracted sprites: key is "spriteIndex_paletteIndex_dataHash"
        private readonly Dictionary<string, Bitmap> _spriteCache = new Dictionary<string, Bitmap>();

        /// <summary>
        /// Clears the sprite cache to force reloading
        /// </summary>
        public void ClearCache()
        {
            foreach (var sprite in _spriteCache.Values)
            {
                sprite?.Dispose();
            }
            _spriteCache.Clear();
        }

        /// <summary>
        /// Reads a 16-color palette from the bin data
        /// </summary>
        /// <param name="data">The bin file data</param>
        /// <param name="paletteIndex">Which palette to read (0-15)</param>
        /// <returns>Array of 16 colors</returns>
        public Color[] ReadPalette(byte[] data, int paletteIndex)
        {
            var palette = new Color[16];
            int offset = paletteIndex * 32; // Each palette is 16 colors * 2 bytes = 32 bytes

            // Check if palette index is out of bounds
            if (offset + 32 > 512) // Palette data is in first 512 bytes
            {
                // Fall back to palette 0
                System.Diagnostics.Debug.WriteLine($"[ReadPalette] Palette {paletteIndex} out of bounds, using palette 0");
                offset = 0;
            }

            bool allBlack = true; // Check if this palette is all black/empty

            for (int i = 0; i < 16; i++)
            {
                int colorOffset = offset + (i * 2);

                // Read BGR555 color (2 bytes, little-endian)
                ushort bgr555 = (ushort)(data[colorOffset] | (data[colorOffset + 1] << 8));

                // First color is always transparent
                if (i == 0)
                {
                    palette[i] = Color.Transparent;
                    continue;
                }

                // Extract 5-bit components
                int r5 = bgr555 & 0x1F;
                int g5 = (bgr555 >> 5) & 0x1F;
                int b5 = (bgr555 >> 10) & 0x1F;

                // Convert 5-bit to 8-bit (multiply by 255/31 ≈ 8.225)
                int r8 = (r5 * 255) / 31;
                int g8 = (g5 * 255) / 31;
                int b8 = (b5 * 255) / 31;

                palette[i] = Color.FromArgb(r8, g8, b8);

                // Check if this is not all black
                if (bgr555 != 0x0000 && bgr555 != 0x8000) // 0x8000 is also sometimes used for black
                {
                    allBlack = false;
                }
            }

            // If palette is all black (invalid), use palette 0 instead
            if (allBlack && paletteIndex != 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ReadPalette] Palette {paletteIndex} is all black, falling back to palette 0");
                return ReadPalette(data, 0);
            }

            return palette;
        }

        /// <summary>
        /// Extracts a single sprite from the bin data
        /// </summary>
        /// <param name="data">The bin file data</param>
        /// <param name="spriteIndex">Which sprite to extract (0-based)</param>
        /// <param name="paletteIndex">Which palette to use (0-15)</param>
        /// <returns>A scaled bitmap (48x56 for display)</returns>
        public Bitmap ExtractSprite(byte[] data, int spriteIndex, int paletteIndex)
        {
            // Create a more unique hash that samples multiple parts of the file
            int dataHash = data.Length;
            // Sample bytes from different parts of the file for better uniqueness
            if (data.Length >= 1024)
            {
                // Sample from palette area
                dataHash ^= (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
                // Sample from sprite data area
                dataHash ^= (data[512] << 24) | (data[513] << 16) | (data[514] << 8) | data[515];
                dataHash ^= (data[1020] << 24) | (data[1021] << 16) | (data[1022] << 8) | data[1023];
                // Sample from middle of sprite data for better differentiation
                if (data.Length >= 2048)
                {
                    dataHash ^= (data[2044] << 24) | (data[2045] << 16) | (data[2046] << 8) | data[2047];
                }
            }
            string cacheKey = $"{spriteIndex}_{paletteIndex}_{dataHash}";

            // Disable cache - it's causing theme switching issues
            // The performance is fine without caching
            // if (_spriteCache.TryGetValue(cacheKey, out var cachedSprite))
            // {
            //     return new Bitmap(cachedSprite); // Return a copy to avoid disposal issues
            // }

            // Get the palette
            var palette = ReadPalette(data, paletteIndex);

            // Create the bitmap at actual sprite size
            var bitmap = new Bitmap(SpriteWidth, SpriteHeight);

            // Sprites are arranged horizontally in the sprite sheet
            // Each sprite is 32 pixels wide, positioned horizontally
            int xOffset = spriteIndex * SpriteWidth;
            int yOffset = 0; // All sprites are in the first row

            // Skip palette data (512 bytes)
            int spriteDataStart = 512;

            // Read the sprite data from the sheet
            for (int y = 0; y < SpriteHeight; y++)
            {
                for (int x = 0; x < SpriteWidth; x++)
                {
                    // Calculate position in the full sprite sheet
                    int sheetX = xOffset + x;
                    int sheetY = yOffset + y;

                    // Calculate pixel index in the sprite data (256-pixel wide sheet)
                    int pixelIndex = (sheetY * SheetWidth) + sheetX;
                    int byteIndex = spriteDataStart + (pixelIndex / 2);

                    if (byteIndex >= data.Length)
                        break;

                    byte pixelData = data[byteIndex];

                    // Get 4-bit value (alternate between low and high nibble)
                    int colorIndex;
                    if (pixelIndex % 2 == 0)
                    {
                        colorIndex = pixelData & 0x0F; // Low nibble
                    }
                    else
                    {
                        colorIndex = (pixelData >> 4) & 0x0F; // High nibble
                    }

                    // Set the pixel using the palette
                    bitmap.SetPixel(x, y, palette[colorIndex]);
                }
            }

            // Scale up to display size (64x80) to match PNG preview size
            // The sprite is 32x40, scaled 2x to 64x80
            var displayBitmap = new Bitmap(DisplayWidth, DisplayHeight);
            using (var g = Graphics.FromImage(displayBitmap))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                // Scale 2x from 32x40 to 64x80
                g.DrawImage(bitmap, 0, 0, DisplayWidth, DisplayHeight);
            }

            // Dispose of the temporary bitmap
            bitmap.Dispose();

            // Don't cache - it's causing theme switching issues
            // _spriteCache[cacheKey] = displayBitmap;

            return displayBitmap;
        }

        /// <summary>
        /// Extracts all 8 directional sprites for a character
        /// </summary>
        /// <param name="data">The bin file data</param>
        /// <param name="characterIndex">Which character (usually 0 for single-character files)</param>
        /// <param name="paletteIndex">Which palette to use (0-15)</param>
        /// <returns>Array of 8 bitmaps representing the 8 directions</returns>
        public Bitmap[] ExtractAllDirections(byte[] data, int characterIndex = 0, int paletteIndex = 0)
        {
            var sprites = new Bitmap[8];

            // FFT sprite sheet layout (corrected based on Python script):
            // Position 0: W (West - facing left)
            // Position 1: SW (Southwest)
            // Position 2: S (South - facing down)
            // Position 3: NW (Northwest)
            // Position 4: N (North - facing up/away)
            // Position 5: Animation frame (not a direction)
            // Position 6: E (actually a duplicate/animation, we'll mirror W for true E)
            // Position 7: Animation frame (not a direction)

            // Extract the base 5 directional sprites
            sprites[6] = ExtractSprite(data, 0, paletteIndex); // W (West)
            sprites[5] = ExtractSprite(data, 1, paletteIndex); // SW (Southwest)
            sprites[4] = ExtractSprite(data, 2, paletteIndex); // S (South)
            sprites[7] = ExtractSprite(data, 3, paletteIndex); // NW (Northwest)
            sprites[0] = ExtractSprite(data, 4, paletteIndex); // N (North)

            // Mirror sprites to create the East directions
            // E (East) = Mirror of W (West)
            sprites[2] = MirrorBitmap(sprites[6]); // E from W

            // NE (Northeast) = Mirror of NW (Northwest)
            sprites[1] = MirrorBitmap(sprites[7]); // NE from NW

            // SE (Southeast) = Mirror of SW (Southwest)
            sprites[3] = MirrorBitmap(sprites[5]); // SE from SW


            return sprites;
        }

        /// <summary>
        /// Mirrors a bitmap horizontally
        /// </summary>
        /// <param name="original">The bitmap to mirror</param>
        /// <returns>A new horizontally mirrored bitmap</returns>
        private Bitmap MirrorBitmap(Bitmap original)
        {
            var mirrored = new Bitmap(original.Width, original.Height);
            using (var g = Graphics.FromImage(mirrored))
            {
                g.TranslateTransform(original.Width, 0);
                g.ScaleTransform(-1, 1);
                g.DrawImage(original, 0, 0);
            }
            return mirrored;
        }

        /// <summary>
        /// Extracts the 4 cardinal direction sprites (N, E, S, W)
        /// </summary>
        /// <param name="data">The bin file data</param>
        /// <param name="characterIndex">Which character (usually 0)</param>
        /// <param name="paletteIndex">Which palette to use (0-15)</param>
        /// <returns>Array of 4 bitmaps for N, E, S, W directions</returns>
        public Bitmap[] ExtractCardinalDirections(byte[] data, int characterIndex = 0, int paletteIndex = 0)
        {
            var allSprites = ExtractAllDirections(data, characterIndex, paletteIndex);

            // Our sprite array order after extraction: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
            // Cardinal directions: N(0), E(2), S(4), W(6)
            return new Bitmap[]
            {
                allSprites[0], // N
                allSprites[2], // E
                allSprites[4], // S
                allSprites[6]  // W
            };
        }

        /// <summary>
        /// Extracts the 4 corner/diagonal direction sprites (NE, SE, SW, NW)
        /// </summary>
        /// <param name="data">The bin file data</param>
        /// <param name="characterIndex">Which character (usually 0)</param>
        /// <param name="paletteIndex">Which palette to use (0-15)</param>
        /// <returns>Array of 4 bitmaps for NE, SE, SW, NW directions</returns>
        public Bitmap[] ExtractCornerDirections(byte[] data, int characterIndex = 0, int paletteIndex = 0)
        {
            var allSprites = ExtractAllDirections(data, characterIndex, paletteIndex);

            // Our sprite array order after extraction: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
            // Corner directions: NE(1), SE(3), SW(5), NW(7)
            return new Bitmap[]
            {
                allSprites[1], // NE
                allSprites[3], // SE
                allSprites[5], // SW
                allSprites[7]  // NW
            };
        }

        /// <summary>
        /// Extracts directional sprites based on the specified mode
        /// </summary>
        /// <param name="data">The bin file data</param>
        /// <param name="characterIndex">Which character (set of 8 sprites)</param>
        /// <param name="paletteIndex">Which palette to use (0-15)</param>
        /// <param name="mode">Which directions to extract</param>
        /// <returns>Array of bitmaps based on the mode</returns>
        public Bitmap[] ExtractDirections(byte[] data, int characterIndex, int paletteIndex, DirectionMode mode)
        {
            switch (mode)
            {
                case DirectionMode.AllEight:
                    return ExtractAllDirections(data, characterIndex, paletteIndex);

                case DirectionMode.Cardinals:
                    return ExtractCardinalDirections(data, characterIndex, paletteIndex);

                case DirectionMode.Corners:
                    return ExtractCornerDirections(data, characterIndex, paletteIndex);

                default:
                    throw new ArgumentException($"Unknown direction mode: {mode}");
            }
        }
    }
}