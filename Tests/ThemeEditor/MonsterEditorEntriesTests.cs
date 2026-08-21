using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    // --- CC-27: the editor must be able to reach ranks II and III, not just rank I ---
    //
    // 16 monster families x 3 ranks = 48 themable monsters exist in the config dropdowns, but
    // the theme editor previously listed one entry per family and always edited palette 0. This
    // pure helper turns the registry into one dropdown entry per (family, rank) so all 48 are
    // reachable, keeping the family (not the rank) as the section-mapping/save key.
    //
    // Owner follow-up: every rank entry carries a 1-based "(rank N)" suffix. A per-family
    // divider row was tried and then dropped again (it cluttered the dropdown); the existing
    // "-- Monsters --" group header above the whole section already does that job.
    public class MonsterEditorEntriesTests
    {
        [Fact]
        public void Build_ReturnsThreeEntriesPerFamily_WithRankSuffixedNamesAndPaletteIndices_InRegistryOrder()
        {
            var availableMonsters = MonsterThemeRegistry.Families.Select(f => f.Family);

            var entries = MonsterEditorEntries.Build(availableMonsters);

            var families = MonsterThemeRegistry.Families;
            Assert.Equal(families.Count * 3, entries.Count);

            for (int i = 0; i < families.Count; i++)
            {
                var family = families[i];

                for (int tier = 0; tier < 3; tier++)
                {
                    var rankEntry = entries[i * 3 + tier];
                    Assert.Equal($"{family.TierDisplayNames[tier]} (rank {tier + 1})", rankEntry.DisplayName);
                    Assert.Equal(family.Family, rankEntry.Family);
                    Assert.Equal(family.PaletteIndices[tier], rankEntry.PaletteIndex);
                }
            }
        }

        [Fact]
        public void Build_SkipsAFamilyWithNoSectionMappingInTheSuppliedSet()
        {
            var availableMonsters = MonsterThemeRegistry.Families
                .Select(f => f.Family)
                .Where(f => f != "Hydra");

            var entries = MonsterEditorEntries.Build(availableMonsters);

            Assert.Equal(45, entries.Count);
            Assert.DoesNotContain(entries, e => e.Family == "Hydra");
        }

        [Fact]
        public void Build_PreservesEachFamilysPaletteIndicesOrder_NotJustTierPosition()
        {
            // A family whose paletteIndices is neither the default {0,1,2} nor equal to the
            // tier position ({2,0,1} instead of {0,1,2}) -- nobody would write this by accident,
            // so it catches a mutation that substitutes the tier loop variable for the real
            // per-family override and would otherwise produce identical values for every family
            // that still uses the default order.
            var oddFamily = new MonsterFamily(
                "OddFamily", "Odd Family", "battle_odd_spr.bin",
                new[] { "Tier0", "Tier1", "Tier2" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>(),
                paletteIndices: new[] { 2, 0, 1 });

            var entries = MonsterEditorEntries.Build(new[] { "OddFamily" }, new[] { oddFamily });

            Assert.Equal(3, entries.Count);
            Assert.Equal(2, entries[0].PaletteIndex);
            Assert.Equal(0, entries[1].PaletteIndex);
            Assert.Equal(1, entries[2].PaletteIndex);
        }
    }
}
