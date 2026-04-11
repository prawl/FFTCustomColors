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

        // =========================================================================
        // ParseRosterNameTable tests — the stride-based parser we actually use.
        // =========================================================================

        /// <summary>
        /// Build a single roster record: 0x10 bytes of filler, then a null-terminated
        /// name, then zeros to fill out the 0x280-byte stride.
        /// </summary>
        private static byte[] BuildRecord(string name)
        {
            var record = new byte[NameTableLookup.RecordStride];
            var nameBytes = Encoding.ASCII.GetBytes(name);
            System.Array.Copy(nameBytes, 0, record, NameTableLookup.NameOffsetInRecord, nameBytes.Length);
            // Null terminator already in place (array zeroed)
            return record;
        }

        private static byte[] ConcatRecords(params string[] names)
        {
            var total = new List<byte>();
            foreach (var name in names)
            {
                total.AddRange(BuildRecord(name));
            }
            return total.ToArray();
        }

        [Fact]
        public void ParseRosterNameTable_EmptyBuffer_ReturnsEmpty()
        {
            var result = NameTableLookup.ParseRosterNameTable(System.Array.Empty<byte>());
            Assert.Empty(result);
        }

        [Fact]
        public void ParseRosterNameTable_NullBuffer_ReturnsEmpty()
        {
            var result = NameTableLookup.ParseRosterNameTable(null!);
            Assert.Empty(result);
        }

        [Fact]
        public void ParseRosterNameTable_SingleRecord_ReturnsSlot0()
        {
            var bytes = BuildRecord("Ramza");
            var result = NameTableLookup.ParseRosterNameTable(bytes);
            Assert.Single(result);
            Assert.Equal("Ramza", result[0]);
        }

        [Fact]
        public void ParseRosterNameTable_FiveRecords_MapsByZeroBasedSlot()
        {
            // Matches the real in-game observation: Ramza, Kenrick, Lloyd, Wilham, Alicia
            var bytes = ConcatRecords("Ramza", "Kenrick", "Lloyd", "Wilham", "Alicia");
            var result = NameTableLookup.ParseRosterNameTable(bytes);
            Assert.Equal(5, result.Count);
            Assert.Equal("Ramza", result[0]);
            Assert.Equal("Kenrick", result[1]);
            Assert.Equal("Lloyd", result[2]);
            Assert.Equal("Wilham", result[3]);
            Assert.Equal("Alicia", result[4]);
        }

        [Fact]
        public void ParseRosterNameTable_EmptyRecordTerminatesParsing()
        {
            // 3 valid records followed by an empty one — parser stops at the empty.
            // Any further records (even if valid) are ignored because we've walked
            // past the end of the recruit list.
            var valid = ConcatRecords("Ramza", "Kenrick", "Lloyd");
            var empty = new byte[NameTableLookup.RecordStride]; // all zeros
            var extra = BuildRecord("ShouldNotAppear");
            var bytes = new byte[valid.Length + empty.Length + extra.Length];
            valid.CopyTo(bytes, 0);
            empty.CopyTo(bytes, valid.Length);
            extra.CopyTo(bytes, valid.Length + empty.Length);

            var result = NameTableLookup.ParseRosterNameTable(bytes);
            Assert.Equal(3, result.Count);
            Assert.DoesNotContain(3, result.Keys);
            Assert.DoesNotContain(4, result.Keys);
        }

        [Fact]
        public void ParseRosterNameTable_NamesAtCorrectOffset()
        {
            // Verify that the parser reads from +0x10 inside each record, not +0x00.
            // Put garbage at +0x00 through +0x0F that would fail validation, real
            // name at +0x10.
            var record = new byte[NameTableLookup.RecordStride];
            for (int i = 0; i < NameTableLookup.NameOffsetInRecord; i++)
                record[i] = 0xFF; // garbage (would be invalid as a name)
            var nameBytes = Encoding.ASCII.GetBytes("Kenrick");
            System.Array.Copy(nameBytes, 0, record, NameTableLookup.NameOffsetInRecord, nameBytes.Length);

            var result = NameTableLookup.ParseRosterNameTable(record);
            Assert.Single(result);
            Assert.Equal("Kenrick", result[0]);
        }

        [Fact]
        public void ParseRosterNameTable_OverLongNameStopsParsing()
        {
            // If a record has a "name" that never hits a null terminator within
            // MaxNameLength, treat it as table end.
            var record = new byte[NameTableLookup.RecordStride];
            for (int i = NameTableLookup.NameOffsetInRecord;
                 i < NameTableLookup.NameOffsetInRecord + NameTableLookup.MaxNameLength + 5;
                 i++)
            {
                record[i] = (byte)'A';
            }
            // No null terminator within MaxNameLength bytes

            var result = NameTableLookup.ParseRosterNameTable(record);
            Assert.Empty(result);
        }

        [Fact]
        public void ParseRosterNameTable_NullTerminatedWithinRange_IsAccepted()
        {
            // Longer name but still within bounds should parse fine.
            var record = BuildRecord("Esperaunce"); // 10 chars, well under MaxNameLength
            var result = NameTableLookup.ParseRosterNameTable(record);
            Assert.Single(result);
            Assert.Equal("Esperaunce", result[0]);
        }
    }
}
