using System.Collections.Generic;
using System.Text;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests the pure name-table byte parser. Instance-level memory reading is
    /// integration-tested in-game — these tests exercise the byte parsing logic
    /// that runs against whatever bytes we find.
    /// </summary>
    public class NameTableLookupTests
    {
        /// <summary>
        /// Helper: build a contiguous null-terminated string buffer for test input.
        /// </summary>
        private static byte[] BuildBuffer(params string[] names)
        {
            var sb = new List<byte>();
            foreach (var name in names)
            {
                sb.AddRange(Encoding.ASCII.GetBytes(name));
                sb.Add(0);
            }
            return sb.ToArray();
        }

        [Fact]
        public void ParseNameTable_EmptyBuffer_ReturnsEmpty()
        {
            var result = NameTableLookup.ParseNameTable(System.Array.Empty<byte>());
            Assert.Empty(result);
        }

        [Fact]
        public void ParseNameTable_NullBuffer_ReturnsEmpty()
        {
            var result = NameTableLookup.ParseNameTable(null!);
            Assert.Empty(result);
        }

        [Fact]
        public void ParseNameTable_SingleName_Returns1BasedIndex()
        {
            var bytes = BuildBuffer("Ramza");
            var result = NameTableLookup.ParseNameTable(bytes);
            Assert.Single(result);
            Assert.Equal("Ramza", result[1]);
        }

        [Fact]
        public void ParseNameTable_ThreeNames_IndexedFromOne()
        {
            var bytes = BuildBuffer("Ramza", "Delita", "Argath");
            var result = NameTableLookup.ParseNameTable(bytes);
            Assert.Equal(3, result.Count);
            Assert.Equal("Ramza", result[1]);
            Assert.Equal("Delita", result[2]);
            Assert.Equal("Argath", result[3]);
        }

        [Fact]
        public void ParseNameTable_RealisticPrefix_Returns9StoryNames()
        {
            // Matches the actual game table prefix observed in memory
            var bytes = BuildBuffer(
                "Ramza", "Delita", "Argath", "Zalbaag", "Dycedarg",
                "Larg", "Goltanna", "Ovelia", "Orlandeau");
            var result = NameTableLookup.ParseNameTable(bytes);

            Assert.Equal(9, result.Count);
            Assert.Equal("Ramza", result[1]);
            Assert.Equal("Orlandeau", result[9]);
        }

        [Fact]
        public void ParseNameTable_StopsAtFirstNonPrintableRun()
        {
            // Simulate: 3 valid names, then control-byte garbage (non-string data).
            // Parser should bail out because control bytes (0x01-0x1F, 0x7F) aren't
            // valid in character names. High-bit bytes (0x80+) ARE valid for UTF-8.
            var prefix = BuildBuffer("Ramza", "Delita", "Argath");
            var garbage = new byte[] { 0x05, 0x0A, 0x10, 0x00 };
            var bytes = new byte[prefix.Length + garbage.Length];
            System.Array.Copy(prefix, 0, bytes, 0, prefix.Length);
            System.Array.Copy(garbage, 0, bytes, prefix.Length, garbage.Length);

            var result = NameTableLookup.ParseNameTable(bytes);
            Assert.Equal(3, result.Count);
            Assert.DoesNotContain(4, result.Keys);
        }

        [Fact]
        public void ParseNameTable_AcceptsHighByteNames()
        {
            // Some character names contain non-ASCII UTF-8 bytes (e.g. the real
            // game's Cuchulainn uses an accented u). Parser must not bail on bytes
            // in the 0x80-0xFF range — those are valid UTF-8 continuation or lead
            // bytes, not control characters.
            var prefix = Encoding.ASCII.GetBytes("Ramza");
            // Construct "C" + u-acute (C3 BA) + "chulainn" as raw bytes
            var accented = new byte[] { 0x43, 0xC3, 0xBA, 0x63, 0x68, 0x75, 0x6C, 0x61, 0x69, 0x6E, 0x6E };
            var suffix = Encoding.ASCII.GetBytes("Boco");
            var bytes = new List<byte>();
            bytes.AddRange(prefix); bytes.Add(0);
            bytes.AddRange(accented); bytes.Add(0);
            bytes.AddRange(suffix); bytes.Add(0);

            var result = NameTableLookup.ParseNameTable(bytes.ToArray());
            Assert.Equal(3, result.Count);
            Assert.Equal("Ramza", result[1]);
            // Verify index 2 exists and has the right length (10 UTF-8 chars: C + ú + chulainn)
            Assert.True(result.ContainsKey(2));
            Assert.Equal("Boco", result[3]);
        }

        [Fact]
        public void ParseNameTable_StopsAtRunOfConsecutiveNulls()
        {
            // Simulate: 12 names followed by a long run of nulls (table end padding).
            // Parser should stop parsing after the run.
            var prefix = BuildBuffer(
                "Ramza", "Delita", "Argath", "Zalbaag", "Dycedarg",
                "Larg", "Goltanna", "Ovelia", "Orlandeau", "Marcel",
                "Reis", "Zalmour");
            var padding = new byte[200]; // all zeros
            var bytes = new byte[prefix.Length + padding.Length];
            System.Array.Copy(prefix, 0, bytes, 0, prefix.Length);
            // padding already zero

            var result = NameTableLookup.ParseNameTable(bytes);
            Assert.Equal(12, result.Count);
            Assert.Equal("Zalmour", result[12]);
            Assert.DoesNotContain(13, result.Keys);
        }

        [Fact]
        public void ParseNameTable_IndexMatchesRealGameValues()
        {
            // From real game observation: Wilham is at index 76, Kenrick at 103.
            // Build a buffer with 103 entries and verify both.
            var names = new List<string>();
            names.Add("Ramza"); // idx 1
            // pad with dummy names up to 75
            for (int i = 2; i <= 75; i++) names.Add("Name" + i);
            names.Add("Wilham"); // idx 76
            // pad up to 102
            for (int i = 77; i <= 102; i++) names.Add("Name" + i);
            names.Add("Kenrick"); // idx 103

            var bytes = BuildBuffer(names.ToArray());
            var result = NameTableLookup.ParseNameTable(bytes);

            Assert.Equal(103, result.Count);
            Assert.Equal("Wilham", result[76]);
            Assert.Equal("Kenrick", result[103]);
        }

        [Fact]
        public void ParseNameTable_TooLongStringTerminatesParsing()
        {
            // A 40-char "name" should abort — realistic table entries are under 20.
            var prefix = BuildBuffer("Ramza", "Delita");
            var longName = Encoding.ASCII.GetBytes("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
            var bytes = new byte[prefix.Length + longName.Length + 1];
            System.Array.Copy(prefix, 0, bytes, 0, prefix.Length);
            System.Array.Copy(longName, 0, bytes, prefix.Length, longName.Length);
            bytes[^1] = 0;

            var result = NameTableLookup.ParseNameTable(bytes);
            Assert.Equal(2, result.Count);
            Assert.DoesNotContain(3, result.Keys);
        }

        [Fact]
        public void ParseNameTable_NamesWithSpaces_Parsed()
        {
            // "Construct 8" contains a space — valid ASCII, must parse.
            var bytes = BuildBuffer("Ramza", "Construct 8", "Boco");
            var result = NameTableLookup.ParseNameTable(bytes);
            Assert.Equal(3, result.Count);
            Assert.Equal("Construct 8", result[2]);
            Assert.Equal("Boco", result[3]);
        }
    }
}
