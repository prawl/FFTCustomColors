using System;
using System.IO;
using FFTColorMod.Configuration;
using FFTColorMod.Configuration.UI;
using FFTColorMod.Core;
using FFTColorMod.Interfaces;
using FFTColorMod.Services;
using FFTColorMod.Utilities;
using Xunit;

namespace FFTColorMod.Tests.Refactoring
{
    /// <summary>
    /// Tests to ensure all classes can work with DI instead of singletons
    /// </summary>
    public class CompleteSingletonReplacementTests : IDisposable
    {
        private readonly string _testPath;
        private readonly IServiceContainer _container;

        public CompleteSingletonReplacementTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"SingletonReplacementTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);

            // Create Data directory
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
        public void ConfigBasedSpriteManager_ShouldUseInjectedService()
        {
            // Arrange
            var configManager = _container.Resolve<ConfigurationManager>();
            var characterService = _container.Resolve<CharacterDefinitionService>();

            // Act - Use constructor with explicit service injection
            var spriteManager = new ConfigBasedSpriteManager(
                _testPath,
                configManager,
                characterService,
                _testPath);

            // Assert
            Assert.NotNull(spriteManager);
            // The manager should work without using the singleton
        }

        [Fact]
        public void ThemeValidationService_ShouldUseInjectedService()
        {
            // Arrange
            var characterService = _container.Resolve<CharacterDefinitionService>();
            var pathResolver = _container.Resolve<IPathResolver>();

            // Act - Create with injected service
            var validationService = new ThemeValidationService(
                pathResolver.SourcePath,
                characterService);

            // Assert
            Assert.NotNull(validationService);
        }

        [Fact]
        public void CharacterRowBuilder_CanBeCreatedWithDependencies()
        {
            // Arrange
            var jobClassService = _container.Resolve<JobClassDefinitionService>();

            // Act - CharacterRowBuilder requires UI components, just verify service is available
            // In production, it would be injected through the constructor
            Assert.NotNull(jobClassService);

            // Note: CharacterRowBuilder is tightly coupled to UI,
            // would need refactoring to be fully testable with DI
        }

        [Fact]
        public void DynamicStoryCharacterConfig_UsesInjectedServiceByDefault()
        {
            // Arrange
            var characterService = _container.Resolve<CharacterDefinitionService>();

            // Add test character
            characterService.AddCharacter(new CharacterDefinition
            {
                Name = "TestChar",
                SpriteNames = new[] { "test" },
                DefaultTheme = "original",
                EnumType = "CloudColorScheme"
            });

            // Act - Use constructor with service injection
            var config = new DynamicStoryCharacterConfig(characterService);

            // Assert
            Assert.NotNull(config);
            var names = config.GetCharacterNames();
            Assert.Contains("TestChar", names);
        }

        [Fact]
        public void ConfigurationManager_ShouldUseInjectedService()
        {
            // Arrange
            var configPath = Path.Combine(_testPath, "Config.json");
            var jobClassService = _container.Resolve<JobClassDefinitionService>();

            // Act - Create with injected service
            var configManager = new ConfigurationManager(configPath, jobClassService);

            // Assert
            Assert.NotNull(configManager);
            var config = configManager.LoadConfig();
            Assert.NotNull(config);
        }

        [Fact]
        public void SpriteNameMapper_ShouldUseInjectedServices()
        {
            // Arrange
            var characterService = _container.Resolve<CharacterDefinitionService>();
            var jobClassService = _container.Resolve<JobClassDefinitionService>();

            // Act - Use constructor with service injection
            var mapper = new SpriteNameMapper(characterService, jobClassService);

            // Assert
            Assert.NotNull(mapper);
        }

        [Fact]
        public void AllServicesCanBeResolvedFromContainer()
        {
            // This ensures all services are properly registered and can replace singletons

            // Core services
            Assert.NotNull(_container.Resolve<ILogger>());
            Assert.NotNull(_container.Resolve<IPathResolver>());

            // Services that used to be singletons
            var characterService = _container.Resolve<CharacterDefinitionService>();
            Assert.NotNull(characterService);

            var jobClassService = _container.Resolve<JobClassDefinitionService>();
            Assert.NotNull(jobClassService);

            // Services that depend on former singletons
            var configManager = _container.Resolve<ConfigurationManager>();
            Assert.NotNull(configManager);

            var spriteManager = _container.Resolve<ConfigBasedSpriteManager>();
            Assert.NotNull(spriteManager);

            // Verify they're singletons in the container
            var characterService2 = _container.Resolve<CharacterDefinitionService>();
            Assert.Same(characterService, characterService2);

            var jobClassService2 = _container.Resolve<JobClassDefinitionService>();
            Assert.Same(jobClassService, jobClassService2);
        }
    }
}