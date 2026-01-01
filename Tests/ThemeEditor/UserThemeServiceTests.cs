using System;
using System.IO;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    public class UserThemeServiceTests
    {
        [Fact]
        public void SaveTheme_CreatesThemeDirectory()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                var paletteData = new byte[512]; // Minimal palette data

                // Act
                service.SaveTheme("Knight_Male", "My Custom Theme", paletteData);

                // Assert - Theme directory should be created
                var expectedDir = Path.Combine(tempDir, "UserThemes", "Knight_Male", "My Custom Theme");
                Assert.True(Directory.Exists(expectedDir), $"Theme directory should exist at {expectedDir}");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SaveTheme_WritesPaletteDataToFile()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                var paletteData = new byte[512];
                paletteData[0] = 0xAB;
                paletteData[511] = 0xCD;

                // Act
                service.SaveTheme("Knight_Male", "My Custom Theme", paletteData);

                // Assert - Palette file should exist with correct data
                var expectedFile = Path.Combine(tempDir, "UserThemes", "Knight_Male", "My Custom Theme", "palette.bin");
                Assert.True(File.Exists(expectedFile), $"Palette file should exist at {expectedFile}");

                var savedData = File.ReadAllBytes(expectedFile);
                Assert.Equal(512, savedData.Length);
                Assert.Equal(0xAB, savedData[0]);
                Assert.Equal(0xCD, savedData[511]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SaveTheme_UpdatesUserThemesRegistry()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                var paletteData = new byte[512];

                // Act
                service.SaveTheme("Knight_Male", "My Custom Theme", paletteData);

                // Assert - UserThemes.json should exist and contain the theme entry
                var registryPath = Path.Combine(tempDir, "UserThemes.json");
                Assert.True(File.Exists(registryPath), $"UserThemes.json should exist at {registryPath}");

                var json = File.ReadAllText(registryPath);
                Assert.Contains("Knight_Male", json);
                Assert.Contains("My Custom Theme", json);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetUserThemes_ReturnsEmptyList_WhenNoThemesSaved()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);

                // Act
                var themes = service.GetUserThemes("Knight_Male");

                // Assert
                Assert.Empty(themes);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetUserThemes_ReturnsSavedThemes()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                service.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]);
                service.SaveTheme("Knight_Male", "Forest Green", new byte[512]);

                // Act
                var themes = service.GetUserThemes("Knight_Male");

                // Assert
                Assert.Equal(2, themes.Count);
                Assert.Contains("Ocean Blue", themes);
                Assert.Contains("Forest Green", themes);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void DeleteTheme_RemovesThemeDirectoryAndRegistry()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                service.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]);
                var themeDir = Path.Combine(tempDir, "UserThemes", "Knight_Male", "Ocean Blue");
                Assert.True(Directory.Exists(themeDir));

                // Act
                service.DeleteTheme("Knight_Male", "Ocean Blue");

                // Assert - Directory should be removed
                Assert.False(Directory.Exists(themeDir));

                // Assert - Registry should no longer contain the theme
                var themes = service.GetUserThemes("Knight_Male");
                Assert.DoesNotContain("Ocean Blue", themes);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void LoadTheme_ReturnsSavedPaletteData()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                var paletteData = new byte[512];
                paletteData[0] = 0xAB;
                paletteData[255] = 0xCD;
                paletteData[511] = 0xEF;
                service.SaveTheme("Knight_Male", "Ocean Blue", paletteData);

                // Act
                var loadedData = service.LoadTheme("Knight_Male", "Ocean Blue");

                // Assert
                Assert.NotNull(loadedData);
                Assert.Equal(512, loadedData.Length);
                Assert.Equal(0xAB, loadedData[0]);
                Assert.Equal(0xCD, loadedData[255]);
                Assert.Equal(0xEF, loadedData[511]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SaveTheme_ThrowsException_WhenThemeNameAlreadyExists()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                service.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]);

                // Act & Assert
                var ex = Assert.Throws<InvalidOperationException>(() =>
                    service.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]));
                Assert.Contains("already exists", ex.Message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SaveTheme_ThrowsArgumentException_WhenThemeNameIsEmpty()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() =>
                    service.SaveTheme("Knight_Male", "", new byte[512]));
                Assert.Contains("Theme name cannot be empty", ex.Message);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SaveTheme_ThrowsArgumentException_WhenThemeNameIsReserved()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);

                // Act & Assert - "Original" is a reserved name
                var ex = Assert.Throws<ArgumentException>(() =>
                    service.SaveTheme("Knight_Male", "Original", new byte[512]));
                Assert.Contains("reserved", ex.Message.ToLower());
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SaveTheme_ThrowsArgumentException_WhenThemeNameContainsInvalidCharacters()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);

                // Act & Assert - Invalid path characters should be rejected
                var ex = Assert.Throws<ArgumentException>(() =>
                    service.SaveTheme("Knight_Male", "My/Theme", new byte[512]));
                Assert.Contains("invalid", ex.Message.ToLower());
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetAllUserThemes_ReturnsThemesGroupedByJob()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                service.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]);
                service.SaveTheme("Knight_Male", "Forest Green", new byte[512]);
                service.SaveTheme("Archer_Female", "Sunset Red", new byte[512]);

                // Act
                var allThemes = service.GetAllUserThemes();

                // Assert
                Assert.Equal(2, allThemes.Count); // 2 jobs
                Assert.True(allThemes.ContainsKey("Knight_Male"));
                Assert.True(allThemes.ContainsKey("Archer_Female"));
                Assert.Equal(2, allThemes["Knight_Male"].Count);
                Assert.Single(allThemes["Archer_Female"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void IsUserTheme_ReturnsTrue_ForSavedUserTheme()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                service.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]);

                // Act & Assert
                Assert.True(service.IsUserTheme("Knight_Male", "Ocean Blue"));
                Assert.False(service.IsUserTheme("Knight_Male", "NonExistent"));
                Assert.False(service.IsUserTheme("Archer_Female", "Ocean Blue"));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetUserThemePalettePath_ReturnsPath_ForExistingTheme()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);
                service.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]);

                // Act
                var path = service.GetUserThemePalettePath("Knight_Male", "Ocean Blue");

                // Assert
                Assert.NotNull(path);
                Assert.True(File.Exists(path));
                Assert.EndsWith("palette.bin", path);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GetUserThemePalettePath_ReturnsNull_ForNonExistentTheme()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "UserThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new UserThemeService(tempDir);

                // Act
                var path = service.GetUserThemePalettePath("Knight_Male", "NonExistent");

                // Assert
                Assert.Null(path);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
