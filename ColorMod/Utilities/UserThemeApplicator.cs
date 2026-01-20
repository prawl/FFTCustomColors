using System;
using System.IO;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.ThemeEditor;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Handles application of user-created themes by merging palettes with original sprites.
    /// Extracted from ConfigBasedSpriteManager to follow Single Responsibility Principle.
    /// </summary>
    public class UserThemeApplicator
    {
        private const int PaletteSize = 512;

        private readonly SpritePathResolver _pathResolver;
        private readonly UserThemeService _userThemeService;

        public UserThemeApplicator(SpritePathResolver pathResolver, UserThemeService userThemeService)
        {
            _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
            _userThemeService = userThemeService ?? throw new ArgumentNullException(nameof(userThemeService));
        }

        /// <summary>
        /// Checks if the given theme is a user-created theme for the specified job/character.
        /// </summary>
        public bool IsUserTheme(string jobOrCharacterName, string themeName)
        {
            return _userThemeService.IsUserTheme(jobOrCharacterName, themeName);
        }

        /// <summary>
        /// Applies a user-created theme by combining the original sprite with the user's palette.
        /// </summary>
        /// <param name="spriteName">The sprite filename (e.g., "battle_knight_m_spr.bin").</param>
        /// <param name="themeName">The theme name.</param>
        /// <param name="jobName">The job name (e.g., "Knight_Male").</param>
        /// <returns>True if the theme was applied successfully.</returns>
        public bool ApplyUserTheme(string spriteName, string themeName, string jobName)
        {
            ModLogger.Log($"[APPLY_USER_THEME] START - sprite={spriteName}, theme={themeName}, job={jobName}");

            var palettePath = _userThemeService.GetUserThemePalettePath(jobName, themeName);
            ModLogger.Log($"[APPLY_USER_THEME] Palette path: {palettePath}");

            if (string.IsNullOrEmpty(palettePath))
            {
                ModLogger.LogWarning($"[APPLY_USER_THEME] Palette path is null/empty for {jobName}/{themeName}");
                return false;
            }

            if (!File.Exists(palettePath))
            {
                ModLogger.LogWarning($"[APPLY_USER_THEME] Palette file does not exist: {palettePath}");
                return false;
            }

            var unitPath = _pathResolver.GetUnitPathForJob(jobName);
            ModLogger.Log($"[APPLY_USER_THEME] Using unit path: {unitPath} (IsWotL: {_pathResolver.IsWotLJob(jobName)})");

            var originalDir = _pathResolver.GetOriginalSpriteDirectory(unitPath);
            var originalFile = Path.Combine(originalDir, spriteName);
            ModLogger.Log($"[APPLY_USER_THEME] Original sprite path: {originalFile}");
            ModLogger.Log($"[APPLY_USER_THEME] Original sprite exists: {File.Exists(originalFile)}");

            if (!File.Exists(originalFile))
            {
                ModLogger.LogWarning($"[APPLY_USER_THEME] Original sprite not found: {originalFile}");
                return false;
            }

            return ApplyPaletteToSprite(originalFile, palettePath, Path.Combine(unitPath, spriteName));
        }

        /// <summary>
        /// Applies a user-created theme for a story character.
        /// </summary>
        /// <param name="spriteName">The sprite filename (e.g., "battle_agrias_spr.bin").</param>
        /// <param name="themeName">The theme name.</param>
        /// <param name="characterName">The character name (proper case, e.g., "Agrias").</param>
        /// <param name="unitPath">The unit path to use.</param>
        /// <returns>The applied palette bytes if successful (for Ramza NXD patching), null otherwise.</returns>
        public byte[] ApplyStoryCharacterUserTheme(string spriteName, string themeName, string characterName, string unitPath)
        {
            ModLogger.Log($"[APPLY_STORY_USER_THEME] START - sprite={spriteName}, theme={themeName}, character={characterName}");

            var palettePath = _userThemeService.GetUserThemePalettePath(characterName, themeName);
            ModLogger.Log($"[APPLY_STORY_USER_THEME] Palette path: {palettePath}");

            if (string.IsNullOrEmpty(palettePath))
            {
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME] Palette path is null/empty for {characterName}/{themeName}");
                return null;
            }

            if (!File.Exists(palettePath))
            {
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME] Palette file does not exist: {palettePath}");
                return null;
            }

            // For story characters, check character-specific folder first, then generic
            var characterOriginalDir = Path.Combine(unitPath, $"sprites_{characterName.ToLower()}_original");
            var characterOriginalFile = Path.Combine(characterOriginalDir, spriteName);
            var genericOriginalDir = _pathResolver.GetOriginalSpriteDirectory(unitPath);
            var genericOriginalFile = Path.Combine(genericOriginalDir, spriteName);

            string originalFile;
            if (File.Exists(characterOriginalFile))
            {
                originalFile = characterOriginalFile;
                ModLogger.Log($"[APPLY_STORY_USER_THEME] Using character-specific original: {originalFile}");
            }
            else if (File.Exists(genericOriginalFile))
            {
                originalFile = genericOriginalFile;
                ModLogger.Log($"[APPLY_STORY_USER_THEME] Using generic original: {originalFile}");
            }
            else
            {
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME] Original sprite not found in either location:");
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME]   Character-specific: {characterOriginalFile}");
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME]   Generic: {genericOriginalFile}");
                return null;
            }

            var destFile = Path.Combine(unitPath, spriteName);
            var success = ApplyPaletteToSprite(originalFile, palettePath, destFile);

            if (success)
            {
                // Return the palette for potential Ramza NXD patching
                return File.ReadAllBytes(palettePath);
            }

            return null;
        }

        /// <summary>
        /// Applies a palette to a sprite file by replacing the first 512 bytes.
        /// </summary>
        private bool ApplyPaletteToSprite(string originalSpritePath, string palettePath, string destPath)
        {
            try
            {
                var originalSprite = File.ReadAllBytes(originalSpritePath);
                var userPalette = File.ReadAllBytes(palettePath);
                ModLogger.Log($"[APPLY_PALETTE] Original sprite size: {originalSprite.Length}, Palette size: {userPalette.Length}");

                if (userPalette.Length != PaletteSize)
                {
                    ModLogger.LogWarning($"[APPLY_PALETTE] Invalid palette size: {userPalette.Length} (expected {PaletteSize})");
                    return false;
                }

                // Replace palette in sprite (first 512 bytes)
                Array.Copy(userPalette, 0, originalSprite, 0, PaletteSize);

                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                ModLogger.Log($"[APPLY_PALETTE] Writing to: {destPath}");
                File.WriteAllBytes(destPath, originalSprite);

                ModLogger.LogSuccess($"[APPLY_PALETTE] SUCCESS - Created: {destPath}");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[APPLY_PALETTE] FAILED: {ex.Message}");
                ModLogger.LogError($"[APPLY_PALETTE] Stack: {ex.StackTrace}");
                return false;
            }
        }
    }
}
