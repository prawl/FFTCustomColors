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

        [Fact]
        public void CollapseJumpAbilities_ReturnsOneEntry_WithHighestHorizontalRange()
        {
            var learned = new List<ActionAbilityInfo>
            {
                new(0, "Horizontal Jump +1", 0, "2", 0, 1, 0, "enemy", ""),
                new(0, "Horizontal Jump +2", 0, "3", 0, 1, 0, "enemy", ""),
                new(0, "Horizontal Jump +3", 0, "4", 0, 1, 0, "enemy", ""),
                new(0, "Vertical Jump +2",   0, "0", 2, 1, 0, "enemy", ""),
                new(0, "Vertical Jump +5",   0, "0", 5, 1, 0, "enemy", ""),
            };

            var result = ActionAbilityLookup.CollapseJumpAbilities(learned);

            Assert.Single(result);
            Assert.Equal("Jump", result[0].Name);
            Assert.Equal("4", result[0].HRange); // highest Horizontal Jump +3 → range 4
            Assert.Equal(5, result[0].VRange);    // highest Vertical Jump +5
        }

        [Fact]
        public void CollapseJumpAbilities_OnlyHorizontal_VRangeIsZero()
        {
            var learned = new List<ActionAbilityInfo>
            {
                new(0, "Horizontal Jump +1", 0, "2", 0, 1, 0, "enemy", ""),
                new(0, "Horizontal Jump +7", 0, "8", 0, 1, 0, "enemy", ""),
            };

            var result = ActionAbilityLookup.CollapseJumpAbilities(learned);

            Assert.Single(result);
            Assert.Equal("Jump", result[0].Name);
            Assert.Equal("8", result[0].HRange);
            Assert.Equal(0, result[0].VRange);
        }

        [Fact]
        public void CollapseJumpAbilities_EmptyList_ReturnsEmpty()
        {
            var result = ActionAbilityLookup.CollapseJumpAbilities(new List<ActionAbilityInfo>());

            Assert.Empty(result);
        }

        [Fact]
        public void CollapseJumpAbilities_NonJumpAbilities_PassedThrough()
        {
            var learned = new List<ActionAbilityInfo>
            {
                new(0, "Horizontal Jump +4", 0, "5", 0, 1, 0, "enemy", ""),
                new(0, "Vertical Jump +3",   0, "0", 3, 1, 0, "enemy", ""),
                new(0, "Some Other Ability",  0, "3", 0, 1, 0, "enemy", ""),
            };

            var result = ActionAbilityLookup.CollapseJumpAbilities(learned);

            Assert.Equal(2, result.Count);
            Assert.Equal("Jump", result[0].Name);
            Assert.Equal("Some Other Ability", result[1].Name);
        }

        [Fact]
        public void GetLearnedAbilitiesFromBitfield_Jump_CollapsesToSingleEntry()
        {
            // Bits 0-4 = Horizontal Jump +1 through +7 (5 entries)
            // Bits 5-11 = Vertical Jump +2 through +8 (7 entries)
            // Learn Horizontal +1 (bit 0), +2 (bit 1), +3 (bit 2) and Vertical +2 (bit 5), +5 (bit 8)
            byte byte0 = 0b_1110_0100; // bits 0,1,2 = H+1,H+2,H+3; bit 5 = V+2
            byte byte1 = 0b_0010_0000; // bit 8 (= bit 0 of byte1) = V+5... wait

            // Actually let me check the bitfield layout. MSB-first means:
            // byte0 bit 7 = ability index 0 (Horizontal Jump +1)
            // byte0 bit 6 = ability index 1 (Horizontal Jump +2)
            // byte0 bit 5 = ability index 2 (Horizontal Jump +3)
            // byte0 bit 4 = ability index 3 (Horizontal Jump +4)
            // byte0 bit 3 = ability index 4 (Horizontal Jump +7)
            // byte0 bit 2 = ability index 5 (Vertical Jump +2)
            // byte0 bit 1 = ability index 6 (Vertical Jump +3)
            // byte0 bit 0 = ability index 7 (Vertical Jump +4)
            // byte1 bit 7 = ability index 8 (Vertical Jump +5)
            // ...

            // Learn H+1(idx0), H+3(idx2), V+5(idx8):
            byte b0 = (byte)((0x80 >> 0) | (0x80 >> 2)); // bit7 + bit5 = 0xA0
            byte b1 = (byte)(0x80 >> 0); // bit7 = 0x80 → ability index 8 = Vertical Jump +5

            var result = ActionAbilityLookup.GetLearnedAbilitiesFromBitfield("Jump", b0, b1);

            // Should be collapsed to single "Jump" entry
            Assert.Single(result);
            Assert.Equal("Jump", result[0].Name);
            Assert.Equal("4", result[0].HRange);  // H+3 → range 4 (highest horizontal)
            Assert.Equal(5, result[0].VRange);     // V+5 (highest vertical)
        }
    }
}
