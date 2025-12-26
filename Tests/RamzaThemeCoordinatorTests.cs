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

            // Create themed tex files in RamzaThemes directory
            string themeDir = Path.Combine(_testSourcePath, "RamzaThemes", "white_heretic");
            Directory.CreateDirectory(themeDir);

            byte[] themedData = new byte[131072];
            themedData[0] = 0xAA; // Marker
            File.WriteAllBytes(Path.Combine(themeDir, "tex_830.bin"), themedData);

            // Create game g2d directory
            string gameG2dPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");
            Directory.CreateDirectory(gameG2dPath);

            // Act
            coordinator.ApplyRamzaTheme("white_heretic");

            // Assert
            string gameFile = Path.Combine(gameG2dPath, "tex_830.bin");
            Assert.True(File.Exists(gameFile));

            byte[] swappedData = File.ReadAllBytes(gameFile);
            Assert.Equal(0xAA, swappedData[0]);
        }

        [Fact]
        public void ApplyRamzaTheme_WithOriginalTheme_ShouldNotCopyFiles()
        {
            // This test demonstrates the bug: calling ApplyRamzaTheme("original")
            // tries to access RamzaThemes/original/ which doesn't exist
            // The method logs an error but doesn't throw an exception
            // Bug: It should handle "original" specially by calling RestoreOriginalTexFiles

            // Arrange
            var coordinator = new RamzaThemeCoordinator(_testSourcePath, _testModPath);

            // Create RamzaThemes directory with test themes (but NOT "original")
            string ramzaThemesPath = Path.Combine(_testSourcePath, "RamzaThemes");
            Directory.CreateDirectory(ramzaThemesPath);

            // Create other themes but NOT original
            CreateTestTheme(ramzaThemesPath, "dark_knight");
            CreateTestTheme(ramzaThemesPath, "white_heretic");

            // Create game g2d directory
            string gameG2dPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");
            Directory.CreateDirectory(gameG2dPath);

            // First apply a valid theme to put some files in place
            coordinator.ApplyRamzaTheme("dark_knight");
            string tex830Path = Path.Combine(gameG2dPath, "tex_830.bin");
            Assert.True(File.Exists(tex830Path), "Should have tex file after applying dark_knight");

            // Act - Apply "original" theme - this currently fails silently (logs error but doesn't throw)
            var exception = Record.Exception(() => coordinator.ApplyRamzaTheme("original"));

            // Assert - No exception is thrown
            Assert.Null(exception);

            // After fix: The tex files should be removed for "original" theme
            Assert.False(File.Exists(tex830Path), "Tex files should be removed for original theme");
        }

        [Fact]
        public void ApplyRamzaTheme_WithOriginalTheme_ShouldRemoveTexFiles()
        {
            // This test verifies that "original" theme properly removes tex files
            // to let the game use built-in textures

            // Arrange
            var coordinator = new RamzaThemeCoordinator(_testSourcePath, _testModPath);

            // Create RamzaThemes directory with a valid theme
            string ramzaThemesPath = Path.Combine(_testSourcePath, "RamzaThemes");
            Directory.CreateDirectory(ramzaThemesPath);
            CreateTestTheme(ramzaThemesPath, "dark_knight");

            // Create game g2d directory
            string gameG2dPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");
            Directory.CreateDirectory(gameG2dPath);

            // First apply a custom theme to put tex files in place
            coordinator.ApplyRamzaTheme("dark_knight");

            // Verify tex files exist
            string tex830Path = Path.Combine(gameG2dPath, "tex_830.bin");
            Assert.True(File.Exists(tex830Path), "Tex file should exist after applying dark_knight theme");

            // Act - Apply "original" theme (should now work with the fix)
            var exception = Record.Exception(() => coordinator.ApplyRamzaTheme("original"));

            // Assert - With fix applied:
            // - Should not throw exception
            // - Should remove tex files
            Assert.Null(exception);
            Assert.False(File.Exists(tex830Path), "Tex files should be removed for original theme");
        }

        private void CreateTestTheme(string ramzaThemesPath, string themeName)
        {
            string themePath = Path.Combine(ramzaThemesPath, themeName);
            Directory.CreateDirectory(themePath);

            // Create dummy tex files for chapters 1-3 (830-835)
            for (int i = 830; i <= 835; i++)
            {
                string texFile = Path.Combine(themePath, $"tex_{i}.bin");
                byte[] dummyData = new byte[131072]; // Standard tex file size
                dummyData[0] = 0xAA; // Marker byte
                File.WriteAllBytes(texFile, dummyData);
            }
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