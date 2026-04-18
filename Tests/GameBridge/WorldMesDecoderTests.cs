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

        [Fact]
        public void RumorLookup_GetByTitle_ZodiacBraves_ReturnsCorpus10()
        {
            var lookup = new RumorLookup();
            var r = lookup.GetByTitle("The Legend of the Zodiac Braves");
            Assert.NotNull(r);
            Assert.Equal(10, r!.Index);
            Assert.Contains("Zodiac Braves", r.Body);
        }

        [Fact]
        public void RumorLookup_GetByTitle_ZodiacStones_ReturnsCorpus11()
        {
            var lookup = new RumorLookup();
            var r = lookup.GetByTitle("Zodiac Stones");
            Assert.NotNull(r);
            Assert.Equal(11, r!.Index);
            Assert.Contains("crystals", r.Body);
        }

        [Fact]
        public void RumorLookup_GetByTitle_HorrorOfRiovanes_ReturnsCorpus19()
        {
            var lookup = new RumorLookup();
            var r = lookup.GetByTitle("The Horror of Riovanes");
            Assert.NotNull(r);
            Assert.Equal(19, r!.Index);
            Assert.Contains("Riovanes", r.Body);
        }

        [Fact]
        public void RumorLookup_GetByTitle_CaseInsensitive()
        {
            var lookup = new RumorLookup();
            var r = lookup.GetByTitle("zodiac stones");
            Assert.NotNull(r);
            Assert.Equal(11, r!.Index);
        }

        [Fact]
        public void RumorLookup_GetByTitle_UnknownReturnsNull()
        {
            var lookup = new RumorLookup();
            Assert.Null(lookup.GetByTitle("Made-Up Title That Does Not Exist"));
        }

        [Fact]
        public void RumorLookup_KnownTitles_AllIndicesInRange()
        {
            var lookup = new RumorLookup();
            foreach (var kv in RumorLookup.KnownTitles)
            {
                Assert.InRange(kv.Value, 0, lookup.Count - 1);
            }
        }

        // Hardcoded corpus regression tests — these pin specific corpus entries so we
        // catch any future decoder change that reshuffles the order. Uses the shipped
        // RumorCorpus.Bodies array, not the file-based ExtractRumors (which only runs
        // when world.bin is present).

        [Fact]
        public void RumorCorpus_HasAtLeast26Entries()
        {
            Assert.True(RumorCorpus.Bodies.Length >= 26,
                $"expected >=26 hardcoded bodies, got {RumorCorpus.Bodies.Length}");
        }

        [Fact]
        public void RumorCorpus_Entry0_IsBrigandryIntro()
        {
            Assert.StartsWith("Brigandry is on the rise", RumorCorpus.Bodies[0]);
            Assert.Contains("Corpse Brigade", RumorCorpus.Bodies[0]);
        }

        [Fact]
        public void RumorCorpus_Entry4_IsCorpseBrigadeDefeat()
        {
            Assert.Contains("Corpse Brigade", RumorCorpus.Bodies[4]);
            Assert.Contains("defeated", RumorCorpus.Bodies[4]);
        }

        [Fact]
        public void RumorCorpus_Entry10_IsZodiacBravesLegend()
        {
            Assert.Contains("Ivalice was united", RumorCorpus.Bodies[10]);
            Assert.Contains("Zodiac Braves", RumorCorpus.Bodies[10]);
        }

        [Fact]
        public void RumorCorpus_Entry11_IsZodiacStones()
        {
            Assert.Contains("crystals are said to date from the age of myth",
                RumorCorpus.Bodies[11]);
            Assert.Contains("Zodiac Stones", RumorCorpus.Bodies[11]);
        }

        [Fact]
        public void RumorCorpus_Entry15_IsDelacroixDeath()
        {
            Assert.Contains("Cardinal Delacroix", RumorCorpus.Bodies[15]);
        }

        [Fact]
        public void RumorCorpus_Entry19_IsRiovanesFiend()
        {
            Assert.Contains("Riovanes", RumorCorpus.Bodies[19]);
            Assert.Contains("fiend", RumorCorpus.Bodies[19]);
        }

        [Fact]
        public void RumorCorpus_AllEntriesNonEmpty()
        {
            for (int i = 0; i < RumorCorpus.Bodies.Length; i++)
            {
                Assert.False(string.IsNullOrWhiteSpace(RumorCorpus.Bodies[i]),
                    $"RumorCorpus.Bodies[{i}] is empty or whitespace");
            }
        }

        [Fact]
        public void RumorCorpus_AllEntriesAtLeast100Chars()
        {
            // Rumor bodies are always multi-sentence prose — short entries indicate
            // a decoder bug that truncated content.
            for (int i = 0; i < RumorCorpus.Bodies.Length; i++)
            {
                Assert.True(RumorCorpus.Bodies[i].Length >= 100,
                    $"RumorCorpus.Bodies[{i}] only {RumorCorpus.Bodies[i].Length} chars: '{RumorCorpus.Bodies[i].Substring(0, System.Math.Min(60, RumorCorpus.Bodies[i].Length))}'");
            }
        }
    }
}
