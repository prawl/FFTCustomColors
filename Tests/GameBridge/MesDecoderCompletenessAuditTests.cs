using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Completeness audit for <see cref="MesDecoder.DecodeByte"/>.
    /// Pins the full u8 → char? mapping. Any new byte that starts
    /// decoding (e.g. a missing control byte added to the alphabet)
    /// fails these tests, forcing explicit test updates.
    ///
    /// Session 47 addition: existing DecodeByte tests cover spot-checks.
    /// This file pins the BOUNDARY between decoded and null bytes across
    /// the full 0x00-0xFF range.
    /// </summary>
    public class MesDecoderCompletenessAuditTests
    {
        /// <summary>
        /// Expected decoded bytes per the current MesDecoder implementation.
        /// Every other byte in 0x00-0xFF returns null (control / unused /
        /// extended character not yet mapped).
        ///
        /// When a new byte is mapped, add it here AND add a targeted
        /// DecodeByte_* test in MesDecoderTests.
        /// </summary>
        private static readonly Dictionary<byte, char> ExpectedDecoded = BuildExpected();

        private static Dictionary<byte, char> BuildExpected()
        {
            var m = new Dictionary<byte, char>();
            for (byte b = 0x00; b <= 0x09; b++) m[b] = (char)('0' + b);
            for (byte b = 0x0A; b <= 0x23; b++) m[b] = (char)('A' + b - 0x0A);
            for (byte b = 0x24; b <= 0x3D; b++) m[b] = (char)('a' + b - 0x24);
            m[0x3E] = '!';
            m[0x3F] = '\'';
            m[0x40] = '?';
            m[0x44] = '-';
            m[0x45] = ';';
            m[0x46] = ':';
            m[0x47] = '"';
            m[0x48] = '(';
            m[0x49] = ')';
            m[0x5F] = '.';
            m[0x78] = '.';
            m[0x7B] = '?';
            m[0x7C] = '\n';
            m[0x7D] = '\'';
            m[0x88] = ' ';
            m[0x8B] = ',';
            m[0xB7] = '/';
            m[0xFA] = ' ';
            return m;
        }

        [Fact]
        public void AllExpected_DecodeToCharacter()
        {
            foreach (var (b, expected) in ExpectedDecoded)
            {
                var actual = MesDecoder.DecodeByte(b);
                Assert.NotNull(actual);
                Assert.Equal(expected, actual!.Value);
            }
        }

        [Fact]
        public void AllUnexpected_ReturnNull()
        {
            // Every byte NOT in the expected dict must decode to null. A
            // new byte being added to DecodeByte without being added to
            // ExpectedDecoded fires this test.
            var leaks = new List<string>();
            for (int i = 0; i <= 0xFF; i++)
            {
                byte b = (byte)i;
                if (ExpectedDecoded.ContainsKey(b)) continue;
                var actual = MesDecoder.DecodeByte(b);
                if (actual.HasValue)
                {
                    leaks.Add($"0x{b:X2} → '{actual.Value}'");
                }
            }
            Assert.True(leaks.Count == 0,
                $"Unexpected bytes decoding to characters — add to ExpectedDecoded " +
                $"AND to MesDecoderTests: {string.Join(", ", leaks)}");
        }

        [Fact]
        public void ExpectedSize_Is_CanonicalCount()
        {
            // Pin the canonical size of the mapping. Prevents silent
            // deletions — if a byte is REMOVED from DecodeByte, the
            // corresponding ExpectedDecoded entry still exists and
            // AllExpected_DecodeToCharacter fires.
            // Size: 10 digits + 26 upper + 26 lower + 18 punctuation/ws = 80.
            Assert.Equal(80, ExpectedDecoded.Count);
        }

        [Fact]
        public void MultipleBytes_DecodeToSameCharacter()
        {
            // Both 0x78 and 0x5F map to '.'. Both 0x40 and 0x7B map to '?'.
            // Both 0x88 and 0xFA map to ' '. Both 0x3F and 0x7D map to '\''.
            // Pin these duplicates so a "cleanup refactor" doesn't drop one.
            Assert.Equal('.', MesDecoder.DecodeByte(0x78));
            Assert.Equal('.', MesDecoder.DecodeByte(0x5F));
            Assert.Equal('?', MesDecoder.DecodeByte(0x40));
            Assert.Equal('?', MesDecoder.DecodeByte(0x7B));
            Assert.Equal(' ', MesDecoder.DecodeByte(0x88));
            Assert.Equal(' ', MesDecoder.DecodeByte(0xFA));
            Assert.Equal('\'', MesDecoder.DecodeByte(0x3F));
            Assert.Equal('\'', MesDecoder.DecodeByte(0x7D));
        }

        [Fact]
        public void ControlBytes_Are_Null()
        {
            // Known control bytes (box boundary, line wrap, speaker tag)
            // must NOT decode to characters — DecodeBoxes / DecodeBytes
            // filter them out as structural markup.
            Assert.Null(MesDecoder.DecodeByte(0xFE));  // box boundary
            Assert.Null(MesDecoder.DecodeByte(0xF8));  // line wrap
            Assert.Null(MesDecoder.DecodeByte(0xE3));  // speaker tag opener
        }
    }
}
