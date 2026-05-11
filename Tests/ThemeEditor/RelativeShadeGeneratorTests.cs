using System.Drawing;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    public class RelativeShadeGeneratorTests
    {
        [Fact]
        public void CaptureRelationships_WithTwoColors_CalculatesLightnessRatio()
        {
            // Arrange - original colors from sprite
            // Base color at lightness 0.5, shadow at lightness 0.3 (60% of base)
            var baseColor = Color.FromArgb(128, 128, 128); // ~0.5 lightness
            var shadowColor = Color.FromArgb(77, 77, 77);  // ~0.3 lightness

            var originalColors = new Dictionary<int, Color>
            {
                { 10, shadowColor },  // shadow
                { 12, baseColor }     // base (primary)
            };

            // Act
            var generator = new RelativeShadeGenerator(originalColors, primaryIndex: 12);

            // Assert - should have captured the relationship
            Assert.NotNull(generator);
        }

        [Fact]
        public void GenerateShade_PreservesLightnessRatio()
        {
            // Arrange - original: base at L=0.5, shadow at L=0.3 (ratio = 0.6)
            var originalBase = Color.FromArgb(128, 128, 128);   // L ~0.5
            var originalShadow = Color.FromArgb(77, 77, 77);    // L ~0.3

            var originalColors = new Dictionary<int, Color>
            {
                { 10, originalShadow },
                { 12, originalBase }
            };

            var generator = new RelativeShadeGenerator(originalColors, primaryIndex: 12);

            // New base color - red at same lightness
            var newBaseColor = Color.FromArgb(255, 128, 128); // Red, L ~0.75

            // Act
            var newShadow = generator.GenerateShade(10, newBaseColor);

            // Assert - new shadow should maintain the ~60% lightness ratio
            var newShadowHsl = HslColor.FromRgb(newShadow);
            var newBaseHsl = HslColor.FromRgb(newBaseColor);

            // The ratio should be preserved (within tolerance for rounding)
            var originalRatio = 0.3 / 0.5; // 0.6
            var newRatio = newShadowHsl.L / newBaseHsl.L;

            Assert.True(Math.Abs(newRatio - originalRatio) < 0.1,
                $"Expected ratio ~{originalRatio}, got {newRatio}");
        }

        [Fact]
        public void GenerateShade_PreservesHueFromNewColor()
        {
            // Arrange - original gray colors (no hue)
            var originalBase = Color.FromArgb(128, 128, 128);
            var originalShadow = Color.FromArgb(77, 77, 77);

            var originalColors = new Dictionary<int, Color>
            {
                { 10, originalShadow },
                { 12, originalBase }
            };

            var generator = new RelativeShadeGenerator(originalColors, primaryIndex: 12);

            // New base color - pure red
            var newBaseColor = Color.FromArgb(255, 0, 0);

            // Act
            var newShadow = generator.GenerateShade(10, newBaseColor);

            // Assert - shadow should be red (same hue as new base)
            var newShadowHsl = HslColor.FromRgb(newShadow);
            var newBaseHsl = HslColor.FromRgb(newBaseColor);

            // Hue should match (within tolerance)
            Assert.True(Math.Abs(newShadowHsl.H - newBaseHsl.H) < 5,
                $"Expected hue ~{newBaseHsl.H}, got {newShadowHsl.H}");
        }

        [Fact]
        public void GenerateShade_PreservesSaturationRatio()
        {
            // Arrange - original colors with different saturations
            var originalBase = Color.FromArgb(200, 100, 100);    // Saturated red
            var originalShadow = Color.FromArgb(120, 80, 80);    // Less saturated, darker

            var originalColors = new Dictionary<int, Color>
            {
                { 10, originalShadow },
                { 12, originalBase }
            };

            var generator = new RelativeShadeGenerator(originalColors, primaryIndex: 12);

            // New base color - blue
            var newBaseColor = Color.FromArgb(100, 100, 200);

            // Act
            var newShadow = generator.GenerateShade(10, newBaseColor);

            // Assert - saturation relationship should be preserved
            var origBaseHsl = HslColor.FromRgb(originalBase);
            var origShadowHsl = HslColor.FromRgb(originalShadow);
            var newBaseHsl = HslColor.FromRgb(newBaseColor);
            var newShadowHsl = HslColor.FromRgb(newShadow);

            var origSatRatio = origShadowHsl.S / origBaseHsl.S;
            var newSatRatio = newShadowHsl.S / newBaseHsl.S;

            Assert.True(Math.Abs(newSatRatio - origSatRatio) < 0.15,
                $"Expected sat ratio ~{origSatRatio}, got {newSatRatio}");
        }

        [Fact]
        public void GenerateShade_ForPrimaryIndex_ReturnsNewBaseColor()
        {
            // Arrange
            var originalBase = Color.FromArgb(128, 128, 128);
            var originalShadow = Color.FromArgb(77, 77, 77);

            var originalColors = new Dictionary<int, Color>
            {
                { 10, originalShadow },
                { 12, originalBase }
            };

            var generator = new RelativeShadeGenerator(originalColors, primaryIndex: 12);

            var newBaseColor = Color.FromArgb(255, 0, 0);

            // Act - ask for the primary index color
            var result = generator.GenerateShade(12, newBaseColor);

            // Assert - should return the new base color exactly
            Assert.Equal(newBaseColor.R, result.R);
            Assert.Equal(newBaseColor.G, result.G);
            Assert.Equal(newBaseColor.B, result.B);
        }

        [Fact]
        public void GenerateShade_WithHighlightLighterThanBase_PreservesRelationship()
        {
            // Arrange - highlight is lighter than base
            var originalBase = Color.FromArgb(128, 128, 128);      // L ~0.5
            var originalHighlight = Color.FromArgb(192, 192, 192); // L ~0.75

            var originalColors = new Dictionary<int, Color>
            {
                { 12, originalBase },
                { 13, originalHighlight }
            };

            var generator = new RelativeShadeGenerator(originalColors, primaryIndex: 12);

            var newBaseColor = Color.FromArgb(128, 0, 0); // Dark red

            // Act
            var newHighlight = generator.GenerateShade(13, newBaseColor);

            // Assert - highlight should be lighter than base
            var newBaseHsl = HslColor.FromRgb(newBaseColor);
            var newHighlightHsl = HslColor.FromRgb(newHighlight);

            Assert.True(newHighlightHsl.L > newBaseHsl.L,
                $"Highlight L ({newHighlightHsl.L}) should be > base L ({newBaseHsl.L})");
        }

        [Fact]
        public void GenerateShade_ClampsLightnessToValidRange()
        {
            // Arrange - extreme case where ratio would push lightness > 1
            var originalBase = Color.FromArgb(64, 64, 64);        // L ~0.25
            var originalHighlight = Color.FromArgb(224, 224, 224); // L ~0.88 (ratio = 3.5x)

            var originalColors = new Dictionary<int, Color>
            {
                { 12, originalBase },
                { 13, originalHighlight }
            };

            var generator = new RelativeShadeGenerator(originalColors, primaryIndex: 12);

            // New base with high lightness - ratio would push highlight above 1.0
            var newBaseColor = Color.FromArgb(200, 200, 200); // L ~0.78

            // Act
            var newHighlight = generator.GenerateShade(13, newBaseColor);

            // Assert - lightness should be clamped to valid range (0-1)
            var newHighlightHsl = HslColor.FromRgb(newHighlight);
            Assert.True(newHighlightHsl.L <= 1.0, $"Lightness {newHighlightHsl.L} should be <= 1.0");
            Assert.True(newHighlightHsl.L >= 0.0, $"Lightness {newHighlightHsl.L} should be >= 0.0");
        }

        [Fact]
        public void UniformHue_AllShadesMatchNewBaseHue_EvenWhenOriginalsHadHueOffsets()
        {
            // Construct 8 case: originals are 5 indices with slightly different hues
            // (artist used warm-brown accents next to neutral-gray base). In Preserve mode
            // those hue deltas carry through and the result isn't cohesive. In UniformHue
            // mode all shades inherit the user's chosen hue exactly.
            var originals = new Dictionary<int, Color>
            {
                { 7, Color.FromArgb(40, 40, 50) },     // outline — slightly blue-tinted gray
                { 6, Color.FromArgb(80, 70, 60) },     // shadow — warm gray
                { 5, Color.FromArgb(140, 130, 120) },  // base — neutral
                { 4, Color.FromArgb(200, 170, 140) },  // accent — warm tan
                { 3, Color.FromArgb(230, 220, 200) }   // highlight — cream
            };

            var generator = new RelativeShadeGenerator(originals, primaryIndex: 5, ShadeMode.UniformHue);
            var newBase = Color.FromArgb(80, 120, 200); // pure blue
            var newBaseHsl = HslColor.FromRgb(newBase);

            foreach (var idx in originals.Keys)
            {
                var shade = generator.GenerateShade(idx, newBase);
                var shadeHsl = HslColor.FromRgb(shade);
                // Allow tiny rounding tolerance (HSL roundtrip can drift a fraction of a degree)
                Assert.True(Math.Abs(shadeHsl.H - newBaseHsl.H) < 2,
                    $"Index {idx}: expected hue ~{newBaseHsl.H}, got {shadeHsl.H}");
            }
        }

        [Fact]
        public void UniformHue_PreservesLightnessDeltas_Additively_NotMultiplicatively()
        {
            // Original: base L=0.5, highlight L=0.75 (delta = +0.25 additive, ratio = 1.5x)
            // Pick a near-white new base (L=0.9). Multiplicative would want 0.9*1.5=1.35 → clamp to 1.0.
            // Additive wants 0.9 + 0.25 = 1.15 → clamps to 1.0 too BUT the shadow side stays well-separated:
            // shadow had delta -0.2 (L=0.3 vs 0.5). New shadow = 0.9 - 0.2 = 0.7 vs new base 0.9 → contrast preserved.
            // Multiplicative shadow = 0.9 * 0.6 = 0.54 → much darker than expected, loses cohesion.
            var originals = new Dictionary<int, Color>
            {
                { 10, Color.FromArgb(77, 77, 77) },    // shadow L~0.3
                { 12, Color.FromArgb(128, 128, 128) }, // base L~0.5
                { 13, Color.FromArgb(192, 192, 192) }  // highlight L~0.75
            };
            var generator = new RelativeShadeGenerator(originals, primaryIndex: 12, ShadeMode.UniformHue);

            var nearWhite = Color.FromArgb(229, 229, 229); // L~0.9
            var newShadow = generator.GenerateShade(10, nearWhite);
            var newShadowHsl = HslColor.FromRgb(newShadow);

            // Additive: 0.9 + (0.3 - 0.5) = 0.7. Within tolerance.
            Assert.True(Math.Abs(newShadowHsl.L - 0.7) < 0.05,
                $"Additive shadow L should be ~0.7 (base 0.9 + delta -0.2), got {newShadowHsl.L}");
        }

        [Fact]
        public void UniformHue_KeepsShadowDarkerThanBase_AndHighlightLighter()
        {
            var originals = new Dictionary<int, Color>
            {
                { 10, Color.FromArgb(77, 77, 77) },
                { 12, Color.FromArgb(128, 128, 128) },
                { 13, Color.FromArgb(192, 192, 192) }
            };
            var generator = new RelativeShadeGenerator(originals, primaryIndex: 12, ShadeMode.UniformHue);

            var newBase = Color.FromArgb(100, 50, 50); // dark red
            var newShadow = generator.GenerateShade(10, newBase);
            var newHighlight = generator.GenerateShade(13, newBase);
            var baseHsl = HslColor.FromRgb(newBase);
            var shadowHsl = HslColor.FromRgb(newShadow);
            var highlightHsl = HslColor.FromRgb(newHighlight);

            Assert.True(shadowHsl.L < baseHsl.L, $"Shadow L ({shadowHsl.L}) should be < base L ({baseHsl.L})");
            Assert.True(highlightHsl.L > baseHsl.L, $"Highlight L ({highlightHsl.L}) should be > base L ({baseHsl.L})");
        }
    }
}
