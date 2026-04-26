using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class DialogueSpeakerReaderTests
    {
        // Real pointer-bytes seen at 0x133D1FA70 during event 045 box 16.
        // Pointer u64 LE = 0x4E17629DD8 (in the speaker-name string table).
        private static readonly byte[] DukeLargPointerBytes = new byte[]
        {
            0xD8, 0x9D, 0x62, 0x17, 0x4E, 0x00, 0x00, 0x00
        };

        // Realistic table snippet at 0x4E17629DD8: "Duke Larg\0Gaffgarion\0..."
        private static readonly byte[] DukeLargTableBytes =
            System.Text.Encoding.ASCII.GetBytes("Duke Larg\0Gaffgarion\0");

        [Fact]
        public void Read_PointerToValidString_ReturnsSpeakerName()
        {
            var reader = new DialogueSpeakerReader(addr =>
            {
                if (addr == 0x4E17629DD8) return DukeLargTableBytes;
                return null;
            });

            var speaker = reader.Read(DukeLargPointerBytes);

            Assert.Equal("Duke Larg", speaker);
        }

        [Fact]
        public void Read_NullPointer_ReturnsNull()
        {
            var reader = new DialogueSpeakerReader(addr => null);
            var nullPtr = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };

            Assert.Null(reader.Read(nullPtr));
        }

        [Fact]
        public void Read_PointerOutsideStringTableRange_ReturnsNull()
        {
            // Pointer to 0x10000 — way outside the 0x4E17xxxxxx string table.
            var bogusPtr = new byte[] { 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var reader = new DialogueSpeakerReader(addr => System.Text.Encoding.ASCII.GetBytes("anything"));

            Assert.Null(reader.Read(bogusPtr));
        }

        [Fact]
        public void Read_ReadCallbackReturnsNull_ReturnsNull()
        {
            var reader = new DialogueSpeakerReader(addr => null);

            Assert.Null(reader.Read(DukeLargPointerBytes));
        }

        [Fact]
        public void Read_StringWithNoNullTerminator_CapsAtMaxLength()
        {
            var unterminated = new byte[64];
            for (int i = 0; i < 64; i++) unterminated[i] = (byte)'A';
            var reader = new DialogueSpeakerReader(addr => unterminated);

            var speaker = reader.Read(DukeLargPointerBytes);

            // Should cap at the buffer length (no string explosion).
            Assert.NotNull(speaker);
            Assert.True(speaker!.Length <= 64);
        }

        [Fact]
        public void Read_NonAsciiBytes_ReturnsNull()
        {
            // A pointer in the table range but bytes that are clearly garbage / binary.
            var binaryGarbage = new byte[] { 0xFF, 0xFE, 0x80, 0x00 };
            var reader = new DialogueSpeakerReader(addr => binaryGarbage);

            Assert.Null(reader.Read(DukeLargPointerBytes));
        }

        [Fact]
        public void Read_EmptyStringAtPointer_ReturnsNull()
        {
            var emptyAtStart = new byte[] { 0x00, 0x41, 0x42 };
            var reader = new DialogueSpeakerReader(addr => emptyAtStart);

            Assert.Null(reader.Read(DukeLargPointerBytes));
        }

        [Fact]
        public void Read_PointerBytesShorterThan8_ReturnsNull()
        {
            var reader = new DialogueSpeakerReader(addr => DukeLargTableBytes);

            Assert.Null(reader.Read(new byte[] { 0xD8, 0x9D, 0x62 }));
        }

        [Fact]
        public void Read_KnownSpeakers_FromLiveCapture()
        {
            // Three captures from event 045 boxes 14/15/16.
            var captures = new[]
            {
                (boxLabel: "14", ptr: new byte[] { 0xE4, 0xA3, 0x62, 0x17, 0x4E, 0, 0, 0 },
                 expectAddr: (long)0x4E1762A3E4, table: "Well-dressed Man\0Executioner\0"),
                (boxLabel: "15", ptr: new byte[] { 0x3F, 0x9D, 0x62, 0x17, 0x4E, 0, 0, 0 },
                 expectAddr: (long)0x4E17629D3F, table: "Dycedarg\0Adrammelech, the Wroth\0"),
                (boxLabel: "16", ptr: new byte[] { 0xD8, 0x9D, 0x62, 0x17, 0x4E, 0, 0, 0 },
                 expectAddr: (long)0x4E17629DD8, table: "Duke Larg\0Gaffgarion\0"),
            };

            foreach (var c in captures)
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes(c.table);
                var reader = new DialogueSpeakerReader(addr =>
                    addr == c.expectAddr ? bytes : null);

                var speaker = reader.Read(c.ptr);

                Assert.NotNull(speaker);
                // Each capture's expected speaker is the prefix of the table.
                Assert.Equal(c.table.Split('\0')[0], speaker);
            }
        }
    }
}
