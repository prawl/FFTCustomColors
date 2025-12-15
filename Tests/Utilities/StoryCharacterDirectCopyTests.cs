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
    /// (e.g., battle_mara_coral_spr.bin -> battle_mara_spr.bin)
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
        public void Malak_Coral_Theme_Should_Copy_To_Base_Sprite_Name()
        {
            // Arrange
            var config = new Config
            {
                Malak = MalakColorScheme.coral
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Create the themed sprite file in the unit directory (where they actually are)
            var themedSprite = Path.Combine(_sourceUnitPath, "battle_mara_coral_spr.bin");
            File.WriteAllBytes(themedSprite, new byte[] { 0xC0, 0xAA, 0x11 }); // Coral theme data

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            // The coral theme should be copied to the BASE sprite name (battle_mara_spr.bin)
            var baseSpriteFile = Path.Combine(_unitPath, "battle_mara_spr.bin");
            File.Exists(baseSpriteFile).Should().BeTrue("Malak's coral theme should overwrite the base sprite");
            File.ReadAllBytes(baseSpriteFile).Should().Equal(new byte[] { 0xC0, 0xAA, 0x11 },
                "The base sprite should contain the coral theme data");

            // The themed file should NOT be created with theme name appended
            var wrongFile = Path.Combine(_unitPath, "battle_mara_coral_spr.bin");
            File.Exists(wrongFile).Should().BeFalse("Should not create a separate themed file");
        }

        [Fact]
        public void Rafa_CrimsonRed_Theme_Should_Copy_To_Base_Sprite_Name()
        {
            // Arrange
            var config = new Config
            {
                Rafa = RafaColorScheme.crimson_red
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Create the themed sprite file
            var themedSprite = Path.Combine(_sourceUnitPath, "battle_rafa_crimson_red_spr.bin");
            File.WriteAllBytes(themedSprite, new byte[] { 0xDC, 0x14, 0x3C }); // Crimson red data

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var baseSpriteFile = Path.Combine(_unitPath, "battle_rafa_spr.bin");
            File.Exists(baseSpriteFile).Should().BeTrue("Rafa's crimson_red theme should overwrite the base sprite");
            File.ReadAllBytes(baseSpriteFile).Should().Equal(new byte[] { 0xDC, 0x14, 0x3C });

            // The themed file should NOT be created with theme name appended
            var wrongFile = Path.Combine(_unitPath, "battle_rafa_crimson_red_spr.bin");
            File.Exists(wrongFile).Should().BeFalse("Should not create a separate themed file");
        }

        [Fact]
        public void All_Story_Characters_Should_Copy_To_Base_Names()
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

            // Create themed sprite files in the unit directory
            CreateThemedSprite("aruma", "golden_yellow");
            CreateThemedSprite("seria", "royal_blue");
            CreateThemedSprite("dily", "forest_green");
            CreateThemedSprite("ledy", "magenta");
            CreateThemedSprite("mara", "silver_steel");
            CreateThemedSprite("rafa", "ocean_blue");
            CreateThemedSprite("reze", "violet");

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert - all should copy to base names
            VerifyBaseSpriteExists("aruma", shouldExist: true);
            VerifyBaseSpriteExists("seria", shouldExist: true);
            VerifyBaseSpriteExists("dily", shouldExist: true);
            VerifyBaseSpriteExists("ledy", shouldExist: true);
            VerifyBaseSpriteExists("mara", shouldExist: true);
            VerifyBaseSpriteExists("rafa", shouldExist: true);
            VerifyBaseSpriteExists("reze", shouldExist: true);

            // Verify NO themed files are created
            VerifyThemedSpriteDoesNotExist("aruma", "golden_yellow");
            VerifyThemedSpriteDoesNotExist("seria", "royal_blue");
            VerifyThemedSpriteDoesNotExist("dily", "forest_green");
            VerifyThemedSpriteDoesNotExist("ledy", "magenta");
            VerifyThemedSpriteDoesNotExist("mara", "silver_steel");
            VerifyThemedSpriteDoesNotExist("rafa", "ocean_blue");
            VerifyThemedSpriteDoesNotExist("reze", "violet");
        }

        [Fact]
        public void Story_Characters_With_Original_Theme_Should_Not_Copy_Anything()
        {
            // Arrange
            var config = new Config
            {
                Malak = MalakColorScheme.original,
                Rafa = RafaColorScheme.original
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            // Even if themed files exist, they should not be copied when theme is "original"
            CreateThemedSprite("mara", "coral");
            CreateThemedSprite("rafa", "crimson_red");

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert - no files should be created when theme is "original"
            var maraBase = Path.Combine(_unitPath, "battle_mara_spr.bin");
            var rafaBase = Path.Combine(_unitPath, "battle_rafa_spr.bin");

            File.Exists(maraBase).Should().BeFalse("Malak base sprite should not be created when theme is original");
            File.Exists(rafaBase).Should().BeFalse("Rafa base sprite should not be created when theme is original");
        }

        [Fact]
        public void Should_Handle_Missing_Themed_Sprite_Files_Gracefully()
        {
            // Arrange
            var config = new Config
            {
                Malak = MalakColorScheme.coral // Selected but file doesn't exist
            };

            _mockConfigManager.Setup(x => x.LoadConfig()).Returns(config);

            var sourcePath = Path.Combine(_testDir, "source");
            _spriteManager = new ConfigBasedSpriteManager(_modPath, _mockConfigManager.Object, sourcePath);

            // Act & Assert
            _spriteManager.Invoking(x => x.ApplyConfiguration())
                .Should().NotThrow("Should handle missing sprite files gracefully");

            var baseSpriteFile = Path.Combine(_unitPath, "battle_mara_spr.bin");
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