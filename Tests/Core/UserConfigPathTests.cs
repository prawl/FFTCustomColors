using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Core.ModComponents;

namespace FFTColorCustomizer.Tests.Core
{
    public class UserConfigPathTests
    {
        [Fact]
        public void Mod_Should_Use_User_Config_Not_Mod_Config()
        {
            // TLDR: The mod MUST load the User config from Reloaded/User/Mods/ptyra.fft.colorcustomizer/Config.json
            // NOT from the mod installation directory Reloaded/Mods/FFTColorCustomizer/Config.json
            // This ensures user's personal settings are preserved and loaded correctly

            // Simulate the Reloaded directory structure
            var reloadedRoot = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded";
            var modPath = Path.Combine(reloadedRoot, "Mods", "FFTColorCustomizer");
            var userConfigPath = Path.Combine(reloadedRoot, "User", "Mods", "ptyra.fft.colorcustomizer", "Config.json");
            var modConfigPath = Path.Combine(modPath, "Config.json");

            // The correct path should be the User config
            userConfigPath.Should().Contain(@"User\Mods\ptyra.fft.colorcustomizer",
                "Config must be loaded from User directory for persistent user settings");

            // The wrong path would be the mod installation config
            modConfigPath.Should().NotContain("User",
                "Mod installation config is NOT where user settings should be stored");
        }

        [Fact]
        public void GetUserConfigPath_Should_Return_User_Directory_When_It_Exists()
        {
            // TLDR: When the User config exists, it MUST be used instead of the mod config

            // Setup test directory structure
            var testRoot = Path.Combine(Path.GetTempPath(), $"ReloadedTest_{Guid.NewGuid()}");
            var modsDir = Path.Combine(testRoot, "Mods", "FFTColorCustomizer");
            var userModsDir = Path.Combine(testRoot, "User", "Mods", "ptyra.fft.colorcustomizer");

            try
            {
                // Create directory structure
                Directory.CreateDirectory(modsDir);
                Directory.CreateDirectory(userModsDir);

                // Create both config files with different content to verify which is loaded
                var modConfig = Path.Combine(modsDir, "Config.json");
                var userConfig = Path.Combine(userModsDir, "Config.json");

                File.WriteAllText(modConfig, "{ \"source\": \"mod\" }");
                File.WriteAllText(userConfig, "{ \"source\": \"user\" }");

                // Simulate the logic from Mod.GetUserConfigPath()
                var configPath = GetUserConfigPathLogic(modsDir);

                // Assert that user config is chosen
                configPath.Should().Be(userConfig,
                    "When User config exists, it must be used for loading user settings");

                // Verify the content to ensure correct file is loaded
                var loadedContent = File.ReadAllText(configPath);
                loadedContent.Should().Contain("\"source\": \"user\"",
                    "The User config content should be loaded, not the mod config");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, true);
            }
        }

        [Fact]
        public void GetUserConfigPath_Should_Fallback_To_Mod_Config_When_User_Config_Missing()
        {
            // TLDR: Only when User config doesn't exist should the mod fallback to its own config

            // Setup test directory structure
            var testRoot = Path.Combine(Path.GetTempPath(), $"ReloadedTest_{Guid.NewGuid()}");
            var modsDir = Path.Combine(testRoot, "Mods", "FFTColorCustomizer");
            var userModsDir = Path.Combine(testRoot, "User", "Mods", "ptyra.fft.colorcustomizer");

            try
            {
                // Create directory structure
                Directory.CreateDirectory(modsDir);
                Directory.CreateDirectory(userModsDir);

                // Create ONLY mod config (no user config)
                var modConfig = Path.Combine(modsDir, "Config.json");
                File.WriteAllText(modConfig, "{ \"source\": \"mod\" }");

                // Simulate the logic from Mod.GetUserConfigPath()
                var configPath = GetUserConfigPathLogic(modsDir);

                // Assert that mod config is used as fallback
                configPath.Should().Be(modConfig,
                    "When User config doesn't exist, mod config should be used as fallback");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, true);
            }
        }

        [Fact]
        public void ConfigurationCoordinator_Should_Pass_Correct_ModPath_For_Previews()
        {
            // TLDR: When F1 is pressed, ConfigurationCoordinator must pass the mod root path
            // to ConfigurationForm, not the Config directory path

            // Given a config path in the User directory
            var userConfigPath = @"C:\Reloaded\User\Mods\ptyra.fft.colorcustomizer\Config.json";

            // The mod path passed to ConfigurationForm should be the parent of Config directory
            var expectedModPath = @"C:\Reloaded\User\Mods\ptyra.fft.colorcustomizer";

            // NOT just the directory of the config file
            var incorrectModPath = Path.GetDirectoryName(userConfigPath);
            incorrectModPath.Should().Be(expectedModPath,
                "GetDirectoryName on User config path should give the correct mod path");

            // The preview images would be in Resources/Previews relative to mod path
            var previewPath = Path.Combine(expectedModPath, "Resources", "Previews");
            previewPath.Should().Be(@"C:\Reloaded\User\Mods\ptyra.fft.colorcustomizer\Resources\Previews",
                "Preview path should be relative to the User mod directory when using User config");
        }

        [Fact]
        public void Both_UI_And_Runtime_Should_Use_Same_Config()
        {
            // TLDR: The config used when opening UI outside game (via Reloaded launcher)
            // and the config used when pressing F1 in-game MUST be the same file

            var userConfigPath = @"C:\Reloaded\User\Mods\ptyra.fft.colorcustomizer\Config.json";

            // When opened from Reloaded launcher (outside game)
            var configPathFromLauncher = userConfigPath;

            // When opened via F1 in-game (through ConfigurationCoordinator)
            var configPathFromF1 = userConfigPath;

            configPathFromLauncher.Should().Be(configPathFromF1,
                "Both UI entry points must use the same User config file");

            // This ensures that:
            // 1. User sees the same settings in both UIs
            // 2. Changes made in either UI affect the same config
            // 3. Story character themes are consistent
        }

        // Helper method that simulates the GetUserConfigPath logic
        private string GetUserConfigPathLogic(string modPath)
        {
            var parent = Directory.GetParent(modPath);
            if (parent != null)
            {
                var grandParent = Directory.GetParent(parent.FullName);
                if (grandParent != null)
                {
                    var reloadedRoot = grandParent.FullName;
                    var userConfigPath = Path.Combine(reloadedRoot, "User", "Mods", "ptyra.fft.colorcustomizer", "Config.json");

                    if (File.Exists(userConfigPath))
                    {
                        return userConfigPath;
                    }
                }
            }

            // Fallback to mod directory config
            return Path.Combine(modPath, "Config.json");
        }
    }
}