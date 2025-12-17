using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Services;
using Xunit;

namespace FFTColorCustomizer.Tests.Core
{
    public class PathResolverTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _testSourcePath;
        private readonly string _testUserPath;
        private readonly CharacterDefinitionService _mockCharacterService;

        public PathResolverTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), $"FFTColorCustomizerTest_{Guid.NewGuid()}");
            _testSourcePath = Path.Combine(_testRoot, "source");
            _testUserPath = Path.Combine(_testRoot, "user");

            Directory.CreateDirectory(_testRoot);
            Directory.CreateDirectory(_testSourcePath);
            Directory.CreateDirectory(_testUserPath);

            // Create a mock character service with test data
            _mockCharacterService = new CharacterDefinitionService();
            _mockCharacterService.AddCharacter(new CharacterDefinition
            {
                Name = "Agrias",
                SpriteNames = new string[] { "aguri", "kanba" },
                DefaultTheme = "original"
            });
        }

        public void Dispose()
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }

        [Fact]
        public void Constructor_ShouldInitializePathsCorrectly()
        {
            // Act
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);

            // Assert
            Assert.Equal(_testRoot, resolver.ModRootPath);
            Assert.Equal(_testSourcePath, resolver.SourcePath);
            Assert.Equal(_testUserPath, resolver.UserConfigPath);
        }

        [Fact]
        public void GetDataPath_ShouldResolveRelativeToDataDirectory()
        {
            // Arrange
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);

            // Act
            var dataPath = resolver.GetDataPath("StoryCharacters.json");

            // Assert
            Assert.Equal(Path.Combine(_testRoot, "Data", "StoryCharacters.json"), dataPath);
        }

        [Fact]
        public void GetConfigPath_ShouldReturnUserConfigPath()
        {
            // Arrange
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);

            // Act
            var configPath = resolver.GetConfigPath();

            // Assert
            Assert.Equal(Path.Combine(_testUserPath, "Config.json"), configPath);
        }

        [Fact]
        public void GetSpritePath_ShouldConstructCorrectPath()
        {
            // Arrange
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);

            // Act
            var spritePath = resolver.GetSpritePath("Agrias", "lucavi", "battle_aguri_0.bmp");

            // Assert
            var expected = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced",
                "fftpack", "unit", "lucavi", "battle_aguri_0.bmp");
            Assert.Equal(expected, spritePath);
        }

        [Fact]
        public void GetThemeDirectory_ShouldReturnThemeFolder()
        {
            // Arrange
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);

            // Act
            var themeDir = resolver.GetThemeDirectory("Orlandeau", "corpse_brigade");

            // Assert
            var expected = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced",
                "fftpack", "unit", "corpse_brigade");
            Assert.Equal(expected, themeDir);
        }

        [Fact]
        public void GetPreviewImagePath_ShouldReturnCorrectPreviewPath()
        {
            // Arrange
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);

            // Act
            var previewPath = resolver.GetPreviewImagePath("Cloud", "vampyre");

            // Assert
            var expected = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced",
                "fftpack", "unit", "vampyre", "preview_cloud_vampyre.png");
            Assert.Equal(expected, previewPath);
        }

        [Fact]
        public void ResolveFirstExistingPath_ShouldReturnFirstExistingFile()
        {
            // Arrange
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);
            var testFile = Path.Combine(_testRoot, "test.txt");
            File.WriteAllText(testFile, "test");

            var candidates = new[]
            {
                Path.Combine(_testRoot, "nonexistent1.txt"),
                Path.Combine(_testRoot, "nonexistent2.txt"),
                testFile,
                Path.Combine(_testRoot, "nonexistent3.txt")
            };

            // Act
            var result = resolver.ResolveFirstExistingPath(candidates);

            // Assert
            Assert.Equal(testFile, result);
        }

        [Fact]
        public void ResolveFirstExistingPath_ShouldReturnNullWhenNoFileExists()
        {
            // Arrange
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);
            var candidates = new[]
            {
                Path.Combine(_testRoot, "nonexistent1.txt"),
                Path.Combine(_testRoot, "nonexistent2.txt")
            };

            // Act
            var result = resolver.ResolveFirstExistingPath(candidates);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetAvailableThemes_ShouldReturnAllThemeDirectories()
        {
            // Arrange
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);
            var unitPath = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);

            // Create theme directories with character sprites
            var themes = new[] { "original", "lucavi", "corpse_brigade", "vampyre" };
            foreach (var theme in themes)
            {
                var themePath = Path.Combine(unitPath, theme);
                Directory.CreateDirectory(themePath);
                // Create a dummy sprite file for the character
                File.WriteAllText(Path.Combine(themePath, "battle_aguri_0.bmp"), "");
            }

            // Act
            var availableThemes = resolver.GetAvailableThemes("Agrias").ToList();

            // Assert
            Assert.Equal(4, availableThemes.Count);
            Assert.Contains("original", availableThemes);
            Assert.Contains("lucavi", availableThemes);
            Assert.Contains("corpse_brigade", availableThemes);
            Assert.Contains("vampyre", availableThemes);
        }

        [Fact]
        public void GetAvailableThemes_ShouldReturnEmptyWhenNoThemesExist()
        {
            // Arrange
            var resolver = new PathResolver(_testRoot, _testSourcePath, _testUserPath, _mockCharacterService);

            // Act
            var availableThemes = resolver.GetAvailableThemes("NonexistentCharacter");

            // Assert
            Assert.Empty(availableThemes);
        }

        [Fact]
        public void Constructor_ShouldThrowWhenModRootPathIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new PathResolver(null!, _testSourcePath, _testUserPath, _mockCharacterService));
        }

        [Fact]
        public void Constructor_ShouldThrowWhenSourcePathIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new PathResolver(_testRoot, null!, _testUserPath, _mockCharacterService));
        }
    }
}
