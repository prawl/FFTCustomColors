using System.Collections.Generic;
using System.Drawing;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.Services
{
    public class ChocoboRecolorTests
    {
        private static ushort ToBgr555(int r, int g, int b)
            => (ushort)(((r >> 3) & 0x1F) | (((g >> 3) & 0x1F) << 5) | (((b >> 3) & 0x1F) << 10));

        private static (int r, int g, int b) FromBgr555(byte lo, byte hi)
        {
            ushort v = (ushort)(lo | (hi << 8));
            return ((v & 0x1F) << 3, ((v >> 5) & 0x1F) << 3, ((v >> 10) & 0x1F) << 3);
        }

        private static void SetSlot(byte[] d, int pal, int idx, int r, int g, int b)
        {
            ushort v = ToBgr555(r, g, b);
            int o = pal * 32 + idx * 2;
            d[o] = (byte)(v & 0xFF);
            d[o + 1] = (byte)((v >> 8) & 0xFF);
        }

        private static JobSection PrimarySection()
            => new JobSection("Primary", "Primary Color", new[] { 3, 4, 5, 6, 7, 8 },
                new[] { "outline", "shadow", "shadow", "base", "accent", "highlight" },
                linkedTo: null, primaryIndex: 6, shadeMode: ShadeMode.UniformHue);

        [Fact]
        public void ApplySection_RecolorsSectionToBaseHue_LeavingOtherIndicesUntouched()
        {
            var data = new byte[512];
            SetSlot(data, 0, 1, 40, 40, 32);   // outline — OUTSIDE the section, must stay put
            SetSlot(data, 0, 3, 100, 80, 30);  // gold body gradient (dark -> light)
            SetSlot(data, 0, 4, 140, 110, 40);
            SetSlot(data, 0, 5, 180, 140, 50);
            SetSlot(data, 0, 6, 200, 160, 60); // primary
            SetSlot(data, 0, 7, 220, 180, 70);
            SetSlot(data, 0, 8, 240, 210, 90);

            var outlineBefore = (data[2], data[3]);
            var index4Before = (data[8], data[9]);

            ChocoboRecolor.ApplySection(data, paletteIndex: 0, PrimarySection(), Color.FromArgb(45, 110, 225)); // blue

            // primary index (6) takes the base color exactly (UniformHue) -> blue-dominant
            var (r6, g6, b6) = FromBgr555(data[6 * 2], data[6 * 2 + 1]);
            Assert.True(b6 > r6 && b6 > g6, $"primary should be blue-dominant, got {r6},{g6},{b6}");

            // a body index actually changed
            Assert.NotEqual(index4Before, (data[8], data[9]));

            // out-of-section index untouched
            Assert.Equal(outlineBefore, (data[2], data[3]));
        }

        [Fact]
        public void ApplySection_TargetsOnlyTheGivenPalette()
        {
            var data = new byte[512];
            for (int idx = 3; idx <= 8; idx++)
            {
                SetSlot(data, 1, idx, 200, 160, 60); // palette 1 body (Black tier)
                SetSlot(data, 0, idx, 200, 160, 60); // palette 0 body (Yellow tier) — must NOT change
            }
            var pal0Before = (data[6 * 2], data[6 * 2 + 1]);

            ChocoboRecolor.ApplySection(data, paletteIndex: 1, PrimarySection(), Color.FromArgb(30, 175, 95)); // emerald

            Assert.Equal(pal0Before, (data[6 * 2], data[6 * 2 + 1])); // palette 0 untouched
            var (r, g, b) = FromBgr555(data[32 + 6 * 2], data[32 + 6 * 2 + 1]);
            Assert.True(g > r && g > b, $"palette 1 primary should be green-dominant, got {r},{g},{b}");
        }

        [Fact]
        public void Presets_OriginalFirst_NoDuplicateNamesAcrossTiers()
        {
            var presetNames = new List<string>();
            foreach (var tier in ChocoboThemePresets.TierKeys)
            {
                var names = ChocoboThemePresets.GetThemeNames(tier);
                Assert.Equal("original", names[0]);
                Assert.True(names.Count > 1, $"{tier} should have presets");
                presetNames.AddRange(names.GetRange(1, names.Count - 1));
            }
            Assert.Equal(presetNames.Count, new HashSet<string>(presetNames).Count);
        }

        [Fact]
        public void PaletteIndexForTier_MapsRanksToZeroOneTwo()
        {
            Assert.Equal(0, ChocoboThemePresets.PaletteIndexForTier("Chocobo_RankI"));
            Assert.Equal(1, ChocoboThemePresets.PaletteIndexForTier("Chocobo_RankII"));
            Assert.Equal(2, ChocoboThemePresets.PaletteIndexForTier("Chocobo_RankIII"));
        }

        [Fact]
        public void TryGetBaseColor_ReturnsFalseForOriginal_TrueForKnownPreset()
        {
            Assert.False(ChocoboThemePresets.TryGetBaseColor("Chocobo_RankI", "original", out _));
            Assert.True(ChocoboThemePresets.TryGetBaseColor("Chocobo_RankI", "Blue", out var c));
            Assert.True(c.B > c.R);
        }

        [Fact]
        public void ApplyTheme_ReturnsFalseForOriginal_TrueForPreset()
        {
            var data = new byte[512];
            for (int idx = 3; idx <= 8; idx++) SetSlot(data, 0, idx, 200, 160, 60);

            Assert.False(ChocoboRecolor.ApplyTheme(data, 0, new[] { PrimarySection() }, "Chocobo_RankI", "original", null));
            Assert.True(ChocoboRecolor.ApplyTheme(data, 0, new[] { PrimarySection() }, "Chocobo_RankI", "Blue", null));

            var (r, g, b) = FromBgr555(data[6 * 2], data[6 * 2 + 1]);
            Assert.True(b > r && b > g, $"preset should recolor to blue, got {r},{g},{b}");
        }

        [Fact]
        public void ApplyUserPaletteSection_CopiesSectionColorsFromUserPalette()
        {
            var data = new byte[512];
            for (int idx = 3; idx <= 8; idx++) SetSlot(data, 1, idx, 200, 160, 60); // tier palette 1, gold

            var userPalette = new byte[512]; // user theme stored as palette 0
            for (int idx = 3; idx <= 8; idx++) SetSlot(userPalette, 0, idx, 30, 175, 95); // emerald body

            ChocoboRecolor.ApplyUserPaletteSection(data, paletteIndex: 1, PrimarySection(), userPalette);

            // tier palette 1's section indices now carry the user palette's colors (green-dominant)
            var (r, g, b) = FromBgr555(data[32 + 6 * 2], data[32 + 6 * 2 + 1]);
            Assert.True(g > r && g > b, $"section should copy emerald from user palette, got {r},{g},{b}");
        }
    }
}
