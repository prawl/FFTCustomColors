using System;
using System.Collections.Generic;
using System.Drawing;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// How the per-index relationships are captured and re-applied. Default is Preserve
    /// (legacy behavior). UniformHue is for sections where every index is meant to be
    /// a lightness/saturation variant of one shared color — useful when a section has
    /// 4+ indices and the original sprite's hue drift between them shouldn't survive
    /// a theme change.
    /// </summary>
    public enum ShadeMode
    {
        /// <summary>
        /// Capture per-index HueDelta (additive) + Saturation/Lightness ratios (multiplicative)
        /// from the original sprite. Preserves hue drift between indices. Works well for small
        /// 2-3 index groups but collapses at extremes and loses cohesion on large groups.
        /// </summary>
        Preserve,

        /// <summary>
        /// Force every index to share the user's chosen hue. Capture per-index Saturation/Lightness
        /// deltas additively (not as ratios). Result: all indices become evenly-spaced shades of
        /// the picked color, robust at any base lightness.
        /// </summary>
        UniformHue
    }

    /// <summary>
    /// Generates color shades by preserving the relative HSL relationships
    /// from original sprite colors rather than using fixed multipliers.
    /// </summary>
    public class RelativeShadeGenerator
    {
        private readonly Dictionary<int, ColorRelationship> _relationships;
        private readonly int _primaryIndex;
        private readonly ShadeMode _mode;

        public RelativeShadeGenerator(Dictionary<int, Color> originalColors, int primaryIndex)
            : this(originalColors, primaryIndex, ShadeMode.Preserve)
        {
        }

        /// <summary>
        /// Captures the HSL relationships between each color index and the primary (base) color.
        /// </summary>
        /// <param name="originalColors">Dictionary mapping palette index to original color</param>
        /// <param name="primaryIndex">The index of the primary/base color</param>
        /// <param name="mode">How to capture and apply the per-index relationships</param>
        public RelativeShadeGenerator(Dictionary<int, Color> originalColors, int primaryIndex, ShadeMode mode)
        {
            _primaryIndex = primaryIndex;
            _mode = mode;
            _relationships = new Dictionary<int, ColorRelationship>();

            if (!originalColors.ContainsKey(primaryIndex))
                throw new ArgumentException($"Primary index {primaryIndex} not found in original colors");

            var primaryHsl = HslColor.FromRgb(originalColors[primaryIndex]);

            foreach (var kvp in originalColors)
            {
                var indexHsl = HslColor.FromRgb(kvp.Value);

                if (mode == ShadeMode.UniformHue)
                {
                    // Capture additive S/L deltas; hue is discarded (all indices share base hue).
                    _relationships[kvp.Key] = new ColorRelationship
                    {
                        SaturationDelta = indexHsl.S - primaryHsl.S,
                        LightnessDelta = indexHsl.L - primaryHsl.L
                    };
                }
                else
                {
                    // Preserve mode: hue delta + multiplicative S/L ratios from the original.
                    _relationships[kvp.Key] = new ColorRelationship
                    {
                        HueDelta = indexHsl.H - primaryHsl.H,
                        SaturationRatio = primaryHsl.S > 0.001 ? indexHsl.S / primaryHsl.S : 1.0,
                        LightnessRatio = primaryHsl.L > 0.001 ? indexHsl.L / primaryHsl.L : 1.0
                    };
                }
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

            double newHue, newSaturation, newLightness;

            if (_mode == ShadeMode.UniformHue)
            {
                // Inherit hue from base; offset S/L additively. Additive offsets keep contrast
                // stable at any base lightness (whereas ratios collapse near 0 or 1).
                newHue = newBaseHsl.H;
                newSaturation = newBaseHsl.S + relationship.SaturationDelta;
                newLightness = newBaseHsl.L + relationship.LightnessDelta;
            }
            else
            {
                newHue = (newBaseHsl.H + relationship.HueDelta) % 360;
                if (newHue < 0) newHue += 360;
                newSaturation = newBaseHsl.S * relationship.SaturationRatio;
                newLightness = newBaseHsl.L * relationship.LightnessRatio;
            }

            newSaturation = Math.Max(0, Math.Min(1, newSaturation));
            newLightness = Math.Max(0, Math.Min(1, newLightness));

            return new HslColor(newHue, newSaturation, newLightness).ToRgb();
        }

        private struct ColorRelationship
        {
            // Preserve mode fields
            public double HueDelta;
            public double SaturationRatio;
            public double LightnessRatio;

            // UniformHue mode fields
            public double SaturationDelta;
            public double LightnessDelta;
        }
    }
}
