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
    }
}
