using System;
using System.IO;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Configures and registers all services in the DI container.
    /// This is the single place where all service dependencies are wired up.
    /// </summary>
    public static class ServiceRegistry
    {
        /// <summary>
        /// Configures all services in the container for the given mod path.
        /// </summary>
        /// <param name="container">The service container to configure</param>
        /// <param name="modPath">The mod installation path</param>
        public static void ConfigureServices(ServiceContainer container, string modPath)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (string.IsNullOrEmpty(modPath)) throw new ArgumentNullException(nameof(modPath));

            ModLogger.Log($"ServiceRegistry: Configuring services for mod path: {modPath}");

            // Register CharacterDefinitionService as singleton
            container.RegisterSingleton(() => CreateCharacterDefinitionService(modPath));

            // Register JobClassDefinitionService as singleton
            container.RegisterSingleton(() => new JobClassDefinitionService(modPath));

            // Register UserThemeService as singleton
            container.RegisterSingleton(() => new UserThemeService(modPath));

            // Also update the static singletons for backward compatibility
            // This allows existing code to continue working during migration
            SyncWithStaticSingletons(container, modPath);

            ModLogger.Log("ServiceRegistry: All services configured successfully");
        }

        /// <summary>
        /// Creates and initializes a CharacterDefinitionService
        /// </summary>
        private static CharacterDefinitionService CreateCharacterDefinitionService(string modPath)
        {
            var service = new CharacterDefinitionService();

            // Try to load from JSON file
            var jsonPath = FindStoryCharactersJson(modPath);
            if (jsonPath != null && File.Exists(jsonPath))
            {
                service.LoadFromJson(jsonPath);
                ModLogger.Log($"ServiceRegistry: Loaded characters from {jsonPath}");
            }

            // If no characters loaded from JSON, auto-discover from attributes
            if (service.GetAllCharacters().Count == 0)
            {
                StoryCharacterRegistry.AutoDiscoverCharacters(service);
                ModLogger.Log("ServiceRegistry: Auto-discovered characters from attributes");
            }

            return service;
        }

        /// <summary>
        /// Finds the StoryCharacters.json file
        /// </summary>
        private static string? FindStoryCharactersJson(string modPath)
        {
            var paths = new[]
            {
                Path.Combine(modPath, ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile),
                Path.Combine(modPath, "ColorMod", ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile)
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        /// <summary>
        /// Syncs the DI container services with the static singletons.
        /// This provides backward compatibility during the migration period.
        /// Once all code uses DI, this method can be removed.
        /// </summary>
        private static void SyncWithStaticSingletons(ServiceContainer container, string modPath)
        {
            // Initialize static singletons so they use the same instances
            CharacterServiceSingleton.SetModPath(modPath);
            JobClassServiceSingleton.Initialize(modPath);
            UserThemeServiceSingleton.Initialize(modPath);

            ModLogger.LogDebug("ServiceRegistry: Static singletons synchronized");
        }

        /// <summary>
        /// Resolves a service from the container with fallback to static singleton.
        /// Use this during migration to gradually move away from singletons.
        /// </summary>
        public static T ResolveWithFallback<T>(ServiceContainer? container) where T : class
        {
            if (container != null && container.TryResolve<T>(out var service) && service != null)
            {
                return service;
            }

            // Fallback to static singletons
            if (typeof(T) == typeof(CharacterDefinitionService))
            {
                return (CharacterServiceSingleton.Instance as T)!;
            }
            if (typeof(T) == typeof(JobClassDefinitionService))
            {
                return (JobClassServiceSingleton.Instance as T)!;
            }
            if (typeof(T) == typeof(UserThemeService))
            {
                return (UserThemeServiceSingleton.Instance as T)!;
            }

            throw new InvalidOperationException($"Cannot resolve service of type {typeof(T).Name}");
        }
    }
}
