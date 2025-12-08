using System;

namespace FFTColorMod
{
    public class ImprovedPaletteHandler
    {
        public int FindPaletteStart(byte[] spriteData)
        {
            // FFT sprites typically have palette data at the beginning
            // Palette is 16 or more colors, 3 bytes each (BGR format)
            if (spriteData.Length >= 48) // At least 16 colors
            {
                return 0; // Palette starts at beginning
            }
            return -1;
        }

        public void ApplyColorTransform(byte[] spriteData, int paletteOffset, string colorScheme)
        {
            // Apply color transformation to first 32 colors (96 bytes) in palette
            int paletteSize = Math.Min(96, spriteData.Length - paletteOffset);

            Console.WriteLine($"Applying {colorScheme} transformation to palette at offset {paletteOffset}");
            int transformedCount = 0;

            for (int i = 0; i < paletteSize; i += 3)
            {
                int idx = paletteOffset + i;
                if (idx + 2 >= spriteData.Length)
                    break;

                byte b = spriteData[idx];
                byte g = spriteData[idx + 1];
                byte r = spriteData[idx + 2];

                // Skip only the first transparent color (00 00 A5 in BGR format)
                if (i == 0 && b == 0 && g == 0 && r == 0xA5)
                {
                    Console.WriteLine($"Skipping transparent color at index {i}");
                    continue;
                }

                // Transform all other colors
                byte origB = b, origG = g, origR = r;

                switch (colorScheme.ToLower())
                {
                    case "red":
                        // Shift colors toward red
                        b = (byte)(b / 3);         // Reduce blue
                        g = (byte)(g / 2);         // Reduce green
                        r = (byte)Math.Min(255, r + 80); // Boost red
                        break;

                    case "blue":
                        // Shift colors toward blue
                        b = (byte)Math.Min(255, b + 80);  // Boost blue
                        g = (byte)(g / 2);         // Reduce green
                        r = (byte)(r / 3);         // Reduce red
                        break;

                    case "green":
                        // Shift colors toward green
                        b = (byte)(b / 3);         // Reduce blue
                        g = (byte)Math.Min(255, g + 80); // Boost green
                        r = (byte)(r / 3);         // Reduce red
                        break;

                    case "purple":
                        // Shift colors toward purple (red + blue)
                        b = (byte)Math.Min(255, b + 60);     // Boost blue
                        g = (byte)(g / 3);         // Reduce green significantly
                        r = (byte)Math.Min(255, r + 60); // Boost red
                        break;
                }

                // Apply the transformation
                spriteData[idx] = b;
                spriteData[idx + 1] = g;
                spriteData[idx + 2] = r;

                if (origB != b || origG != g || origR != r)
                {
                    transformedCount++;
                }
            }

            Console.WriteLine($"Transformed {transformedCount} colors");
        }
    }
}