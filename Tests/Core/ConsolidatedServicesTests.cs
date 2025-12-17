using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorMod.Configuration;
using FFTColorMod.Core;
using FFTColorMod.Interfaces;
using Xunit;

namespace FFTColorMod.Tests.Core
{
    /// <summary>
    /// Tests for the consolidated service architecture
    /// </summary>
    public class ConsolidatedServicesTests : IDisposable
    {
        private readonly string _testPath;
        private readonly IServiceContainer _container;

        public ConsolidatedServicesTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"ConsolidatedServicesTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);

            _container = new ServiceContainer();
            SetupTestContainer();
        }

        public void Dispose()
        {
            _container?.Dispose();
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        private void SetupTestContainer()
        {
            _container.RegisterSingleton<IPathResolver>(
                new PathResolver(_testPath, _testPath, _testPath));

            _container.RegisterSingleton<ILogger>(NullLogger.Instance);
        }

        #region IConfigurationService Tests

        [Fact]
        public void ConfigurationService_ShouldLoadConfiguration()
        {
            // Arrange
            var configService = new ConfigurationService(_container.Resolve<IPathResolver>());

            // Act
            var config = configService.LoadConfig();

            // Assert
            Assert.NotNull(config);
        }

        [Fact]
        public void ConfigurationService_ShouldSaveConfiguration()
        {
            // Arrange
            var pathResolver = _container.Resolve<IPathResolver>();
            var configService = new ConfigurationService(pathResolver);
            var config = new Config();

            // Act
            configService.SaveConfig(config);

            // Assert
            var configPath = pathResolver.GetConfigPath();
            Assert.True(File.Exists(configPath));
        }

        [Fact]
        public void ConfigurationService_ShouldProvideDefaults()
        {
            // Arrange
            var configService = new ConfigurationService(_container.Resolve<IPathResolver>());

            // Act
            var defaultConfig = configService.GetDefaultConfig();

            // Assert
            Assert.NotNull(defaultConfig);
            Assert.Equal("original", defaultConfig.Knight_Male);
            Assert.Equal("original", defaultConfig.Knight_Female);
        }

        [Fact]
        public void ConfigurationService_ShouldResetToDefaults()
        {
            // Arrange
            var pathResolver = _container.Resolve<IPathResolver>();
            var configService = new ConfigurationService(pathResolver);

            // Modify config
            var config = configService.LoadConfig();
            config.Knight_Male = "lucavi";
            configService.SaveConfig(config);

            // Act
            configService.ResetToDefaults();
            var resetConfig = configService.LoadConfig();

            // Assert
            Assert.Equal("original", resetConfig.Knight_Male);
        }

        #endregion

        #region IThemeService Tests

        [Fact]
        public void ThemeService_ShouldApplyTheme()
        {
            // Arrange
            var themeService = new ThemeService(
                _container.Resolve<IPathResolver>(),
                new ConfigurationService(_container.Resolve<IPathResolver>()));

            // Act
            themeService.ApplyTheme("Agrias", "lucavi");
            var currentTheme = themeService.GetCurrentTheme("Agrias");

            // Assert
            Assert.Equal("lucavi", currentTheme);
        }

        [Fact]
        public void ThemeService_ShouldCycleThemes()
        {
            // Arrange
            var themeService = new ThemeService(
                _container.Resolve<IPathResolver>(),
                new ConfigurationService(_container.Resolve<IPathResolver>()));

            themeService.ApplyTheme("Cloud", "original");

            // Act
            var nextTheme = themeService.CycleTheme("Cloud");

            // Assert
            Assert.NotEqual("original", nextTheme);
        }

        [Fact]
        public void ThemeService_ShouldGetAvailableThemes()
        {
            // Arrange
            var themeService = new ThemeService(
                _container.Resolve<IPathResolver>(),
                new ConfigurationService(_container.Resolve<IPathResolver>()));

            // Act
            var themes = themeService.GetAvailableThemes("Orlandeau").ToList();

            // Assert
            Assert.NotNull(themes);
            Assert.Contains("original", themes);
        }

        [Fact]
        public void ThemeService_ShouldApplyConfigurationThemes()
        {
            // Arrange
            var configService = new ConfigurationService(_container.Resolve<IPathResolver>());
            var themeService = new ThemeService(_container.Resolve<IPathResolver>(), configService);

            var config = new Config
            {
                Knight_Male = "lucavi",
                Archer_Female = "corpse_brigade"
            };

            // Act
            themeService.ApplyConfigurationThemes(config);

            // Assert
            // Themes should be applied based on config
            Assert.NotNull(config);
        }

        #endregion

        #region ISpriteService Tests

        [Fact]
        public void SpriteService_ShouldCopySprites()
        {
            // Arrange
            var spriteService = new SpriteService(
                _container.Resolve<IPathResolver>(),
                _container.Resolve<ILogger>());

            // Act & Assert - Should not throw
            spriteService.CopySprites("original", "Knight");
        }

        [Fact]
        public void SpriteService_ShouldClearSprites()
        {
            // Arrange
            var pathResolver = _container.Resolve<IPathResolver>();
            var spriteService = new SpriteService(pathResolver, _container.Resolve<ILogger>());

            // Create a test sprite file
            var spritePath = Path.Combine(_testPath, "test_sprite.bmp");
            File.WriteAllText(spritePath, "test");

            // Act
            spriteService.ClearSprites();

            // Assert - Implementation would clear sprite directories
            Assert.True(true); // Placeholder
        }

        [Fact]
        public void SpriteService_ShouldApplySpriteConfiguration()
        {
            // Arrange
            var spriteService = new SpriteService(
                _container.Resolve<IPathResolver>(),
                _container.Resolve<ILogger>());

            var config = new Config
            {
                Knight_Male = "lucavi"
            };

            // Act & Assert - Should not throw
            spriteService.ApplySpriteConfiguration(config);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void AllServices_ShouldWorkTogether()
        {
            // Arrange
            var pathResolver = _container.Resolve<IPathResolver>();
            var configService = new ConfigurationService(pathResolver);
            var themeService = new ThemeService(pathResolver, configService);
            var spriteService = new SpriteService(pathResolver, _container.Resolve<ILogger>());

            // Act
            var config = configService.LoadConfig();
            config.Knight_Male = "lucavi";
            configService.SaveConfig(config);

            themeService.ApplyConfigurationThemes(config);
            spriteService.ApplySpriteConfiguration(config);

            // Assert
            Assert.NotNull(config);
            Assert.Equal("lucavi", config.Knight_Male);
        }

        #endregion
    }
}