using System;
using System.IO;
using FFTColorCustomizer.Core;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Handles sprite file copy operations.
    /// Extracted from ConfigBasedSpriteManager to follow Single Responsibility Principle.
    /// </summary>
    public class SpriteFileCopier
    {
        private readonly SpritePathResolver _pathResolver;

        public SpriteFileCopier(SpritePathResolver pathResolver)
        {
            _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        }

        /// <summary>
        /// Copies a themed sprite file to the destination, handling file lock scenarios gracefully.
        /// </summary>
        /// <param name="sourceFile">Full path to the source sprite file.</param>
        /// <param name="destFile">Full path to the destination sprite file.</param>
        /// <returns>True if copy succeeded, false if file was locked or copy failed.</returns>
        public bool CopySpriteFile(string sourceFile, string destFile)
        {
            if (!File.Exists(sourceFile))
            {
                ModLogger.LogWarning($"Source sprite not found: {sourceFile}");
                return false;
            }

            try
            {
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    ModLogger.LogDebug($"Created destination directory: {destDir}");
                }

                File.Copy(sourceFile, destFile, true);
                return true;
            }
            catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
            {
                ModLogger.LogDebug($"Sprite {Path.GetFileName(destFile)} is in use, will use path redirection");
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                ModLogger.LogDebug($"Cannot overwrite {Path.GetFileName(destFile)} (access denied), using path redirection");
                return false;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to copy sprite: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores a sprite to its original state by copying from sprites_original directory.
        /// </summary>
        /// <param name="spriteName">The sprite filename (e.g., "battle_knight_m_spr.bin").</param>
        /// <param name="unitPath">Optional unit path override. If null, uses default unit path.</param>
        /// <returns>True if restoration succeeded.</returns>
        public bool RestoreOriginalSprite(string spriteName, string unitPath = null)
        {
            var effectiveUnitPath = unitPath ?? _pathResolver.UnitPath;
            var originalDir = _pathResolver.GetOriginalSpriteDirectory(effectiveUnitPath);
            var originalFile = Path.Combine(originalDir, spriteName);
            var destFile = Path.Combine(effectiveUnitPath, spriteName);

            if (!File.Exists(originalFile))
            {
                ModLogger.LogWarning($"Original sprite not found: {originalFile}");
                return false;
            }

            var success = CopySpriteFile(originalFile, destFile);
            if (success)
            {
                ModLogger.Log($"✓ Restored original sprite: {spriteName}");
            }
            return success;
        }

        /// <summary>
        /// Copies a themed sprite for a generic job.
        /// </summary>
        /// <param name="spriteName">The sprite filename.</param>
        /// <param name="themeName">The theme name (e.g., "corpse_brigade").</param>
        /// <param name="jobType">The job type (e.g., "knight").</param>
        /// <param name="jobName">The full job name (e.g., "Knight_Male").</param>
        /// <returns>True if copy succeeded.</returns>
        public bool CopyThemedSprite(string spriteName, string themeName, string jobType, string jobName)
        {
            var unitPath = _pathResolver.GetUnitPathForJob(jobName);

            // First check for job-specific theme directory (e.g., sprites_mediator_holy_knight)
            var jobSpecificDir = _pathResolver.GetJobSpecificThemeDirectory(jobType, themeName, unitPath);
            var jobSpecificFile = Path.Combine(jobSpecificDir, spriteName);

            // Then check for generic theme directory (e.g., sprites_corpse_brigade)
            var genericDir = _pathResolver.GetThemeSpriteDirectory(themeName, unitPath);
            var genericFile = Path.Combine(genericDir, spriteName);

            var destFile = Path.Combine(unitPath, spriteName);

            string sourceFile;
            if (File.Exists(jobSpecificFile))
            {
                sourceFile = jobSpecificFile;
                ModLogger.Log($"Using job-specific theme: sprites_{jobType}_{themeName.ToLower()}");
            }
            else if (File.Exists(genericFile))
            {
                sourceFile = genericFile;
                ModLogger.Log($"Using generic theme: sprites_{themeName.ToLower()}");
            }
            else
            {
                ModLogger.LogWarning($"Theme not found: tried sprites_{jobType}_{themeName.ToLower()} and sprites_{themeName.ToLower()}");
                LogAvailableThemeDirectories(unitPath);
                return false;
            }

            var success = CopySpriteFile(sourceFile, destFile);
            if (success)
            {
                ModLogger.LogSuccess($"Copied {themeName} theme for {jobType} to {destFile}");
            }
            return success;
        }

        /// <summary>
        /// Copies a themed sprite for a story character.
        /// </summary>
        /// <param name="characterName">The character name (lowercase).</param>
        /// <param name="spriteName">The sprite name (without battle_ prefix or _spr.bin suffix).</param>
        /// <param name="themeName">The theme name.</param>
        /// <param name="unitPath">The unit path to use.</param>
        /// <returns>True if copy succeeded.</returns>
        public bool CopyStoryCharacterThemedSprite(string characterName, string spriteName, string themeName, string unitPath)
        {
            var themeNameLower = themeName.ToLower();

            // Try directory-based structure first
            var themeDir = $"sprites_{characterName}_{themeNameLower}";
            var sourceDirPath = Path.Combine(unitPath, themeDir, $"battle_{spriteName}_spr.bin");

            // Also check flat file structure for backward compatibility
            var sourceFlatPath = Path.Combine(unitPath, $"battle_{spriteName}_{themeNameLower}_spr.bin");

            string sourceFile;
            if (File.Exists(sourceDirPath))
            {
                sourceFile = sourceDirPath;
                ModLogger.Log($"Using directory-based theme: {themeDir}");
            }
            else if (File.Exists(sourceFlatPath))
            {
                sourceFile = sourceFlatPath;
                ModLogger.Log($"Using flat file theme: battle_{spriteName}_{themeName}_spr.bin");
            }
            else
            {
                ModLogger.Log($"Warning: Theme file not found for {characterName} - {themeName}");
                ModLogger.Log($"  Tried: {sourceDirPath}");
                ModLogger.Log($"  Tried: {sourceFlatPath}");
                return false;
            }

            var destFile = Path.Combine(unitPath, $"battle_{spriteName}_spr.bin");
            var success = CopySpriteFile(sourceFile, destFile);
            if (success)
            {
                ModLogger.LogSuccess($"Applied {characterName} theme: {themeName} - copied to {Path.GetFileName(destFile)}");
            }
            return success;
        }

        /// <summary>
        /// Restores a story character's original sprite.
        /// </summary>
        public bool RestoreStoryCharacterOriginalSprite(string characterName, string spriteName, string unitPath)
        {
            var originalDir = Path.Combine(unitPath, "sprites_original");
            var originalFile = Path.Combine(originalDir, $"battle_{spriteName}_spr.bin");
            var destFile = Path.Combine(unitPath, $"battle_{spriteName}_spr.bin");

            if (!File.Exists(originalFile))
            {
                ModLogger.LogWarning($"Original sprite not found for {characterName}: {originalFile}");
                return false;
            }

            var success = CopySpriteFile(originalFile, destFile);
            if (success)
            {
                ModLogger.Log($"✓ Restored original sprite for {characterName}: {spriteName}");
            }
            return success;
        }

        private void LogAvailableThemeDirectories(string unitPath)
        {
            if (Directory.Exists(unitPath))
            {
                var dirs = Directory.GetDirectories(unitPath, "sprites_*");
                ModLogger.LogDebug($"  Available theme directories: {dirs.Length} found");
                foreach (var dir in dirs)
                {
                    if (dirs.Length <= 5 || Array.IndexOf(dirs, dir) < 5)
                    {
                        ModLogger.LogDebug($"    - {Path.GetFileName(dir)}");
                    }
                }
            }
            else
            {
                ModLogger.LogError($"  Unit path does not exist: {unitPath}");
            }
        }
    }
}
