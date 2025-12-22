using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Tests
{
    public class RamzaThemeCoordinatorTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testSourcePath;

        public RamzaThemeCoordinatorTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), "test_mod_" + Guid.NewGuid().ToString());
            _testSourcePath = Path.Combine(Path.GetTempPath(), "test_source_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testModPath);
            Directory.CreateDirectory(_testSourcePath);
        }

        [Fact]
        public void GenerateAllRamzaThemes_ShouldCreateThemeDirectories()
        {
            // Arrange
            var coordinator = new RamzaThemeCoordinator(_testSourcePath, _testModPath);

            // Create original backup files
            string backupPath = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d", "original_backup");
            Directory.CreateDirectory(backupPath);
            File.WriteAllBytes(Path.Combine(backupPath, "tex_830.bin"), new byte[131072]);

            // Act
            coordinator.GenerateAllRamzaThemes(new[] { "white_heretic" });

            // Assert
            string themeDir = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d", "white_heretic");
            Assert.True(Directory.Exists(themeDir));
        }

        [Fact]
        public void GenerateAllRamzaThemes_ShouldCreateModifiedTexFiles()
        {
            // Arrange
            var coordinator = new RamzaThemeCoordinator(_testSourcePath, _testModPath);

            // Create original backup with brown color
            string backupPath = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d", "original_backup");
            Directory.CreateDirectory(backupPath);

            byte[] originalData = new byte[131072];
            ushort brownColor = (ushort)(((72 >> 3) & 0x1F) |
                                        (((64 >> 3) & 0x1F) << 5) |
                                        (((16 >> 3) & 0x1F) << 10));
            originalData[0x0E50] = (byte)(brownColor & 0xFF);
            originalData[0x0E51] = (byte)((brownColor >> 8) & 0xFF);
            File.WriteAllBytes(Path.Combine(backupPath, "tex_830.bin"), originalData);

            // Act
            coordinator.GenerateAllRamzaThemes(new[] { "white_heretic" });

            // Assert
            string themedFile = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d", "white_heretic", "tex_830.bin");
            Assert.True(File.Exists(themedFile));

            // Verify the color was changed
            byte[] themedData = File.ReadAllBytes(themedFile);
            ushort themedColor = (ushort)(themedData[0x0E50] | (themedData[0x0E51] << 8));
            Assert.NotEqual(brownColor, themedColor);
        }

        [Fact]
        public void ApplyRamzaTheme_ShouldCopyTexFilesToGamePath()
        {
            // Arrange
            var coordinator = new RamzaThemeCoordinator(_testSourcePath, _testModPath);

            // Create themed tex files
            string themeDir = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d", "white_heretic");
            Directory.CreateDirectory(themeDir);

            byte[] themedData = new byte[131072];
            themedData[0] = 0xAA; // Marker
            File.WriteAllBytes(Path.Combine(themeDir, "tex_830.bin"), themedData);

            // Act
            coordinator.ApplyRamzaTheme("white_heretic");

            // Assert
            string gameFile = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d", "tex_830.bin");
            Assert.True(File.Exists(gameFile));

            byte[] swappedData = File.ReadAllBytes(gameFile);
            Assert.Equal(0xAA, swappedData[0]);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testModPath))
                Directory.Delete(_testModPath, true);
            if (Directory.Exists(_testSourcePath))
                Directory.Delete(_testSourcePath, true);
        }
    }
}