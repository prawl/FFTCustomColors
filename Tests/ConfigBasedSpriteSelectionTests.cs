using System;
using System.IO;
using Xunit;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests
{
    public class ConfigBasedSpriteSelectionTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testModPath;
        private readonly ConfigurationManager _configManager;
        private readonly ConfigBasedSpriteManager _spriteManager;

        public ConfigBasedSpriteSelectionTests()
        {
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
            _testModPath = Path.Combine(Path.GetTempPath(), $"test_mod_{Guid.NewGuid()}");

            // Create test directory structure
            CreateTestDirectoryStructure();

            _configManager = new ConfigurationManager(_testConfigPath);
            _spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager);
        }

        private void CreateTestDirectoryStructure()
        {
            // Create base unit directory
            var unitDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitDir);

            // Create color scheme directories with test sprites
            var schemes = new[] { "sprites_original", "sprites_corpse_brigade", "sprites_lucavi", "sprites_northern_sky" };
            foreach (var scheme in schemes)
            {
                var schemeDir = Path.Combine(unitDir, scheme);
                Directory.CreateDirectory(schemeDir);

                // Create test sprite files
                File.WriteAllText(Path.Combine(schemeDir, "battle_knight_m_spr.bin"), $"{scheme}_knight_male");
                File.WriteAllText(Path.Combine(schemeDir, "battle_knight_w_spr.bin"), $"{scheme}_knight_female");
                File.WriteAllText(Path.Combine(schemeDir, "battle_yumi_m_spr.bin"), $"{scheme}_archer_male");
                File.WriteAllText(Path.Combine(schemeDir, "battle_yumi_w_spr.bin"), $"{scheme}_archer_female");
                File.WriteAllText(Path.Combine(schemeDir, "battle_monk_m_spr.bin"), $"{scheme}_monk_male");
            }

            // Copy original sprites to base unit directory as defaults
            var originalDir = Path.Combine(unitDir, "sprites_original");
            foreach (var file in Directory.GetFiles(originalDir))
            {
                File.Copy(file, Path.Combine(unitDir, Path.GetFileName(file)), true);
            }
        }

        public void Dispose()
        {
            if (File.Exists(_testConfigPath))
                File.Delete(_testConfigPath);
            if (Directory.Exists(_testModPath))
                Directory.Delete(_testModPath, true);
        }

        [Fact]
        public void InterceptFilePath_WithNoConfig_ReturnsOriginalPath()
        {
            // Arrange
            var originalPath = @"C:\Game\data\battle_knight_m_spr.bin";

            // Act
            var interceptedPath = _spriteManager.InterceptFilePath(originalPath);

            // Assert
            Assert.Equal(originalPath, interceptedPath); // No change when using default config
        }

        [Fact]
        public void InterceptFilePath_WithConfiguredJob_ReturnsModifiedPath()
        {
            // Arrange
            var config = new Config
            {
                KnightMale = "corpse_brigade",
                ArcherFemale = "lucavi"
            };
            _configManager.SaveConfig(config);

            // Act
            var knightPath = _spriteManager.InterceptFilePath(@"C:\Game\data\battle_knight_m_spr.bin");
            var archerPath = _spriteManager.InterceptFilePath(@"C:\Game\data\battle_yumi_w_spr.bin");
            var monkPath = _spriteManager.InterceptFilePath(@"C:\Game\data\battle_monk_m_spr.bin");

            // Assert
            Assert.Contains("sprites_corpse_brigade", knightPath);
            Assert.Contains("battle_knight_m_spr.bin", knightPath);

            Assert.Contains("sprites_lucavi", archerPath);
            Assert.Contains("battle_yumi_w_spr.bin", archerPath);

            Assert.Equal(@"C:\Game\data\battle_monk_m_spr.bin", monkPath); // Not configured, should return unchanged
        }

        [Fact]
        public void ApplyConfiguration_SwapsCorrectSprites()
        {
            // Arrange
            var config = new Config
            {
                KnightMale = "northern_sky",
                ArcherFemale = "corpse_brigade",
                MonkMale = "lucavi"
            };
            _configManager.SaveConfig(config);
            var unitDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert - Check that the correct sprites were copied to the unit directory
            var knightContent = File.ReadAllText(Path.Combine(unitDir, "battle_knight_m_spr.bin"));
            var archerContent = File.ReadAllText(Path.Combine(unitDir, "battle_yumi_w_spr.bin"));
            var monkContent = File.ReadAllText(Path.Combine(unitDir, "battle_monk_m_spr.bin"));

            Assert.Equal("sprites_northern_sky_knight_male", knightContent);
            Assert.Equal("sprites_corpse_brigade_archer_female", archerContent);
            Assert.Equal("sprites_lucavi_monk_male", monkContent);
        }

        [Fact]
        public void GetActiveColorForJob_ReturnsCorrectColor()
        {
            // Arrange
            var config = new Config
            {
                KnightMale = "corpse_brigade",
                DragoonFemale = "northern_sky"
            };
            _configManager.SaveConfig(config);

            // Act
            var knightColor = _spriteManager.GetActiveColorForJob("KnightMale");
            var dragoonColor = _spriteManager.GetActiveColorForJob("DragoonFemale");
            var monkColor = _spriteManager.GetActiveColorForJob("MonkMale");

            // Assert
            Assert.Equal("corpse_brigade", knightColor);
            Assert.Equal("northern_sky", dragoonColor);
            Assert.Equal("original", monkColor);
        }

        [Fact]
        public void SetColorForJob_UpdatesConfigAndAppliesChanges()
        {
            // Arrange
            var unitDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Act
            _spriteManager.SetColorForJob("KnightMale", "lucavi");

            // Assert - Config should be updated
            var config = _configManager.LoadConfig();
            Assert.Equal("lucavi", config.KnightMale);

            // Assert - Sprite should be swapped
            var knightContent = File.ReadAllText(Path.Combine(unitDir, "battle_knight_m_spr.bin"));
            Assert.Equal("sprites_lucavi_knight_male", knightContent);
        }

        [Fact]
        public void ResetAllToOriginal_ResetsAllSprites()
        {
            // Arrange
            var config = new Config
            {
                KnightMale = "corpse_brigade",
                ArcherFemale = "lucavi",
                MonkMale = "northern_sky"
            };
            _configManager.SaveConfig(config);
            _spriteManager.ApplyConfiguration();

            var unitDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Act
            _spriteManager.ResetAllToOriginal();

            // Assert - Config should be reset
            var resetConfig = _configManager.LoadConfig();
            Assert.Equal("original", resetConfig.KnightMale);
            Assert.Equal("original", resetConfig.ArcherFemale);
            Assert.Equal("original", resetConfig.MonkMale);

            // Assert - Sprites should be original
            var knightContent = File.ReadAllText(Path.Combine(unitDir, "battle_knight_m_spr.bin"));
            var archerContent = File.ReadAllText(Path.Combine(unitDir, "battle_yumi_w_spr.bin"));
            var monkContent = File.ReadAllText(Path.Combine(unitDir, "battle_monk_m_spr.bin"));

            Assert.Equal("sprites_original_knight_male", knightContent);
            Assert.Equal("sprites_original_archer_female", archerContent);
            Assert.Equal("sprites_original_monk_male", monkContent);
        }

        [Fact]
        public void GetJobFromSpriteName_ReturnsCorrectJobProperty()
        {
            // Act & Assert
            Assert.Equal("KnightMale", _spriteManager.GetJobFromSpriteName("battle_knight_m_spr.bin"));
            Assert.Equal("ArcherFemale", _spriteManager.GetJobFromSpriteName("battle_yumi_w_spr.bin"));
            Assert.Equal("MonkMale", _spriteManager.GetJobFromSpriteName("battle_monk_m_spr.bin"));
            Assert.Equal("TimeMageFemale", _spriteManager.GetJobFromSpriteName("battle_toki_w_spr.bin"));
            Assert.Null(_spriteManager.GetJobFromSpriteName("unknown_sprite.bin"));
        }
    }
}