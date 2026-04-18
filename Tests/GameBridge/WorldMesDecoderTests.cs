using System;
using System.IO;
using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Fact-based tests that run only when %TEMP%/world.bin is present. Verify the
    /// decoder produces the expected rumor text at known indices.
    /// </summary>
    public class WorldMesDecoderTests
    {
        private static byte[]? LoadWorldBin()
        {
            var temp = Environment.GetEnvironmentVariable("TEMP");
            if (string.IsNullOrEmpty(temp)) return null;
            var path = Path.Combine(temp, "world.bin");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        [Fact]
        public void DecodeByte_Digit0_ReturnsZeroChar()
        {
            Assert.Equal('0', WorldMesDecoder.DecodeByte(0x00));
        }

        [Fact]
        public void DecodeByte_0x95_ReturnsSpace()
        {
            Assert.Equal(' ', WorldMesDecoder.DecodeByte(0x95));
        }

        [Fact]
        public void DecodeByte_0x93_ReturnsApostrophe()
        {
            Assert.Equal('\'', WorldMesDecoder.DecodeByte(0x93));
        }

        [Fact]
        public void DecodeByte_0x91_ReturnsQuote()
        {
            Assert.Equal('"', WorldMesDecoder.DecodeByte(0x91));
        }

        [Fact]
        public void Digraph_DA74_RendersAsComma()
        {
            byte[] input = { 0x2F, 0x24, 0x31, 0x27, 0xDA, 0x74, 0x95, 0x24, 0x31, 0x27 }; // "land, and"
            var output = WorldMesDecoder.Decode(input, 0, input.Length, markUnknown: false);
            Assert.Equal("land, and", output);
        }

        [Fact]
        public void Digraph_D11D_RendersAsHyphen()
        {
            // "high-ranking": h(0x2B) i(0x2C) g(0x2A) h(0x2B) D1 1D r(0x35) a(0x24) n(0x31) k(0x2E) i(0x2C) n(0x31) g(0x2A)
            byte[] input = { 0x2B, 0x2C, 0x2A, 0x2B, 0xD1, 0x1D, 0x35, 0x24, 0x31, 0x2E, 0x2C, 0x31, 0x2A };
            var output = WorldMesDecoder.Decode(input, 0, input.Length, markUnknown: false);
            Assert.Equal("high-ranking", output);
        }

        [Fact]
        public void ContentLength_TrimsTrailingZeros()
        {
            byte[] input = { 0x41, 0x42, 0x00, 0x00, 0x00 };
            Assert.Equal(2, WorldMesDecoder.ContentLength(input));
        }

        [Fact]
        public void ExtractRumors_ReturnsAtLeast25Records_WhenWorldBinPresent()
        {
            var bytes = LoadWorldBin();
            if (bytes == null) return; // skip when file missing
            var rumors = WorldMesDecoder.ExtractRumors(bytes);
            Assert.True(rumors.Count >= 25, $"expected ≥25 rumors, got {rumors.Count}");
        }

        [Fact]
        public void ExtractRumors_FirstRumor_MentionsCorpseBrigadeAndBrigandry()
        {
            var bytes = LoadWorldBin();
            if (bytes == null) return;
            var rumors = WorldMesDecoder.ExtractRumors(bytes);
            Assert.Contains("Brigandry", rumors[0].Body);
            Assert.Contains("Corpse Brigade", rumors[0].Body);
        }

        [Fact]
        public void ExtractRumors_ContainsRiovanesHorrorRumor()
        {
            var bytes = LoadWorldBin();
            if (bytes == null) return;
            var rumors = WorldMesDecoder.ExtractRumors(bytes);
            Assert.Contains(rumors, r => r.Body.Contains("Riovanes") && r.Body.Contains("fiend"));
        }

        [Fact]
        public void ExtractRumors_ContainsZodiacStonesEntry()
        {
            var bytes = LoadWorldBin();
            if (bytes == null) return;
            var rumors = WorldMesDecoder.ExtractRumors(bytes);
            Assert.Contains(rumors, r => r.Body.Contains("Zodiac Stones"));
        }

        [Fact]
        public void ExtractRumors_ContainsFiftyYearsWarEntry()
        {
            var bytes = LoadWorldBin();
            if (bytes == null) return;
            var rumors = WorldMesDecoder.ExtractRumors(bytes);
            Assert.Contains(rumors, r => r.Body.Contains("Fifty Years' War"));
        }
    }
}
