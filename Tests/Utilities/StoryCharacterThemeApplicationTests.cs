using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;
using Moq;

namespace Tests.Utilities
{
    public class StoryCharacterThemeApplicationTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _modPath;
        private readonly string _unitPath;
        private readonly string _sourceUnitPath;
        private readonly Mock<ConfigurationManager> _mockConfigManager;
        private ConfigBasedSpriteManager _spriteManager;

        public StoryCharacterThemeApplicationTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            _modPath = Path.Combine(_testDir, "mod");
            _unitPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // The ConfigBasedSpriteManager expects the source root path, not the unit path
            var sourcePath = Path.Combine(_testDir, "source");
            _sourceUnitPath = Path.Combine(sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            Directory.CreateDirectory(_unitPath);
            Directory.CreateDirectory(_sourceUnitPath);

            _mockConfigManager = new Mock<ConfigurationManager>("dummy_path");
        }

        [Fact]
        public void ApplyConfiguration_Should_Apply_Alma_Theme()
        {
            // Arrange
            var config = new Config
            {
                Alma = AlmaColorScheme.crimson_red,
                Squire_Male = ColorScheme.original // Include a generic for comparison
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Create source sprite files - new format: themed sprites are in unit directory
            var almaSpriteSource = Path.Combine(_sourceUnitPath, "battle_aruma_crimson_red_spr.bin");
            File.WriteAllBytes(almaSpriteSource, new byte[] { 1, 2, 3 });

            // Create generic sprite for comparison
            var originalDir = Path.Combine(_sourceUnitPath, "sprites_original");
            Directory.CreateDirectory(originalDir);
            var squireSpriteSource = Path.Combine(originalDir, "battle_mina_m_spr.bin");
            File.WriteAllBytes(squireSpriteSource, new byte[] { 4, 5, 6 });

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert - theme should be copied to base sprite name
            var almaDest = Path.Combine(_unitPath, "battle_aruma_spr.bin");
            File.Exists(almaDest).Should().BeTrue("Alma's crimson_red theme should overwrite base sprite");
            File.ReadAllBytes(almaDest).Should().Equal(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void ApplyConfiguration_Should_Apply_All_Story_Character_Themes()
        {
            // Arrange
            var config = new Config
            {
                Alma = AlmaColorScheme.golden_yellow,
                Celia = CeliaColorScheme.royal_blue,
                Delita = DelitaColorScheme.forest_green,
                Lettie = LettieColorScheme.magenta,
                Malak = MalakColorScheme.silver_steel,
                Rafa = RafaColorScheme.ocean_blue,
                Reis = ReisColorScheme.violet
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Create source directories and files for each character
            CreateStoryCharacterSprite("alma", "golden_yellow", "aruma");
            CreateStoryCharacterSprite("celia", "royal_blue", "seria");
            CreateStoryCharacterSprite("delita", "forest_green", "dily");
            CreateStoryCharacterSprite("lettie", "magenta", "ledy");
            CreateStoryCharacterSprite("malak", "silver_steel", "mara");
            CreateStoryCharacterSprite("rafa", "ocean_blue", "rafa");
            CreateStoryCharacterSprite("reis", "violet", "reze");

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            VerifyStoryCharacterSprite("aruma", "golden_yellow");
            VerifyStoryCharacterSprite("seria", "royal_blue");
            VerifyStoryCharacterSprite("dily", "forest_green");
            VerifyStoryCharacterSprite("ledy", "magenta");
            VerifyStoryCharacterSprite("mara", "silver_steel");
            VerifyStoryCharacterSprite("rafa", "ocean_blue");
            VerifyStoryCharacterSprite("reze", "violet");
        }

        [Fact]
        public void ApplyConfiguration_Should_Skip_Story_Characters_With_Original_Theme()
        {
            // Arrange
            var config = new Config
            {
                Alma = AlmaColorScheme.original,
                Celia = CeliaColorScheme.original
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Create theme files that should NOT be copied when theme is "original"
            CreateStoryCharacterSprite("alma", "crimson_red", "aruma");
            CreateStoryCharacterSprite("celia", "royal_blue", "seria");

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert - when theme is original, base sprites should NOT be created/overwritten
            var almaBase = Path.Combine(_unitPath, "battle_aruma_spr.bin");
            var celiaBase = Path.Combine(_unitPath, "battle_seria_spr.bin");

            File.Exists(almaBase).Should().BeFalse("Alma base sprite should not be created when theme is original");
            File.Exists(celiaBase).Should().BeFalse("Celia base sprite should not be created when theme is original");
        }

        [Fact]
        public void ApplyConfiguration_Should_Handle_Missing_Story_Character_Sprite_Files()
        {
            // Arrange
            var config = new Config
            {
                Alma = AlmaColorScheme.crimson_red // Theme selected but file doesn't exist
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act & Assert
            _spriteManager.Invoking(x => x.ApplyConfiguration())
                .Should().NotThrow("Should handle missing sprite files gracefully");
        }

        [Fact]
        public void ApplyConfiguration_Should_Apply_Both_Generic_And_Story_Characters()
        {
            // Arrange
            var config = new Config
            {
                // Generic character
                Knight_Male = ColorScheme.corpse_brigade,
                // Story character
                Alma = AlmaColorScheme.crimson_red
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Create generic sprite
            var corpseDir = Path.Combine(_sourceUnitPath, "sprites_corpse_brigade");
            Directory.CreateDirectory(corpseDir);
            File.WriteAllBytes(Path.Combine(corpseDir, "battle_knight_m_spr.bin"), new byte[] { 1 });

            // Create story character sprite
            CreateStoryCharacterSprite("alma", "crimson_red", "aruma");

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var knightDest = Path.Combine(_unitPath, "battle_knight_m_spr.bin");
            var almaDest = Path.Combine(_unitPath, "battle_aruma_spr.bin");

            File.Exists(knightDest).Should().BeTrue("Generic knight sprite should be applied");
            File.Exists(almaDest).Should().BeTrue("Story character Alma base sprite should be overwritten with theme");
        }

        private void CreateStoryCharacterSprite(string character, string theme, string spriteName)
        {
            // New format: themed sprites are directly in unit directory
            var spriteFile = Path.Combine(_sourceUnitPath, $"battle_{spriteName}_{theme}_spr.bin");
            File.WriteAllBytes(spriteFile, new byte[] { 1, 2, 3 });
        }

        private void VerifyStoryCharacterSprite(string spriteName, string theme)
        {
            // New behavior: themes are copied to base sprite name
            var destFile = Path.Combine(_unitPath, $"battle_{spriteName}_spr.bin");
            File.Exists(destFile).Should().BeTrue($"Base sprite for {spriteName} should be overwritten with {theme} theme");
            File.ReadAllBytes(destFile).Should().Equal(new byte[] { 1, 2, 3 });
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
    }
}