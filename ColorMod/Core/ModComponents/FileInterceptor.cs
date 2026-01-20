using System;
using System.IO;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using static FFTColorCustomizer.Core.ColorModConstants;

namespace FFTColorCustomizer.Core.ModComponents
{
    /// <summary>
    /// Handles file path interception for sprite replacement.
    /// Extracted from Mod.cs to separate concerns.
    /// </summary>
    public class FileInterceptor
    {
        private readonly string _modPath;
        private readonly ThemeCoordinator _themeCoordinator;
        private readonly Func<string, string?> _getJobColorFunc;

        /// <summary>
        /// Creates a new FileInterceptor
        /// </summary>
        /// <param name="modPath">The mod installation path</param>
        /// <param name="themeCoordinator">The theme coordinator for global theme handling</param>
        /// <param name="getJobColorFunc">Function to get job color for a specific sprite (for per-job config)</param>
        public FileInterceptor(
            string modPath,
            ThemeCoordinator themeCoordinator,
            Func<string, string?> getJobColorFunc)
        {
            _modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));
            _themeCoordinator = themeCoordinator ?? throw new ArgumentNullException(nameof(themeCoordinator));
            _getJobColorFunc = getJobColorFunc ?? throw new ArgumentNullException(nameof(getJobColorFunc));
        }

        /// <summary>
        /// Intercepts a file path and returns the themed version if applicable
        /// </summary>
        /// <param name="originalPath">The original file path requested</param>
        /// <returns>The themed path if a theme is active and sprite exists, otherwise the original path</returns>
        public string InterceptFilePath(string originalPath)
        {
            ModLogger.LogDebug($"[INTERCEPT] Called with path: {originalPath}");

            // First, try to use per-job configuration if available
            // This fixes the hot reload issue where F1 config changes weren't being applied
            var fileName = Path.GetFileName(originalPath);

            // Check if this is a job sprite that might have a per-job configuration
            if (fileName.Contains("battle_") && fileName.EndsWith("_spr.bin"))
            {
                var jobColor = _getJobColorFunc(fileName);
                if (!string.IsNullOrEmpty(jobColor) && jobColor != "original")
                {
                    // Build the themed sprite path
                    var themedDir = Path.Combine(_modPath, FFTIVCPath, DataPath, EnhancedPath,
                        FFTPackPath, UnitPath, $"{SpritesPrefix}{jobColor}");
                    var themedPath = Path.Combine(themedDir, fileName);

                    // Return themed path if it exists
                    if (File.Exists(themedPath))
                    {
                        ModLogger.LogSuccess($"[INTERCEPT] Per-job redirect: {Path.GetFileName(originalPath)} -> {jobColor}/{Path.GetFileName(themedPath)}");
                        return themedPath;
                    }
                }
            }

            // Fall back to the original global theme behavior
            var result = _themeCoordinator.InterceptFilePath(originalPath);
            if (result != originalPath)
            {
                ModLogger.LogSuccess($"[INTERCEPT] Global redirect: {Path.GetFileName(originalPath)} -> {Path.GetFileName(result)}");
                Console.WriteLine($"[FFT Color Mod] Intercepted: {Path.GetFileName(originalPath)} -> {Path.GetFileName(result)}");
            }
            else
            {
                // Log why interception didn't happen
                if (originalPath.Contains(".bin") && originalPath.Contains("battle_"))
                {
                    ModLogger.LogDebug($"[INTERCEPT] No redirect for sprite: {Path.GetFileName(originalPath)}");
                }
            }
            return result;
        }
    }
}
