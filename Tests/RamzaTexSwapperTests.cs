using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Tests
{
    public class RamzaTexSwapperTests : IDisposable
    {
        private readonly string _testBasePath;
        private readonly string _testGamePath;

        public RamzaTexSwapperTests()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), "test_themes_" + Guid.NewGuid().ToString());
            _testGamePath = Path.Combine(Path.GetTempPath(), "test_game_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testBasePath);
            Directory.CreateDirectory(_testGamePath);
        }

        [Fact]
        public void SwapTexFilesForTheme_ShouldCopyThemedFilesToGamePath()
        {
            // Arrange
            var swapper = new RamzaTexSwapper(_testBasePath, _testGamePath);

            // Create themed tex files
            string themeDir = Path.Combine(_testBasePath, "white_heretic");
            Directory.CreateDirectory(themeDir);

            byte[] themedData = new byte[131072];
            themedData[0] = 0xAA; // Marker to identify themed file
            File.WriteAllBytes(Path.Combine(themeDir, "tex_830.bin"), themedData);

            // Act
            swapper.SwapTexFilesForTheme("white_heretic");

            // Assert
            string gameFile = Path.Combine(_testGamePath, "tex_830.bin");
            Assert.True(File.Exists(gameFile));

            byte[] swappedData = File.ReadAllBytes(gameFile);
            Assert.Equal(0xAA, swappedData[0]); // Should have themed marker
        }

        [Fact]
        public void RestoreOriginalTexFiles_ShouldCopyOriginalFilesToGamePath()
        {
            // Arrange
            var swapper = new RamzaTexSwapper(_testBasePath, _testGamePath);

            // Create original backup files
            string backupDir = Path.Combine(_testBasePath, "original_backup");
            Directory.CreateDirectory(backupDir);

            byte[] originalData = new byte[131072];
            originalData[0] = 0xFF; // Marker for original
            File.WriteAllBytes(Path.Combine(backupDir, "tex_830.bin"), originalData);

            // Put modified file in game path first
            byte[] modifiedData = new byte[131072];
            modifiedData[0] = 0xAA;
            File.WriteAllBytes(Path.Combine(_testGamePath, "tex_830.bin"), modifiedData);

            // Act
            swapper.RestoreOriginalTexFiles();

            // Assert
            string gameFile = Path.Combine(_testGamePath, "tex_830.bin");
            byte[] restoredData = File.ReadAllBytes(gameFile);
            Assert.Equal(0xFF, restoredData[0]); // Should have original marker
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBasePath))
                Directory.Delete(_testBasePath, true);
            if (Directory.Exists(_testGamePath))
                Directory.Delete(_testGamePath, true);
        }
    }
}