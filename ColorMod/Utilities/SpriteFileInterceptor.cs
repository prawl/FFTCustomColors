using System;
using System.IO;
using System.Text.RegularExpressions;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.ThemeEditor;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Handles runtime file path interception for sprite loading.
    /// Extracted from ConfigBasedSpriteManager to follow Single Responsibility Principle.
    /// </summary>
    public class SpriteFileInterceptor
    {
        private readonly SpritePathResolver _pathResolver;
        private readonly ConfigurationManager _configManager;
        private readonly UserThemeService _userThemeService;

        public SpriteFileInterceptor(
            SpritePathResolver pathResolver,
            ConfigurationManager configManager,
            UserThemeService userThemeService)
        {
            _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _userThemeService = userThemeService ?? throw new ArgumentNullException(nameof(userThemeService));
        }

        /// <summary>
        /// Intercepts a file path request and redirects to themed sprite if applicable.
        /// </summary>
        /// <param name="originalPath">The original file path being requested.</param>
        /// <returns>The redirected path if a theme is configured, otherwise the original path.</returns>
        public string InterceptFilePath(string originalPath)
        {
            var fileName = Path.GetFileName(originalPath);

            // Get the job property for this sprite
            var jobProperty = _configManager.GetJobPropertyForSprite(fileName);
            if (jobProperty == null)
                return originalPath;

            // Get the configured color for this job
            var config = _configManager.LoadConfig();
            var propertyInfo = typeof(Config).GetProperty(jobProperty);
            if (propertyInfo == null)
            {
                ModLogger.LogWarning($"Property not found: {jobProperty}");
                return originalPath;
            }

            var colorSchemeValue = propertyInfo.GetValue(config);
            if (colorSchemeValue == null || !(colorSchemeValue is string))
            {
                ModLogger.LogWarning($"No color scheme for: {jobProperty}");
                return originalPath;
            }

            var colorSchemeOriginal = colorSchemeValue.ToString();
            var colorScheme = colorSchemeOriginal.ToLower();
            ModLogger.Log($"{jobProperty} configured as: {colorScheme}");

            if (colorScheme == "original")
            {
                return originalPath;
            }

            // User themes are written directly to base unit folder by ApplyUserTheme,
            // so no special interception is needed
            var isUserTheme = _userThemeService.IsUserTheme(jobProperty, colorSchemeOriginal);
            if (isUserTheme)
            {
                ModLogger.Log($"[USER_THEME_CHECK] {jobProperty} uses user theme '{colorSchemeOriginal}' - file should be in base folder");
                return originalPath;
            }

            // Build path to the themed sprite
            var variantPath = Path.Combine(_pathResolver.UnitPath, $"sprites_{colorScheme}", fileName);
            if (File.Exists(variantPath))
            {
                ModLogger.LogDebug($"Redirecting {fileName} to themed variant: {colorScheme}");
                return variantPath;
            }

            // If the themed file doesn't exist in the expected location,
            // check if it exists in the source location (but don't copy it during interception)
            if (originalPath.Contains("sprites_"))
            {
                var pattern = @"sprites_[^\\\/]*";
                return Regex.Replace(originalPath, pattern, $"sprites_{colorScheme}");
            }

            return originalPath.Replace(fileName, Path.Combine($"sprites_{colorScheme}", fileName));
        }

        /// <summary>
        /// Gets the active color scheme for a job property.
        /// </summary>
        /// <param name="jobProperty">The job property name (e.g., "Knight_Male").</param>
        /// <returns>The display name of the active theme.</returns>
        public string GetActiveColorForJob(string jobProperty)
        {
            var config = _configManager.LoadConfig();
            var propertyInfo = typeof(Config).GetProperty(jobProperty);
            if (propertyInfo == null)
                return "Original";

            var colorSchemeValue = propertyInfo.GetValue(config) as string;
            var themeName = colorSchemeValue ?? "original";

            return _pathResolver.ConvertThemeNameToDisplayName(themeName);
        }
    }
}
