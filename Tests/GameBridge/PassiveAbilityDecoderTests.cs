using FFTColorCustomizer.GameBridge;
using Xunit;

namespace Tests.GameBridge
{
    public class PassiveAbilityDecoderTests
    {
        // Layout: Reaction 4 bytes at heap +0x74 (base 166), Support 5 bytes at +0x78 (base 198)
        // MSB-first: position = id - base, byteIdx = pos/8, bitIdx = 7 - (pos%8)

        [Fact]
        public void DecodeReaction_Knight_Parry()
        {
            // Knight: reaction field = 00 00 00 40 → Parry(191)
            var bytes = new byte[] { 0x00, 0x00, 0x00, 0x40 };
            var result = PassiveAbilityDecoder.DecodeReaction(bytes);
            Assert.Equal("Parry", result);
        }

        [Fact]
        public void DecodeReaction_Archer_GilSnapper()
        {
            // Archer: reaction field = 00 00 40 00 → Gil Snapper(183)
            var bytes = new byte[] { 0x00, 0x00, 0x40, 0x00 };
            var result = PassiveAbilityDecoder.DecodeReaction(bytes);
            Assert.Equal("Gil Snapper", result);
        }

        [Fact]
        public void DecodeReaction_NoneEquipped()
        {
            var bytes = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            var result = PassiveAbilityDecoder.DecodeReaction(bytes);
            Assert.Null(result);
        }

        [Fact]
        public void DecodeSupport_Knight_EquipSwords()
        {
            // Knight: support field = 20 00 00 00 00 → Equip Swords(200)
            var bytes = new byte[] { 0x20, 0x00, 0x00, 0x00, 0x00 };
            var result = PassiveAbilityDecoder.DecodeSupport(bytes);
            Assert.Equal("Equip Swords", result);
        }

        [Fact]
        public void DecodeSupport_Archer_EvasiveStance()
        {
            // Archer: support field = 00 00 00 40 00 → Evasive Stance(223)
            var bytes = new byte[] { 0x00, 0x00, 0x00, 0x40, 0x00 };
            var result = PassiveAbilityDecoder.DecodeSupport(bytes);
            Assert.Equal("Evasive Stance", result);
        }

        [Fact]
        public void DecodeSupport_NoneEquipped()
        {
            var bytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 };
            var result = PassiveAbilityDecoder.DecodeSupport(bytes);
            Assert.Null(result);
        }

        [Fact]
        public void DecodeReaction_CounterTackle()
        {
            // Counter Tackle(180): pos = 180-166 = 14, byte1 bit1 = 0x02
            var bytes = new byte[] { 0x00, 0x02, 0x00, 0x00 };
            var result = PassiveAbilityDecoder.DecodeReaction(bytes);
            Assert.Equal("Counter Tackle", result);
        }

        [Fact]
        public void DecodeReaction_Counter()
        {
            // Counter(186): pos = 186-166 = 20, byte2 bit3 = 0x08
            var bytes = new byte[] { 0x00, 0x00, 0x08, 0x00 };
            var result = PassiveAbilityDecoder.DecodeReaction(bytes);
            Assert.Equal("Counter", result);
        }

        [Fact]
        public void DecodeSupport_DualWield()
        {
            // Dual Wield(221): pos = 221-198 = 23, byte2 bit0 = 0x01
            var bytes = new byte[] { 0x00, 0x00, 0x01, 0x00, 0x00 };
            var result = PassiveAbilityDecoder.DecodeSupport(bytes);
            Assert.Equal("Dual Wield", result);
        }

        [Fact]
        public void DecodeReaction_TooShort_ReturnsNull()
        {
            var bytes = new byte[] { 0x40 };
            var result = PassiveAbilityDecoder.DecodeReaction(bytes);
            Assert.Null(result);
        }

        [Fact]
        public void DecodeSupport_TooShort_ReturnsNull()
        {
            var bytes = new byte[] { 0x20, 0x00 };
            var result = PassiveAbilityDecoder.DecodeSupport(bytes);
            Assert.Null(result);
        }
    }
}
