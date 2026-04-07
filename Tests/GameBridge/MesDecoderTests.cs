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
    }
}
