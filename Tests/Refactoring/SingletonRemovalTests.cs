using System;
using System.IO;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Services;
using Xunit;

namespace FFTColorCustomizer.Tests.Refactoring
{
    /// <summary>
    /// Tests to verify that classes can work with dependency injection instead of singletons
    /// </summary>
    public class SingletonRemovalTests : IDisposable
    {
        private readonly IServiceContainer _container;
        private readonly string _testPath;

        public SingletonRemovalTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"SingletonRemovalTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);

            _container = SetupTestContainer();
        }

        public void Dispose()
        {
            _container?.Dispose();
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        private IServiceContainer SetupTestContainer()
        {
            var container = new ServiceContainer();

            // Register test services
            container.RegisterSingleton<ILogger>(NullLogger.Instance);
            container.RegisterSingleton<IPathResolver>(new PathResolver(_testPath, _testPath, _testPath));

            // Register the services that used to be singletons
            container.RegisterSingleton<CharacterDefinitionService>(() =>
            {
                var service = new CharacterDefinitionService();
                // Add test data with EnumType so DynamicStoryCharacterConfig picks it up
                service.AddCharacter(new CharacterDefinition
                {
                    Name = "TestCharacter",
                    SpriteNames = new[] { "test_sprite" },
                    DefaultTheme = "original",
                    EnumType = "AgriasColorScheme" // Use existing enum
                });
                return service;
            });

            container.RegisterSingleton<JobClassDefinitionService>(() =>
            {
                return new JobClassDefinitionService(_testPath);
            });

            container.RegisterSingleton<ConfigurationManager>(() =>
            {
                var configPath = Path.Combine(_testPath, "Config.json");
                return new ConfigurationManager(configPath);
            });

            return container;
        }

        [Fact]
        public void DynamicStoryCharacterConfig_ShouldWorkWithInjectedService()
        {
            // Arrange
            var characterService = _container.Resolve<CharacterDefinitionService>();

            // Act - Create with injected service instead of singleton
            var config = new DynamicStoryCharacterConfig(characterService);

            // Assert
            Assert.NotNull(config);
            var characterNames = config.GetCharacterNames();
            Assert.Contains("TestCharacter", characterNames);
        }

        [Fact]
        public void SpriteNameMapper_ShouldWorkWithInjectedService()
        {
            // Arrange
            var characterService = _container.Resolve<CharacterDefinitionService>();
            var jobClassService = _container.Resolve<JobClassDefinitionService>();

            // Act - Create with injected services instead of singletons
            var mapper = new SpriteNameMapper(characterService, jobClassService);

            // Assert
            Assert.NotNull(mapper);
            // Test that it can get character key for sprite
            var characterKey = mapper.GetCharacterKeyForSprite("test_sprite_0");
            Assert.NotNull(characterKey);
        }

        [Fact]
        public void ConfigBasedSpriteManager_ShouldWorkWithInjectedService()
        {
            // Arrange
            var configManager = _container.Resolve<ConfigurationManager>();
            var characterService = _container.Resolve<CharacterDefinitionService>();

            // Act - Already has a constructor that accepts the service
            var spriteManager = new FFTColorCustomizer.Utilities.ConfigBasedSpriteManager(
                _testPath,
                configManager,
                characterService,
                _testPath);

            // Assert
            Assert.NotNull(spriteManager);
        }

        [Fact]
        public void MultipleConsumers_ShouldShareSameServiceInstance()
        {
            // Arrange
            var characterService = _container.Resolve<CharacterDefinitionService>();

            // Act
            var config1 = new DynamicStoryCharacterConfig(characterService);
            var config2 = new DynamicStoryCharacterConfig(characterService);

            // Add a character through one instance
            characterService.AddCharacter(new CharacterDefinition
            {
                Name = "SharedCharacter",
                SpriteNames = new[] { "shared" },
                DefaultTheme = "original",
                EnumType = "CloudColorScheme" // Use existing enum
            });

            // Need to reinitialize configs after adding character
            config1 = new DynamicStoryCharacterConfig(characterService);
            config2 = new DynamicStoryCharacterConfig(characterService);

            // Assert - Both should see the same character
            var names1 = config1.GetCharacterNames();
            var names2 = config2.GetCharacterNames();
            Assert.Contains("SharedCharacter", names1);
            Assert.Contains("SharedCharacter", names2);
        }

        [Fact]
        public void ServiceContainer_ShouldReplaceCharacterServiceSingleton()
        {
            // Arrange & Act
            var service1 = _container.Resolve<CharacterDefinitionService>();
            var service2 = _container.Resolve<CharacterDefinitionService>();

            // Assert - Should be same instance (registered as singleton)
            Assert.Same(service1, service2);

            // And it should have our test data
            var character = service1.GetCharacterByName("TestCharacter");
            Assert.NotNull(character);
            Assert.Equal("TestCharacter", character.Name);
        }

        [Fact]
        public void ServiceContainer_ShouldReplaceJobClassServiceSingleton()
        {
            // Arrange & Act
            var service1 = _container.Resolve<JobClassDefinitionService>();
            var service2 = _container.Resolve<JobClassDefinitionService>();

            // Assert - Should be same instance (registered as singleton)
            Assert.Same(service1, service2);
        }
    }
}
