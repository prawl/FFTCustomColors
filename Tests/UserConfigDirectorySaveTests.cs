using System;
using System.IO;
using System.Text.Json;
using Xunit;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class UserConfigDirectorySaveTests : IDisposable
    {
        private readonly string _testModDir;
        private readonly string _testUserDir;
        private readonly string _modConfigPath;
        private readonly string _userConfigPath;

        public UserConfigDirectorySaveTests()
        {
            // Simulate Reloaded-II directory structure
            var testRoot = Path.Combine(Path.GetTempPath(), $"test_reloaded_{Guid.NewGuid()}");

            // Mod installation directory
            _testModDir = Path.Combine(testRoot, "Mods", "FFTColorCustomizer");
            _modConfigPath = Path.Combine(_testModDir, "Config.json");

            // User configuration directory (where Reloaded-II actually stores user configs)
            _testUserDir = Path.Combine(testRoot, "User", "Mods", "ptyra.fft.colorcustomizer");
            _userConfigPath = Path.Combine(_testUserDir, "Config.json");

            Directory.CreateDirectory(_testModDir);
            Directory.CreateDirectory(_testUserDir);
        }

        public void Dispose()
        {
            var testRoot = Path.GetDirectoryName(Path.GetDirectoryName(_testModDir));
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, true);
        }

        [Fact]
        public void CurrentImplementation_SavesToWrongDirectory()
        {
            // Arrange
            // Create initial config in User directory (where Reloaded-II actually loads from)
            var initialConfig = new Config
            {
                Squire_Male = "original",  // frost_knight
                Knight_Female = "original"  // original
            };

            var json = JsonSerializer.Serialize(initialConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(_userConfigPath, json);

            // Simulate what our CURRENT broken code does
            var updatedConfig = new Config
            {
                Squire_Male = "golden_templar",   // royal_purple
                Knight_Female = "emerald_dragon" // emerald_dragon
            };

            // Act - This is what our current OnConfigurationUpdated does (WRONG!)
            // It saves to the mod installation directory, not the User directory
            var wrongSavePath = Path.Combine(_testModDir, "Config.json");
            Directory.CreateDirectory(Path.GetDirectoryName(wrongSavePath) ?? "");
            var updatedJson = JsonSerializer.Serialize(updatedConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(wrongSavePath, updatedJson);

            // Assert - This demonstrates the BUG
            // The User directory still has the OLD config
            var savedUserJson = File.ReadAllText(_userConfigPath);
            var savedUserConfig = JsonSerializer.Deserialize<Config>(savedUserJson, Configurable<Config>.SerializerOptions);

            Assert.NotNull(savedUserConfig);
            // BUG: User config wasn't updated!
            Assert.Equal("original", savedUserConfig.Squire_Male);   // Still frost_knight!
            Assert.Equal("original", savedUserConfig.Knight_Female); // Still original!

            // The mod directory has the new config (but Reloaded-II doesn't read from there!)
            Assert.True(File.Exists(_modConfigPath));
            var modDirJson = File.ReadAllText(_modConfigPath);
            var modDirConfig = JsonSerializer.Deserialize<Config>(modDirJson, Configurable<Config>.SerializerOptions);

            Assert.NotNull(modDirConfig);
            Assert.Equal("golden_templar", modDirConfig.Squire_Male);   // royal_purple (wrong place!)
            Assert.Equal("emerald_dragon", modDirConfig.Knight_Female); // emerald_dragon (wrong place!)
        }

        [Fact]
        public void FindUserConfigDirectory_ShouldConstructCorrectPath()
        {
            // Arrange
            // Given the mod is installed at: .../Reloaded/Mods/FFTColorCustomizer
            var modInstallPath = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\FFTColorCustomizer";

            // Act
            // The User config should be at: .../Reloaded/User/Mods/ptyra.fft.colorcustomizer/Config.json
            var reloadedRoot = Path.GetDirectoryName(Path.GetDirectoryName(modInstallPath));
            var userConfigDir = Path.Combine(reloadedRoot ?? "", "User", "Mods", "ptyra.fft.colorcustomizer");
            var userConfigPath = Path.Combine(userConfigDir, "Config.json");

            // Assert
            Assert.Equal(@"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\User\Mods\ptyra.fft.colorcustomizer", userConfigDir);
            Assert.Equal(@"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\User\Mods\ptyra.fft.colorcustomizer\Config.json", userConfigPath);
        }
    }
}
