using System;

namespace FFTColorMod
{
    public class GenericPaletteHandler
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

            for (int i = 0; i < paletteSize; i += 3)
            {
                int idx = paletteOffset + i;
                if (idx + 2 >= spriteData.Length)
                    break;

                byte b = spriteData[idx];
                byte g = spriteData[idx + 1];
                byte r = spriteData[idx + 2];

                // Skip pure black (0,0,0) and special transparent color (A5,00,00)
                if ((b == 0 && g == 0 && r == 0) || (b == 0xA5 && g == 0 && r == 0))
                    continue;

                switch (colorScheme.ToLower())
                {
                    case "red":
                        // Shift colors toward red
                        spriteData[idx] = (byte)(b / 3);         // Reduce blue
                        spriteData[idx + 1] = (byte)(g / 2);     // Reduce green
                        spriteData[idx + 2] = (byte)Math.Min(255, r + 80); // Boost red
                        break;

                    case "blue":
                        // Shift colors toward blue
                        spriteData[idx] = (byte)Math.Min(255, b + 80);  // Boost blue
                        spriteData[idx + 1] = (byte)(g / 2);     // Reduce green
                        spriteData[idx + 2] = (byte)(r / 3);     // Reduce red
                        break;

                    case "green":
                        // Shift colors toward green
                        spriteData[idx] = (byte)(b / 3);         // Reduce blue
                        spriteData[idx + 1] = (byte)Math.Min(255, g + 80); // Boost green
                        spriteData[idx + 2] = (byte)(r / 3);     // Reduce red
                        break;

                    case "purple":
                        // Shift colors toward purple (red + blue)
                        spriteData[idx] = (byte)Math.Min(255, b + 60);     // Boost blue
                        spriteData[idx + 1] = (byte)(g / 3);     // Reduce green significantly
                        spriteData[idx + 2] = (byte)Math.Min(255, r + 60); // Boost red
                        break;
                }
            }
        }
    }
}