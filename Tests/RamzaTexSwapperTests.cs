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

            // Create themed tex files in RamzaThemes subdirectory
            string themeDir = Path.Combine(_testBasePath, "RamzaThemes", "white_heretic");
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
        public void RestoreOriginalTexFiles_ShouldRemoveAllTexFiles()
        {
            // Arrange
            var swapper = new RamzaTexSwapper(_testBasePath, _testGamePath);

            // Put tex files in game path
            File.WriteAllBytes(Path.Combine(_testGamePath, "tex_830.bin"), new byte[131072]);
            File.WriteAllBytes(Path.Combine(_testGamePath, "tex_835.bin"), new byte[118784]);

            // Create some theme subdirectories
            Directory.CreateDirectory(Path.Combine(_testGamePath, "white_heretic"));
            Directory.CreateDirectory(Path.Combine(_testGamePath, "active_theme"));

            // Act
            swapper.RestoreOriginalTexFiles();

            // Assert - tex files should be removed to let game use built-in textures
            Assert.False(File.Exists(Path.Combine(_testGamePath, "tex_830.bin")));
            Assert.False(File.Exists(Path.Combine(_testGamePath, "tex_835.bin")));
            Assert.False(Directory.Exists(Path.Combine(_testGamePath, "white_heretic")));
            Assert.False(Directory.Exists(Path.Combine(_testGamePath, "active_theme")));
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