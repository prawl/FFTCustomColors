using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class MesDecoderTests
    {
        [Fact]
        public void DecodeByte_Digit0_Returns0()
        {
            Assert.Equal('0', MesDecoder.DecodeByte(0x00));
        }

        [Fact]
        public void DecodeByte_Digit9_Returns9()
        {
            Assert.Equal('9', MesDecoder.DecodeByte(0x09));
        }

        [Fact]
        public void DecodeByte_UpperA_ReturnsA()
        {
            Assert.Equal('A', MesDecoder.DecodeByte(0x0A));
        }

        [Fact]
        public void DecodeByte_UpperZ_ReturnsZ()
        {
            Assert.Equal('Z', MesDecoder.DecodeByte(0x23));
        }

        [Fact]
        public void DecodeByte_LowerA_Returnsa()
        {
            Assert.Equal('a', MesDecoder.DecodeByte(0x24));
        }

        [Fact]
        public void DecodeByte_LowerZ_Returnsz()
        {
            Assert.Equal('z', MesDecoder.DecodeByte(0x3D));
        }

        [Fact]
        public void DecodeByte_Space_ReturnsSpace()
        {
            Assert.Equal(' ', MesDecoder.DecodeByte(0x88));
        }

        [Fact]
        public void DecodeByte_AltSpace_ReturnsSpace()
        {
            Assert.Equal(' ', MesDecoder.DecodeByte(0xFA));
        }

        [Fact]
        public void DecodeByte_Period_ReturnsPeriod()
        {
            Assert.Equal('.', MesDecoder.DecodeByte(0x78));
        }

        [Fact]
        public void DecodeByte_Comma_ReturnsComma()
        {
            Assert.Equal(',', MesDecoder.DecodeByte(0x8B));
        }

        [Fact]
        public void DecodeByte_Exclamation_ReturnsExclamation()
        {
            Assert.Equal('!', MesDecoder.DecodeByte(0x3E));
        }

        [Fact]
        public void DecodeByte_Question_ReturnsQuestion()
        {
            Assert.Equal('?', MesDecoder.DecodeByte(0x40));
        }

        [Fact]
        public void DecodeByte_Apostrophe_ReturnsApostrophe()
        {
            Assert.Equal('\'', MesDecoder.DecodeByte(0x3F));
        }

        [Fact]
        public void DecodeByte_Apostrophe2_ReturnsApostrophe()
        {
            Assert.Equal('\'', MesDecoder.DecodeByte(0x7D));
        }

        [Fact]
        public void DecodeDialogue_LadyOvelia()
        {
            // "Lady Ovelia" in PSX encoding: L=0x15 a=0x24 d=0x27 y=0x3C O=0x18 v=0x39 e=0x28 l=0x2F i=0x2C a=0x24
            byte[] bytes = { 0x15, 0x24, 0x27, 0x3C, 0x88, 0x18, 0x39, 0x28, 0x2F, 0x2C, 0x24 };
            Assert.Equal("Lady Ovelia", MesDecoder.DecodeBytes(bytes));
        }

        [Fact]
        public void DecodeFile_ReturnsDialogueLines()
        {
            // Test with actual event002.en.mes if available
            var path = FindMesFile("event002.en.mes");
            if (path == null) return; // Skip if file not available

            var result = MesDecoder.DecodeFile(path);
            Assert.NotEmpty(result);
            Assert.Contains(result, line => line.Text.Contains("Lady Ovelia"));
        }

        [Fact]
        public void DecodeFile_ParsesSpeakerTags()
        {
            var path = FindMesFile("event002.en.mes");
            if (path == null) return;

            var result = MesDecoder.DecodeFile(path);
            Assert.Contains(result, line => line.Speaker == "Knight");
            Assert.Contains(result, line => line.Speaker == "Ovelia");
            Assert.Contains(result, line => line.Speaker == "Agrias");
        }

        private static string? FindMesFile(string filename)
        {
            var path = $"c:/Users/ptyRa/OneDrive/Desktop/Pac Files/0002.en/fftpack/text/{filename}";
            return File.Exists(path) ? path : null;
        }

        // Additional edge cases (session 33 batch 7).

        [Fact]
        public void DecodeBytes_EmptyArray_ReturnsEmptyList()
        {
            var lines = MesDecoder.DecodeBytes(System.Array.Empty<byte>(), out var raw);
            Assert.Empty(lines);
            Assert.Equal("", raw);
        }

        [Fact]
        public void DecodeBytes_AllNullBytes_ReturnsEmptyList()
        {
            // Bytes 0x80-0xEE that don't match any control codes should just be skipped
            // and produce no lines.
            var lines = MesDecoder.DecodeBytes(new byte[] { 0xC0, 0xC1, 0xC2 }, out _);
            Assert.Empty(lines);
        }

        [Fact]
        public void DecodeBytes_JustText_NoSpeaker_ReturnsOneLine()
        {
            // "Hello" = H(0x11) e(0x28) l(0x2F) l(0x2F) o(0x32).
            var lines = MesDecoder.DecodeBytes(new byte[] { 0x11, 0x28, 0x2F, 0x2F, 0x32 }, out var raw);
            Assert.Single(lines);
            Assert.Null(lines[0].Speaker);
            Assert.Equal("Hello", lines[0].Text);
            Assert.Equal("Hello", raw);
        }

        [Theory]
        [InlineData(0x00, '0')]
        [InlineData(0x09, '9')]
        [InlineData(0x0A, 'A')]
        [InlineData(0x23, 'Z')]
        [InlineData(0x24, 'a')]
        [InlineData(0x3D, 'z')]
        public void DecodeByte_AllRanges(byte b, char expected)
        {
            Assert.Equal(expected, MesDecoder.DecodeByte(b));
        }

        [Theory]
        [InlineData(0x78, '.')]
        [InlineData(0x5F, '.')]
        [InlineData(0x3E, '!')]
        [InlineData(0x40, '?')]
        [InlineData(0x7B, '?')]
        [InlineData(0x46, ':')]
        [InlineData(0x45, ';')]
        [InlineData(0x47, '"')]
        [InlineData(0x44, '-')]
        [InlineData(0x48, '(')]
        [InlineData(0x49, ')')]
        [InlineData(0xB7, '/')]
        [InlineData(0x8B, ',')]
        [InlineData(0x7C, '\n')]
        public void DecodeByte_PunctuationAll(byte b, char expected)
        {
            Assert.Equal(expected, MesDecoder.DecodeByte(b));
        }

        [Theory]
        [InlineData(0x88)]
        [InlineData(0xFA)]
        public void DecodeByte_SpaceVariants_ReturnSpace(byte b)
        {
            Assert.Equal(' ', MesDecoder.DecodeByte(b));
        }

        [Theory]
        [InlineData(0x3F)]
        [InlineData(0x7D)]
        public void DecodeByte_ApostropheVariants_ReturnApostrophe(byte b)
        {
            Assert.Equal('\'', MesDecoder.DecodeByte(b));
        }

        [Theory]
        [InlineData(0xFF)]
        [InlineData(0xA0)]
        [InlineData(0xD0)]
        public void DecodeByte_UnmappedByte_ReturnsNull(byte b)
        {
            Assert.Null(MesDecoder.DecodeByte(b));
        }

        [Fact]
        public void DecodeBytes_F8_FlushesLine()
        {
            // F8 = new dialogue entry marker. Two text fragments separated by F8
            // should produce two distinct lines.
            // "Hi" + F8 + "Bye" = H(0x11) i(0x2C) F8 B(0x0B) y(0x38) e(0x28)
            var lines = MesDecoder.DecodeBytes(
                new byte[] { 0x11, 0x2C, 0xF8, 0x0B, 0x3C, 0x28 }, out _);
            Assert.Equal(2, lines.Count);
            Assert.Equal("Hi", lines[0].Text);
            Assert.Equal("Bye", lines[1].Text);
        }

        [Fact]
        public void DecodeBytes_FE_FlushesLine()
        {
            // FE = line break within same entry. Same flush behavior.
            var lines = MesDecoder.DecodeBytes(
                new byte[] { 0x11, 0x2C, 0xFE, 0x0B, 0x3C, 0x28 }, out _);
            Assert.Equal(2, lines.Count);
        }
    }
}
