using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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

        // --- CC-27: the editor must be able to reach ranks II and III, not just rank I ---
        //
        // A monster family's three ranks live in ONE bin as palettes 0/1/2. The theme editor
        // could only ever address palette 0, so designing a look for a Black Chocobo silently
        // showed and edited the YELLOW Chocobo colours instead.

        /// <summary>Bin whose palette 0 index 1 is RED and palette 1 index 1 is GREEN.</summary>
        private string WriteTwoRankBin()
        {
            var data = new byte[512 + 1024];
            data[2] = 0x1F; data[3] = 0x00;      // palette 0 index 1 -> red
            data[34] = 0xE0; data[35] = 0x03;    // palette 1 index 1 -> green (1*32 + 1*2)
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, data);
            return path;
        }

        [Fact]
        public void PaletteIndex_ReadsTheSelectedRanksColours_NotAlwaysRankOne()
        {
            var path = WriteTwoRankBin();
            try
            {
                var modifier = new PaletteModifier();
                modifier.LoadTemplate(path);

                modifier.PaletteIndex = 1; // rank II

                var c = modifier.GetOriginalPaletteColor(1);
                Assert.Equal(0, c.R);
                Assert.Equal(255, c.G);
                Assert.Equal(0, c.B);
            }
            finally { File.Delete(path); }
        }

        /// <summary>
        /// Bin whose SW sprite pixel (0,0) is color index 5, palette 0 index 5 is RED and
        /// palette 1 index 5 is GREEN. Used to prove the sprite PREVIEW follows PaletteIndex
        /// too, not just direct color reads.
        /// </summary>
        private string WriteTwoRankBinWithSwPixel()
        {
            var data = new byte[512 + 1024];
            data[10] = 0x1F; data[11] = 0x00;          // palette 0 index 5 -> red
            data[32 + 10] = 0xE0; data[32 + 11] = 0x03; // palette 1 index 5 -> green
            data[528] = 0x05;                           // SW sprite (index 1) pixel (0,0) -> color index 5
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, data);
            return path;
        }

        [Fact]
        public void GetPreview_UsesTheSelectedRanksPalette_NotAlwaysRankOne()
        {
            var path = WriteTwoRankBinWithSwPixel();
            try
            {
                var modifier = new PaletteModifier();
                modifier.LoadTemplate(path); // no jobName/modPath, so no HD BMP path — bin extraction only

                modifier.PaletteIndex = 1; // rank II

                using var preview = modifier.GetPreview(); // default direction 5 = SW
                var pixel = preview.GetPixel(1, 1); // maps back to source pixel (0,0)

                Assert.True(pixel.G > pixel.R,
                    $"expected rank II's green at the SW sprite's origin pixel, got R={pixel.R} G={pixel.G} B={pixel.B}");
            }
            finally { File.Delete(path); }
        }

        /// <summary>
        /// Builds a tiny modPath/Images/&lt;job&gt;/original/*.bmp fixture and a matching bin
        /// whose palette 0 index 2 is RED (rank I) and palette 1 index 2 is GREEN (rank II).
        /// Exercises the HD-BMP preview path (TryGetHdPreview), which is the path that actually
        /// renders for monsters in production (bin extraction is only the fallback when no HD
        /// BMP is found) — a separate hardcode from the bin-extraction one covered above.
        /// </summary>
        private static string WriteHdPreviewFixture(string modPath, string jobName, string binPath)
        {
            var bmpDir = Path.Combine(modPath, "Images", jobName, "original");
            Directory.CreateDirectory(bmpDir);

            // Standard FrameLayout: SW pose is the 64x80 box at (64, 0). Make the sheet big
            // enough to hold it, with one marker pixel at the SW box's origin (64, 0) set to
            // palette index 2 — everything else stays index 0 (transparent background).
            const int sheetWidth = 200;
            const int sheetHeight = 100;
            using (var bmp = new Bitmap(sheetWidth, sheetHeight, PixelFormat.Format4bppIndexed))
            {
                var rect = new Rectangle(0, 0, sheetWidth, sheetHeight);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format4bppIndexed);
                try
                {
                    var buffer = new byte[data.Stride * sheetHeight];
                    // Marker pixel (64, 0) -> palette index 2 (even x -> high nibble).
                    buffer[64 >> 1] = 0x20;
                    Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
                }
                finally { bmp.UnlockBits(data); }

                bmp.Save(Path.Combine(bmpDir, "0_test_hd.bmp"), ImageFormat.Bmp);
            }

            var binData = new byte[512];
            binData[4] = 0x1F; binData[5] = 0x00;   // palette 0 index 2 -> red
            binData[36] = 0xE0; binData[37] = 0x03; // palette 1 index 2 -> green (1*32 + 2*2)
            File.WriteAllBytes(binPath, binData);

            return bmpDir;
        }

        [Fact]
        public void TryGetHdPreview_UsesTheSelectedRanksPalette_NotAlwaysRankOne()
        {
            var modPath = Path.Combine(Path.GetTempPath(), "PaletteModifierHdPreview_" + Guid.NewGuid().ToString("N"));
            var binPath = Path.Combine(Path.GetTempPath(), "hd_preview_test_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                WriteHdPreviewFixture(modPath, "TestMonster", binPath);

                var modifier = new PaletteModifier();
                modifier.LoadTemplate(binPath, "TestMonster", modPath); // jobName + modPath -> HD preview path, not bin fallback

                modifier.PaletteIndex = 1; // rank II

                using var preview = modifier.GetPreview(); // default direction 5 = SW
                var pixel = preview.GetPixel(0, 0); // sheet (64, 0), the SW box's origin

                // Exact values, not a loose G > R comparison — GDI's default 4bpp palette
                // entry 2 happens to already be a shade of green (0,128,0), so a loose
                // comparison would pass even with the palette swap switched off entirely.
                Assert.Equal(0, pixel.R);
                Assert.Equal(255, pixel.G);
                Assert.Equal(0, pixel.B);
            }
            finally
            {
                if (File.Exists(binPath)) File.Delete(binPath);
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);
            }
        }

        /// <summary>
        /// Construct 8's non-standard 48x48 layout is read through ExtractCustomRect rather
        /// than ExtractAllDirections, and is only reached when the HD preview path returns
        /// null (no modPath here). Its own paletteIndex argument is a separate hardcode from
        /// the ExtractAllDirections one covered above — cover it directly rather than relying
        /// on the fact that Construct8 is a story character and never a monster in production.
        /// </summary>
        [Fact]
        public void GetPreview_ForConstruct8_UsesTheSelectedRanksPalette_NotAlwaysRankOne()
        {
            var data = new byte[512 + 1024];
            data[10] = 0x1F; data[11] = 0x00;           // palette 0 index 5 -> red
            data[32 + 10] = 0xE0; data[32 + 11] = 0x03; // palette 1 index 5 -> green
            data[536] = 0x05;                            // custom-rect pixel (0,0) at sheet (48, 0) -> color index 5

            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, data);
            try
            {
                var modifier = new PaletteModifier();
                modifier.LoadTemplate(path, "Construct8", null); // no modPath -> HD preview path returns null, custom-rect branch taken

                modifier.PaletteIndex = 1; // rank II

                using var preview = modifier.GetPreview(); // default direction 5 = SW (irrelevant to Construct8's custom rect)
                var pixel = preview.GetPixel(1, 25); // scaled custom rect: source pixel (0,0) covers display (0,24)-(2,26)

                Assert.Equal(0, pixel.R);
                Assert.Equal(255, pixel.G);
                Assert.Equal(0, pixel.B);
            }
            finally { File.Delete(path); }
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
        public void ApplySectionColor_WithAccentRoles_PreservesOriginalRelationships()
        {
            // Arrange - RelativeShadeGenerator preserves original color relationships
            var modifier = new PaletteModifier();
            modifier.LoadTemplate(_testBinPath);

            var section = new JobSection(
                name: "HeadbandArmsBoots",
                displayName: "Headband, Arms & Boots",
                indices: new[] { 4, 5, 3, 7, 6 },
                roles: new[] { "base", "highlight", "shadow", "accent", "accent_shadow" }
            );

            // Get original color relationships
            var origBase = modifier.GetOriginalPaletteColor(4);
            var origAccent = modifier.GetOriginalPaletteColor(7);
            var origAccentShadow = modifier.GetOriginalPaletteColor(6);

            var origBaseHsl = HslColor.FromRgb(origBase);
            var origAccentHsl = HslColor.FromRgb(origAccent);
            var origAccentShadowHsl = HslColor.FromRgb(origAccentShadow);

            // Calculate original ratios
            var origAccentLRatio = origBaseHsl.L > 0.001 ? origAccentHsl.L / origBaseHsl.L : 1.0;
            var origAccentShadowLRatio = origBaseHsl.L > 0.001 ? origAccentShadowHsl.L / origBaseHsl.L : 1.0;

            var baseColor = System.Drawing.Color.FromArgb(0, 200, 100); // Green

            // Act
            modifier.ApplySectionColor(section, baseColor);

            // Assert - Relationships should be preserved
            var newBase = modifier.GetPaletteColor(4);
            var newAccent = modifier.GetPaletteColor(7);
            var newAccentShadow = modifier.GetPaletteColor(6);

            var newBaseHsl = HslColor.FromRgb(newBase);
            var newAccentHsl = HslColor.FromRgb(newAccent);
            var newAccentShadowHsl = HslColor.FromRgb(newAccentShadow);

            // Calculate new ratios
            var newAccentLRatio = newBaseHsl.L > 0.001 ? newAccentHsl.L / newBaseHsl.L : 1.0;
            var newAccentShadowLRatio = newBaseHsl.L > 0.001 ? newAccentShadowHsl.L / newBaseHsl.L : 1.0;

            // Ratios should be preserved (within tolerance for rounding)
            Assert.True(Math.Abs(newAccentLRatio - origAccentLRatio) < 0.15,
                $"Accent lightness ratio should be preserved. Expected ~{origAccentLRatio}, got {newAccentLRatio}");
            Assert.True(Math.Abs(newAccentShadowLRatio - origAccentShadowLRatio) < 0.15,
                $"AccentShadow lightness ratio should be preserved. Expected ~{origAccentShadowLRatio}, got {newAccentShadowLRatio}");
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

        /// <summary>
        /// Pins the saved-theme format contract: a flat 512 byte block (16 palettes x 32 bytes)
        /// regardless of which rank is selected. Nothing else in the suite asserts this against
        /// a modifier that actually loaded a real template — MonsterRecolor.ApplyUserPaletteSection
        /// reads this length as a hard assumption. See CC-27.
        /// </summary>
        [Fact]
        public void GetModifiedPalette_ReturnsA512ByteBlock()
        {
            var modifier = new PaletteModifier();
            modifier.LoadTemplate(_testBinPath);

            Assert.Equal(512, modifier.GetModifiedPalette().Length);
        }
    }
}
