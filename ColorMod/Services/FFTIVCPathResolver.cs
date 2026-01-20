using System;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Centralized path resolution for FFTIVC directories.
    /// Consolidates the duplicated path-finding logic from ConfigBasedSpriteManager,
    /// CharacterRowBuilder, ConfigurationCoordinator, and ThemeManagerAdapter.
    /// </summary>
    public static class FFTIVCPathResolver
    {
        /// <summary>
        /// Finds the FFTIVC unit directory path, searching versioned directories if needed.
        /// </summary>
        /// <param name="modPath">The mod installation path</param>
        /// <returns>Path to the unit directory (may not exist)</returns>
        public static string FindUnitPath(string modPath)
        {
            return FindFFTIVCSubPath(modPath, "unit");
        }

        /// <summary>
        /// Finds the FFTIVC unit_psp directory path (for WotL jobs), searching versioned directories if needed.
        /// </summary>
        /// <param name="modPath">The mod installation path</param>
        /// <returns>Path to the unit_psp directory (may not exist)</returns>
        public static string FindUnitPspPath(string modPath)
        {
            return FindFFTIVCSubPath(modPath, "unit_psp");
        }

        /// <summary>
        /// Finds a specific FFTIVC subdirectory path, searching versioned directories if needed.
        /// </summary>
        /// <param name="modPath">The mod installation path</param>
        /// <param name="subDirectory">The subdirectory under fftpack (e.g., "unit" or "unit_psp")</param>
        /// <returns>Path to the subdirectory (may not exist)</returns>
        public static string FindFFTIVCSubPath(string modPath, string subDirectory)
        {
            // First try the direct path
            var directPath = Path.Combine(modPath, "FFTIVC", "data", "enhanced", "fftpack", subDirectory);
            if (Directory.Exists(directPath))
            {
                ModLogger.Log($"Found FFTIVC {subDirectory} at direct path: {directPath}");
                return directPath;
            }

            // If not found, check if we're in a subdirectory and FFTIVC is in a versioned parent
            var parentDir = Path.GetDirectoryName(modPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                var versionedPath = FindInVersionedDirectories(parentDir, subDirectory);
                if (versionedPath != null)
                {
                    return versionedPath;
                }
            }

            // As a fallback, return the expected path even if it doesn't exist
            ModLogger.LogWarning($"FFTIVC {subDirectory} not found, using expected path: {directPath}");
            return directPath;
        }

        /// <summary>
        /// Searches for FFTIVC path in versioned FFTColorCustomizer directories.
        /// </summary>
        private static string? FindInVersionedDirectories(string parentDir, string subDirectory)
        {
            try
            {
                var versionedDirs = Directory.GetDirectories(parentDir, "FFTColorCustomizer_v*")
                    .OrderByDescending(ExtractVersionNumber)
                    .ToArray();

                foreach (var versionedDir in versionedDirs)
                {
                    var versionedPath = Path.Combine(versionedDir, "FFTIVC", "data", "enhanced", "fftpack", subDirectory);
                    if (Directory.Exists(versionedPath))
                    {
                        ModLogger.Log($"Found FFTIVC {subDirectory} in versioned directory: {versionedPath}");
                        return versionedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogWarning($"Error searching for versioned directories: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extracts the version number from a versioned directory name.
        /// </summary>
        private static int ExtractVersionNumber(string directoryPath)
        {
            var dirName = Path.GetFileName(directoryPath);
            var versionStr = dirName.Substring(dirName.LastIndexOf('v') + 1);
            if (int.TryParse(versionStr, out int version))
                return version;
            return 0;
        }

        /// <summary>
        /// Finds the actual mod installation path from a user config path.
        /// Handles the case where config is stored in User/Mods/ but actual mod is in Mods/.
        /// </summary>
        /// <param name="configPath">Path to the config file</param>
        /// <returns>The actual mod installation path, or null if not found</returns>
        public static string? FindModPathFromConfigPath(string configPath)
        {
            // If config path is in User directory, find the actual mod installation
            if (!configPath.Contains(@"User\Mods") && !configPath.Contains(@"User/Mods"))
            {
                // Config is not in User directory, return its parent as the mod path
                return Path.GetDirectoryName(configPath);
            }

            var configDir = Path.GetDirectoryName(configPath);
            if (configDir == null)
                return null;

            var userModsIdx = configDir.IndexOf(@"User\Mods", StringComparison.OrdinalIgnoreCase);
            if (userModsIdx == -1)
                userModsIdx = configDir.IndexOf(@"User/Mods", StringComparison.OrdinalIgnoreCase);

            if (userModsIdx < 0)
                return null;

            var reloadedRoot = configDir.Substring(0, userModsIdx);
            var modsDir = Path.Combine(reloadedRoot, "Mods");

            // First try the non-versioned path
            var directPath = Path.Combine(modsDir, "FFTColorCustomizer");
            if (Directory.Exists(directPath))
            {
                return directPath;
            }

            // Look for versioned directories and use the highest version
            try
            {
                var versionedDirs = Directory.GetDirectories(modsDir, "FFTColorCustomizer_v*")
                    .OrderByDescending(ExtractVersionNumber)
                    .ToArray();

                if (versionedDirs.Length > 0)
                {
                    return versionedDirs[0];
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogWarning($"Error searching for versioned mod directories: {ex.Message}");
            }

            return null;
        }
    }
}
