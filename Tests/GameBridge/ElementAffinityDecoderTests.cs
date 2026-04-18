using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ElementAffinityDecoderTests
    {
        [Fact]
        public void Decode_ZeroMask_ReturnsEmpty()
        {
            Assert.Empty(ElementAffinityDecoder.Decode(0x00));
        }

        // Live-verified single-element masks (session 30 data points).
        [Theory]
        [InlineData(0x80, "Fire")]
        [InlineData(0x40, "Lightning")]
        [InlineData(0x20, "Ice")]
        [InlineData(0x10, "Wind")]
        [InlineData(0x08, "Earth")]
        [InlineData(0x04, "Water")]
        [InlineData(0x02, "Holy")]
        [InlineData(0x01, "Dark")]
        public void Decode_SingleBit_ReturnsOneElement(byte mask, string expected)
        {
            var result = ElementAffinityDecoder.Decode(mask);
            Assert.Single(result);
            Assert.Equal(expected, result[0]);
        }

        // Live-verified multi-element masks from Kaiser (Fire+Lightning+Ice)
        // and Venetian (same), both set bits 7+6+5 = 0xE0.
        [Theory]
        [InlineData(0xE0, new[] { "Fire", "Lightning", "Ice" })]
        // Gaia Gear absorbs+boosts Earth only (bit 3).
        [InlineData(0x08, new[] { "Earth" })]
        // Chameleon Robe absorbs Holy only (bit 1).
        [InlineData(0x02, new[] { "Holy" })]
        // All 8 elements set.
        [InlineData(0xFF, new[] { "Fire", "Lightning", "Ice", "Wind", "Earth", "Water", "Holy", "Dark" })]
        public void Decode_MultipleBits_ReturnsAllElements(byte mask, string[] expected)
        {
            var result = ElementAffinityDecoder.Decode(mask);
            Assert.Equal(expected, result.ToArray());
        }

        [Theory]
        [InlineData(0x80, "Fire", true)]
        [InlineData(0x80, "fire", true)] // case-insensitive
        [InlineData(0x80, "FIRE", true)]
        [InlineData(0x80, "Ice", false)]
        [InlineData(0xE0, "Lightning", true)]
        [InlineData(0xE0, "Earth", false)]
        [InlineData(0x00, "Fire", false)]
        [InlineData(0xFF, "Dark", true)]
        public void Has_ReturnsExpectedForElement(byte mask, string element, bool expected)
        {
            Assert.Equal(expected, ElementAffinityDecoder.Has(mask, element));
        }

        [Theory]
        [InlineData("Bogus")]
        [InlineData("")]
        [InlineData("Psychic")]
        public void Has_UnknownElement_ReturnsFalse(string element)
        {
            Assert.False(ElementAffinityDecoder.Has(0xFF, element));
        }

        // Regression guards for the session 30 live data points.
        [Fact]
        public void FlameShield_AbsorbsFireHalvesIceWeakToWater()
        {
            // +0x5A Absorb = 0x80 (Fire)
            Assert.Equal(new[] { "Fire" }, ElementAffinityDecoder.Decode(0x80).ToArray());
            // +0x5C Half = 0x20 (Ice)
            Assert.Equal(new[] { "Ice" }, ElementAffinityDecoder.Decode(0x20).ToArray());
            // +0x5D Weak = 0x04 (Water)
            Assert.Equal(new[] { "Water" }, ElementAffinityDecoder.Decode(0x04).ToArray());
        }

        [Fact]
        public void IceShield_AbsorbsIceHalvesFireWeakToLightning()
        {
            Assert.Equal(new[] { "Ice" }, ElementAffinityDecoder.Decode(0x20).ToArray());
            Assert.Equal(new[] { "Fire" }, ElementAffinityDecoder.Decode(0x80).ToArray());
            Assert.Equal(new[] { "Lightning" }, ElementAffinityDecoder.Decode(0x40).ToArray());
        }

        // Round-trip tests (session 33 batch 5).

        [Theory]
        [InlineData(0x00)]
        [InlineData(0x01)]
        [InlineData(0x80)]
        [InlineData(0xE0)]
        [InlineData(0xFF)]
        [InlineData(0x55)]
        [InlineData(0xAA)]
        public void Decode_ThenHas_RoundTripsForEveryElement(byte mask)
        {
            // For every element name returned by Decode, Has should return true;
            // for names NOT returned, Has should return false.
            var decoded = new HashSet<string>(ElementAffinityDecoder.Decode(mask));
            var allElements = new[] { "Fire", "Lightning", "Ice", "Wind", "Earth", "Water", "Holy", "Dark" };
            foreach (var el in allElements)
            {
                bool inList = decoded.Contains(el);
                bool hasReports = ElementAffinityDecoder.Has(mask, el);
                Assert.Equal(inList, hasReports);
            }
        }

        [Fact]
        public void Decode_AllMasks_ReturnsCountMatchingBitCount()
        {
            // For every possible u8 mask, Decode should return exactly as many
            // element names as there are set bits.
            for (int m = 0; m < 256; m++)
            {
                var result = ElementAffinityDecoder.Decode((byte)m);
                int bits = System.Numerics.BitOperations.PopCount((uint)m);
                Assert.Equal(bits, result.Count);
            }
        }

        [Fact]
        public void Decode_OrderIsBit7ToBit0()
        {
            // Output order must be Fire, Lightning, Ice, Wind, Earth, Water, Holy, Dark
            // (bit 7 → bit 0). Shell render and JSON responses depend on this order.
            var all = ElementAffinityDecoder.Decode(0xFF);
            Assert.Equal("Fire", all[0]);
            Assert.Equal("Lightning", all[1]);
            Assert.Equal("Ice", all[2]);
            Assert.Equal("Wind", all[3]);
            Assert.Equal("Earth", all[4]);
            Assert.Equal("Water", all[5]);
            Assert.Equal("Holy", all[6]);
            Assert.Equal("Dark", all[7]);
        }

        [Fact]
        public void Decode_AllMasks_NeverReturnsDuplicates()
        {
            for (int m = 0; m < 256; m++)
            {
                var result = ElementAffinityDecoder.Decode((byte)m);
                Assert.Equal(result.Count, result.Distinct().Count());
            }
        }

        [Theory]
        [InlineData("Fire", 0x80)]
        [InlineData("Lightning", 0x40)]
        [InlineData("Ice", 0x20)]
        [InlineData("Wind", 0x10)]
        [InlineData("Earth", 0x08)]
        [InlineData("Water", 0x04)]
        [InlineData("Holy", 0x02)]
        [InlineData("Dark", 0x01)]
        public void Has_SingleElementMask_ReturnsTrueOnlyForMatchingElement(string element, byte mask)
        {
            Assert.True(ElementAffinityDecoder.Has(mask, element));
            // Every OTHER element should report false on this single-bit mask.
            var others = new[] { "Fire", "Lightning", "Ice", "Wind", "Earth", "Water", "Holy", "Dark" };
            foreach (var other in others)
            {
                if (other == element) continue;
                Assert.False(ElementAffinityDecoder.Has(mask, other),
                    $"Has({mask:X2}, {other}) should be false; mask represents {element}");
            }
        }

        [Fact]
        public void Has_NullElement_ReturnsFalse()
        {
            Assert.False(ElementAffinityDecoder.Has(0xFF, null!));
        }
    }
}
