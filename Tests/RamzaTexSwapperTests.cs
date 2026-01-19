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

        /// <summary>
        /// Tests that SwapTexFilesPerChapter only copies tex files for the themed chapter.
        /// Ch1 = dark_knight, Ch23 = original, Ch4 = original
        /// Only Ch1 tex files (830, 831) should be copied.
        /// </summary>
        [Fact]
        public void SwapTexFilesPerChapter_OnlyCopiesCh1Files_WhenOnlyCh1IsThemed()
        {
            // Arrange
            var swapper = new RamzaTexSwapper(_testBasePath, _testGamePath);

            // Create a theme with ALL tex files
            string themeDir = Path.Combine(_testBasePath, "RamzaThemes", "dark_knight");
            Directory.CreateDirectory(themeDir);

            byte[] ch1Data = new byte[1024]; ch1Data[0] = 0x01;
            byte[] ch23Data = new byte[1024]; ch23Data[0] = 0x23;
            byte[] ch4Data = new byte[1024]; ch4Data[0] = 0x04;

            File.WriteAllBytes(Path.Combine(themeDir, "tex_830.bin"), ch1Data);
            File.WriteAllBytes(Path.Combine(themeDir, "tex_831.bin"), ch1Data);
            File.WriteAllBytes(Path.Combine(themeDir, "tex_832.bin"), ch23Data);
            File.WriteAllBytes(Path.Combine(themeDir, "tex_833.bin"), ch23Data);
            File.WriteAllBytes(Path.Combine(themeDir, "tex_834.bin"), ch4Data);
            File.WriteAllBytes(Path.Combine(themeDir, "tex_835.bin"), ch4Data);

            // Act - Use the NEW per-chapter method
            swapper.SwapTexFilesPerChapter("dark_knight", "original", "original");

            // Assert - Only Ch1 files should be copied
            Assert.True(File.Exists(Path.Combine(_testGamePath, "tex_830.bin")), "Ch1 tex_830 should exist");
            Assert.True(File.Exists(Path.Combine(_testGamePath, "tex_831.bin")), "Ch1 tex_831 should exist");

            // Ch23 and Ch4 files should NOT exist (original = no tex files)
            Assert.False(File.Exists(Path.Combine(_testGamePath, "tex_832.bin")), "Ch23 tex_832 should NOT exist");
            Assert.False(File.Exists(Path.Combine(_testGamePath, "tex_833.bin")), "Ch23 tex_833 should NOT exist");
            Assert.False(File.Exists(Path.Combine(_testGamePath, "tex_834.bin")), "Ch4 tex_834 should NOT exist");
            Assert.False(File.Exists(Path.Combine(_testGamePath, "tex_835.bin")), "Ch4 tex_835 should NOT exist");
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