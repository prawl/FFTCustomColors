using FFTColorCustomizer.GameBridge;
using Xunit;
using System.Collections.Generic;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ActionAbilityTests
    {
        [Fact]
        public void FilterLearnedAbilities_ReturnsOnlyLearnedOnes()
        {
            var skillsetAbilities = new List<ActionAbilityInfo>
            {
                new(0x22, "Focus", 0, "Self", 0, 1, 0, "self", "test"),
                new(0x41, "Tailwind", 0, "3", 99, 1, 0, "ally", "test"),
                new(0x55, "Steel", 0, "3", 99, 1, 0, "ally", "test"),
                new(0x99, "Shout", 0, "Self", 0, 1, 0, "self", "test"),
            };

            // Unit has learned 0x22 and 0x55 but not 0x41 or 0x99
            var learnedIds = new HashSet<int> { 0x22, 0x55 };

            var result = ActionAbilityLookup.FilterLearned(skillsetAbilities, learnedIds);

            Assert.Equal(2, result.Count);
            Assert.Equal("Focus", result[0].Name);
            Assert.Equal("Steel", result[1].Name);
        }

        [Fact]
        public void FilterLearnedAbilities_EmptyLearned_ReturnsEmpty()
        {
            var skillsetAbilities = new List<ActionAbilityInfo>
            {
                new(0x22, "Focus", 0, "Self", 0, 1, 0, "self", "test"),
            };
            var learnedIds = new HashSet<int>();

            var result = ActionAbilityLookup.FilterLearned(skillsetAbilities, learnedIds);
            Assert.Empty(result);
        }

        [Fact]
        public void FilterLearnedAbilities_AllLearned_ReturnsAll()
        {
            var skillsetAbilities = new List<ActionAbilityInfo>
            {
                new(0x22, "Focus", 0, "Self", 0, 1, 0, "self", "test"),
                new(0x41, "Tailwind", 0, "3", 99, 1, 0, "ally", "test"),
            };
            var learnedIds = new HashSet<int> { 0x22, 0x41, 0xFF }; // extra ID ignored

            var result = ActionAbilityLookup.FilterLearned(skillsetAbilities, learnedIds);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void ParseLearnedIds_FromFFFFTerminatedBytes_ReturnsIds()
        {
            // Simulates reading uint16 LE values terminated by 0xFFFF
            var bytes = new byte[] { 0x22, 0x00, 0x41, 0x00, 0x55, 0x00, 0xFF, 0xFF };
            var ids = ActionAbilityLookup.ParseLearnedIdsFromBytes(bytes);

            Assert.Equal(3, ids.Count);
            Assert.Contains(0x22, ids);
            Assert.Contains(0x41, ids);
            Assert.Contains(0x55, ids);
        }

        [Fact]
        public void ParseLearnedIds_EmptyList_ReturnsEmpty()
        {
            var bytes = new byte[] { 0xFF, 0xFF };
            var ids = ActionAbilityLookup.ParseLearnedIdsFromBytes(bytes);
            Assert.Empty(ids);
        }

        [Fact]
        public void ResolveLearnedAbilities_RamzaMettle_ReturnsCorrectNames()
        {
            // Verified in-game: Ramza's condensed struct ability list
            var ids = new HashSet<int> { 0x22, 0x41, 0x55, 0x8B, 0x9F, 0xAE, 0xB5, 0xCD, 0xD7, 0xE7 };
            var abilities = ActionAbilityLookup.ResolveLearnedAbilities(ids);

            // Attack (0x22) should be excluded
            Assert.DoesNotContain(abilities, a => a.Name == "Attack");
            // Mettle abilities should be present
            Assert.Contains(abilities, a => a.Name == "Focus");
            Assert.Contains(abilities, a => a.Name == "Tailwind");
            Assert.Contains(abilities, a => a.Name == "Steel");
            Assert.Contains(abilities, a => a.Name == "Shout");
            Assert.Contains(abilities, a => a.Name == "Ultima");
            Assert.Equal(9, abilities.Count); // 10 IDs minus Attack
        }

        [Fact]
        public void ShouldReadAbilities_OnlyForActiveUnit()
        {
            // The condensed struct ability list only reflects the active unit.
            // During C+Up cycling, the list doesn't update for hovered units.
            // So we should only read abilities for the first unit scanned (active).
            Assert.True(ActionAbilityLookup.ShouldReadAbilities(unitIndex: 0));
            Assert.False(ActionAbilityLookup.ShouldReadAbilities(unitIndex: 1));
            Assert.False(ActionAbilityLookup.ShouldReadAbilities(unitIndex: 5));
        }

        [Fact]
        public void GetById_VerifiedId_ReturnsCorrectAbility()
        {
            var focus = ActionAbilityLookup.GetById(0x41);
            Assert.NotNull(focus);
            Assert.Equal("Focus", focus!.Name);

            var tailwind = ActionAbilityLookup.GetById(0xAE);
            Assert.NotNull(tailwind);
            Assert.Equal("Tailwind", tailwind!.Name);
        }
        [Fact]
        public void GetSkillsetForAbilityId_MettleFocus_ReturnsMettle()
        {
            Assert.Equal("Mettle", ActionAbilityLookup.GetSkillsetForAbilityId(0x41));
        }

        [Fact]
        public void GetSkillsetForAbilityId_UnknownId_ReturnsNull()
        {
            Assert.Null(ActionAbilityLookup.GetSkillsetForAbilityId(0x9999));
        }

        [Fact]
        public void FilterBySkillsets_OnlyReturnsAbilitiesFromGivenSkillsets()
        {
            // A Monk with secondary Time Magicks should only show Martial Arts + Time Magicks abilities,
            // NOT Fundaments or Mettle abilities even if the unit learned them.
            var allLearned = new List<ActionAbilityInfo>
            {
                // From Mettle (should be filtered OUT if not equipped)
                ActionAbilityLookup.GetById(0x41)!, // Focus (Mettle)
                ActionAbilityLookup.GetById(0xAE)!, // Tailwind (Mettle)
            };

            var filtered = ActionAbilityLookup.FilterBySkillsets(allLearned, new[] { "Time Magicks" });

            // Focus and Tailwind are Mettle, not Time Magicks — should be excluded
            Assert.Empty(filtered);
        }
        [Fact]
        public void FilterBySkillsets_SquireWithMettleIds_ShouldIncludeFundaments()
        {
            // Squire's primary skillset is "Fundaments" but learned ability IDs
            // from the condensed struct use Mettle IDs (0x41=Focus, 0x55=Rush, etc.).
            // FilterBySkillsets must recognize Mettle abilities as valid for Fundaments.
            var learnedAbilities = new List<ActionAbilityInfo>
            {
                ActionAbilityLookup.GetById(0x41)!, // Focus (Mettle ID)
                ActionAbilityLookup.GetById(0x55)!, // Rush (Mettle ID)
                ActionAbilityLookup.GetById(0x8B)!, // Throw Stone (Mettle ID)
            };

            var filtered = ActionAbilityLookup.FilterBySkillsets(learnedAbilities, new[] { "Fundaments" });

            // These should NOT be filtered out — Fundaments is the Squire version of Mettle
            Assert.Equal(3, filtered.Count);
        }
    }
}
