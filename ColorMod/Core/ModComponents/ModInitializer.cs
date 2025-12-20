using System;
using System.IO;
using System.Linq;
using ColorMod.Registry;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using static FFTColorCustomizer.Core.ColorModConstants;

namespace FFTColorCustomizer.Core.ModComponents
{
    /// <summary>
    /// Handles initialization of mod components and dependencies
    /// </summary>
    public class ModInitializer
    {
        private readonly string _modPath;
        private readonly string _sourcePath;
        private readonly bool _isTestEnvironment;

        public ModInitializer(string modPath, bool isTestEnvironment = false)
        {
            _modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));
            // In deployment, source and mod path are the same
            _sourcePath = _modPath;
            _isTestEnvironment = isTestEnvironment;
        }

        /// <summary>
        /// Initialize the registry system
        /// </summary>
        public void InitializeRegistry()
        {
            // Clear and auto-discover all story characters with attributes
            StoryCharacterRegistry.Clear();
            StoryCharacterRegistry.AutoDiscoverCharacters();

            ModLogger.Log($"Registry initialized with {StoryCharacterRegistry.GetAllCharacterNames().Count()} story characters");

            // Initialize job class service with mod path
            JobClassServiceSingleton.Initialize(_modPath);
            var jobClassService = JobClassServiceSingleton.Instance;
            ModLogger.Log($"Loaded {jobClassService.GetAllJobClasses().Count()} generic job classes from JobClasses.json");
        }

        /// <summary>
        /// Initialize color scheme cycler
        /// </summary>
        public ColorSchemeCycler InitializeColorSchemeCycler()
        {
            // Use the deployed mod directory, not the source directory
            // This ensures we only detect themes that were actually deployed
            string spritesPath = Path.Combine(_modPath, FFTIVCPath, DataPath, EnhancedPath, FFTPackPath, UnitPath);
            var colorCycler = new ColorSchemeCycler(spritesPath);
            var schemes = colorCycler.GetAvailableSchemes();

            if (schemes.Count > 0)
            {
                ModLogger.Log($"Auto-detected {schemes.Count} color schemes");
                colorCycler.SetCurrentScheme(DefaultTheme);
            }
            else
            {
                ModLogger.LogWarning($"No color schemes found in: {spritesPath}");
                colorCycler.SetCurrentScheme(DefaultTheme);
            }

            return colorCycler;
        }

        /// <summary>
        /// Initialize configuration manager
        /// </summary>
        public ConfigurationManager InitializeConfiguration(string configPath)
        {
            var configurationManager = new ConfigurationManager(configPath);
            return configurationManager;
        }

        /// <summary>
        /// Initialize sprite managers
        /// </summary>
        public ConfigBasedSpriteManager InitializeSpriteManager(string configPath, ConfigurationManager configManager)
        {
            return new ConfigBasedSpriteManager(Path.GetDirectoryName(configPath), configManager, _sourcePath);
        }

        /// <summary>
        /// Initialize theme manager
        /// </summary>
        public ThemeManager InitializeThemeManager()
        {
            return new ThemeManager(_sourcePath, _modPath);
        }

        /// <summary>
        /// Initialize story character themes from config
        /// </summary>
        public void InitializeStoryCharacterThemes(Config config, ThemeManager themeManager)
        {
            // Validate theme files exist
            var validationService = new ThemeValidationService(_modPath);
            var validationResults = validationService.ValidateConfiguration(config);
            validationService.LogValidationResults();

            // Initialize story character themes from config
            if (themeManager != null)
            {
                var storyManager = themeManager.GetStoryCharacterManager();
                if (storyManager != null)
                {
                    // Story characters
                    storyManager.SetCurrentTheme("Cloud", config.Cloud);
                    storyManager.SetCurrentTheme("Agrias", config.Agrias);
                    storyManager.SetCurrentTheme("Orlandeau", config.Orlandeau);
                    storyManager.SetCurrentTheme("Mustadio", config.Mustadio);
                    storyManager.SetCurrentTheme("Reis", config.Reis);
                    storyManager.SetCurrentTheme("Rapha", config.Rapha);
                    storyManager.SetCurrentTheme("Marach", config.Marach);
                    storyManager.SetCurrentTheme("Beowulf", config.Beowulf);
                    storyManager.SetCurrentTheme("Meliadoul", config.Meliadoul);

                    // Log all themes for debugging
                    ModLogger.Log($"Applying initial Cloud theme: {config.Cloud}");
                    ModLogger.Log($"Applying initial Agrias theme: {config.Agrias}");
                    ModLogger.Log($"Applying initial Orlandeau theme: {config.Orlandeau}");
                    ModLogger.Log($"Applying initial Mustadio theme: {config.Mustadio}");
                    ModLogger.Log($"Applying initial Reis theme: {config.Reis}");
                    ModLogger.Log($"Applying initial Rapha theme: {config.Rapha}");
                    ModLogger.Log($"Applying initial Marach theme: {config.Marach}");
                    ModLogger.Log($"Applying initial Beowulf theme: {config.Beowulf}");
                    ModLogger.Log($"Applying initial Meliadoul theme: {config.Meliadoul}");
                }
            }
        }
    }
}
