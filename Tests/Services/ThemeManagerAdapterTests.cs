using System;
using System.IO;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using Xunit;
using FluentAssertions;

namespace FFTColorCustomizer.Tests.Services
{
    /// <summary>
    /// Tests to ensure ThemeManagerAdapter maintains backward compatibility
    /// with the original ThemeManager behavior
    /// </summary>
    public class ThemeManagerAdapterTests : IDisposable
    {
        private readonly string _testPath;
        private readonly string _sourcePath;
        private readonly string _modPath;

        public ThemeManagerAdapterTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"ThemeManagerAdapterTest_{Guid.NewGuid()}");
            _sourcePath = Path.Combine(_testPath, "source");
            _modPath = Path.Combine(_testPath, "mod");

            Directory.CreateDirectory(_sourcePath);
            Directory.CreateDirectory(_modPath);

            // Create test sprite directories in the expected location
            var unitsPath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitsPath);
            Directory.CreateDirectory(Path.Combine(unitsPath, "sprites_original"));
            Directory.CreateDirectory(Path.Combine(unitsPath, "sprites_lucavi"));
            Directory.CreateDirectory(Path.Combine(unitsPath, "sprites_corpse_brigade"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        [Fact]
        public void Constructor_ShouldInitializeSuccessfully()
        {
            // Act
            var adapter = new ThemeManagerAdapter(_sourcePath, _modPath);

            // Assert
            adapter.Should().NotBeNull();
        }

        [Fact]
        public void GetStoryCharacterManager_ShouldReturnManager()
        {
            // Arrange
            var adapter = new ThemeManagerAdapter(_sourcePath, _modPath);

            // Act
            var manager = adapter.GetStoryCharacterManager();

            // Assert
            manager.Should().NotBeNull();
            manager.Should().BeOfType<StoryCharacterThemeManager>();
        }

        [Fact]
        public void ApplyInitialThemes_ShouldNotThrow()
        {
            // Arrange
            var adapter = new ThemeManagerAdapter(_sourcePath, _modPath);

            // Act
            Action act = () => adapter.ApplyInitialThemes();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CycleOrlandeauTheme_ShouldNotThrow()
        {
            // Arrange
            var adapter = new ThemeManagerAdapter(_sourcePath, _modPath);

            // Act
            Action act = () => adapter.CycleOrlandeauTheme();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CycleAgriasTheme_ShouldNotThrow()
        {
            // Arrange
            var adapter = new ThemeManagerAdapter(_sourcePath, _modPath);

            // Act
            Action act = () => adapter.CycleAgriasTheme();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CycleCloudTheme_ShouldNotThrow()
        {
            // Arrange
            var adapter = new ThemeManagerAdapter(_sourcePath, _modPath);

            // Act
            Action act = () => adapter.CycleCloudTheme();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void ThemeManager_ShouldInheritFromAdapter()
        {
            // Act
            var manager = new ThemeManager(_sourcePath, _modPath);

            // Assert
            manager.Should().BeAssignableTo<ThemeManagerAdapter>();
            manager.Should().BeOfType<ThemeManager>();
        }

        #region Multi-Sprite Character Tests

        [Theory]
        [InlineData("Agrias", new[] { "battle_aguri_spr.bin", "battle_kanba_spr.bin" })]
        [InlineData("Mustadio", new[] { "battle_musu_spr.bin", "battle_garu_spr.bin" })]
        [InlineData("Reis", new[] { "battle_reze_spr.bin", "battle_reze_d_spr.bin" })]
        public void GetSpriteNamesForCharacter_ReturnsCorrectSprites_ForMultiSpriteCharacters(string characterName, string[] expectedSprites)
        {
            // Arrange - create section mapping files
            var storyMappingsPath = Path.Combine(_modPath, "Data", "SectionMappings", "Story");
            Directory.CreateDirectory(storyMappingsPath);

            // Create mapping file with sprites array
            var spritesJson = "[\"" + string.Join("\", \"", expectedSprites) + "\"]";
            var json = $@"{{
                ""job"": ""{characterName}"",
                ""sprites"": {spritesJson},
                ""sections"": []
            }}";
            File.WriteAllText(Path.Combine(storyMappingsPath, $"{characterName}.json"), json);

            var adapter = new ThemeManagerAdapter(_sourcePath, _modPath);

            // Act
            var spriteNames = adapter.GetSpriteNamesForCharacter(characterName);

            // Assert
            spriteNames.Should().NotBeNull();
            spriteNames.Should().HaveCount(expectedSprites.Length);
            spriteNames.Should().BeEquivalentTo(expectedSprites);
        }

        [Fact]
        public void GetSpriteNamesForCharacter_FallsBackToHardcoded_WhenMappingNotFound()
        {
            // Arrange - no mapping files created
            var adapter = new ThemeManagerAdapter(_sourcePath, _modPath);

            // Act
            var spriteNames = adapter.GetSpriteNamesForCharacter("Cloud");

            // Assert - should use fallback
            spriteNames.Should().NotBeNull();
            spriteNames.Should().Contain("battle_cloud_spr.bin");
        }

        #endregion
    }
}
