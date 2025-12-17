using System;
using System.IO;
using FFTColorMod;
using FFTColorMod.Core;
using FFTColorMod.Interfaces;
using FFTColorMod.Services;
using Xunit;

namespace FFTColorMod.Tests.Refactoring
{
    /// <summary>
    /// Tests to verify that the Mod class can work with dependency injection
    /// </summary>
    public class ModDependencyInjectionTests : IDisposable
    {
        private readonly string _testPath;
        private readonly IServiceContainer _container;

        public ModDependencyInjectionTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"ModDITest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);

            // Create Data directory for JSON files
            var dataPath = Path.Combine(_testPath, "Data");
            Directory.CreateDirectory(dataPath);

            _container = ServiceProvider.ConfigureServices(_testPath);
        }

        public void Dispose()
        {
            _container?.Dispose();
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        [Fact]
        public void ServiceProvider_ShouldRegisterAllRequiredServices()
        {
            // Assert - All services should be resolvable
            Assert.NotNull(_container.Resolve<ILogger>());
            Assert.NotNull(_container.Resolve<IPathResolver>());
            Assert.NotNull(_container.Resolve<CharacterDefinitionService>());
            Assert.NotNull(_container.Resolve<JobClassDefinitionService>());
            Assert.NotNull(_container.Resolve<FFTColorMod.Configuration.ConfigurationManager>());
            Assert.NotNull(_container.Resolve<ThemeManager>());
            Assert.NotNull(_container.Resolve<FFTColorMod.Utilities.ConfigBasedSpriteManager>());
        }

        [Fact]
        public void Mod_ShouldInitializeWithDependencyInjection()
        {
            // Arrange
            var container = ServiceProvider.ConfigureServices(_testPath);

            // Act - Create mod with DI container
            var mod = new Mod(container);

            // Assert
            Assert.NotNull(mod);
            // The mod should be able to get services from the container
            var characterService = container.Resolve<CharacterDefinitionService>();
            Assert.NotNull(characterService);
        }

        [Fact]
        public void Mod_ShouldWorkWithoutSingletons()
        {
            // Arrange
            var container = ServiceProvider.ConfigureServices(_testPath);
            var mod = new Mod(container);

            // Act - Try to use features that previously relied on singletons
            var config = mod.GetConfiguration();

            // Assert
            Assert.NotNull(config);
        }

        [Fact]
        public void ServiceProvider_ShouldShareSingletonInstances()
        {
            // Arrange
            var container = ServiceProvider.ConfigureServices(_testPath);

            // Act
            var charService1 = container.Resolve<CharacterDefinitionService>();
            var charService2 = container.Resolve<CharacterDefinitionService>();
            var jobService1 = container.Resolve<JobClassDefinitionService>();
            var jobService2 = container.Resolve<JobClassDefinitionService>();

            // Assert - Should be same instances (registered as singletons)
            Assert.Same(charService1, charService2);
            Assert.Same(jobService1, jobService2);
        }

        [Fact]
        public void ConfigurationManager_ShouldUseInjectedServices()
        {
            // Arrange
            var container = ServiceProvider.ConfigureServices(_testPath);
            var configManager = container.Resolve<FFTColorMod.Configuration.ConfigurationManager>();

            // Act
            var config = configManager.LoadConfig();

            // Assert
            Assert.NotNull(config);
        }

        [Fact]
        public void ThemeManager_ShouldWorkWithInjectedServices()
        {
            // Arrange
            var container = ServiceProvider.ConfigureServices(_testPath);
            var themeManager = container.Resolve<ThemeManager>();

            // Act & Assert - Should not throw
            var storyManager = themeManager.GetStoryCharacterManager();
            Assert.NotNull(storyManager);
        }
    }
}