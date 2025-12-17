using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorMod.Core.ModComponents;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests.Core.ModComponents
{
    public class ConfigurationCoordinatorTests : IDisposable
    {
        private readonly string _testPath;
        private readonly string _configPath;
        private readonly ConfigurationCoordinator _coordinator;

        public ConfigurationCoordinatorTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"ConfigCoordinatorTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);
            _configPath = Path.Combine(_testPath, "Config.json");

            _coordinator = new ConfigurationCoordinator(_configPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        [Fact]
        public void GetConfiguration_ShouldReturnDefaultConfig_WhenNoConfigExists()
        {
            // Act
            var config = _coordinator.GetConfiguration();

            // Assert
            config.Should().NotBeNull();
            config.Knight_Male.Should().Be("original");
            config.Archer_Female.Should().Be("original");
        }

        [Fact]
        public void SetJobColor_ShouldUpdateConfiguration()
        {
            // Act
            _coordinator.SetJobColor("Knight_Male", "lucavi");
            var config = _coordinator.GetConfiguration();

            // Assert
            config.Knight_Male.Should().Be("lucavi");
        }

        [Fact]
        public void GetJobColor_ShouldReturnCorrectColor()
        {
            // Arrange
            _coordinator.SetJobColor("Archer_Female", "corpse_brigade");

            // Act
            var color = _coordinator.GetJobColor("Archer_Female");

            // Assert
            color.Should().Be("Corpse Brigade"); // Note: display name format
        }

        [Fact]
        public void GetAllJobColors_ShouldReturnAllJobColors()
        {
            // Arrange
            _coordinator.SetJobColor("Knight_Male", "lucavi");
            _coordinator.SetJobColor("Archer_Female", "corpse_brigade");

            // Act
            var allColors = _coordinator.GetAllJobColors();

            // Assert
            allColors.Should().NotBeNull();
            allColors.Should().ContainKey("Knight_Male");
            allColors.Should().ContainKey("Archer_Female");
            allColors["Knight_Male"].Should().Be("Lucavi");
            allColors["Archer_Female"].Should().Be("Corpse Brigade");
        }

        [Fact]
        public void ResetToDefaults_ShouldResetAllColors()
        {
            // Arrange
            _coordinator.SetJobColor("Knight_Male", "lucavi");
            _coordinator.SetJobColor("Monk_Female", "corpse_brigade");

            // Act
            _coordinator.ResetToDefaults();
            var config = _coordinator.GetConfiguration();

            // Assert
            config.Knight_Male.Should().Be("original");
            config.Monk_Female.Should().Be("original");
        }

        [Fact]
        public void SaveConfiguration_ShouldPersistToFile()
        {
            // Arrange
            _coordinator.SetJobColor("Knight_Male", "lucavi");

            // Act
            _coordinator.SaveConfiguration();

            // Assert
            File.Exists(_configPath).Should().BeTrue();
            var fileContent = File.ReadAllText(_configPath);
            fileContent.Should().Contain("lucavi");
        }

        [Fact]
        public void LoadConfiguration_ShouldRestoreFromFile()
        {
            // Arrange
            var config = new Config { Knight_Male = "lucavi", Archer_Female = "corpse_brigade" };
            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(_configPath, json);

            // Act
            var coordinator = new ConfigurationCoordinator(_configPath);
            var loadedConfig = coordinator.GetConfiguration();

            // Assert
            loadedConfig.Knight_Male.Should().Be("lucavi");
            loadedConfig.Archer_Female.Should().Be("corpse_brigade");
        }

        [Fact]
        public void UpdateConfiguration_ShouldApplyNewConfig()
        {
            // Arrange
            var newConfig = new Config
            {
                Knight_Male = "northern_sky",
                Squire_Female = "southern_sky"
            };

            // Act
            _coordinator.UpdateConfiguration(newConfig);
            var config = _coordinator.GetConfiguration();

            // Assert
            config.Knight_Male.Should().Be("northern_sky");
            config.Squire_Female.Should().Be("southern_sky");
        }

        [Fact]
        public void HasConfigurationManager_ShouldReturnTrue()
        {
            // Act
            var hasManager = _coordinator.HasConfigurationManager();

            // Assert
            hasManager.Should().BeTrue();
        }

        [Fact]
        public void GetAvailableThemes_ShouldReturnThemeList()
        {
            // Act
            var themes = _coordinator.GetAvailableThemes();

            // Assert
            themes.Should().NotBeNull();
            themes.Should().Contain("original");
            themes.Should().Contain("lucavi");
            themes.Should().Contain("corpse_brigade");
        }

        [Fact]
        public void Constructor_WithNullConfigPath_ShouldThrowException()
        {
            // Act & Assert
            var action = () => new ConfigurationCoordinator(null);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("configPath");
        }
    }
}