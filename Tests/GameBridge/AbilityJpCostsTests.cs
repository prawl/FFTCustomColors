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
    }
}
