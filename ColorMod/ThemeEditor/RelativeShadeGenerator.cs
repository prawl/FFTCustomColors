using System;
using System.Collections.Generic;
using System.Drawing;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Generates color shades by preserving the relative HSL relationships
    /// from original sprite colors rather than using fixed multipliers.
    /// </summary>
    public class RelativeShadeGenerator
    {
        private readonly Dictionary<int, ColorRelationship> _relationships;
        private readonly int _primaryIndex;

        /// <summary>
        /// Captures the HSL relationships between each color index and the primary (base) color.
        /// </summary>
        /// <param name="originalColors">Dictionary mapping palette index to original color</param>
        /// <param name="primaryIndex">The index of the primary/base color</param>
        public RelativeShadeGenerator(Dictionary<int, Color> originalColors, int primaryIndex)
        {
            _primaryIndex = primaryIndex;
            _relationships = new Dictionary<int, ColorRelationship>();

            if (!originalColors.ContainsKey(primaryIndex))
                throw new ArgumentException($"Primary index {primaryIndex} not found in original colors");

            var primaryHsl = HslColor.FromRgb(originalColors[primaryIndex]);

            foreach (var kvp in originalColors)
            {
                var indexHsl = HslColor.FromRgb(kvp.Value);

                // Calculate ratios relative to primary color
                // Use differences for hue (additive), ratios for saturation/lightness (multiplicative)
                var relationship = new ColorRelationship
                {
                    HueDelta = indexHsl.H - primaryHsl.H,
                    SaturationRatio = primaryHsl.S > 0.001 ? indexHsl.S / primaryHsl.S : 1.0,
                    LightnessRatio = primaryHsl.L > 0.001 ? indexHsl.L / primaryHsl.L : 1.0
                };

                _relationships[kvp.Key] = relationship;
            }
        }

        /// <summary>
        /// Generates a shade for the specified index based on the new base color,
        /// preserving the original relative relationships.
        /// </summary>
        /// <param name="index">The palette index to generate a color for</param>
        /// <param name="newBaseColor">The new base color selected by the user</param>
        /// <returns>The generated color that preserves the original relationship</returns>
        public Color GenerateShade(int index, Color newBaseColor)
        {
            // If this is the primary index, return the new base color exactly
            if (index == _primaryIndex)
                return newBaseColor;

            if (!_relationships.ContainsKey(index))
                throw new ArgumentException($"Index {index} was not in the original colors");

            var relationship = _relationships[index];
            var newBaseHsl = HslColor.FromRgb(newBaseColor);

            // Apply the relationships to the new base color
            var newHue = (newBaseHsl.H + relationship.HueDelta) % 360;
            if (newHue < 0) newHue += 360;

            var newSaturation = newBaseHsl.S * relationship.SaturationRatio;
            var newLightness = newBaseHsl.L * relationship.LightnessRatio;

            // Clamp to valid ranges
            newSaturation = Math.Max(0, Math.Min(1, newSaturation));
            newLightness = Math.Max(0, Math.Min(1, newLightness));

            var resultHsl = new HslColor(newHue, newSaturation, newLightness);
            return resultHsl.ToRgb();
        }

        private struct ColorRelationship
        {
            public double HueDelta;
            public double SaturationRatio;
            public double LightnessRatio;
        }
    }
}
