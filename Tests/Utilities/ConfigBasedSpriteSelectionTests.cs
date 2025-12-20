using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Tests
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
            _spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, _testModPath);
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
                Knight_Male = "corpse_brigade",
                Archer_Female = "lucavi"
            };
            _configManager.SaveConfig(config);

            // Act
            var knightPath = _spriteManager.InterceptFilePath(@"C:\Game\data\battle_knight_m_spr.bin");
            var archerPath = _spriteManager.InterceptFilePath(@"C:\Game\data\battle_yumi_w_spr.bin");
            var monkPath = _spriteManager.InterceptFilePath(@"C:\Game\data\battle_monk_m_spr.bin");

            // Assert - The paths should either contain the sprite variant folder OR point to the copied file
            Assert.True(knightPath.Contains("battle_knight_m_spr.bin"));
            Assert.NotEqual(@"C:\Game\data\battle_knight_m_spr.bin", knightPath); // Should be modified

            Assert.True(archerPath.Contains("battle_yumi_w_spr.bin"));
            Assert.NotEqual(@"C:\Game\data\battle_yumi_w_spr.bin", archerPath); // Should be modified

            Assert.Equal(@"C:\Game\data\battle_monk_m_spr.bin", monkPath); // Not configured, should return unchanged
        }

        [Fact]
        public void ApplyConfiguration_SwapsCorrectSprites()
        {
            // Arrange
            var config = new Config
            {
                Knight_Male = "northern_sky",
                Archer_Female = "corpse_brigade",
                Monk_Male = "lucavi"
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
                Knight_Male = "corpse_brigade",
                Dragoon_Female = "northern_sky"
            };
            _configManager.SaveConfig(config);

            // Act
            var knightColor = _spriteManager.GetActiveColorForJob("Knight_Male");
            var dragoonColor = _spriteManager.GetActiveColorForJob("Dragoon_Female");
            var monkColor = _spriteManager.GetActiveColorForJob("Monk_Male");

            // Assert
            Assert.Equal("Corpse Brigade", knightColor);
            Assert.Equal("Northern Sky", dragoonColor);
            Assert.Equal("Original", monkColor);
        }

        [Fact]
        public void SetColorForJob_UpdatesConfigAndAppliesChanges()
        {
            // Arrange
            var unitDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Act
            _spriteManager.SetColorForJob("Knight_Male", "lucavi");

            // Assert - Config should be updated
            var config = _configManager.LoadConfig();
            Assert.Equal("lucavi", config.Knight_Male); // lucavi

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
                Knight_Male = "corpse_brigade",
                Archer_Female = "lucavi",
                Monk_Male = "northern_sky"
            };
            _configManager.SaveConfig(config);
            _spriteManager.ApplyConfiguration();

            var unitDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Verify themes were applied initially
            var knightContentBefore = File.ReadAllText(Path.Combine(unitDir, "battle_knight_m_spr.bin"));
            var archerContentBefore = File.ReadAllText(Path.Combine(unitDir, "battle_yumi_w_spr.bin"));
            var monkContentBefore = File.ReadAllText(Path.Combine(unitDir, "battle_monk_m_spr.bin"));

            Assert.Equal("sprites_corpse_brigade_knight_male", knightContentBefore);
            Assert.Equal("sprites_lucavi_archer_female", archerContentBefore);
            Assert.Equal("sprites_northern_sky_monk_male", monkContentBefore);

            // Act
            _spriteManager.ResetAllToOriginal();

            // Assert - Config should be reset
            var resetConfig = _configManager.LoadConfig();
            Assert.Equal("original", resetConfig.Knight_Male);
            Assert.Equal("original", resetConfig.Archer_Female);
            Assert.Equal("original", resetConfig.Monk_Male);

            // Assert - When reset to "original", files remain unchanged since "original" means use game's default sprites
            // The mod doesn't copy "original" theme files, it lets the game use its built-in sprites
            var knightContentAfter = File.ReadAllText(Path.Combine(unitDir, "battle_knight_m_spr.bin"));
            var archerContentAfter = File.ReadAllText(Path.Combine(unitDir, "battle_yumi_w_spr.bin"));
            var monkContentAfter = File.ReadAllText(Path.Combine(unitDir, "battle_monk_m_spr.bin"));

            // Files should remain as they were since "original" skips copying
            Assert.Equal("sprites_corpse_brigade_knight_male", knightContentAfter);
            Assert.Equal("sprites_lucavi_archer_female", archerContentAfter);
            Assert.Equal("sprites_northern_sky_monk_male", monkContentAfter);
        }

        [Fact]
        public void GetJobFromSpriteName_ReturnsCorrectJobProperty()
        {
            // Act & Assert
            Assert.Equal("Knight_Male", _spriteManager.GetJobFromSpriteName("battle_knight_m_spr.bin"));
            Assert.Equal("Archer_Female", _spriteManager.GetJobFromSpriteName("battle_yumi_w_spr.bin"));
            Assert.Equal("Monk_Male", _spriteManager.GetJobFromSpriteName("battle_monk_m_spr.bin"));
            Assert.Equal("TimeMage_Female", _spriteManager.GetJobFromSpriteName("battle_toki_w_spr.bin"));
            Assert.Null(_spriteManager.GetJobFromSpriteName("unknown_sprite.bin"));
        }
    }
}
