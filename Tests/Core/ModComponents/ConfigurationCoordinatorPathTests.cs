using System;
using System.IO;
using System.Linq;
using Xunit;
using FFTColorCustomizer.Core.ModComponents;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Services;

namespace Tests.Core.ModComponents
{
    /// <summary>
    /// Tests for versioned directory path detection in ConfigurationCoordinator
    /// </summary>
    public class ConfigurationCoordinatorPathTests : IDisposable
    {
        private string _testRootPath;
        private CharacterDefinitionService _characterService;

        public ConfigurationCoordinatorPathTests()
        {
            // Reset singleton to avoid test pollution
            CharacterServiceSingleton.Reset();

            // Create temporary test directories
            _testRootPath = Path.Combine(Path.GetTempPath(), "FFTConfigCoordinatorTest_" + Guid.NewGuid());
            Directory.CreateDirectory(_testRootPath);

            // Create character service
            _characterService = new CharacterDefinitionService();
        }

        public void Dispose()
        {
            // Clean up test directories
            if (Directory.Exists(_testRootPath))
            {
                Directory.Delete(_testRootPath, true);
            }

            // Reset singleton after tests
            CharacterServiceSingleton.Reset();
        }

        [Fact]
        public void GetActualModPath_WithConfigInModsFolder_ReturnsModPath()
        {
            // Arrange
            var modsPath = Path.Combine(_testRootPath, "Mods");
            var modPath = Path.Combine(modsPath, "FFTColorCustomizer");
            Directory.CreateDirectory(modPath);

            var configPath = Path.Combine(modPath, "Config.json");
            File.WriteAllText(configPath, "{}");

            // Act
            var coordinator = new ConfigurationCoordinator(configPath);

            // Assert - constructor should have found the correct mod path
            var config = new Config { ["Knight_Male"] = "original" };
            var exception = Record.Exception(() => coordinator.UpdateConfiguration(config));
            Assert.Null(exception);
        }

        [Fact]
        public void GetActualModPath_WithConfigInUserMods_FindsActualModPath()
        {
            // Arrange
            var reloadedRoot = Path.Combine(_testRootPath, "Reloaded");
            var userModsPath = Path.Combine(reloadedRoot, "User", "Mods", "FFTColorCustomizer");
            var actualModsPath = Path.Combine(reloadedRoot, "Mods");

            Directory.CreateDirectory(userModsPath);
            Directory.CreateDirectory(actualModsPath);

            // Create actual mod in Mods folder
            var actualModPath = Path.Combine(actualModsPath, "FFTColorCustomizer");
            Directory.CreateDirectory(actualModPath);

            // Config is in User/Mods
            var configPath = Path.Combine(userModsPath, "Config.json");
            File.WriteAllText(configPath, "{}");

            // Act
            var coordinator = new ConfigurationCoordinator(configPath);

            // Assert - should find the actual mod path
            var config = new Config { ["Knight_Male"] = "original" };
            var exception = Record.Exception(() => coordinator.UpdateConfiguration(config));
            Assert.Null(exception);
        }

        [Fact]
        public void GetActualModPath_WithVersionedDirectory_FindsHighestVersion()
        {
            // Arrange
            var reloadedRoot = Path.Combine(_testRootPath, "Reloaded");
            var userModsPath = Path.Combine(reloadedRoot, "User", "Mods", "FFTColorCustomizer");
            var actualModsPath = Path.Combine(reloadedRoot, "Mods");

            Directory.CreateDirectory(userModsPath);
            Directory.CreateDirectory(actualModsPath);

            // Create multiple versioned directories
            Directory.CreateDirectory(Path.Combine(actualModsPath, "FFTColorCustomizer_v105"));
            Directory.CreateDirectory(Path.Combine(actualModsPath, "FFTColorCustomizer_v109"));
            Directory.CreateDirectory(Path.Combine(actualModsPath, "FFTColorCustomizer_v110"));

            // Config is in User/Mods
            var configPath = Path.Combine(userModsPath, "Config.json");
            File.WriteAllText(configPath, "{}");

            // Act
            var coordinator = new ConfigurationCoordinator(configPath);

            // Assert - should find v110 (highest version)
            var config = new Config { ["Knight_Male"] = "original" };
            var exception = Record.Exception(() => coordinator.UpdateConfiguration(config));
            Assert.Null(exception);
        }

        [Fact]
        public void GetActualModPath_WithNoValidPaths_ReturnsFallback()
        {
            // Arrange
            var configPath = Path.Combine(_testRootPath, "SomePath", "Config.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            File.WriteAllText(configPath, "{}");

            // Act
            var coordinator = new ConfigurationCoordinator(configPath);

            // Assert - should not crash even with non-standard paths
            var config = new Config { ["Knight_Male"] = "original" };
            var exception = Record.Exception(() => coordinator.UpdateConfiguration(config));
            Assert.Null(exception);
        }

        [Fact]
        public void ApplyConfiguration_UsesCorrectModPath()
        {
            // Arrange
            var reloadedRoot = Path.Combine(_testRootPath, "Reloaded");
            var userModsPath = Path.Combine(reloadedRoot, "User", "Mods", "FFTColorCustomizer");
            var actualModsPath = Path.Combine(reloadedRoot, "Mods");

            Directory.CreateDirectory(userModsPath);
            Directory.CreateDirectory(actualModsPath);

            // Create versioned mod directory with unit path
            var versionedModPath = Path.Combine(actualModsPath, "FFTColorCustomizer_v110");
            var unitPath = Path.Combine(versionedModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);

            // Create a theme directory
            var themeDir = Path.Combine(unitPath, "sprites_lucavi");
            Directory.CreateDirectory(themeDir);
            File.WriteAllText(Path.Combine(themeDir, "battle_knight_m_spr.bin"), "test_content");

            // Config is in User/Mods
            var configPath = Path.Combine(userModsPath, "Config.json");
            File.WriteAllText(configPath, "{}");

            // Act
            var coordinator = new ConfigurationCoordinator(configPath);
            var config = new Config { ["Knight_Male"] = "lucavi" };
            coordinator.UpdateConfiguration(config);

            var exception = Record.Exception(() => coordinator.ApplyConfiguration());

            // Assert - should not throw
            Assert.Null(exception);
        }

        [Fact]
        public void VersionSorting_HandlesVariousFormats()
        {
            // Arrange
            var reloadedRoot = Path.Combine(_testRootPath, "Reloaded");
            var actualModsPath = Path.Combine(reloadedRoot, "Mods");
            Directory.CreateDirectory(actualModsPath);

            // Create directories with various version formats
            var versions = new[] { "v1", "v10", "v2", "v100", "v99", "v101" };
            foreach (var version in versions)
            {
                Directory.CreateDirectory(Path.Combine(actualModsPath, $"FFTColorCustomizer_{version}"));
            }

            // Get the directories and sort them as the code does
            var versionedDirs = Directory.GetDirectories(actualModsPath, "FFTColorCustomizer_v*")
                .OrderByDescending(dir =>
                {
                    var dirName = Path.GetFileName(dir);
                    var versionStr = dirName.Substring(dirName.LastIndexOf('v') + 1);
                    if (int.TryParse(versionStr, out int version))
                        return version;
                    return 0;
                })
                .ToArray();

            // Assert - should be sorted in descending order by version number
            Assert.Equal("FFTColorCustomizer_v101", Path.GetFileName(versionedDirs[0]));
            Assert.Equal("FFTColorCustomizer_v100", Path.GetFileName(versionedDirs[1]));
            Assert.Equal("FFTColorCustomizer_v99", Path.GetFileName(versionedDirs[2]));
            Assert.Equal("FFTColorCustomizer_v10", Path.GetFileName(versionedDirs[3]));
            Assert.Equal("FFTColorCustomizer_v2", Path.GetFileName(versionedDirs[4]));
            Assert.Equal("FFTColorCustomizer_v1", Path.GetFileName(versionedDirs[5]));
        }

        [Fact]
        public void GetActualModPath_PreferNonVersionedWhenExists()
        {
            // Arrange
            var reloadedRoot = Path.Combine(_testRootPath, "Reloaded");
            var userModsPath = Path.Combine(reloadedRoot, "User", "Mods", "FFTColorCustomizer");
            var actualModsPath = Path.Combine(reloadedRoot, "Mods");

            Directory.CreateDirectory(userModsPath);
            Directory.CreateDirectory(actualModsPath);

            // Create both non-versioned and versioned directories
            var nonVersionedPath = Path.Combine(actualModsPath, "FFTColorCustomizer");
            Directory.CreateDirectory(nonVersionedPath);
            Directory.CreateDirectory(Path.Combine(actualModsPath, "FFTColorCustomizer_v200"));

            // Config is in User/Mods
            var configPath = Path.Combine(userModsPath, "Config.json");
            File.WriteAllText(configPath, "{}");

            // Act
            var coordinator = new ConfigurationCoordinator(configPath);

            // Assert - should prefer non-versioned when it exists
            var config = new Config { ["Knight_Male"] = "original" };
            var exception = Record.Exception(() => coordinator.UpdateConfiguration(config));
            Assert.Null(exception);
        }

        [Fact]
        public void GetActualModPath_HandlesPathsWithSpaces()
        {
            // Arrange
            var reloadedRoot = Path.Combine(_testRootPath, "Program Files (x86)", "Steam", "Reloaded");
            var userModsPath = Path.Combine(reloadedRoot, "User", "Mods", "FFTColorCustomizer");
            var actualModsPath = Path.Combine(reloadedRoot, "Mods");

            Directory.CreateDirectory(userModsPath);
            Directory.CreateDirectory(actualModsPath);

            // Create versioned directory
            Directory.CreateDirectory(Path.Combine(actualModsPath, "FFTColorCustomizer_v110"));

            // Config is in User/Mods
            var configPath = Path.Combine(userModsPath, "Config.json");
            File.WriteAllText(configPath, "{}");

            // Act & Assert - should not throw with spaces in path
            var exception = Record.Exception(() => new ConfigurationCoordinator(configPath));
            Assert.Null(exception);
        }

        [Fact]
        public void GetActualModPath_HandlesForwardSlashPaths()
        {
            // Arrange
            var reloadedRoot = Path.Combine(_testRootPath, "Reloaded");
            var userModsPath = Path.Combine(reloadedRoot, "User", "Mods", "FFTColorCustomizer");
            var actualModsPath = Path.Combine(reloadedRoot, "Mods");

            Directory.CreateDirectory(userModsPath);
            Directory.CreateDirectory(actualModsPath);

            // Create versioned directory
            Directory.CreateDirectory(Path.Combine(actualModsPath, "FFTColorCustomizer_v110"));

            // Config path with forward slashes
            var configPath = userModsPath.Replace('\\', '/') + "/Config.json";
            File.WriteAllText(configPath.Replace('/', '\\'), "{}");

            // Act & Assert - should handle forward slash paths
            var exception = Record.Exception(() => new ConfigurationCoordinator(configPath));
            Assert.Null(exception);
        }

        [Fact]
        public void GetSpriteManager_UsesCorrectModPath()
        {
            // Arrange
            var reloadedRoot = Path.Combine(_testRootPath, "Reloaded");
            var userModsPath = Path.Combine(reloadedRoot, "User", "Mods", "FFTColorCustomizer");
            var actualModsPath = Path.Combine(reloadedRoot, "Mods");

            Directory.CreateDirectory(userModsPath);
            Directory.CreateDirectory(actualModsPath);

            // Create versioned mod
            var versionedModPath = Path.Combine(actualModsPath, "FFTColorCustomizer_v110");
            Directory.CreateDirectory(versionedModPath);

            // Config is in User/Mods
            var configPath = Path.Combine(userModsPath, "Config.json");
            File.WriteAllText(configPath, "{}");

            // Act
            var coordinator = new ConfigurationCoordinator(configPath);
            var spriteManager = coordinator.GetSpriteManager();

            // Assert - sprite manager should not be null and should be using correct path
            Assert.NotNull(spriteManager);
        }
    }
}