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

        [Fact]
        public void GetSqliteTempPath_ReturnsTempPath()
        {
            // Arrange
            var saver = new RamzaThemeSaver();

            // Act
            var result = saver.GetSqliteTempPath();

            // Assert
            Assert.Contains("charclut", result);
            Assert.EndsWith(".sqlite", result);
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

        [Fact]
        public void BuildUpdateSql_ReturnsValidSqlStatement()
        {
            // Arrange
            var saver = new RamzaThemeSaver();
            var clutDataJson = "[255,0,0,0,255,0]";
            int key = 1;
            int key2 = 0;

            // Act
            var sql = saver.BuildUpdateSql(key, key2, clutDataJson);

            // Assert
            Assert.Contains("UPDATE", sql);
            Assert.Contains("CharCLUT", sql);
            Assert.Contains("CLUTData", sql);
            Assert.Contains(clutDataJson, sql);
            Assert.Contains("Key = 1", sql);
            Assert.Contains("Key2 = 0", sql);
        }

        [Fact]
        public void UpdateSqliteDatabase_WithValidData_UpdatesDatabase()
        {
            // Arrange
            var saver = new RamzaThemeSaver();

            // Create a temp copy of the base SQLite database
            var baseSqlitePath = GetBaseSqlitePath();
            if (!File.Exists(baseSqlitePath))
            {
                // Skip test if base SQLite doesn't exist
                return;
            }

            var tempSqlitePath = Path.Combine(Path.GetTempPath(), $"charclut_test_{Guid.NewGuid()}.sqlite");
            File.Copy(baseSqlitePath, tempSqlitePath);

            try
            {
                // Create test palette with red color
                var paletteBytes = new byte[512];
                paletteBytes[0] = 0x1F; // Red in BGR555
                paletteBytes[1] = 0x00;

                var clutData = saver.ConvertPaletteToClutData(paletteBytes);
                var clutDataJson = saver.CreateClutDataJson(clutData);

                // Act
                var result = saver.UpdateSqliteDatabase(tempSqlitePath, 1, clutDataJson);

                // Assert
                Assert.True(result);

                // Verify the update was applied
                var readBack = saver.ReadClutDataFromSqlite(tempSqlitePath, 1, 0);
                Assert.NotNull(readBack);
                Assert.Contains("255", readBack); // Red value should be in the JSON
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempSqlitePath))
                    File.Delete(tempSqlitePath);
            }
        }

        [Fact]
        public void ReadClutDataFromSqlite_WithValidKey_ReturnsClutData()
        {
            // Arrange
            var saver = new RamzaThemeSaver();
            var baseSqlitePath = GetBaseSqlitePath();

            if (!File.Exists(baseSqlitePath))
            {
                // Skip test if base SQLite doesn't exist
                return;
            }

            // Act - read Chapter 1 data (Key=1, Key2=0)
            var clutDataJson = saver.ReadClutDataFromSqlite(baseSqlitePath, 1, 0);

            // Assert
            Assert.NotNull(clutDataJson);
            Assert.StartsWith("[", clutDataJson);
            Assert.EndsWith("]", clutDataJson);
        }

        [Fact]
        public void GetBaseSqlitePath_ReturnsValidPath()
        {
            // Arrange
            var saver = new RamzaThemeSaver();

            // Act
            var path = saver.GetBaseSqlitePath();

            // Assert
            Assert.EndsWith("charclut.sqlite", path);
        }

        private string GetBaseSqlitePath()
        {
            // Find the ColorMod/Data/nxd directory relative to test execution
            var baseDir = Directory.GetCurrentDirectory();
            var sqlitePath = Path.Combine(baseDir, "..", "..", "..", "..", "ColorMod", "Data", "nxd", "charclut.sqlite");
            return Path.GetFullPath(sqlitePath);
        }
    }
}
