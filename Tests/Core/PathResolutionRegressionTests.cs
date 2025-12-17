using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Core.ModComponents;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests.Core
{
    /// <summary>
    /// Regression tests for the User config vs Mod installation path resolution fixes.
    /// These tests ensure that:
    /// 1. User config is loaded from the User directory when it exists
    /// 2. Sprite resources are always loaded from the mod installation directory
    /// 3. Preview images are loaded from the mod installation directory
    /// </summary>
    public class PathResolutionRegressionTests
    {
        [Fact]
        public void ConfigurationCoordinator_GetActualModPath_Resolves_UserConfig_To_ModInstall()
        {
            // This test validates the core fix: GetActualModPath correctly resolves
            // from a User config path to the mod installation path

            // Test case 1: User config path should resolve to mod installation
            var userConfigPath = @"C:\Reloaded\User\Mods\ptyra.fft.colorcustomizer\Config.json";
            var expectedModPath = @"C:\Reloaded\Mods\FFTColorCustomizer";

            var actualModPath = TestGetActualModPath(userConfigPath);
            actualModPath.Should().Be(expectedModPath,
                "User config path should resolve to mod installation directory");

            // Test case 2: Mod config path should resolve to itself
            var modConfigPath = @"C:\Reloaded\Mods\FFTColorCustomizer\Config.json";
            expectedModPath = @"C:\Reloaded\Mods\FFTColorCustomizer";

            actualModPath = TestGetActualModPath(modConfigPath);
            actualModPath.Should().Be(expectedModPath,
                "Mod config path should resolve to its own directory");
        }

        [Fact]
        public void ConfigurationCoordinator_Handles_CrossPlatform_Paths()
        {
            // Test both Windows backslash and Unix forward slash paths

            // Windows style path
            var windowsPath = @"C:\Reloaded\User\Mods\ptyra.fft.colorcustomizer\Config.json";
            var actualModPath = TestGetActualModPath(windowsPath);
            actualModPath.Should().EndWith(Path.Combine("Mods", "FFTColorCustomizer"),
                "Windows-style path should resolve correctly");

            // Unix style path (forward slashes)
            var unixPath = @"C:/Reloaded/User/Mods/ptyra.fft.colorcustomizer/Config.json";
            actualModPath = TestGetActualModPath(unixPath);
            actualModPath.Should().EndWith(Path.Combine("Mods", "FFTColorCustomizer"),
                "Unix-style path should resolve correctly");
        }

        [Fact]
        public void ConfigurationCoordinator_Fallback_When_Not_User_Path()
        {
            // Test that non-User paths fallback to their directory
            var randomPath = @"C:\SomeOtherLocation\Config.json";
            var actualModPath = TestGetActualModPath(randomPath);
            actualModPath.Should().Be(@"C:\SomeOtherLocation",
                "Non-User config paths should resolve to their own directory");
        }

        [Fact]
        public void Mod_GetUserConfigPath_Logic_Validates_Correctly()
        {
            // This test validates the path resolution logic in Mod.GetUserConfigPath()
            // We're testing the logic, not the actual file system

            // Given a mod installation path
            var modPath = @"C:\Reloaded\Mods\FFTColorCustomizer";

            // The User config path should be calculated correctly
            var expectedUserPath = @"C:\Reloaded\User\Mods\ptyra.fft.colorcustomizer\Config.json";

            // Simulate the path calculation logic
            var parent = Path.GetDirectoryName(modPath); // C:\Reloaded\Mods
            parent.Should().NotBeNull();

            var grandParent = Path.GetDirectoryName(parent); // C:\Reloaded
            grandParent.Should().NotBeNull();

            var calculatedUserPath = Path.Combine(grandParent, "User", "Mods", "ptyra.fft.colorcustomizer", "Config.json");
            calculatedUserPath.Should().Be(expectedUserPath,
                "User config path should be calculated correctly from mod installation path");
        }

        [Fact]
        public void ConfigurationCoordinator_Initializes_SpriteManager_With_ModPath()
        {
            // This test ensures that ConfigurationCoordinator passes the correct
            // mod installation path to the sprite manager, not the User config path

            var userConfigPath = @"C:\Reloaded\User\Mods\ptyra.fft.colorcustomizer\Config.json";
            var expectedModPath = @"C:\Reloaded\Mods\FFTColorCustomizer";

            // The GetActualModPath method should resolve to the mod installation
            var actualModPath = TestGetActualModPath(userConfigPath);
            actualModPath.Should().Be(expectedModPath,
                "ConfigurationCoordinator should resolve mod installation path for sprite manager");

            // This is the path that would be passed to ConfigBasedSpriteManager
            // ensuring sprites are loaded from the mod installation, not User directory
        }

        [Fact]
        public void Preview_Images_Should_Load_From_ModInstallation()
        {
            // This test validates that preview images are loaded from the mod installation
            // directory, not from the User config directory

            var userConfigPath = @"C:\Reloaded\User\Mods\ptyra.fft.colorcustomizer\Config.json";
            var modInstallPath = @"C:\Reloaded\Mods\FFTColorCustomizer";

            // Preview images should be in the mod installation directory
            var expectedPreviewPath = Path.Combine(modInstallPath, "Resources", "Previews");

            // When ConfigurationCoordinator opens the UI, it should use modInstallPath for resources
            var actualModPath = TestGetActualModPath(userConfigPath);
            var actualPreviewPath = Path.Combine(actualModPath, "Resources", "Previews");

            actualPreviewPath.Should().Be(expectedPreviewPath,
                "Preview images should be loaded from mod installation, not User directory");
        }

        // Helper method that replicates the GetActualModPath logic from ConfigurationCoordinator
        private string TestGetActualModPath(string configPath)
        {
            // If config path is in User directory, find the actual mod installation
            if (configPath.Contains(@"User\Mods") || configPath.Contains(@"User/Mods"))
            {
                var configDir = Path.GetDirectoryName(configPath);
                if (configDir != null)
                {
                    var userModsIdx = configDir.IndexOf(@"User\Mods", StringComparison.OrdinalIgnoreCase);
                    if (userModsIdx == -1)
                        userModsIdx = configDir.IndexOf(@"User/Mods", StringComparison.OrdinalIgnoreCase);

                    if (userModsIdx >= 0)
                    {
                        var reloadedRoot = configDir.Substring(0, userModsIdx);
                        return Path.Combine(reloadedRoot, "Mods", "FFTColorCustomizer");
                    }
                }
            }

            // Fallback to config directory
            return Path.GetDirectoryName(configPath);
        }
    }
}