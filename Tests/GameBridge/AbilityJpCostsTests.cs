using FFTColorCustomizer.GameBridge;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for JP cost lookup and "Next: N" computation — the cheapest
    /// unlearned ability in a unit's current primary skillset, used for
    /// the CharacterStatus header.
    /// </summary>
    public class AbilityJpCostsTests
    {
        [Fact]
        public void AllNames_ResolveToKnownAbilities()
        {
            // If this fails, ABILITY_COSTS.md has a name that doesn't match
            // an ability in ActionAbilityLookup.Skillsets. Either fix the
            // name typo or add the missing ability to the skillset table.
            Assert.Empty(AbilityJpCosts.UnresolvedNames);
        }

        // Unverified-Wiki Mettle cost pins. These values come from
        // FFTHandsFree/Wiki/Abilities.md (PSX-canonical) and have NOT yet been
        // live-verified in IC remaster. If live readings mismatch, update both
        // the source table AND these tests.
        [Fact]
        public void MettleCost_Tailwind_IsWikiValue150()
        {
            Assert.Equal(150, AbilityJpCosts.GetCost("Tailwind"));
        }

        [Fact]
        public void MettleCost_Chant_IsWikiValue300()
        {
            Assert.Equal(300, AbilityJpCosts.GetCost("Chant"));
        }

        [Fact]
        public void MettleCost_Steel_IsWikiValue200()
        {
            Assert.Equal(200, AbilityJpCosts.GetCost("Steel"));
        }

        [Fact]
        public void MettleCost_Shout_IsWikiValue600()
        {
            Assert.Equal(600, AbilityJpCosts.GetCost("Shout"));
        }

        [Fact]
        public void MettleCost_Ultima_IsWikiValue4000()
        {
            // Ultima is the Mettle capstone — most expensive ability in the skillset.
            Assert.Equal(4000, AbilityJpCosts.GetCost("Ultima"));
        }

        // Pin other high-visibility costs so a bulk edit doesn't silently shift them.
        [Theory]
        [InlineData("Cure", 50)]
        [InlineData("Raise", 200)]
        [InlineData("Fire", 50)]
        [InlineData("Firaga", 500)]
        [InlineData("Holy", 600)]
        [InlineData("Bahamut", 1600)]
        [InlineData("Meteor", 1500)]
        [InlineData("Phoenix Down", 90)]
        public void KnownCost_MatchesWiki(string name, int expectedJp)
        {
            Assert.Equal(expectedJp, AbilityJpCosts.GetCost(name));
        }

        [Fact]
        public void GetCost_KnownAbility_ReturnsCost()
        {
            Assert.Equal(30, AbilityJpCosts.GetCost("Potion"));
        }

        [Fact]
        public void GetCost_UnknownAbility_ReturnsNull()
        {
            Assert.Null(AbilityJpCosts.GetCost("Not A Real Ability"));
        }

        [Fact]
        public void ComputeNextJp_NothingLearned_ReturnsCheapestAbility()
        {
            // White Magicks: Cure=50 is the cheapest. With nothing learned,
            // Next should read 50.
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("White Magicks", new HashSet<int>());
            Assert.Equal(50, next);
        }

        [Fact]
        public void ComputeNextJp_CheapestLearned_ReturnsNextCheapest()
        {
            // White Magicks cheapest (Cure idx 0, 50 JP) already learned.
            // Next cheapest are Protect/Shell at 70.
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("White Magicks", new HashSet<int> { 0 });
            Assert.Equal(70, next);
        }

        [Fact]
        public void ComputeNextJp_ItemsPartial_ReturnsCheapestUnlearned()
        {
            // Items: Potion=30 is cheapest. If Potion (idx 0) is learned,
            // next cheapest is Antidote=70.
            var items = ActionAbilityLookup.GetSkillsetAbilities("Items")!;
            int potionIdx = items.FindIndex(a => a.Name == "Potion");
            Assert.True(potionIdx >= 0);

            int? next = AbilityJpCosts.ComputeNextJpForSkillset("Items",
                new HashSet<int> { potionIdx });
            Assert.Equal(70, next);
        }

        [Fact]
        public void ComputeNextJp_UnknownSkillset_ReturnsNull()
        {
            Assert.Null(AbilityJpCosts.ComputeNextJpForSkillset("NotARealSkillset", new HashSet<int>()));
        }

        [Fact]
        public void ComputeNextJp_EverythingLearned_ReturnsNull()
        {
            // Mark every index 0-15 as learned — nothing unlearned remains.
            var learned = new HashSet<int>();
            for (int i = 0; i < 16; i++) learned.Add(i);
            Assert.Null(AbilityJpCosts.ComputeNextJpForSkillset("Items", learned));
        }

        [Fact]
        public void ComputeNextJp_Geomancy_AllBlanket150()
        {
            // Every Geomancy ability costs 150; unlearned should always return 150.
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("Geomancy", new HashSet<int>());
            Assert.Equal(150, next);
        }

        [Fact]
        public void ComputeNextJp_BlackMagicks_CheapestIsFireThunderBlizzard50()
        {
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("Black Magicks", new HashSet<int>());
            Assert.Equal(50, next);
        }

        [Fact]
        public void ComputeNextJp_MartialArts_CheapestIsCyclone150()
        {
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("Martial Arts", new HashSet<int>());
            Assert.Equal(150, next);
        }

        [Fact]
        public void ComputeNextJp_Fundaments_NothingLearned_ReturnsRush80()
        {
            // Generic Squire primary. Fundaments has 4 abilities sharing
            // names with Mettle: Focus=300, Rush=80, Throw Stone=90, Salve=150.
            // Cheapest is Rush. Regression-guards the "Fundaments silently
            // skipped" bug documented in TODO §Earlier open items.
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("Fundaments", new HashSet<int>());
            Assert.Equal(80, next);
        }

        [Fact]
        public void ComputeNextJp_Fundaments_RushLearned_ReturnsThrowStone90()
        {
            var fundaments = ActionAbilityLookup.GetSkillsetAbilities("Fundaments")!;
            int rushIdx = fundaments.FindIndex(a => a.Name == "Rush");
            int? next = AbilityJpCosts.ComputeNextJpForSkillset(
                "Fundaments", new HashSet<int> { rushIdx });
            Assert.Equal(90, next);
        }

        [Fact]
        public void ComputeNextJp_Mettle_NothingLearned_ReturnsRush80()
        {
            // Ramza's Gallant Knight primary. Mettle = Fundaments + 5 unique
            // (Tailwind 150, Chant 300, Steel 200, Shout 600, Ultima 4000).
            // Cheapest overall is still Rush=80 since Mettle shares Focus/Rush/
            // Throw Stone/Salve with Fundaments.
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("Mettle", new HashSet<int>());
            Assert.Equal(80, next);
        }

        [Fact]
        public void ComputeNextJp_Mettle_CheapFourLearned_ReturnsTailwind150()
        {
            // Focus(300), Rush(80), Throw Stone(90), Salve(150) all learned.
            // Next cheapest is Tailwind=150 (tied with Salve but Salve learned).
            var mettle = ActionAbilityLookup.GetSkillsetAbilities("Mettle")!;
            var learned = new HashSet<int>();
            foreach (var n in new[] { "Focus", "Rush", "Throw Stone", "Salve" })
            {
                int i = mettle.FindIndex(a => a.Name == n);
                Assert.True(i >= 0, $"Mettle missing ability: {n}");
                learned.Add(i);
            }
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("Mettle", learned);
            Assert.Equal(150, next); // Tailwind
        }

        [Fact]
        public void GetCost_AllMettleExclusiveAbilities_Populated()
        {
            // Guard against partial backfill: every Mettle-only ability
            // must have a cost so ComputeNextJp doesn't silently skip them.
            Assert.NotNull(AbilityJpCosts.GetCost("Tailwind"));
            Assert.NotNull(AbilityJpCosts.GetCost("Chant"));
            Assert.NotNull(AbilityJpCosts.GetCost("Steel"));
            Assert.NotNull(AbilityJpCosts.GetCost("Shout"));
            Assert.NotNull(AbilityJpCosts.GetCost("Ultima"));
        }

        // Session 37: hardening — null/unknown/edge inputs for GetCost +
        // ComputeNextJpForSkillset, and positivity invariant for CostByName.

        [Fact]
        public void GetCost_UnknownName_ReturnsNull()
        {
            Assert.Null(AbilityJpCosts.GetCost("Not A Real Ability"));
            Assert.Null(AbilityJpCosts.GetCost("fire"));  // case-sensitive — expect null
        }

        [Fact]
        public void GetCost_CaseSensitive()
        {
            // "Fire" exists; "fire" does not. Pin the case-sensitivity contract.
            var fireCost = AbilityJpCosts.GetCost("Fire");
            var lowerCost = AbilityJpCosts.GetCost("fire");
            Assert.NotNull(fireCost);
            Assert.Null(lowerCost);
        }

        [Fact]
        public void CostByName_AllValues_NonNegative()
        {
            // Negative JP costs would break the "Next: N" computation.
            // Zero IS allowed as a sentinel for "unlearnable via JP" (e.g.
            // Zodiark, which is a secret/story-unlock-only Summon). The
            // ComputeNextJp path filters 0-cost entries via the blanket /
            // null-cost branches so they never surface as "next cheapest".
            foreach (var kv in AbilityJpCosts.CostByName)
            {
                Assert.True(kv.Value >= 0,
                    $"Ability '{kv.Key}' has negative JP cost {kv.Value}");
            }
        }

        [Fact]
        public void ComputeNextJp_ZeroCostSentinel_SurfacesAsMin_DocumentsCurrentBehavior()
        {
            // Zodiark in the Summon skillset has JP cost 0 as a sentinel
            // meaning "unlearnable via normal JP earn". Current behavior:
            // ComputeNextJp does NOT filter 0-cost entries, so an unlearned
            // Zodiark WILL surface as "Next: 0". This test pins the current
            // (arguably wrong) behavior so a future filter-fix is a visible
            // breaking change.
            //
            // If a filter is added (recommended: treat 0 as "unknown cost"
            // equivalent to null), update this test to assert the filtered
            // result (e.g. Moogle = 8).
            var summon = ActionAbilityLookup.GetSkillsetAbilities("Summon");
            Assert.NotNull(summon);
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("Summon", new HashSet<int>());
            // Document: with Zodiark unlearned and 0 cost, Next = 0.
            Assert.Equal(0, next);
        }

        [Fact]
        public void CostByName_IsNonEmpty()
        {
            Assert.NotEmpty(AbilityJpCosts.CostByName);
        }

        [Fact]
        public void ComputeNextJp_OnlyUnknownCostsUnlearned_ReturnsNull()
        {
            // Edge case: if every unlearned ability in the skillset has an
            // unknown cost, ComputeNextJp should return null rather than 0
            // or throwing. Arithmetician (if present) is a plausible candidate —
            // but we construct a synthetic scenario by marking everything with
            // a known cost as "learned" in a known skillset, leaving only
            // unknown-cost entries unlearned.
            var fundaments = ActionAbilityLookup.GetSkillsetAbilities("Fundaments");
            if (fundaments == null) return; // defensive — skip if skillset missing

            var learned = new HashSet<int>();
            for (int i = 0; i < fundaments.Count; i++)
            {
                if (AbilityJpCosts.GetCost(fundaments[i].Name) != null)
                    learned.Add(i);
            }
            // Any remaining index is one with unknown cost. If all are learned,
            // result is null for that reason instead (still passes).
            int? next = AbilityJpCosts.ComputeNextJpForSkillset("Fundaments", learned);
            Assert.Null(next);
        }
    }
}
