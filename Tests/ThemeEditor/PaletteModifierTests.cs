using System;
using System.IO;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    public class PaletteModifierTests : IDisposable
    {
        private readonly string _testBinPath;
        private readonly byte[] _testBinData;

        public PaletteModifierTests()
        {
            // Create minimal test .bin file (512 bytes palette + sprite data)
            _testBinData = new byte[512 + 1024];

            // Set up a basic palette with some colors
            // Color 1: Red (BGR555: 0x001F)
            _testBinData[2] = 0x1F;
            _testBinData[3] = 0x00;

            // Save to temp file
            _testBinPath = Path.GetTempFileName();
            File.WriteAllBytes(_testBinPath, _testBinData);
        }

        public void Dispose()
        {
            if (File.Exists(_testBinPath))
                File.Delete(_testBinPath);
        }

        [Fact]
        public void LoadTemplate_WithValidBinFile_LoadsDataSuccessfully()
        {
            // Arrange
            var modifier = new PaletteModifier();

            // Act
            modifier.LoadTemplate(_testBinPath);

            // Assert
            Assert.True(modifier.IsLoaded);
        }

        [Fact]
        public void SetPaletteColor_WithValidIndex_UpdatesPaletteData()
        {
            // Arrange
            var modifier = new PaletteModifier();
            modifier.LoadTemplate(_testBinPath);
            var color = System.Drawing.Color.FromArgb(255, 0, 0); // Pure red

            // Act
            modifier.SetPaletteColor(1, color);

            // Assert - verify the BGR555 value was written correctly
            // Red (255,0,0) -> BGR555: r5=31, g5=0, b5=0 -> 0x001F
            var paletteData = modifier.GetModifiedPalette();
            ushort bgr555 = (ushort)(paletteData[2] | (paletteData[3] << 8));
            Assert.Equal(0x001F, bgr555);
        }

        [Fact]
        public void Reset_AfterModification_RestoresOriginalPalette()
        {
            // Arrange
            var modifier = new PaletteModifier();
            modifier.LoadTemplate(_testBinPath);
            var originalPalette = modifier.GetModifiedPalette();
            byte originalByte2 = originalPalette[2];

            // Modify the palette
            modifier.SetPaletteColor(1, System.Drawing.Color.FromArgb(0, 255, 0)); // Green

            // Act
            modifier.Reset();

            // Assert - palette should be restored to original
            var resetPalette = modifier.GetModifiedPalette();
            Assert.Equal(originalByte2, resetPalette[2]);
        }

        [Fact]
        public void GetPreview_AfterLoad_ReturnsBitmap()
        {
            // Arrange
            var modifier = new PaletteModifier();
            modifier.LoadTemplate(_testBinPath);

            // Act
            var bitmap = modifier.GetPreview();

            // Assert
            Assert.NotNull(bitmap);
            Assert.True(bitmap.Width > 0);
            Assert.True(bitmap.Height > 0);
        }

        [Fact]
        public void ApplySectionColor_WithShadowBaseHighlight_AppliesShadesToIndices()
        {
            // Arrange
            var modifier = new PaletteModifier();
            modifier.LoadTemplate(_testBinPath);

            var section = new JobSection(
                name: "Cape",
                displayName: "Cape",
                indices: new[] { 3, 4, 5 },
                roles: new[] { "shadow", "base", "highlight" }
            );
            var baseColor = System.Drawing.Color.FromArgb(0, 100, 200); // Blue

            // Act
            modifier.ApplySectionColor(section, baseColor);

            // Assert - verify palette bytes were modified at correct indices
            var palette = modifier.GetModifiedPalette();

            // Index 3 (shadow) should be darker than index 4 (base)
            // Index 5 (highlight) should be lighter than index 4 (base)
            // Just verify all three indices were written (non-zero)
            ushort shadow = (ushort)(palette[6] | (palette[7] << 8));
            ushort baseVal = (ushort)(palette[8] | (palette[9] << 8));
            ushort highlight = (ushort)(palette[10] | (palette[11] << 8));

            Assert.NotEqual(0, shadow);
            Assert.NotEqual(0, baseVal);
            Assert.NotEqual(0, highlight);
        }

        [Fact]
        public void ApplySectionColor_WithAccentRoles_DoesNotOverwriteAccents()
        {
            // Arrange - Bug: ApplySectionColor was treating accent roles as "base",
            // which overwrites the original accent colors when reset is clicked.
            var modifier = new PaletteModifier();
            modifier.LoadTemplate(_testBinPath);

            // Capture original colors at accent indices before any modification
            var originalAccentColor = modifier.GetPaletteColor(2);
            var originalAccentShadowColor = modifier.GetPaletteColor(1);

            var section = new JobSection(
                name: "MainArmor",
                displayName: "Main Armor",
                indices: new[] { 4, 5, 3, 2, 1 },
                roles: new[] { "base", "highlight", "shadow", "accent", "accent_shadow" }
            );
            var baseColor = System.Drawing.Color.FromArgb(0, 100, 200); // Blue

            // Act
            modifier.ApplySectionColor(section, baseColor);

            // Assert - Accent indices (2, 1) should NOT be overwritten
            // They should remain unchanged from original
            var afterAccentColor = modifier.GetPaletteColor(2);
            var afterAccentShadowColor = modifier.GetPaletteColor(1);

            Assert.Equal(originalAccentColor.R, afterAccentColor.R);
            Assert.Equal(originalAccentColor.G, afterAccentColor.G);
            Assert.Equal(originalAccentColor.B, afterAccentColor.B);
            Assert.Equal(originalAccentShadowColor.R, afterAccentShadowColor.R);
            Assert.Equal(originalAccentShadowColor.G, afterAccentShadowColor.G);
            Assert.Equal(originalAccentShadowColor.B, afterAccentShadowColor.B);
        }

        [Fact]
        public void SaveToFile_AfterModification_WritesModifiedData()
        {
            // Arrange
            var modifier = new PaletteModifier();
            modifier.LoadTemplate(_testBinPath);
            modifier.SetPaletteColor(5, System.Drawing.Color.FromArgb(255, 128, 0)); // Orange

            var outputPath = Path.GetTempFileName();
            try
            {
                // Act
                modifier.SaveToFile(outputPath);

                // Assert - verify file was written and contains modified palette
                Assert.True(File.Exists(outputPath));
                var savedData = File.ReadAllBytes(outputPath);
                Assert.Equal(_testBinData.Length, savedData.Length);

                // Index 5 should have the orange color (BGR555)
                ushort color = (ushort)(savedData[10] | (savedData[11] << 8));
                Assert.NotEqual(0, color);
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }
    }
}
