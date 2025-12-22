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
        // Define color mappings for each theme
        private readonly Dictionary<string, Dictionary<Color, Color>> _themeMappings;

        public RamzaColorTransformer()
        {
            _themeMappings = new Dictionary<string, Dictionary<Color, Color>>();
            InitializeThemeMappings();
        }

        private void InitializeThemeMappings()
        {
            // White Heretic theme - transforms brown/purple armor to white
            _themeMappings["white_heretic"] = new Dictionary<Color, Color>();

            // We'll define specific color ranges to transform
            // This is simplified - in reality we'd need to handle color ranges
        }

        public Color TransformColor(Color originalColor, string themeName)
        {
            // For white_heretic theme, transform brownish colors to white
            if (themeName == "white_heretic")
            {
                // If it's a brownish/purplish color (armor), make it white
                if (IsBrownishColor(originalColor))
                {
                    // Calculate brightness to maintain shading
                    int brightness = (originalColor.R + originalColor.G + originalColor.B) / 3;

                    // Map to white/light gray range while preserving some depth
                    // Darker browns become light gray, lighter browns become white
                    int newValue = 180 + (brightness / 2); // Range from 180-220
                    newValue = Math.Min(240, newValue); // Cap at 240 to keep some contrast

                    return Color.FromArgb(originalColor.A, newValue, newValue, newValue);
                }
            }

            // Default: return original color
            return originalColor;
        }

        public Bitmap TransformBitmap(Bitmap originalBitmap, string themeName)
        {
            var transformedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height);

            for (int x = 0; x < originalBitmap.Width; x++)
            {
                for (int y = 0; y < originalBitmap.Height; y++)
                {
                    var originalColor = originalBitmap.GetPixel(x, y);
                    var transformedColor = TransformColor(originalColor, themeName);
                    transformedBitmap.SetPixel(x, y, transformedColor);
                }
            }

            return transformedBitmap;
        }

        private bool IsBrownishColor(Color color)
        {
            // Skip transparent pixels
            if (color.A < 10) return false;

            // Check if color is in the brownish/purplish range
            // Ramza's armor is typically dark brown/purple
            // We need to be more selective to avoid changing skin/hair

            // Brown/purple armor colors have these characteristics:
            // - Red component is higher than blue
            // - Green is usually between red and blue
            // - Overall darker tones

            bool isBrownish = color.R >= 50 && color.R <= 100 &&
                              color.G >= 30 && color.G <= 70 &&
                              color.B >= 20 && color.B <= 60 &&
                              color.R > color.B; // Brown has more red than blue

            // Also check for darker purple shades in the armor
            bool isPurplish = color.R >= 60 && color.R <= 90 &&
                              color.G >= 40 && color.G <= 70 &&
                              color.B >= 50 && color.B <= 80 &&
                              Math.Abs(color.R - color.B) < 30; // Purple has similar red and blue

            return isBrownish || isPurplish;
        }
    }
}