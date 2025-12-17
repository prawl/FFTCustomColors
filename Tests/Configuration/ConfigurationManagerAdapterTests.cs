using System;
using System.IO;
using FFTColorMod.Configuration;
using Xunit;
using FluentAssertions;

namespace FFTColorMod.Tests.Configuration
{
    /// <summary>
    /// Tests to ensure ConfigurationManagerAdapter maintains backward compatibility
    /// with the original ConfigurationManager behavior
    /// </summary>
    public class ConfigurationManagerAdapterTests : IDisposable
    {
        private readonly string _testPath;
        private readonly string _configPath;

        public ConfigurationManagerAdapterTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"ConfigAdapterTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);
            _configPath = Path.Combine(_testPath, "Config.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        [Fact]
        public void LoadConfig_ShouldReturnDefaultConfig_WhenFileDoesNotExist()
        {
            // Arrange
            var adapter = new ConfigurationManagerAdapter(_configPath);

            // Act
            var config = adapter.LoadConfig();

            // Assert
            config.Should().NotBeNull();
            config.Knight_Male.Should().Be("original");
            config.Knight_Female.Should().Be("original");
        }

        [Fact]
        public void SaveConfig_ShouldPersistConfiguration()
        {
            // Arrange
            var adapter = new ConfigurationManagerAdapter(_configPath);
            var config = new Config
            {
                Knight_Male = "lucavi",
                Archer_Female = "corpse_brigade"
            };

            // Act
            adapter.SaveConfig(config);
            var loadedConfig = adapter.LoadConfig();

            // Assert
            loadedConfig.Knight_Male.Should().Be("lucavi");
            loadedConfig.Archer_Female.Should().Be("corpse_brigade");
            File.Exists(_configPath).Should().BeTrue();
        }

        [Fact]
        public void GetDefaultConfiguration_ShouldReturnAllOriginalThemes()
        {
            // Arrange
            var adapter = new ConfigurationManagerAdapter(_configPath);

            // Act
            var defaultConfig = adapter.GetDefaultConfiguration();

            // Assert
            defaultConfig.Should().NotBeNull();
            defaultConfig.Knight_Male.Should().Be("original");
            defaultConfig.Knight_Female.Should().Be("original");
            defaultConfig.Squire_Male.Should().Be("original");
            defaultConfig.Squire_Female.Should().Be("original");

            // Story characters should also be original
            defaultConfig.Agrias.Should().Be("original");
            defaultConfig.Cloud.Should().Be("original");
            defaultConfig.Orlandeau.Should().Be("original");
        }

        [Fact]
        public void ResetToDefaults_ShouldRevertAllSettings()
        {
            // Arrange
            var adapter = new ConfigurationManagerAdapter(_configPath);
            var config = adapter.LoadConfig();
            config.Knight_Male = "lucavi";
            config.Archer_Female = "corpse_brigade";
            adapter.SaveConfig(config);

            // Act
            adapter.ResetToDefaults();
            var resetConfig = adapter.LoadConfig();

            // Assert
            resetConfig.Knight_Male.Should().Be("original");
            resetConfig.Archer_Female.Should().Be("original");
        }

        [Fact]
        public void GetAvailableColorSchemes_ShouldReturnStandardSchemes()
        {
            // Arrange
            var adapter = new ConfigurationManagerAdapter(_configPath);

            // Act
            var schemes = adapter.GetAvailableColorSchemes();

            // Assert
            schemes.Should().NotBeNull();
            schemes.Should().Contain("original");
            schemes.Should().Contain("corpse_brigade");
            schemes.Should().Contain("lucavi");
            schemes.Should().Contain("northern_sky");
            schemes.Should().Contain("southern_sky");
        }

        [Fact]
        public void MultipleInstances_ShouldShareConfiguration()
        {
            // Arrange
            var adapter1 = new ConfigurationManagerAdapter(_configPath);
            var adapter2 = new ConfigurationManagerAdapter(_configPath);

            var config = new Config { Knight_Male = "lucavi" };
            adapter1.SaveConfig(config);

            // Act
            var loadedConfig = adapter2.LoadConfig();

            // Assert
            loadedConfig.Knight_Male.Should().Be("lucavi");
        }

        [Fact]
        public void LoadConfig_ShouldCreateFileIfNotExists()
        {
            // Arrange
            var adapter = new ConfigurationManagerAdapter(_configPath);

            // Act
            var config = adapter.LoadConfig();

            // Assert
            File.Exists(_configPath).Should().BeTrue();
        }
    }
}