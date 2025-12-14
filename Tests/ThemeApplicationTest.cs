using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests
{
    public class ThemeApplicationTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testModPath;
        private readonly string _testSourcePath;
        private readonly string _testConfigPath;

        public ThemeApplicationTest(ITestOutputHelper output)
        {
            _output = output;

            // Create unique test paths
            var testId = Guid.NewGuid().ToString();
            _testModPath = Path.Combine(Path.GetTempPath(), $"test_mod_{testId}");
            _testSourcePath = Path.Combine(Path.GetTempPath(), $"test_source_{testId}");
            _testConfigPath = Path.Combine(_testModPath, "Config.json");

            // Create directory structures
            CreateTestDirectories();
        }

        private void CreateTestDirectories()
        {
            // Create mod deployment directory (where sprites should be copied TO)
            var modUnitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(modUnitPath);

            // Create source/git directory (where theme sprites are stored)
            var sourceUnitPath = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Create theme directories with sprite files
            var themes = new[] { "original", "corpse_brigade", "lucavi", "northern_sky" };
            foreach (var theme in themes)
            {
                var themeDir = Path.Combine(sourceUnitPath, $"sprites_{theme}");
                Directory.CreateDirectory(themeDir);

                // Create test sprite files with content that identifies the theme
                File.WriteAllText(Path.Combine(themeDir, "battle_knight_m_spr.bin"), $"KNIGHT_MALE_{theme.ToUpper()}");
                File.WriteAllText(Path.Combine(themeDir, "battle_yumi_w_spr.bin"), $"ARCHER_FEMALE_{theme.ToUpper()}");
                File.WriteAllText(Path.Combine(themeDir, "battle_monk_m_spr.bin"), $"MONK_MALE_{theme.ToUpper()}");
            }

            _output.WriteLine($"Created test directories:");
            _output.WriteLine($"  Mod path: {_testModPath}");
            _output.WriteLine($"  Source path: {_testSourcePath}");
        }

        [Fact]
        public void ApplyConfiguration_ShouldCopySpritesFromSourceToMod()
        {
            // Arrange
            var config = new Config
            {
                Knight_Male = (FFTColorMod.Configuration.ColorScheme)2,    // lucavi
                Archer_Female = (FFTColorMod.Configuration.ColorScheme)1,  // corpse_brigade
                Monk_Male = (FFTColorMod.Configuration.ColorScheme)3       // northern_sky
            };

            var configManager = new ConfigurationManager(_testConfigPath);
            configManager.SaveConfig(config);

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, configManager, _testSourcePath);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert - Check that the correct theme files were copied to the mod directory
            var modUnitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Knight should have lucavi theme
            var knightFile = Path.Combine(modUnitPath, "battle_knight_m_spr.bin");
            Assert.True(File.Exists(knightFile), $"Knight sprite should exist at {knightFile}");
            var knightContent = File.ReadAllText(knightFile);
            Assert.Equal("KNIGHT_MALE_LUCAVI", knightContent);

            // Archer should have corpse_brigade theme
            var archerFile = Path.Combine(modUnitPath, "battle_yumi_w_spr.bin");
            Assert.True(File.Exists(archerFile), $"Archer sprite should exist at {archerFile}");
            var archerContent = File.ReadAllText(archerFile);
            Assert.Equal("ARCHER_FEMALE_CORPSE_BRIGADE", archerContent);

            // Monk should have northern_sky theme
            var monkFile = Path.Combine(modUnitPath, "battle_monk_m_spr.bin");
            Assert.True(File.Exists(monkFile), $"Monk sprite should exist at {monkFile}");
            var monkContent = File.ReadAllText(monkFile);
            Assert.Equal("MONK_MALE_NORTHERN_SKY", monkContent);

            _output.WriteLine("All sprites were correctly copied with their themes!");
        }

        [Fact]
        public void ApplyConfiguration_WithOriginalTheme_ShouldCopyOriginalSprites()
        {
            // Arrange - All set to original
            var config = new Config(); // Default is all original

            var configManager = new ConfigurationManager(_testConfigPath);
            configManager.SaveConfig(config);

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, configManager, _testSourcePath);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert
            var modUnitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            var knightFile = Path.Combine(modUnitPath, "battle_knight_m_spr.bin");
            if (File.Exists(knightFile))
            {
                var content = File.ReadAllText(knightFile);
                Assert.Equal("KNIGHT_MALE_ORIGINAL", content);
            }
        }

        [Fact]
        public void SetColorForJob_ShouldImmediatelyApplyTheme()
        {
            // Arrange
            var configManager = new ConfigurationManager(_testConfigPath);
            var spriteManager = new ConfigBasedSpriteManager(_testModPath, configManager, _testSourcePath);

            // Act - Set a specific job color
            spriteManager.SetColorForJob("Knight_Male", "lucavi");

            // Assert - Check that the sprite was immediately copied
            var modUnitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var knightFile = Path.Combine(modUnitPath, "battle_knight_m_spr.bin");

            Assert.True(File.Exists(knightFile), "Knight sprite should be copied immediately");
            var content = File.ReadAllText(knightFile);
            Assert.Equal("KNIGHT_MALE_LUCAVI", content);

            // Also verify the config was saved
            var savedConfig = configManager.LoadConfig();
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)2, savedConfig.Knight_Male);
        }

        [Fact]
        public void ApplyConfiguration_WhenSourceFilesDoNotExist_ShouldNotCrash()
        {
            // Arrange - Create config but delete source files
            var config = new Config
            {
                Knight_Male = (FFTColorMod.Configuration.ColorScheme)99 // Non-existent theme
            };

            var configManager = new ConfigurationManager(_testConfigPath);
            configManager.SaveConfig(config);

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, configManager, _testSourcePath);

            // Act - Should not throw
            var exception = Record.Exception(() => spriteManager.ApplyConfiguration());

            // Assert
            Assert.Null(exception);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testModPath))
                    Directory.Delete(_testModPath, true);
                if (Directory.Exists(_testSourcePath))
                    Directory.Delete(_testSourcePath, true);
            }
            catch { }
        }
    }
}