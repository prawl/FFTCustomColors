using System;
using System.IO;
using FFTColorMod.Configuration;
using FFTColorMod.Interfaces;
using FFTColorMod.Services;
using FFTColorMod.Utilities;

namespace FFTColorMod.Core
{
    /// <summary>
    /// Configures and provides dependency injection services for the mod
    /// </summary>
    public static class ServiceProvider
    {
        private static IServiceContainer? _container;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the configured service container
        /// </summary>
        public static IServiceContainer Container
        {
            get
            {
                if (_container == null)
                {
                    lock (_lock)
                    {
                        if (_container == null)
                        {
                            _container = ConfigureServices(null);
                        }
                    }
                }
                return _container;
            }
        }

        /// <summary>
        /// Configures all services for the mod
        /// </summary>
        public static IServiceContainer ConfigureServices(string? modPath)
        {
            var container = new ServiceContainer();

            // Determine paths
            string actualModPath = modPath ?? AppDomain.CurrentDomain.BaseDirectory;
            string sourcePath = GetSourcePath(actualModPath);
            string userConfigPath = GetUserConfigPath(actualModPath);

            // Register core services
            container.RegisterSingleton<ILogger>(() => new ConsoleLogger(ColorModConstants.LogPrefix));
            container.RegisterSingleton<IPathResolver>(() => new PathResolver(actualModPath, sourcePath, userConfigPath));

            // Register configuration services
            container.RegisterSingleton<ConfigurationManager>(() =>
            {
                var pathResolver = container.Resolve<IPathResolver>();
                var jobClassService = container.Resolve<JobClassDefinitionService>();
                return new ConfigurationManager(pathResolver.GetConfigPath(), jobClassService);
            });

            // Register character and job services (replace singletons)
            container.RegisterSingleton<CharacterDefinitionService>(() =>
            {
                var service = new CharacterDefinitionService();
                var pathResolver = container.Resolve<IPathResolver>();
                var storyCharactersPath = pathResolver.GetDataPath(ColorModConstants.StoryCharactersFile);

                if (File.Exists(storyCharactersPath))
                {
                    service.LoadFromJson(storyCharactersPath);
                }
                return service;
            });

            container.RegisterSingleton<JobClassDefinitionService>(() =>
            {
                // JobClassDefinitionService loads from its constructor
                return new JobClassDefinitionService(actualModPath);
            });

            // Register theme and sprite managers
            container.RegisterSingleton<ThemeManager>(() =>
            {
                return new ThemeManager(sourcePath, actualModPath);
            });

            container.RegisterSingleton<SpriteFileManager>(() =>
            {
                return new SpriteFileManager(sourcePath, actualModPath);
            });

            container.RegisterSingleton<ConfigBasedSpriteManager>(() =>
            {
                var configManager = container.Resolve<ConfigurationManager>();
                var characterService = container.Resolve<CharacterDefinitionService>();
                return new ConfigBasedSpriteManager(
                    actualModPath,
                    configManager,
                    characterService,
                    sourcePath);
            });

            container.RegisterSingleton<StoryCharacterThemeManager>(() =>
            {
                return new StoryCharacterThemeManager(actualModPath);
            });

            // Register the main mod class dependencies
            container.RegisterSingleton<ColorSchemeCycler>(() =>
            {
                var pathResolver = container.Resolve<IPathResolver>();
                var themeManager = container.Resolve<ThemeManager>();
                return new ColorSchemeCycler(sourcePath);
            });

            return container;
        }

        /// <summary>
        /// Resets the service container (useful for testing)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _container?.Dispose();
                _container = null;
            }
        }

        /// <summary>
        /// Sets a custom service container (useful for testing)
        /// </summary>
        public static void SetContainer(IServiceContainer container)
        {
            lock (_lock)
            {
                _container?.Dispose();
                _container = container;
            }
        }

        private static string GetSourcePath(string modPath)
        {
            // Try to detect if we're in dev environment
            var devSourcePath = Path.Combine(modPath, "..", "..", "..", "..", "..");
            if (Directory.Exists(Path.Combine(devSourcePath, "ColorMod")))
            {
                return Path.GetFullPath(devSourcePath);
            }

            // Otherwise use mod path
            return modPath;
        }

        private static string GetUserConfigPath(string modPath)
        {
            var userPath = Path.Combine(modPath, ColorModConstants.UserDirectory);
            if (!Directory.Exists(userPath))
            {
                Directory.CreateDirectory(userPath);
            }
            return userPath;
        }
    }
}