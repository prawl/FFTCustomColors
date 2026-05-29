using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.Services
{
    public class MonsterRecolorTests
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

        // ---- Recolor engine -------------------------------------------------

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

            MonsterRecolor.ApplySection(data, paletteIndex: 0, PrimarySection(), Color.FromArgb(45, 110, 225)); // blue

            var (r6, g6, b6) = FromBgr555(data[6 * 2], data[6 * 2 + 1]);
            Assert.True(b6 > r6 && b6 > g6, $"primary should be blue-dominant, got {r6},{g6},{b6}");
            Assert.NotEqual(index4Before, (data[8], data[9]));     // a body index actually changed
            Assert.Equal(outlineBefore, (data[2], data[3]));       // out-of-section index untouched
        }

        [Fact]
        public void ApplySection_TargetsOnlyTheGivenPalette()
        {
            var data = new byte[512];
            for (int idx = 3; idx <= 8; idx++)
            {
                SetSlot(data, 1, idx, 200, 160, 60); // palette 1 body (Rank II)
                SetSlot(data, 0, idx, 200, 160, 60); // palette 0 body (Rank I) — must NOT change
            }
            var pal0Before = (data[6 * 2], data[6 * 2 + 1]);

            MonsterRecolor.ApplySection(data, paletteIndex: 1, PrimarySection(), Color.FromArgb(30, 175, 95)); // emerald

            Assert.Equal(pal0Before, (data[6 * 2], data[6 * 2 + 1])); // palette 0 untouched
            var (r, g, b) = FromBgr555(data[32 + 6 * 2], data[32 + 6 * 2 + 1]);
            Assert.True(g > r && g > b, $"palette 1 primary should be green-dominant, got {r},{g},{b}");
        }

        [Fact]
        public void ApplyTheme_ReturnsFalseForOriginal_TrueForPreset()
        {
            var data = new byte[512];
            for (int idx = 3; idx <= 8; idx++) SetSlot(data, 0, idx, 200, 160, 60);

            Assert.False(MonsterRecolor.ApplyTheme(data, 0, new[] { PrimarySection() }, "Chocobo_RankI", "original", null));
            Assert.True(MonsterRecolor.ApplyTheme(data, 0, new[] { PrimarySection() }, "Chocobo_RankI", "Blue", null));

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

            MonsterRecolor.ApplyUserPaletteSection(data, paletteIndex: 1, PrimarySection(), userPalette);

            var (r, g, b) = FromBgr555(data[32 + 6 * 2], data[32 + 6 * 2 + 1]);
            Assert.True(g > r && g > b, $"section should copy emerald from user palette, got {r},{g},{b}");
        }

        // ---- Registry invariants (all families) -----------------------------

        [Fact]
        public void EveryFamily_HasThreeTiers_PaletteIndices_AndUniqueKeys()
        {
            Assert.NotEmpty(MonsterThemeRegistry.Families);
            var allTierKeys = new List<string>();
            var allJsonKeys = new List<string>();
            foreach (var fam in MonsterThemeRegistry.Families)
            {
                Assert.Equal(3, fam.TierKeys.Length);
                Assert.Equal(3, fam.TierDisplayNames.Length);
                Assert.Equal(3, fam.PaletteIndices.Length);
                allTierKeys.AddRange(fam.TierKeys);
                for (int i = 0; i < 3; i++) allJsonKeys.Add(fam.JsonKey(i));
            }
            // Tier keys and JSON keys must be globally unique (collisions break config persistence).
            Assert.Equal(allTierKeys.Count, allTierKeys.Distinct().Count());
            Assert.Equal(allJsonKeys.Count, allJsonKeys.Distinct().Count());
        }

        [Fact]
        public void EveryFamily_ThemeNames_OriginalFirst_NoDuplicatesWithinFamily()
        {
            foreach (var fam in MonsterThemeRegistry.Families)
            {
                var familyPresetNames = new List<string>();
                foreach (var tierKey in fam.TierKeys)
                {
                    var names = MonsterThemeRegistry.GetThemeNames(tierKey);
                    Assert.Equal("original", names[0]);
                    Assert.True(names.Count > 1, $"{tierKey} should expose presets");
                    familyPresetNames.AddRange(names.GetRange(1, names.Count - 1));
                }
                Assert.Equal(familyPresetNames.Count, familyPresetNames.Distinct().Count());
            }
        }

        [Fact]
        public void PaletteIndexForTier_MapsRanksToZeroOneTwo_ByDefault()
        {
            Assert.Equal(0, MonsterThemeRegistry.PaletteIndexForTier("Chocobo_RankI"));
            Assert.Equal(1, MonsterThemeRegistry.PaletteIndexForTier("Chocobo_RankII"));
            Assert.Equal(2, MonsterThemeRegistry.PaletteIndexForTier("Chocobo_RankIII"));
            Assert.Equal(1, MonsterThemeRegistry.PaletteIndexForTier("Goblin_RankII"));
        }

        [Fact]
        public void ForTierKey_And_ForFamily_Resolve()
        {
            Assert.Equal("Goblin", MonsterThemeRegistry.ForTierKey("Goblin_RankIII").Family);
            Assert.Null(MonsterThemeRegistry.ForTierKey("NotAMonster_RankI"));
            Assert.Equal(2, MonsterThemeRegistry.TierIndexForKey("Bomb_RankIII"));
        }

        [Fact]
        public void CorrectedSourceBins_AreUsed_ForMalboroAndDragon()
        {
            // The original asset table pointed Malboro at battle_mara (a humanoid) and Dragon at
            // battle_dora (humanoid, empty palettes 1/2). The correct 3-tier bins are mol / dora1.
            Assert.Equal("battle_mol_spr.bin", MonsterThemeRegistry.ForFamily("Malboro").Bin);
            Assert.Equal("battle_dora1_spr.bin", MonsterThemeRegistry.ForFamily("Dragon").Bin);
        }

        [Fact]
        public void TryGetPreset_ReturnsFalseForOriginal_TrueForKnownPreset()
        {
            Assert.False(MonsterThemeRegistry.TryGetPreset("Chocobo_RankI", "original", out _));
            Assert.True(MonsterThemeRegistry.TryGetPreset("Chocobo_RankI", "Blue", out var p));
            // Single-color preset: every section resolves to the same whole-creature tint (blue).
            var c = p.ColorFor("Primary").Value;
            Assert.True(c.B > c.R);
            Assert.Equal(c, p.ColorFor("AnySectionName").Value);
        }

        [Fact]
        public void AevisPresets_ArePerSection_WithDistinctColorsPerPart()
        {
            Assert.True(MonsterThemeRegistry.TryGetPreset("Aevis_RankI", "Tempest Steel", out var p));
            var wings = p.ColorFor("Wings").Value;   // steel blue
            var feet = p.ColorFor("Feet").Value;     // bronze
            Assert.True(wings.B > wings.R, "wings should be blue-dominant");
            Assert.True(feet.R > feet.B, "feet should be warm/bronze");
            Assert.NotEqual(wings, feet);            // genuinely per-section, not one tint
            // A section the preset names is colored; the bird's sections are all covered here.
            Assert.NotNull(p.ColorFor("Eye"));
        }

        [Fact]
        public void JsonKey_PreservesChocoboCompatNames()
        {
            var chocobo = MonsterThemeRegistry.ForFamily("Chocobo");
            Assert.Equal("ChocoboRankI", chocobo.JsonKey(0));
            Assert.Equal("ChocoboRankII", chocobo.JsonKey(1));
            Assert.Equal("ChocoboRankIII", chocobo.JsonKey(2));
        }
    }
}
