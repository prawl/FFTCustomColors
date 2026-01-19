using Xunit;
using FFTColorCustomizer.Services;
using System;
using System.IO;

namespace FFTColorCustomizer.Tests.Services
{
    public class RamzaThemeSaverTests
    {
        [Theory]
        [InlineData("RamzaCh1", true)]
        [InlineData("RamzaCh23", true)]
        [InlineData("RamzaCh4", true)]
        [InlineData("RamzaChapter1", true)]
        [InlineData("RamzaChapter23", true)]
        [InlineData("RamzaChapter4", true)]
        [InlineData("Squire_Male", false)]
        [InlineData("Knight_Female", false)]
        [InlineData("Agrias", false)]
        [InlineData("Cloud", false)]
        public void IsRamzaChapter_ReturnsCorrectResult(string jobName, bool expected)
        {
            // Arrange
            var saver = new RamzaThemeSaver();

            // Act
            var result = saver.IsRamzaChapter(jobName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("RamzaCh1", 1)]
        [InlineData("RamzaCh23", 2)]
        [InlineData("RamzaCh4", 4)]
        [InlineData("RamzaChapter1", 1)]
        [InlineData("RamzaChapter23", 2)]
        [InlineData("RamzaChapter4", 4)]
        public void GetChapterFromJobName_ReturnsCorrectChapter(string jobName, int expectedChapter)
        {
            // Arrange
            var saver = new RamzaThemeSaver();

            // Act
            var result = saver.GetChapterFromJobName(jobName);

            // Assert
            Assert.Equal(expectedChapter, result);
        }

        [Theory]
        [InlineData("Squire_Male")]
        [InlineData("Knight_Female")]
        [InlineData("Agrias")]
        public void GetChapterFromJobName_WithNonRamza_ThrowsException(string jobName)
        {
            // Arrange
            var saver = new RamzaThemeSaver();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => saver.GetChapterFromJobName(jobName));
        }

        [Fact]
        public void ConvertPaletteToClutData_WithValidPalette_ReturnsClutData()
        {
            // Arrange
            var saver = new RamzaThemeSaver();
            var paletteBytes = new byte[512]; // Full palette size
            // Set first color to red (BGR555)
            paletteBytes[0] = 0x1F;
            paletteBytes[1] = 0x00;

            // Act
            var clutData = saver.ConvertPaletteToClutData(paletteBytes);

            // Assert
            Assert.Equal(48, clutData.Length);
            Assert.Equal(255, clutData[0]); // R
            Assert.Equal(0, clutData[1]);   // G
            Assert.Equal(0, clutData[2]);   // B
        }

        [Fact]
        public void CreateClutDataJson_ReturnsValidJsonArray()
        {
            // Arrange
            var saver = new RamzaThemeSaver();
            var clutData = new int[] { 255, 0, 0, 0, 255, 0, 0, 0, 255,
                                       100, 100, 100, 50, 50, 50, 200, 200, 200,
                                       150, 150, 150, 75, 75, 75, 175, 175, 175,
                                       125, 125, 125, 25, 25, 25, 225, 225, 225,
                                       90, 90, 90, 60, 60, 60, 180, 180, 180,
                                       110, 110, 110 };

            // Act
            var json = saver.CreateClutDataJson(clutData);

            // Assert
            Assert.StartsWith("[", json);
            Assert.EndsWith("]", json);
            Assert.Contains("255", json);
        }

        [Fact]
        public void GetNxdDeploymentPath_ReturnsCorrectPath()
        {
            // Arrange
            var saver = new RamzaThemeSaver();
            var modPath = @"C:\Mods\FFTColorCustomizer";

            // Act
            var result = saver.GetNxdDeploymentPath(modPath);

            // Assert
            Assert.EndsWith("charclut.nxd", result);
            Assert.Contains("FFTIVC", result);
            Assert.Contains("data", result);
            Assert.Contains("enhanced", result);
            Assert.Contains("nxd", result);
        }

        [Theory]
        [InlineData(1, 1, 0)]
        [InlineData(2, 2, 0)]
        [InlineData(4, 3, 0)]
        public void GetNxdKeyAndKey2_ReturnsCorrectValues(int chapter, int expectedKey, int expectedKey2)
        {
            // Arrange
            var saver = new RamzaThemeSaver();

            // Act
            var (key, key2) = saver.GetNxdKeyAndKey2(chapter);

            // Assert
            Assert.Equal(expectedKey, key);
            Assert.Equal(expectedKey2, key2);
        }

        [Theory]
        [InlineData("RamzaCh1", "RamzaChapter1")]
        [InlineData("RamzaCh23", "RamzaChapter23")]
        [InlineData("RamzaCh4", "RamzaChapter4")]
        [InlineData("RamzaChapter1", "RamzaChapter1")]
        [InlineData("Agrias", "Agrias")]
        public void NormalizeJobName_ReturnsCanonicalFormat(string input, string expected)
        {
            // Arrange
            var saver = new RamzaThemeSaver();

            // Act
            var result = saver.NormalizeJobName(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetBaseNxdPath_ReturnsValidPath()
        {
            // Arrange
            var saver = new RamzaThemeSaver();

            // Act
            var path = saver.GetBaseNxdPath();

            // Assert
            Assert.EndsWith("charclut.nxd", path);
        }
    }
}
