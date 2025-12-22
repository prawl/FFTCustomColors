using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Tests
{
    public class RamzaTexGeneratorTests : IDisposable
    {
        private readonly string _testBasePath;

        public RamzaTexGeneratorTests()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testBasePath);
        }

        [Fact]
        public void GenerateThemedTexFiles_ShouldCreateThemeDirectory()
        {
            // Arrange
            var generator = new RamzaTexGenerator(_testBasePath);
            string originalPath = Path.Combine(_testBasePath, "original_backup");
            Directory.CreateDirectory(originalPath);

            // Create a simple test tex file
            File.WriteAllBytes(Path.Combine(originalPath, "tex_830.bin"), new byte[131072]);

            // Act
            generator.GenerateThemedTexFiles("white_heretic");

            // Assert
            string themeDir = Path.Combine(_testBasePath, "white_heretic");
            Assert.True(Directory.Exists(themeDir));
        }

        [Fact]
        public void GenerateThemedTexFiles_ShouldCreateModifiedTexFile()
        {
            // Arrange
            var generator = new RamzaTexGenerator(_testBasePath);
            string originalPath = Path.Combine(_testBasePath, "original_backup");
            Directory.CreateDirectory(originalPath);

            // Create test tex with brown color
            byte[] testData = new byte[131072];
            ushort brownColor = (ushort)(((72 >> 3) & 0x1F) |
                                        (((64 >> 3) & 0x1F) << 5) |
                                        (((16 >> 3) & 0x1F) << 10));
            testData[0x0E50] = (byte)(brownColor & 0xFF);
            testData[0x0E51] = (byte)((brownColor >> 8) & 0xFF);
            File.WriteAllBytes(Path.Combine(originalPath, "tex_830.bin"), testData);

            // Act
            generator.GenerateThemedTexFiles("white_heretic");

            // Assert
            string themedFile = Path.Combine(_testBasePath, "white_heretic", "tex_830.bin");
            Assert.True(File.Exists(themedFile));

            // Verify color was changed
            byte[] themedData = File.ReadAllBytes(themedFile);
            ushort themedColor = (ushort)(themedData[0x0E50] | (themedData[0x0E51] << 8));
            Assert.NotEqual(brownColor, themedColor);
        }

        [Fact]
        public void GenerateThemedTexFiles_ShouldGenerateAllSixTexFiles()
        {
            // Arrange
            var generator = new RamzaTexGenerator(_testBasePath);
            string originalPath = Path.Combine(_testBasePath, "original_backup");
            Directory.CreateDirectory(originalPath);

            // Create all 6 tex files
            for (int i = 830; i <= 835; i++)
            {
                File.WriteAllBytes(Path.Combine(originalPath, $"tex_{i}.bin"), new byte[131072]);
            }

            // Act
            generator.GenerateThemedTexFiles("white_heretic");

            // Assert
            for (int i = 830; i <= 835; i++)
            {
                string themedFile = Path.Combine(_testBasePath, "white_heretic", $"tex_{i}.bin");
                Assert.True(File.Exists(themedFile), $"tex_{i}.bin should exist");
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBasePath))
            {
                Directory.Delete(_testBasePath, true);
            }
        }
    }
}