using Xunit;
using FFTColorCustomizer.Services;
using System;

namespace FFTColorCustomizer.Tests.Services
{
    public class RamzaBinToNxdBridgeTests
    {
        private readonly RamzaBinToNxdBridge _bridge;

        public RamzaBinToNxdBridgeTests()
        {
            _bridge = new RamzaBinToNxdBridge();
        }

        [Fact]
        public void ConvertBinPaletteToClutData_WithValidPalette_ReturnsCorrectLength()
        {
            // Arrange - 32 bytes for 16 colors in BGR555 format
            var paletteBytes = new byte[32];

            // Act
            var clutData = _bridge.ConvertBinPaletteToClutData(paletteBytes);

            // Assert
            Assert.Equal(48, clutData.Length); // 16 colors × 3 RGB values
        }

        [Fact]
        public void ConvertBinPaletteToClutData_WithBlackColor_ReturnsZeros()
        {
            // Arrange - all zeros = black
            var paletteBytes = new byte[32];

            // Act
            var clutData = _bridge.ConvertBinPaletteToClutData(paletteBytes);

            // Assert - first color should be black (0, 0, 0)
            Assert.Equal(0, clutData[0]); // R
            Assert.Equal(0, clutData[1]); // G
            Assert.Equal(0, clutData[2]); // B
        }

        [Fact]
        public void ConvertBinPaletteToClutData_WithWhiteColor_ReturnsMaxValues()
        {
            // Arrange - BGR555 white = 0xFF7F (all bits set except MSB)
            var paletteBytes = new byte[32];
            paletteBytes[0] = 0xFF;
            paletteBytes[1] = 0x7F;

            // Act
            var clutData = _bridge.ConvertBinPaletteToClutData(paletteBytes);

            // Assert - first color should be white (255, 255, 255)
            Assert.Equal(255, clutData[0]); // R
            Assert.Equal(255, clutData[1]); // G
            Assert.Equal(255, clutData[2]); // B
        }

        [Fact]
        public void ConvertBinPaletteToClutData_WithRedColor_ReturnsCorrectRgb()
        {
            // Arrange - BGR555 red = R=31, G=0, B=0 = 0x001F
            var paletteBytes = new byte[32];
            paletteBytes[0] = 0x1F;
            paletteBytes[1] = 0x00;

            // Act
            var clutData = _bridge.ConvertBinPaletteToClutData(paletteBytes);

            // Assert - first color should be red (255, 0, 0)
            Assert.Equal(255, clutData[0]); // R
            Assert.Equal(0, clutData[1]);   // G
            Assert.Equal(0, clutData[2]);   // B
        }

        [Fact]
        public void ConvertBinPaletteToClutData_WithGreenColor_ReturnsCorrectRgb()
        {
            // Arrange - BGR555 green = R=0, G=31, B=0 = 0x03E0
            var paletteBytes = new byte[32];
            paletteBytes[0] = 0xE0;
            paletteBytes[1] = 0x03;

            // Act
            var clutData = _bridge.ConvertBinPaletteToClutData(paletteBytes);

            // Assert - first color should be green (0, 255, 0)
            Assert.Equal(0, clutData[0]);   // R
            Assert.Equal(255, clutData[1]); // G
            Assert.Equal(0, clutData[2]);   // B
        }

        [Fact]
        public void ConvertBinPaletteToClutData_WithBlueColor_ReturnsCorrectRgb()
        {
            // Arrange - BGR555 blue = R=0, G=0, B=31 = 0x7C00
            var paletteBytes = new byte[32];
            paletteBytes[0] = 0x00;
            paletteBytes[1] = 0x7C;

            // Act
            var clutData = _bridge.ConvertBinPaletteToClutData(paletteBytes);

            // Assert - first color should be blue (0, 0, 255)
            Assert.Equal(0, clutData[0]);   // R
            Assert.Equal(0, clutData[1]);   // G
            Assert.Equal(255, clutData[2]); // B
        }

        [Fact]
        public void ConvertClutDataToBinPalette_WithValidClutData_ReturnsCorrectLength()
        {
            // Arrange
            var clutData = new int[48];

            // Act
            var paletteBytes = _bridge.ConvertClutDataToBinPalette(clutData);

            // Assert
            Assert.Equal(32, paletteBytes.Length); // 16 colors × 2 bytes
        }

        [Fact]
        public void RoundTrip_BinToClutToBin_PreservesColors()
        {
            // Arrange - create a palette with some colors
            var originalBytes = new byte[32];
            // Color 0: Red
            originalBytes[0] = 0x1F;
            originalBytes[1] = 0x00;
            // Color 1: Green
            originalBytes[2] = 0xE0;
            originalBytes[3] = 0x03;
            // Color 2: Blue
            originalBytes[4] = 0x00;
            originalBytes[5] = 0x7C;

            // Act
            var clutData = _bridge.ConvertBinPaletteToClutData(originalBytes);
            var roundTripBytes = _bridge.ConvertClutDataToBinPalette(clutData);

            // Assert - colors should match
            Assert.Equal(originalBytes[0], roundTripBytes[0]);
            Assert.Equal(originalBytes[1], roundTripBytes[1]);
            Assert.Equal(originalBytes[2], roundTripBytes[2]);
            Assert.Equal(originalBytes[3], roundTripBytes[3]);
            Assert.Equal(originalBytes[4], roundTripBytes[4]);
            Assert.Equal(originalBytes[5], roundTripBytes[5]);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(23, 2)]
        [InlineData(4, 3)]
        public void GetNxdKeyForChapter_ReturnsCorrectKey(int chapter, int expectedKey)
        {
            // Act
            var key = _bridge.GetNxdKeyForChapter(chapter);

            // Assert
            Assert.Equal(expectedKey, key);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 4)]
        public void GetChapterFromNxdKey_ReturnsCorrectChapter(int nxdKey, int expectedChapter)
        {
            // Act
            var chapter = _bridge.GetChapterFromNxdKey(nxdKey);

            // Assert
            Assert.Equal(expectedChapter, chapter);
        }

        [Theory]
        [InlineData(1, "battle_ramuza_spr.bin")]
        [InlineData(2, "battle_ramuza2_spr.bin")]
        [InlineData(23, "battle_ramuza2_spr.bin")]
        [InlineData(4, "battle_ramuza3_spr.bin")]
        public void GetSpriteFilenameForChapter_ReturnsCorrectFilename(int chapter, string expectedFilename)
        {
            // Act
            var filename = _bridge.GetSpriteFilenameForChapter(chapter);

            // Assert
            Assert.Equal(expectedFilename, filename);
        }

        [Theory]
        [InlineData(1, "RamzaCh1")]
        [InlineData(2, "RamzaCh23")]
        [InlineData(23, "RamzaCh23")]
        [InlineData(4, "RamzaCh4")]
        public void GetJobNameForChapter_ReturnsCorrectJobName(int chapter, string expectedJobName)
        {
            // Act
            var jobName = _bridge.GetJobNameForChapter(chapter);

            // Assert
            Assert.Equal(expectedJobName, jobName);
        }

        [Fact]
        public void GetNxdKeyForChapter_WithInvalidChapter_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _bridge.GetNxdKeyForChapter(5));
        }

        [Fact]
        public void ConvertBinPaletteToClutData_WithNullInput_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _bridge.ConvertBinPaletteToClutData(null));
        }

        [Fact]
        public void ConvertBinPaletteToClutData_WithTooSmallInput_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _bridge.ConvertBinPaletteToClutData(new byte[10]));
        }

        [Fact]
        public void ConvertClutDataToBinPalette_WithNullInput_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _bridge.ConvertClutDataToBinPalette(null));
        }

        [Fact]
        public void ConvertClutDataToBinPalette_WithTooSmallInput_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _bridge.ConvertClutDataToBinPalette(new int[10]));
        }
    }
}
