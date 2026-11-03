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
    /// Handles initialization of mod components and dependencies.
    /// </summary>
    public class ModInitializer
    {
        private readonly string _modPath;
        private readonly string _sourcePath;

        public ModInitializer(string modPath, bool isTestEnvironment = false)
        {
            _modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));
            // In deployment, source and mod path are the same
            _sourcePath = _modPath;
        }

        /// <summary>
        /// Initialize the registry system
        /// </summary>
        public void InitializeRegistry()
        {
            // Clear and auto-discover all story characters with attributes
            StoryCharacterRegistry.Clear();
            StoryCharacterRegistry.AutoDiscoverCharacters();

            ModLogger.LogDebug(LogVerb.Startup, $"Registry initialized with {StoryCharacterRegistry.GetAllCharacterNames().Count()} story characters");

            // Initialize job class service with mod path
            JobClassServiceSingleton.Initialize(_modPath);
            var jobClassService = JobClassServiceSingleton.Instance;
            ModLogger.LogDebug(LogVerb.Startup, $"Loaded {jobClassService.GetAllJobClasses().Count()} generic job classes from JobClasses.json");
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
                ModLogger.LogDebug(LogVerb.Startup, $"Auto-detected {schemes.Count} color schemes");
                colorCycler.SetCurrentScheme(DefaultTheme);
            }
            else
            {
                ModLogger.LogWarning(LogVerb.Startup, $"No color schemes found in: {spritesPath}");
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
            _ = validationService.ValidateConfiguration(config);
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
                    storyManager.SetCurrentTheme("Construct8", config.Construct8);

                    // NPC characters
                    storyManager.SetCurrentTheme("Alma", config.Alma);
                    storyManager.SetCurrentTheme("Argath", config.Argath);
                    storyManager.SetCurrentTheme("Celia", config.Celia);
                    storyManager.SetCurrentTheme("DelitaChapter1", config.DelitaChapter1);
                    storyManager.SetCurrentTheme("DelitaChapter23", config.DelitaChapter23);
                    storyManager.SetCurrentTheme("DelitaChapter4", config.DelitaChapter4);
                    storyManager.SetCurrentTheme("Elmdore", config.Elmdore);
                    storyManager.SetCurrentTheme("Gaffgarion", config.Gaffgarion);
                    storyManager.SetCurrentTheme("Isilud", config.Isilud);
                    storyManager.SetCurrentTheme("Lettie", config.Lettie);
                    storyManager.SetCurrentTheme("Orran", config.Orran);
                    storyManager.SetCurrentTheme("Ovelia", config.Ovelia);
                    storyManager.SetCurrentTheme("Simon", config.Simon);
                    storyManager.SetCurrentTheme("Valmafra", config.Valmafra);
                    storyManager.SetCurrentTheme("Zalmour", config.Zalmour);

                    // Log all themes for debugging
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Cloud theme: {config.Cloud}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Agrias theme: {config.Agrias}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Orlandeau theme: {config.Orlandeau}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Mustadio theme: {config.Mustadio}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Reis theme: {config.Reis}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Rapha theme: {config.Rapha}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Marach theme: {config.Marach}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Beowulf theme: {config.Beowulf}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Meliadoul theme: {config.Meliadoul}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Construct8 theme: {config.Construct8}");
                    ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Alma theme: {config.Alma}");
                }
            }
        }
    }
}
