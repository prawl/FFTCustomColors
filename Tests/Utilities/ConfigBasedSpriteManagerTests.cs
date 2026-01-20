using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Services;

namespace Tests.Utilities
{
    public class ConfigBasedSpriteManagerTests : IDisposable
    {
        private string _testModPath;
        private string _testSourcePath;
        private ConfigurationManager _configManager;

        public ConfigBasedSpriteManagerTests()
        {
            // Reset the singleton to avoid test pollution
            CharacterServiceSingleton.Reset();

            // Create temporary test directories
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTColorCustomizerTest_" + Guid.NewGuid());
            _testSourcePath = Path.Combine(_testModPath, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(_testSourcePath);

            // Create the deployed mod path where sprites get copied to
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(deployedPath);

            // Setup config manager with test config
            var configPath = Path.Combine(_testModPath, "Config.json");
            _configManager = new ConfigurationManager(configPath);
        }

        public void Dispose()
        {
            // Clean up test directories
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }

            // Reset the singleton after tests
            CharacterServiceSingleton.Reset();
        }

        [Fact]
        public void ApplyConfiguration_Should_Use_CharacterDefinitionService()
        {
            // Arrange
            var service = new CharacterDefinitionService();
            service.AddCharacter(new CharacterDefinition
            {
                Name = "Agrias",
                SpriteNames = new[] { "aguri" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "ash_dark" },
                EnumType = "StoryCharacter"
            });

            var config = new Config
            {
                Agrias = "ash_dark"
            };
            _configManager.SaveConfig(config);

            // CRITICAL FIX: Create theme in deployed path, not source path
            // After the fix, ConfigBasedSpriteManager looks for themes in mod path
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var themeDir = Path.Combine(deployedPath, "sprites_agrias_ash_dark");
            Directory.CreateDirectory(themeDir);

            // Create a dummy sprite file in the theme directory
            var themeSpriteFile = Path.Combine(themeDir, "battle_aguri_spr.bin");
            File.WriteAllText(themeSpriteFile, "ash_dark_theme_content");

            // Create the destination directory
            var destDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(destDir);

            var sourceBasePath = Path.Combine(_testModPath, "ColorMod");
            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service, sourceBasePath);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert
            var expectedDestFile = Path.Combine(destDir, "battle_aguri_spr.bin");
            Assert.True(File.Exists(expectedDestFile), "Theme sprite should be copied to destination");

            var content = File.ReadAllText(expectedDestFile);
            Assert.Equal("ash_dark_theme_content", content);
        }

        [Fact]
        public void ApplyConfiguration_Should_GenerateUserThemeSprite_InBaseUnitFolder()
        {
            // Arrange
            var service = new CharacterDefinitionService();

            var config = new Config
            {
                Knight_Male = "Ocean Blue"  // User theme name
            };
            _configManager.SaveConfig(config);

            // Create original sprite in sprites_original directory
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var originalDir = Path.Combine(deployedPath, "sprites_original");
            Directory.CreateDirectory(originalDir);

            // Create a minimal sprite file with known palette data (512 bytes palette + some sprite data)
            var originalSprite = new byte[1024];
            for (int i = 0; i < 512; i++)
            {
                originalSprite[i] = 0xFF; // Original palette: all 0xFF
            }
            for (int i = 512; i < 1024; i++)
            {
                originalSprite[i] = 0xAA; // Sprite data: all 0xAA
            }
            var originalSpriteFile = Path.Combine(originalDir, "battle_knight_m_spr.bin");
            File.WriteAllBytes(originalSpriteFile, originalSprite);

            // Create user theme with custom palette
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "Knight_Male", "Ocean Blue");
            Directory.CreateDirectory(userThemesDir);
            var userPalette = new byte[512];
            for (int i = 0; i < 512; i++)
            {
                userPalette[i] = 0x42; // User palette: all 0x42
            }
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), userPalette);

            // Create user themes registry
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Knight_Male\":[\"Ocean Blue\"]}");

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert - Generated sprite should be in BASE unit folder (for FFTPack compatibility)
            var generatedSpriteFile = Path.Combine(deployedPath, "battle_knight_m_spr.bin");
            Assert.True(File.Exists(generatedSpriteFile), "User theme sprite should be generated in base unit folder");

            var resultSprite = File.ReadAllBytes(generatedSpriteFile);

            // Palette should be from user theme (0x42)
            Assert.Equal(0x42, resultSprite[0]);
            Assert.Equal(0x42, resultSprite[255]);
            Assert.Equal(0x42, resultSprite[511]);

            // Sprite data should be preserved from original (0xAA)
            Assert.Equal(0xAA, resultSprite[512]);
            Assert.Equal(0xAA, resultSprite[1023]);

            // Original sprite in sprites_original should be UNCHANGED
            var originalAfter = File.ReadAllBytes(originalSpriteFile);
            Assert.Equal(0xFF, originalAfter[0]); // Palette unchanged
            Assert.Equal(0xAA, originalAfter[512]); // Sprite data unchanged
        }

        [Fact]
        public void InterceptFilePath_Should_ReturnOriginalPath_WhenUserThemeSelected()
        {
            // Arrange
            // User themes are now written directly to the base unit folder by ApplyConfiguration,
            // so InterceptFilePath doesn't need to redirect - the file is already in the right place.
            var service = new CharacterDefinitionService();

            var config = new Config
            {
                Knight_Male = "Ocean Blue"  // User theme name
            };
            _configManager.SaveConfig(config);

            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(deployedPath);

            // Create the user theme sprite file in the base folder (as if ApplyConfiguration already ran)
            var userThemeSpriteFile = Path.Combine(deployedPath, "battle_knight_m_spr.bin");
            File.WriteAllBytes(userThemeSpriteFile, new byte[] { 0x42 });

            // Create user themes registry
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Knight_Male\":[\"Ocean Blue\"]}");

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service);

            // Act
            var originalPath = Path.Combine(deployedPath, "battle_knight_m_spr.bin");
            var interceptedPath = spriteManager.InterceptFilePath(originalPath);

            // Assert - Should return original path since file is already in base folder
            Assert.Equal(originalPath, interceptedPath);
        }

        [Fact]
        public void ApplyConfiguration_Should_CreateUserThemeSpriteFile_InBaseUnitFolder()
        {
            // Arrange
            var service = new CharacterDefinitionService();

            var config = new Config
            {
                Knight_Male = "Ocean Blue"  // User theme name
            };
            _configManager.SaveConfig(config);

            // Create original sprite in sprites_original directory
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var originalDir = Path.Combine(deployedPath, "sprites_original");
            Directory.CreateDirectory(originalDir);

            // Create a minimal sprite file with known palette data (512 bytes palette + some sprite data)
            var originalSprite = new byte[1024];
            for (int i = 0; i < 512; i++)
            {
                originalSprite[i] = 0xFF; // Original palette: all 0xFF
            }
            for (int i = 512; i < 1024; i++)
            {
                originalSprite[i] = 0xAA; // Sprite data: all 0xAA
            }
            var originalSpriteFile = Path.Combine(originalDir, "battle_knight_m_spr.bin");
            File.WriteAllBytes(originalSpriteFile, originalSprite);

            // Create user theme with custom palette
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "Knight_Male", "Ocean Blue");
            Directory.CreateDirectory(userThemesDir);
            var userPalette = new byte[512];
            for (int i = 0; i < 512; i++)
            {
                userPalette[i] = 0x42; // User palette: all 0x42
            }
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), userPalette);

            // Create user themes registry
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Knight_Male\":[\"Ocean Blue\"]}");

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert - Generated sprite should be in BASE unit folder (for FFTPack compatibility)
            var generatedSpriteFile = Path.Combine(deployedPath, "battle_knight_m_spr.bin");

            Assert.True(File.Exists(generatedSpriteFile), $"User theme sprite should be generated at: {generatedSpriteFile}");

            var resultSprite = File.ReadAllBytes(generatedSpriteFile);

            // Palette should be from user theme (0x42)
            Assert.Equal(0x42, resultSprite[0]);
            Assert.Equal(0x42, resultSprite[255]);
            Assert.Equal(0x42, resultSprite[511]);

            // Sprite data should be preserved from original (0xAA)
            Assert.Equal(0xAA, resultSprite[512]);
        }

        [Fact]
        public void ApplyUserTheme_Should_CopyToBaseUnitFolder_NotSubdirectory()
        {
            // Arrange
            // This test verifies the FIX: user theme sprites must be copied to the BASE unit folder
            // (just like regular themes) so the FFTPack mod loader can find them.
            // Previously, user themes were written to sprites_user_* subdirectories which the
            // mod loader didn't use for the main file mapping.
            var service = new CharacterDefinitionService();

            var config = new Config
            {
                Knight_Male = "Ocean Blue"
            };
            _configManager.SaveConfig(config);

            // Create original sprite in sprites_original directory
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var originalDir = Path.Combine(deployedPath, "sprites_original");
            Directory.CreateDirectory(originalDir);

            var originalSprite = new byte[1024];
            for (int i = 0; i < 512; i++)
            {
                originalSprite[i] = 0xFF;
            }
            for (int i = 512; i < 1024; i++)
            {
                originalSprite[i] = 0xAA;
            }
            File.WriteAllBytes(Path.Combine(originalDir, "battle_knight_m_spr.bin"), originalSprite);

            // Create user theme
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "Knight_Male", "Ocean Blue");
            Directory.CreateDirectory(userThemesDir);
            var userPalette = new byte[512];
            for (int i = 0; i < 512; i++)
            {
                userPalette[i] = 0x42;
            }
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), userPalette);

            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Knight_Male\":[\"Ocean Blue\"]}");

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert - The sprite should be in the BASE unit folder (not a subdirectory)
            // This is where FFTPack looks for files when the game requests them
            var baseUnitFile = Path.Combine(deployedPath, "battle_knight_m_spr.bin");
            Assert.True(File.Exists(baseUnitFile),
                $"User theme sprite MUST be in base unit folder for FFTPack: {baseUnitFile}");

            var resultSprite = File.ReadAllBytes(baseUnitFile);

            // Verify the palette was applied (user theme palette, not original)
            Assert.Equal(0x42, resultSprite[0]);
            Assert.Equal(0x42, resultSprite[511]);

            // Verify sprite data preserved
            Assert.Equal(0xAA, resultSprite[512]);
        }

        [Fact]
        public void ApplyConfiguration_Should_ApplyUserTheme_ForBlackMage()
        {
            // Arrange
            // This test verifies that compound job names like BlackMage_Male work correctly.
            // The bug: ConvertJobTypeToJobName converts "blackmage" to "Blackmage_Male" (ToTitleCase)
            // but the registry stores themes under "BlackMage_Male" (with capital M in Mage).
            var service = new CharacterDefinitionService();

            var config = new Config
            {
                BlackMage_Male = "Dark Fire"  // User theme name
            };
            _configManager.SaveConfig(config);

            // Create original sprite in sprites_original directory
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var originalDir = Path.Combine(deployedPath, "sprites_original");
            Directory.CreateDirectory(originalDir);

            var originalSprite = new byte[1024];
            for (int i = 0; i < 512; i++)
            {
                originalSprite[i] = 0xFF; // Original palette
            }
            for (int i = 512; i < 1024; i++)
            {
                originalSprite[i] = 0xAA; // Sprite data
            }
            // BlackMage sprite name is battle_kuro_m_spr.bin
            File.WriteAllBytes(Path.Combine(originalDir, "battle_kuro_m_spr.bin"), originalSprite);

            // Create user theme with custom palette - stored under "BlackMage_Male" (capital M)
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "BlackMage_Male", "Dark Fire");
            Directory.CreateDirectory(userThemesDir);
            var userPalette = new byte[512];
            for (int i = 0; i < 512; i++)
            {
                userPalette[i] = 0x42; // User palette
            }
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), userPalette);

            // Registry uses "BlackMage_Male" (capital M in Mage)
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"BlackMage_Male\":[\"Dark Fire\"]}");

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert - The user theme should be applied (palette replaced)
            var generatedSpriteFile = Path.Combine(deployedPath, "battle_kuro_m_spr.bin");
            Assert.True(File.Exists(generatedSpriteFile),
                $"BlackMage user theme sprite should be generated at: {generatedSpriteFile}");

            var resultSprite = File.ReadAllBytes(generatedSpriteFile);

            // Palette should be from user theme (0x42), not original (0xFF)
            Assert.Equal(0x42, resultSprite[0]);
            Assert.Equal(0x42, resultSprite[511]);

            // Sprite data should be preserved
            Assert.Equal(0xAA, resultSprite[512]);
        }

        [Fact]
        public void ApplyConfiguration_Should_ApplyUserTheme_ForTimeMage()
        {
            // Arrange
            // Same bug as BlackMage - TimeMage_Male stored in registry but lookup uses Timemage_Male
            var service = new CharacterDefinitionService();

            var config = new Config
            {
                TimeMage_Male = "Chrono"  // User theme name
            };
            _configManager.SaveConfig(config);

            // Create original sprite in sprites_original directory
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var originalDir = Path.Combine(deployedPath, "sprites_original");
            Directory.CreateDirectory(originalDir);

            var originalSprite = new byte[1024];
            for (int i = 0; i < 512; i++)
            {
                originalSprite[i] = 0xFF; // Original palette
            }
            for (int i = 512; i < 1024; i++)
            {
                originalSprite[i] = 0xAA; // Sprite data
            }
            // TimeMage sprite name is battle_toki_m_spr.bin
            File.WriteAllBytes(Path.Combine(originalDir, "battle_toki_m_spr.bin"), originalSprite);

            // Create user theme - stored under "TimeMage_Male" (capital M)
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "TimeMage_Male", "Chrono");
            Directory.CreateDirectory(userThemesDir);
            var userPalette = new byte[512];
            for (int i = 0; i < 512; i++)
            {
                userPalette[i] = 0x42; // User palette
            }
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), userPalette);

            // Registry uses "TimeMage_Male" (capital M in Mage)
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"TimeMage_Male\":[\"Chrono\"]}");

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert - The user theme should be applied
            var generatedSpriteFile = Path.Combine(deployedPath, "battle_toki_m_spr.bin");
            Assert.True(File.Exists(generatedSpriteFile),
                $"TimeMage user theme sprite should be generated at: {generatedSpriteFile}");

            var resultSprite = File.ReadAllBytes(generatedSpriteFile);

            // Palette should be from user theme (0x42), not original (0xFF)
            Assert.Equal(0x42, resultSprite[0]);
            Assert.Equal(0x42, resultSprite[511]);

            // Sprite data should be preserved
            Assert.Equal(0xAA, resultSprite[512]);
        }

        [Fact]
        public void ApplyConfiguration_Should_ApplyUserTheme_ForWhiteMage()
        {
            // Arrange
            // Same bug as BlackMage/TimeMage - WhiteMage_Male stored but lookup uses Whitemage_Male
            var service = new CharacterDefinitionService();

            var config = new Config
            {
                WhiteMage_Male = "Holy Light"  // User theme name
            };
            _configManager.SaveConfig(config);

            // Create original sprite in sprites_original directory
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var originalDir = Path.Combine(deployedPath, "sprites_original");
            Directory.CreateDirectory(originalDir);

            var originalSprite = new byte[1024];
            for (int i = 0; i < 512; i++)
            {
                originalSprite[i] = 0xFF; // Original palette
            }
            for (int i = 512; i < 1024; i++)
            {
                originalSprite[i] = 0xAA; // Sprite data
            }
            // WhiteMage sprite name is battle_siro_m_spr.bin
            File.WriteAllBytes(Path.Combine(originalDir, "battle_siro_m_spr.bin"), originalSprite);

            // Create user theme - stored under "WhiteMage_Male" (capital M)
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "WhiteMage_Male", "Holy Light");
            Directory.CreateDirectory(userThemesDir);
            var userPalette = new byte[512];
            for (int i = 0; i < 512; i++)
            {
                userPalette[i] = 0x42; // User palette
            }
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), userPalette);

            // Registry uses "WhiteMage_Male" (capital M in Mage)
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"WhiteMage_Male\":[\"Holy Light\"]}");

            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert - The user theme should be applied
            var generatedSpriteFile = Path.Combine(deployedPath, "battle_siro_m_spr.bin");
            Assert.True(File.Exists(generatedSpriteFile),
                $"WhiteMage user theme sprite should be generated at: {generatedSpriteFile}");

            var resultSprite = File.ReadAllBytes(generatedSpriteFile);

            // Palette should be from user theme (0x42), not original (0xFF)
            Assert.Equal(0x42, resultSprite[0]);
            Assert.Equal(0x42, resultSprite[511]);

            // Sprite data should be preserved
            Assert.Equal(0xAA, resultSprite[512]);
        }

        [Theory]
        [InlineData("DarkKnight_Male", "spr_dst_bchr_ankoku_m_spr.bin")]
        [InlineData("DarkKnight_Female", "spr_dst_bchr_ankoku_w_spr.bin")]
        [InlineData("OnionKnight_Male", "spr_dst_bchr_tama_m_spr.bin")]
        [InlineData("OnionKnight_Female", "spr_dst_bchr_tama_w_spr.bin")]
        public void GetSpriteNameForJob_Should_Return_Correct_WotL_SpriteName(string jobProperty, string expectedSpriteName)
        {
            // Arrange - SpritePathResolver now handles sprite name resolution
            var pathResolver = new SpritePathResolver(_testModPath);

            // Act
            var result = pathResolver.GetSpriteNameForJob(jobProperty);

            // Assert
            Assert.Equal(expectedSpriteName, result);
        }

        [Fact]
        public void ApplyConfiguration_Should_ApplyUserTheme_ForStoryCharacter_Agrias()
        {
            // Arrange
            // This test verifies that user themes work for story characters (e.g., Agrias)
            // Story characters use ApplyStoryCharacterTheme which must check for user themes
            var service = new CharacterDefinitionService();
            service.AddCharacter(new CharacterDefinition
            {
                Name = "Agrias",
                SpriteNames = new[] { "aguri" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original" },
                EnumType = "StoryCharacter"
            });

            var config = new Config
            {
                Agrias = "Ocean Blue"  // User theme name
            };
            _configManager.SaveConfig(config);

            // Create original sprite in sprites_original directory
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var originalDir = Path.Combine(deployedPath, "sprites_original");
            Directory.CreateDirectory(originalDir);

            var originalSprite = new byte[1024];
            for (int i = 0; i < 512; i++)
            {
                originalSprite[i] = 0xFF; // Original palette
            }
            for (int i = 512; i < 1024; i++)
            {
                originalSprite[i] = 0xAA; // Sprite data
            }
            // Agrias sprite name is battle_aguri_spr.bin
            File.WriteAllBytes(Path.Combine(originalDir, "battle_aguri_spr.bin"), originalSprite);

            // Create user theme for Agrias
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "Agrias", "Ocean Blue");
            Directory.CreateDirectory(userThemesDir);
            var userPalette = new byte[512];
            for (int i = 0; i < 512; i++)
            {
                userPalette[i] = 0x42; // User palette
            }
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), userPalette);

            // Registry uses "Agrias" (character name, not job format)
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Agrias\":[\"Ocean Blue\"]}");

            var sourceBasePath = Path.Combine(_testModPath, "ColorMod");
            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service, sourceBasePath);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert - The user theme should be applied (palette replaced)
            var generatedSpriteFile = Path.Combine(deployedPath, "battle_aguri_spr.bin");
            Assert.True(File.Exists(generatedSpriteFile),
                $"Agrias user theme sprite should be generated at: {generatedSpriteFile}");

            var resultSprite = File.ReadAllBytes(generatedSpriteFile);

            // Palette should be from user theme (0x42), not original (0xFF)
            Assert.Equal(0x42, resultSprite[0]);
            Assert.Equal(0x42, resultSprite[511]);

            // Sprite data should be preserved
            Assert.Equal(0xAA, resultSprite[512]);
        }
    }
}
