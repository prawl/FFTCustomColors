using System;
using System.Drawing;
using System.Collections.Generic;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Transforms colors in Ramza sprites to match different themes
    /// </summary>
    public class RamzaColorTransformer
    {
        public RamzaColorTransformer()
        {
        }

        public Color TransformColor(Color originalColor, string themeName, string characterName)
        {
            // Skip transparent pixels
            if (originalColor.A < 10) return originalColor;

            switch (themeName.ToLower())
            {
                case "white_heretic":
                    return TransformToWhite(originalColor, characterName);
                case "crimson_blade":
                    return TransformToCrimson(originalColor, characterName);
                case "dark_knight":
                    return TransformToDarkKnight(originalColor, characterName);
                default:
                    return originalColor;
            }
        }

        public Bitmap TransformBitmap(Bitmap originalBitmap, string themeName, string characterName = "RamzaChapter1")
        {
            var transformedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height);

            for (int x = 0; x < originalBitmap.Width; x++)
            {
                for (int y = 0; y < originalBitmap.Height; y++)
                {
                    var originalColor = originalBitmap.GetPixel(x, y);
                    var transformedColor = TransformColor(originalColor, themeName, characterName);
                    transformedBitmap.SetPixel(x, y, transformedColor);
                }
            }

            return transformedBitmap;
        }

        private Color TransformToWhite(Color originalColor, string characterName)
        {
            // Transform armor colors to white/gray
            if (IsArmorColor(originalColor, characterName))
            {
                int brightness = (originalColor.R + originalColor.G + originalColor.B) / 3;

                // Map to white/gray range
                int newValue;
                if (brightness > 150)
                    newValue = 255; // Pure white
                else if (brightness > 100)
                    newValue = 189; // Light gray
                else if (brightness > 60)
                    newValue = 126; // Medium gray
                else
                    newValue = 105; // Dark gray

                return Color.FromArgb(originalColor.A, newValue, newValue, newValue);
            }

            return originalColor;
        }

        private Color TransformToCrimson(Color originalColor, string characterName)
        {
            // Transform armor colors to crimson red
            if (IsArmorColor(originalColor, characterName))
            {
                int brightness = (originalColor.R + originalColor.G + originalColor.B) / 3;

                // Map to crimson red range (avoiding orange)
                int red, green, blue;
                if (characterName == "RamzaChapter1")
                {
                    // Brighter reds for Chapter 1 to avoid brown
                    if (brightness > 180)
                    {
                        red = 240; green = 0; blue = 0;
                    }
                    else if (brightness > 140)
                    {
                        red = 220; green = 0; blue = 0;
                    }
                    else if (brightness > 100)
                    {
                        red = 200; green = 0; blue = 0;
                    }
                    else if (brightness > 70)
                    {
                        red = 180; green = 0; blue = 0;
                    }
                    else if (brightness > 50)
                    {
                        red = 160; green = 0; blue = 0;
                    }
                    else if (brightness > 30)
                    {
                        red = 120; green = 0; blue = 0;
                    }
                    else
                    {
                        red = 80; green = 0; blue = 0;
                    }
                }
                else if (characterName == "RamzaChapter2")
                {
                    // Vibrant reds for Chapter 2-3
                    if (brightness > 150)
                    {
                        red = 220; green = 20; blue = 20;
                    }
                    else if (brightness > 100)
                    {
                        red = 180; green = 10; blue = 10;
                    }
                    else if (brightness > 50)
                    {
                        red = 136; green = 16; blue = 0;
                    }
                    else
                    {
                        red = 72; green = 8; blue = 8;
                    }
                }
                else // Chapter 3-4
                {
                    // Crimson armor colors
                    if (brightness > 150)
                    {
                        red = 208; green = 26; blue = 52;
                    }
                    else if (brightness > 100)
                    {
                        red = 144; green = 18; blue = 36;
                    }
                    else if (brightness > 50)
                    {
                        red = 80; green = 10; blue = 20;
                    }
                    else
                    {
                        red = 40; green = 5; blue = 10;
                    }
                }

                return Color.FromArgb(originalColor.A, red, green, blue);
            }

            // Keep very dark colors (under armor) as dark gray
            if (originalColor.R < 30 && originalColor.G < 30 && originalColor.B < 30)
            {
                return Color.FromArgb(originalColor.A, 20, 20, 20);
            }

            return originalColor;
        }

        private Color TransformToDarkKnight(Color originalColor, string characterName)
        {
            // Transform armor colors to dark blue/black
            if (IsArmorColor(originalColor, characterName))
            {
                int brightness = (originalColor.R + originalColor.G + originalColor.B) / 3;

                // Map to dark blue/black range
                int value;
                if (brightness > 150)
                {
                    // Dark blue for highlights
                    return Color.FromArgb(originalColor.A, 40, 50, 80);
                }
                else if (brightness > 100)
                {
                    // Darker blue
                    return Color.FromArgb(originalColor.A, 30, 40, 60);
                }
                else if (brightness > 60)
                {
                    // Very dark blue
                    return Color.FromArgb(originalColor.A, 20, 25, 40);
                }
                else
                {
                    // Near black
                    return Color.FromArgb(originalColor.A, 10, 10, 20);
                }
            }

            return originalColor;
        }

        private bool IsArmorColor(Color color, string characterName)
        {
            // Skip transparent pixels
            if (color.A < 10) return false;

            // Different armor detection for each chapter
            if (characterName == "RamzaChapter1")
            {
                // Chapter 1: Blue armor
                return (color.B > color.R && color.B > color.G) ||
                       (color.R < 100 && color.G < 140 && color.B > 70);
            }
            else if (characterName == "RamzaChapter2")
            {
                // Chapter 2-3: Purple armor
                return (color.R > 30 && color.B > 50 && color.B > color.G) ||
                       (color.R > 40 && color.R < 130 && color.B > 70);
            }
            else // Chapter 3-4
            {
                // Chapter 3-4: Teal armor
                return (color.B > color.R && color.G > color.R * 0.7) ||
                       (color.G > 60 && color.B > 70 && color.R < 50);
            }
        }
    }
}