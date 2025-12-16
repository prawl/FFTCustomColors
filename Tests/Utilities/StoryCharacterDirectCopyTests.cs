using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;
using Moq;

namespace Tests.Utilities
{
    /// <summary>
    /// TDD Tests for story character theme application.
    /// Story characters should have their themed sprites copied DIRECTLY to the base sprite name
    /// (e.g., battle_agrias_ash_dark_spr.bin -> battle_agrias_spr.bin)
    /// instead of creating a new file with the theme name appended.
    /// </summary>
    public class StoryCharacterDirectCopyTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _modPath;
        private readonly string _unitPath;
        private readonly string _sourceUnitPath;
        private readonly Mock<ConfigurationManager> _mockConfigManager;
        private ConfigBasedSpriteManager _spriteManager;

        public StoryCharacterDirectCopyTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            _modPath = Path.Combine(_testDir, "mod");
            _unitPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            var sourcePath = Path.Combine(_testDir, "source");
            _sourceUnitPath = Path.Combine(sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            Directory.CreateDirectory(_unitPath);
            Directory.CreateDirectory(_sourceUnitPath);

            _mockConfigManager = new Mock<ConfigurationManager>("dummy_path");
        }

        [Fact]
        public void Agrias_EmeraldGreen_Theme_Should_Copy_To_Base_Sprite_Name()
        {
            // Arrange
            var config = new Config
            {
                Agrias = AgriasColorScheme.ash_dark
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Create the themed sprite file in the unit directory (where they actually are)
            var themedSprite = Path.Combine(_sourceUnitPath, "battle_aguri_ash_dark_spr.bin");
            File.WriteAllBytes(themedSprite, new byte[] { 0xC0, 0xAA, 0x11 }); // Ash dark theme data

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            // The ash dark theme should be copied to the BASE sprite name (battle_aguri_spr.bin)
            var baseSpriteFile = Path.Combine(_unitPath, "battle_aguri_spr.bin");
            File.Exists(baseSpriteFile).Should().BeTrue("Agrias's emerald green theme should overwrite the base sprite");
            File.ReadAllBytes(baseSpriteFile).Should().Equal(new byte[] { 0xC0, 0xAA, 0x11 },
                "The base sprite should contain the emerald green theme data");

            // The themed file should NOT be created with theme name appended
            var wrongFile = Path.Combine(_unitPath, "battle_aguri_ash_dark_spr.bin");
            File.Exists(wrongFile).Should().BeFalse("Should not create a separate themed file");
        }

        [Fact]
        public void Cloud_NavyBlue_Theme_Should_Copy_To_Base_Sprite_Name()
        {
            // Arrange
            var config = new Config
            {
                Cloud = CloudColorScheme.knights_round
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Create the themed sprite file
            var themedSprite = Path.Combine(_sourceUnitPath, "battle_cloud_knights_round_spr.bin");
            File.WriteAllBytes(themedSprite, new byte[] { 0xDC, 0x14, 0x3C }); // Knights round data

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var baseSpriteFile = Path.Combine(_unitPath, "battle_cloud_spr.bin");
            File.Exists(baseSpriteFile).Should().BeTrue("Cloud's knights_round theme should overwrite the base sprite");
            File.ReadAllBytes(baseSpriteFile).Should().Equal(new byte[] { 0xDC, 0x14, 0x3C });

            // The themed file should NOT be created with theme name appended
            var wrongFile = Path.Combine(_unitPath, "battle_cloud_knights_round_spr.bin");
            File.Exists(wrongFile).Should().BeFalse("Should not create a separate themed file");
        }

        [Fact]
        public void All_Story_Characters_Should_Copy_To_Base_Names()
        {
            // Arrange
            var config = new Config
            {
                Agrias = AgriasColorScheme.ash_dark,
                Cloud = CloudColorScheme.knights_round,
                Orlandeau = OrlandeauColorScheme.thunder_god
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Create themed sprite files in the unit directory
            CreateThemedSprite("aguri", "ash_dark");
            CreateThemedSprite("cloud", "knights_round");
            CreateThemedSprite("oru", "thunder_god");

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert - all should copy to base names
            VerifyBaseSpriteExists("aguri", shouldExist: true);
            VerifyBaseSpriteExists("cloud", shouldExist: true);
            VerifyBaseSpriteExists("oru", shouldExist: true);

            // Verify NO themed files are created
            VerifyThemedSpriteDoesNotExist("aguri", "ash_dark");
            VerifyThemedSpriteDoesNotExist("cloud", "knights_round");
            VerifyThemedSpriteDoesNotExist("oru", "thunder_god");
        }

        [Fact]
        public void Story_Characters_With_Original_Theme_Should_Not_Copy_Anything()
        {
            // Arrange
            var config = new Config
            {
                Agrias = AgriasColorScheme.original,
                Cloud = CloudColorScheme.original
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Even if themed files exist, they should not be copied when theme is "original"
            CreateThemedSprite("aguri", "ash_dark");
            CreateThemedSprite("cloud", "knights_round");

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert - no files should be created when theme is "original"
            var agriasBase = Path.Combine(_unitPath, "battle_aguri_spr.bin");
            var cloudBase = Path.Combine(_unitPath, "battle_cloud_spr.bin");

            File.Exists(agriasBase).Should().BeFalse("Agrias base sprite should not be created when theme is original");
            File.Exists(cloudBase).Should().BeFalse("Cloud base sprite should not be created when theme is original");
        }

        [Fact]
        public void Should_Handle_Missing_Themed_Sprite_Files_Gracefully()
        {
            // Arrange
            var config = new Config
            {
                Agrias = AgriasColorScheme.ash_dark // Selected but file doesn't exist
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act & Assert
            _spriteManager.Invoking(x => x.ApplyConfiguration())
                .Should().NotThrow("Should handle missing sprite files gracefully");

            var baseSpriteFile = Path.Combine(_unitPath, "battle_aguri_spr.bin");
            File.Exists(baseSpriteFile).Should().BeFalse("Should not create file when source doesn't exist");
        }

        private void CreateThemedSprite(string spriteName, string theme)
        {
            var themedFile = Path.Combine(_sourceUnitPath, $"battle_{spriteName}_{theme}_spr.bin");
            File.WriteAllBytes(themedFile, new byte[] { 0x01, 0x02, 0x03 });
        }

        private void VerifyBaseSpriteExists(string spriteName, bool shouldExist)
        {
            var baseSpriteFile = Path.Combine(_unitPath, $"battle_{spriteName}_spr.bin");
            if (shouldExist)
            {
                File.Exists(baseSpriteFile).Should().BeTrue($"Base sprite for {spriteName} should exist");
                File.ReadAllBytes(baseSpriteFile).Should().Equal(new byte[] { 0x01, 0x02, 0x03 });
            }
            else
            {
                File.Exists(baseSpriteFile).Should().BeFalse($"Base sprite for {spriteName} should not exist");
            }
        }

        private void VerifyThemedSpriteDoesNotExist(string spriteName, string theme)
        {
            var themedFile = Path.Combine(_unitPath, $"battle_{spriteName}_{theme}_spr.bin");
            File.Exists(themedFile).Should().BeFalse(
                $"Should not create themed file battle_{spriteName}_{theme}_spr.bin, only base sprite");
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