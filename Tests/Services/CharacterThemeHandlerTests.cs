using System;
using System.Collections.Generic;
using System.IO;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using Moq;
using Xunit;

namespace FFTColorCustomizer.Tests.Services
{
    /// <summary>
    /// Tests for ICharacterThemeHandler implementations and CharacterThemeHandlerRegistry.
    /// These tests verify the Strategy pattern for character theme handling.
    /// </summary>
    public class CharacterThemeHandlerTests : IDisposable
    {
        private readonly string _testPath;
        private readonly string _sourcePath;
        private readonly string _modPath;
        private readonly Mock<IThemeService> _mockThemeService;
        private readonly StoryCharacterThemeManager _storyCharacterManager;

        public CharacterThemeHandlerTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"CharacterThemeHandlerTest_{Guid.NewGuid()}");
            _sourcePath = Path.Combine(_testPath, "source");
            _modPath = Path.Combine(_testPath, "mod");

            Directory.CreateDirectory(_sourcePath);
            Directory.CreateDirectory(_modPath);

            // Create test data directory structure
            var dataPath = Path.Combine(_modPath, "Data");
            Directory.CreateDirectory(dataPath);
            CreateStoryCharactersJson(dataPath);

            // Create test sprite directories
            var unitsPath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitsPath);
            Directory.CreateDirectory(Path.Combine(unitsPath, "sprites_original"));

            _mockThemeService = new Mock<IThemeService>();
            _storyCharacterManager = new StoryCharacterThemeManager(_modPath);
        }

        private void CreateStoryCharactersJson(string dataPath)
        {
            var json = @"{
                ""characters"": [
                    { ""name"": ""RamzaChapter1"", ""defaultTheme"": ""original"", ""availableThemes"": [""original"", ""dark_knight""] },
                    { ""name"": ""RamzaChapter23"", ""defaultTheme"": ""original"", ""availableThemes"": [""original"", ""dark_knight""] },
                    { ""name"": ""RamzaChapter4"", ""defaultTheme"": ""original"", ""availableThemes"": [""original"", ""dark_knight""] },
                    { ""name"": ""Orlandeau"", ""defaultTheme"": ""original"", ""availableThemes"": [""original"", ""dark""] },
                    { ""name"": ""Agrias"", ""defaultTheme"": ""original"", ""availableThemes"": [""original""] },
                    { ""name"": ""Cloud"", ""defaultTheme"": ""original"", ""availableThemes"": [""original""] },
                    { ""name"": ""Mustadio"", ""defaultTheme"": ""original"", ""availableThemes"": [""original""] },
                    { ""name"": ""Marach"", ""defaultTheme"": ""original"", ""availableThemes"": [""original""] },
                    { ""name"": ""Beowulf"", ""defaultTheme"": ""original"", ""availableThemes"": [""original""] },
                    { ""name"": ""Meliadoul"", ""defaultTheme"": ""original"", ""availableThemes"": [""original""] },
                    { ""name"": ""Rapha"", ""defaultTheme"": ""original"", ""availableThemes"": [""original""] },
                    { ""name"": ""Reis"", ""defaultTheme"": ""original"", ""availableThemes"": [""original""] }
                ]
            }";
            File.WriteAllText(Path.Combine(dataPath, "StoryCharacters.json"), json);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                try { Directory.Delete(_testPath, true); } catch { }
            }
        }

        #region ICharacterThemeHandler Interface Tests

        [Fact]
        public void ICharacterThemeHandler_CharacterName_ShouldReturnCorrectName()
        {
            // Arrange
            var handler = new StandardCharacterThemeHandler(
                "Orlandeau", _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Assert
            handler.CharacterName.Should().Be("Orlandeau");
        }

        [Fact]
        public void StandardCharacterThemeHandler_GetCurrentTheme_ShouldReturnOriginalByDefault()
        {
            // Arrange
            var handler = new StandardCharacterThemeHandler(
                "Orlandeau", _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var theme = handler.GetCurrentTheme();

            // Assert
            theme.Should().Be("original");
        }

        [Fact]
        public void StandardCharacterThemeHandler_CycleTheme_ShouldCallThemeService()
        {
            // Arrange
            _mockThemeService.Setup(s => s.CycleTheme("Orlandeau")).Returns("dark");
            var handler = new StandardCharacterThemeHandler(
                "Orlandeau", _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var newTheme = handler.CycleTheme();

            // Assert
            newTheme.Should().Be("dark");
            _mockThemeService.Verify(s => s.CycleTheme("Orlandeau"), Times.Once);
        }

        [Fact]
        public void StandardCharacterThemeHandler_ApplyTheme_ShouldCallThemeService()
        {
            // Arrange
            var handler = new StandardCharacterThemeHandler(
                "Orlandeau", _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            handler.ApplyTheme("dark");

            // Assert
            _mockThemeService.Verify(s => s.ApplyTheme("Orlandeau", "dark"), Times.Once);
        }

        [Fact]
        public void StandardCharacterThemeHandler_GetAvailableThemes_ShouldReturnFromService()
        {
            // Arrange
            var themes = new[] { "original", "dark", "light" };
            _mockThemeService.Setup(s => s.GetAvailableThemes("Orlandeau")).Returns(themes);
            var handler = new StandardCharacterThemeHandler(
                "Orlandeau", _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var result = handler.GetAvailableThemes();

            // Assert
            result.Should().BeEquivalentTo(themes);
        }

        #endregion

        #region RamzaCharacterThemeHandler Tests

        [Fact]
        public void RamzaCharacterThemeHandler_CharacterName_ShouldBeRamza()
        {
            // Arrange
            var handler = new RamzaCharacterThemeHandler(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Assert
            handler.CharacterName.Should().Be("Ramza");
        }

        [Fact]
        public void RamzaCharacterThemeHandler_GetChapterNames_ShouldReturnAllChapters()
        {
            // Arrange
            var handler = new RamzaCharacterThemeHandler(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var chapters = handler.GetChapterNames();

            // Assert
            chapters.Should().HaveCount(3);
            chapters.Should().Contain("RamzaChapter1");
            chapters.Should().Contain("RamzaChapter23");
            chapters.Should().Contain("RamzaChapter4");
        }

        [Fact]
        public void RamzaCharacterThemeHandler_ImplementsIMultiChapterCharacterHandler()
        {
            // Arrange
            var handler = new RamzaCharacterThemeHandler(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Assert
            handler.Should().BeAssignableTo<IMultiChapterCharacterHandler>();
        }

        [Fact]
        public void RamzaCharacterThemeHandler_CycleTheme_ShouldCallThemeService()
        {
            // Arrange
            _mockThemeService.Setup(s => s.CycleTheme("Ramza")).Returns("dark_knight");
            var handler = new RamzaCharacterThemeHandler(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var newTheme = handler.CycleTheme();

            // Assert
            newTheme.Should().Be("dark_knight");
            _mockThemeService.Verify(s => s.CycleTheme("Ramza"), Times.Once);
        }

        #endregion

        #region CharacterThemeHandlerRegistry Tests

        [Fact]
        public void Registry_ShouldRegisterAllCharacters()
        {
            // Arrange & Act
            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Assert - All characters should be registered
            registry.HasHandler("Ramza").Should().BeTrue();
            registry.HasHandler("Orlandeau").Should().BeTrue();
            registry.HasHandler("Agrias").Should().BeTrue();
            registry.HasHandler("Cloud").Should().BeTrue();
            registry.HasHandler("Mustadio").Should().BeTrue();
            registry.HasHandler("Marach").Should().BeTrue();
            registry.HasHandler("Beowulf").Should().BeTrue();
            registry.HasHandler("Meliadoul").Should().BeTrue();
            registry.HasHandler("Rapha").Should().BeTrue();
            registry.HasHandler("Reis").Should().BeTrue();
        }

        [Fact]
        public void Registry_GetHandler_ShouldReturnCorrectHandlerType()
        {
            // Arrange
            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act & Assert
            registry.GetHandler("Ramza").Should().BeOfType<RamzaCharacterThemeHandler>();
            registry.GetHandler("Orlandeau").Should().BeOfType<StandardCharacterThemeHandler>();
        }

        [Fact]
        public void Registry_GetRamzaHandler_ShouldReturnMultiChapterHandler()
        {
            // Arrange
            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var handler = registry.GetRamzaHandler();

            // Assert
            handler.Should().NotBeNull();
            handler.Should().BeAssignableTo<IMultiChapterCharacterHandler>();
        }

        [Fact]
        public void Registry_CycleTheme_ShouldDelegateToHandler()
        {
            // Arrange
            _mockThemeService.Setup(s => s.CycleTheme("Orlandeau")).Returns("dark");
            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var newTheme = registry.CycleTheme("Orlandeau");

            // Assert
            newTheme.Should().Be("dark");
            _mockThemeService.Verify(s => s.CycleTheme("Orlandeau"), Times.Once);
        }

        [Fact]
        public void Registry_ApplyTheme_ShouldDelegateToHandler()
        {
            // Arrange
            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            registry.ApplyTheme("Agrias", "dark");

            // Assert
            _mockThemeService.Verify(s => s.ApplyTheme("Agrias", "dark"), Times.Once);
        }

        [Fact]
        public void Registry_GetHandler_ForUnknownCharacter_ShouldReturnNull()
        {
            // Arrange
            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var handler = registry.GetHandler("UnknownCharacter");

            // Assert
            handler.Should().BeNull();
        }

        [Fact]
        public void Registry_CycleTheme_ForUnknownCharacter_ShouldReturnOriginal()
        {
            // Arrange
            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var theme = registry.CycleTheme("UnknownCharacter");

            // Assert
            theme.Should().Be("original");
        }

        [Fact]
        public void Registry_GetRegisteredCharacters_ShouldReturnAllNames()
        {
            // Arrange
            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var characters = registry.GetRegisteredCharacters();

            // Assert
            characters.Should().Contain("Ramza");
            characters.Should().Contain("Orlandeau");
            characters.Should().Contain("Agrias");
        }

        [Fact]
        public void Registry_GetStoryCharacterManager_ShouldReturnManager()
        {
            // Arrange
            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            var manager = registry.GetStoryCharacterManager();

            // Assert
            manager.Should().BeSameAs(_storyCharacterManager);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void StandardCharacterHandler_ApplyFromConfiguration_ShouldApplyTheme()
        {
            // Arrange
            var config = new Config();
            config.SetStoryCharacterTheme("Orlandeau", "dark");

            var handler = new StandardCharacterThemeHandler(
                "Orlandeau", _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            handler.ApplyFromConfiguration(config);

            // Assert
            _mockThemeService.Verify(s => s.ApplyTheme("Orlandeau", "dark"), Times.Once);
        }

        [Fact]
        public void Registry_ApplyFromConfiguration_ShouldApplyToAllHandlers()
        {
            // Arrange
            var config = new Config();
            config.SetStoryCharacterTheme("Orlandeau", "dark");
            config.SetStoryCharacterTheme("Agrias", "light");

            var registry = new CharacterThemeHandlerRegistry(
                _sourcePath, _modPath, _mockThemeService.Object, _storyCharacterManager);

            // Act
            registry.ApplyFromConfiguration(config);

            // Assert
            _mockThemeService.Verify(s => s.ApplyTheme("Orlandeau", "dark"), Times.Once);
            _mockThemeService.Verify(s => s.ApplyTheme("Agrias", "light"), Times.Once);
        }

        #endregion
    }
}
