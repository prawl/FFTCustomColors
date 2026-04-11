using FFTColorCustomizer.GameBridge;
using Xunit;
using System.Linq;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for decoding the per-job learned-action-ability bitfield stored in each
    /// roster slot at +0x32 + jobIdx*3.
    ///
    /// Layout (confirmed empirically 2026-04-11 via in-game purchase of Stop and Thundaga):
    ///   byte 0: learned action bits 0-7, MSB-first (bit 7 = ability index 0)
    ///   byte 1: learned action bits 8-15, MSB-first
    ///   byte 2: reaction/support/movement/other flags (not yet decoded)
    /// </summary>
    public class RosterLearnedAbilitiesTests
    {
        [Fact]
        public void DecodeLearnedBitfield_FirstAbilityOnly_ReturnsIndexZero()
        {
            // byte0 = 0x80 = 10000000 MSB-first → only bit at MSB position 0 = index 0
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0x80, 0x00);
            Assert.Single(learned);
            Assert.Contains(0, learned);
        }

        [Fact]
        public void DecodeLearnedBitfield_AllEight_ReturnsIndicesZeroThroughSeven()
        {
            // byte0 = 0xFF → all 8 MSB positions set = indices 0-7
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0xFF, 0x00);
            Assert.Equal(8, learned.Count);
            for (int i = 0; i < 8; i++) Assert.Contains(i, learned);
        }

        [Fact]
        public void DecodeLearnedBitfield_MsbOrdering_BitZeroMapsToIndex0()
        {
            // 0x80 = bit 7 set (LSB) = MSB position 0 = ability index 0
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0x80, 0x00);
            Assert.Contains(0, learned);
            Assert.DoesNotContain(7, learned);
        }

        [Fact]
        public void DecodeLearnedBitfield_SecondByte_ReturnsIndices8Through15()
        {
            // byte1 = 0xFF → all 8 MSB positions set in second byte = indices 8-15
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0x00, 0xFF);
            Assert.Equal(8, learned.Count);
            for (int i = 8; i < 16; i++) Assert.Contains(i, learned);
        }

        [Fact]
        public void DecodeLearnedBitfield_RamzaTimeMagicks_BeforeStopPurchase()
        {
            // Before Stop was purchased, Ramza's Time Magicks byte 0 was 0xA4 = 10100100.
            // MSB-first: bits at positions 0, 2, 5 set = Haste, Slow, Immobilize.
            // (Ramza hadn't learned Hasteja(1), Slowja(3), Stop(4), Float(6), Reflect(7) yet.)
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0xA4, 0x00);
            Assert.Equal(3, learned.Count);
            Assert.Contains(0, learned);   // Haste
            Assert.Contains(2, learned);   // Slow
            Assert.Contains(5, learned);   // Immobilize
            Assert.DoesNotContain(4, learned); // Stop not yet
        }

        [Fact]
        public void DecodeLearnedBitfield_RamzaTimeMagicks_AfterStopPurchase()
        {
            // After purchasing Stop: byte 0 = 0xAC = 10101100.
            // MSB-first: bits at positions 0, 2, 4, 5 = Haste, Slow, Stop, Immobilize.
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0xAC, 0x00);
            Assert.Equal(4, learned.Count);
            Assert.Contains(0, learned);   // Haste
            Assert.Contains(2, learned);   // Slow
            Assert.Contains(4, learned);   // Stop
            Assert.Contains(5, learned);   // Immobilize
        }

        [Fact]
        public void DecodeLearnedBitfield_RamzaBlackMagicks_BeforeThundagaPurchase()
        {
            // Before Thundaga: byte 0 = 0xFC = 11111100 → positions 0,1,2,3,4,5 (6 abilities)
            // That's Fire, Fira, Firaga, Firaja, Thunder, Thundara (the first 6).
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0xFC, 0x00);
            Assert.Equal(6, learned.Count);
            for (int i = 0; i < 6; i++) Assert.Contains(i, learned);
            Assert.DoesNotContain(6, learned); // Thundaga not yet
        }

        [Fact]
        public void DecodeLearnedBitfield_RamzaBlackMagicks_AfterThundagaPurchase()
        {
            // After Thundaga: byte 0 = 0xFE = 11111110 → positions 0-6 (7 abilities).
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0xFE, 0x00);
            Assert.Equal(7, learned.Count);
            for (int i = 0; i < 7; i++) Assert.Contains(i, learned);
            Assert.Contains(6, learned); // Thundaga now learned
        }

        [Fact]
        public void DecodeLearnedBitfield_TwoByteRange_ReturnsMergedIndices()
        {
            // Simulates Black Magicks with byte0=0xFE, byte1=0xE8.
            // byte0 MSB bits 0,1,2,3,4,5,6 set → indices 0-6
            // byte1 = 0xE8 = 11101000 MSB → bits 0,1,2,4 set → indices 8,9,10,12
            // Total = Fire, Fira, Firaga, Firaja, Thunder, Thundara, Thundaga,
            //        Blizzard, Blizzara, Blizzaga, Poison (11 abilities).
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0xFE, 0xE8);
            Assert.Equal(11, learned.Count);
            foreach (var idx in new[] { 0, 1, 2, 3, 4, 5, 6, 8, 9, 10, 12 })
                Assert.Contains(idx, learned);
            Assert.DoesNotContain(7, learned);  // Thundaja not learned
            Assert.DoesNotContain(11, learned); // Blizzaja not learned
        }

        [Fact]
        public void DecodeLearnedBitfield_EmptyByte_ReturnsEmpty()
        {
            var learned = ActionAbilityLookup.DecodeLearnedBitfield(0x00, 0x00);
            Assert.Empty(learned);
        }

        [Fact]
        public void GetLearnedAbilitiesFromBitfield_MatchesSkillsetPositions()
        {
            // Decode Ramza's Time Magicks (0xAC 0x00) using the Time Magicks skillset
            // and verify we get the actual ActionAbilityInfo records for Haste/Slow/Stop/Immobilize.
            var abilities = ActionAbilityLookup.GetLearnedAbilitiesFromBitfield(
                "Time Magicks", 0xAC, 0x00);
            Assert.Equal(4, abilities.Count);
            var names = abilities.Select(a => a.Name).ToList();
            Assert.Contains("Haste", names);
            Assert.Contains("Slow", names);
            Assert.Contains("Stop", names);
            Assert.Contains("Immobilize", names);
        }

        [Fact]
        public void GetLearnedAbilitiesFromBitfield_BlackMagicks_TwoBytePaddedCorrectly()
        {
            var abilities = ActionAbilityLookup.GetLearnedAbilitiesFromBitfield(
                "Black Magicks", 0xFE, 0xE8);
            Assert.Equal(11, abilities.Count);
            var names = abilities.Select(a => a.Name).ToList();
            Assert.Contains("Fire", names);
            Assert.Contains("Thundaga", names);
            Assert.Contains("Poison", names);
            Assert.DoesNotContain("Thundaja", names);
            Assert.DoesNotContain("Flare", names);
        }

        [Fact]
        public void GetLearnedAbilitiesFromBitfield_UnknownSkillset_ReturnsEmpty()
        {
            var abilities = ActionAbilityLookup.GetLearnedAbilitiesFromBitfield(
                "Nonexistent", 0xFF, 0xFF);
            Assert.Empty(abilities);
        }

        [Fact]
        public void GetLearnedAbilitiesFromBitfield_NoBitsSet_ReturnsEmpty()
        {
            var abilities = ActionAbilityLookup.GetLearnedAbilitiesFromBitfield(
                "Martial Arts", 0x00, 0x00);
            Assert.Empty(abilities);
        }

        [Fact]
        public void GetLearnedAbilitiesFromBitfield_MartialArtsAllLearned_ReturnsEightEntries()
        {
            // Ramza's Monk primary: byte 0 = 0xFF = 8 Martial Arts learned.
            var abilities = ActionAbilityLookup.GetLearnedAbilitiesFromBitfield(
                "Martial Arts", 0xFF, 0x00);
            Assert.Equal(8, abilities.Count);
        }

        [Theory]
        [InlineData("Mettle", 0)]
        [InlineData("Fundaments", 0)]
        [InlineData("Items", 1)]
        [InlineData("Arts of War", 2)]
        [InlineData("Aim", 3)]
        [InlineData("Martial Arts", 4)]
        [InlineData("White Magicks", 5)]
        [InlineData("Black Magicks", 6)]
        [InlineData("Time Magicks", 7)]
        [InlineData("Summon", 8)]
        [InlineData("Steal", 9)]
        [InlineData("Darkness", 19)]
        [InlineData("NotARealSkillset", -1)]
        public void GetJobIdxBySkillsetName_ReturnsCorrectIndex(string skillset, int expected)
        {
            Assert.Equal(expected, AbilityData.GetJobIdxBySkillsetName(skillset));
        }
    }
}
