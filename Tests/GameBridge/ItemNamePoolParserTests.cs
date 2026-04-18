using System.Collections.Generic;
using System.Text;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure parser for the static item-name pool around game address 0x3F18000.
    /// Record layout per memory/project_item_name_pool.md:
    ///   offset  size  meaning
    ///   0x00    8     pointer (locale/shared, not useful for lookup)
    ///   0x08    4     length (u32, in chars)
    ///   0x0C    N*2   UTF-16LE characters
    ///   ...           zero-padding to 4-byte boundary before next record
    /// </summary>
    public class ItemNamePoolParserTests
    {
        /// <summary>Build a single record's bytes, padded to the given alignment.</summary>
        private static byte[] BuildRecord(string name, int recordAlign = 4)
        {
            var nameBytes = Encoding.Unicode.GetBytes(name); // UTF-16LE
            int headerSize = 12; // 8 pointer + 4 length
            int bodySize = nameBytes.Length;
            int unaligned = headerSize + bodySize;
            int padded = ((unaligned + recordAlign - 1) / recordAlign) * recordAlign;
            var buf = new byte[padded];
            // 8-byte pointer is irrelevant — leave zeros.
            // length at offset 8
            buf[8] = (byte)(name.Length & 0xFF);
            buf[9] = (byte)((name.Length >> 8) & 0xFF);
            buf[10] = (byte)((name.Length >> 16) & 0xFF);
            buf[11] = (byte)((name.Length >> 24) & 0xFF);
            // UTF-16 bytes at offset 12
            System.Array.Copy(nameBytes, 0, buf, 12, nameBytes.Length);
            return buf;
        }

        [Fact]
        public void Decode_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Empty(ItemNamePoolParser.Decode(null!));
            Assert.Empty(ItemNamePoolParser.Decode(System.Array.Empty<byte>()));
        }

        [Fact]
        public void Decode_SingleRecord_ReturnsOne()
        {
            var bytes = BuildRecord("Oak Staff");
            var records = ItemNamePoolParser.Decode(bytes);
            Assert.Single(records);
            Assert.Equal("Oak Staff", records[0].Name);
            Assert.Equal(9, records[0].CharLength);
            Assert.Equal(0, records[0].OffsetInBuffer);
        }

        [Fact]
        public void Decode_MultipleRecords_ReturnsAll()
        {
            var r1 = BuildRecord("Oak Staff");
            var r2 = BuildRecord("White Staff");
            var r3 = BuildRecord("Ragnarok");
            var combined = new byte[r1.Length + r2.Length + r3.Length];
            System.Array.Copy(r1, 0, combined, 0, r1.Length);
            System.Array.Copy(r2, 0, combined, r1.Length, r2.Length);
            System.Array.Copy(r3, 0, combined, r1.Length + r2.Length, r3.Length);

            var records = ItemNamePoolParser.Decode(combined);
            Assert.Equal(3, records.Count);
            Assert.Equal("Oak Staff", records[0].Name);
            Assert.Equal("White Staff", records[1].Name);
            Assert.Equal("Ragnarok", records[2].Name);

            // Offsets align with the start of each record in the buffer.
            Assert.Equal(0, records[0].OffsetInBuffer);
            Assert.Equal(r1.Length, records[1].OffsetInBuffer);
            Assert.Equal(r1.Length + r2.Length, records[2].OffsetInBuffer);
        }

        [Fact]
        public void Decode_StopsAtZeroLength()
        {
            // A length-0 record is the end-of-pool sentinel — stop.
            var r1 = BuildRecord("Oak Staff");
            var sentinel = new byte[16]; // all zeros, length=0 at offset 8
            var noise = BuildRecord("ShouldNotAppear");
            var combined = new byte[r1.Length + sentinel.Length + noise.Length];
            System.Array.Copy(r1, 0, combined, 0, r1.Length);
            System.Array.Copy(sentinel, 0, combined, r1.Length, sentinel.Length);
            System.Array.Copy(noise, 0, combined, r1.Length + sentinel.Length, noise.Length);

            var records = ItemNamePoolParser.Decode(combined);
            Assert.Single(records);
            Assert.Equal("Oak Staff", records[0].Name);
        }

        [Fact]
        public void Decode_StopsAtImplausiblyLongLength()
        {
            // If the length field is absurdly large (> remaining buffer),
            // treat as garbage / end-of-pool — don't try to read past buffer end.
            var r1 = BuildRecord("Oak Staff");
            var corrupt = new byte[16];
            corrupt[8] = 0xFF; corrupt[9] = 0xFF; corrupt[10] = 0xFF; corrupt[11] = 0x7F;
            var combined = new byte[r1.Length + corrupt.Length];
            System.Array.Copy(r1, 0, combined, 0, r1.Length);
            System.Array.Copy(corrupt, 0, combined, r1.Length, corrupt.Length);

            var records = ItemNamePoolParser.Decode(combined);
            Assert.Single(records);
            Assert.Equal("Oak Staff", records[0].Name);
        }

        [Fact]
        public void Decode_BufferTooShortForHeader_ReturnsEmpty()
        {
            // Less than 12 bytes — can't even read a full header. Return empty.
            var tiny = new byte[8];
            Assert.Empty(ItemNamePoolParser.Decode(tiny));
        }

        [Fact]
        public void GetByIndex_ReturnsNthRecord()
        {
            var r1 = BuildRecord("Oak Staff");
            var r2 = BuildRecord("White Staff");
            var combined = new byte[r1.Length + r2.Length];
            System.Array.Copy(r1, 0, combined, 0, r1.Length);
            System.Array.Copy(r2, 0, combined, r1.Length, r2.Length);

            var records = ItemNamePoolParser.Decode(combined);
            Assert.Equal("Oak Staff", ItemNamePoolParser.GetByIndex(records, 0)?.Name);
            Assert.Equal("White Staff", ItemNamePoolParser.GetByIndex(records, 1)?.Name);
            Assert.Null(ItemNamePoolParser.GetByIndex(records, 2));
            Assert.Null(ItemNamePoolParser.GetByIndex(records, -1));
        }

        [Fact]
        public void GetByIndex_NullList_ReturnsNull()
        {
            Assert.Null(ItemNamePoolParser.GetByIndex(null!, 0));
        }

        [Fact]
        public void FindByName_CaseInsensitive()
        {
            var records = ItemNamePoolParser.Decode(BuildRecord("Oak Staff"));
            Assert.NotNull(ItemNamePoolParser.FindByName(records, "oak staff"));
            Assert.NotNull(ItemNamePoolParser.FindByName(records, "OAK STAFF"));
            Assert.Null(ItemNamePoolParser.FindByName(records, "Not An Item"));
        }

        [Fact]
        public void Decode_UnicodeNames_Preserved()
        {
            // FFT has items like "Cachusha" or accented names — UTF-16 decode
            // must not mangle multibyte chars.
            var r = BuildRecord("Cúchulainn's Sword");
            var records = ItemNamePoolParser.Decode(r);
            Assert.Single(records);
            Assert.Equal("Cúchulainn's Sword", records[0].Name);
        }
    }
}
