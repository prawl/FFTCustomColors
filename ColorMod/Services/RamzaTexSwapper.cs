using System;
using System.IO;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Handles swapping of Ramza tex files when themes are activated
    /// </summary>
    public class RamzaTexSwapper
    {
        private readonly string _modRootPath;
        private readonly string _gameTexPath;

        // Tex file numbers by chapter
        private static readonly int[] Chapter1TexNumbers = { 830, 831 };
        private static readonly int[] Chapter23TexNumbers = { 832, 833 };
        private static readonly int[] Chapter4TexNumbers = { 834, 835 };

        // All tex numbers (for backward compatibility and cleanup)
        private readonly int[] _ramzaTexNumbers = { 830, 831, 832, 833, 834, 835 };

        public RamzaTexSwapper(string modRootPath, string gameTexPath)
        {
            _modRootPath = modRootPath;
            _gameTexPath = gameTexPath;
            ModLogger.LogDebug($"RamzaTexSwapper initialized - ModRoot: {_modRootPath}, Game: {_gameTexPath}");
        }

        /// <summary>
        /// Gets the tex file numbers for a specific chapter.
        /// </summary>
        public static int[] GetTexNumbersForChapter(int chapter)
        {
            return chapter switch
            {
                1 => Chapter1TexNumbers,
                2 => Chapter23TexNumbers,
                4 => Chapter4TexNumbers,
                _ => Array.Empty<int>()
            };
        }

        /// <summary>
        /// Swaps tex files for all chapters based on their individual theme selections.
        /// This is the NEW method that supports per-chapter themes.
        /// </summary>
        /// <param name="ch1Theme">Theme for Chapter 1 (or "original" to restore)</param>
        /// <param name="ch23Theme">Theme for Chapter 2/3 (or "original" to restore)</param>
        /// <param name="ch4Theme">Theme for Chapter 4 (or "original" to restore)</param>
        public void SwapTexFilesPerChapter(string ch1Theme, string ch23Theme, string ch4Theme)
        {
            // Clean up theme directories
            RemoveAllThemeDirectories();

            // Ensure g2d directory exists
            if (!Directory.Exists(_gameTexPath))
            {
                ModLogger.LogDebug($"Creating g2d directory: {_gameTexPath}");
                Directory.CreateDirectory(_gameTexPath);
            }

            ModLogger.Log($"Applying per-chapter tex themes: Ch1={ch1Theme}, Ch23={ch23Theme}, Ch4={ch4Theme}");

            // Apply each chapter's theme separately
            int totalCopied = 0;
            totalCopied += SwapTexFilesForChapter(1, ch1Theme);
            totalCopied += SwapTexFilesForChapter(2, ch23Theme);
            totalCopied += SwapTexFilesForChapter(4, ch4Theme);

            ModLogger.Log($"Activated per-chapter themes with {totalCopied} tex files (restart required)");
        }

        /// <summary>
        /// Swaps tex files for a specific chapter only.
        /// </summary>
        /// <param name="chapter">Chapter number (1, 2, or 4)</param>
        /// <param name="themeName">Theme name (or "original" to restore)</param>
        /// <returns>Number of files copied</returns>
        public int SwapTexFilesForChapter(int chapter, string themeName)
        {
            var texNumbers = GetTexNumbersForChapter(chapter);
            if (texNumbers.Length == 0)
            {
                ModLogger.LogWarning($"Invalid chapter number: {chapter}");
                return 0;
            }

            // For "original" theme, remove the tex files for this chapter
            if (themeName.ToLower() == "original")
            {
                foreach (int texNumber in texNumbers)
                {
                    string texFile = Path.Combine(_gameTexPath, $"tex_{texNumber}.bin");
                    if (File.Exists(texFile))
                    {
                        File.Delete(texFile);
                        ModLogger.LogDebug($"Removed tex_{texNumber}.bin for original theme");
                    }
                }
                return 0;
            }

            // Theme files are stored in RamzaThemes folder at mod root
            string themeSourceDir = Path.Combine(_modRootPath, "RamzaThemes", themeName);

            if (!Directory.Exists(themeSourceDir))
            {
                ModLogger.LogWarning($"Theme directory does not exist: {themeSourceDir}");
                return 0;
            }

            int copiedCount = 0;
            foreach (int texNumber in texNumbers)
            {
                string sourceFile = Path.Combine(themeSourceDir, $"tex_{texNumber}.bin");
                string destFile = Path.Combine(_gameTexPath, $"tex_{texNumber}.bin");

                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, destFile, overwrite: true);
                    ModLogger.LogDebug($"Copied tex_{texNumber}.bin from {themeName} for chapter {chapter}");
                    copiedCount++;
                }
                else
                {
                    ModLogger.LogWarning($"Source file not found: {sourceFile}");
                }
            }

            return copiedCount;
        }

        /// <summary>
        /// DEPRECATED: Swaps ALL tex files for a single theme (applies same theme to all chapters).
        /// Use SwapTexFilesPerChapter instead for per-chapter theming.
        /// </summary>
        public void SwapTexFilesForTheme(string themeName)
        {
            // Clean up theme directories but don't remove tex files yet - we'll overwrite them
            RemoveAllThemeDirectories();

            // Theme files are stored in RamzaThemes folder at mod root
            string themeSourceDir = Path.Combine(_modRootPath, "RamzaThemes", themeName);

            ModLogger.LogDebug($"Activating theme '{themeName}' from {themeSourceDir}");

            if (!Directory.Exists(themeSourceDir))
            {
                ModLogger.LogError($"Theme directory does not exist: {themeSourceDir}");
                return;
            }

            // Ensure g2d directory exists (it might not in production builds)
            if (!Directory.Exists(_gameTexPath))
            {
                ModLogger.LogDebug($"Creating g2d directory: {_gameTexPath}");
                Directory.CreateDirectory(_gameTexPath);
            }

            // Copy tex files directly to the g2d directory
            int copiedCount = 0;
            foreach (int texNumber in _ramzaTexNumbers)
            {
                string sourceFile = Path.Combine(themeSourceDir, $"tex_{texNumber}.bin");
                string destFile = Path.Combine(_gameTexPath, $"tex_{texNumber}.bin");

                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, destFile, overwrite: true);
                    ModLogger.LogDebug($"Copied tex_{texNumber}.bin to g2d directory");
                    copiedCount++;
                }
                else
                {
                    ModLogger.LogWarning($"Source file not found: {sourceFile}");
                }
            }
            ModLogger.Log($"Activated {themeName} theme with {copiedCount} tex files (restart required)");
        }

        private void RemoveMainDirectoryTexFiles()
        {
            foreach (int texNumber in _ramzaTexNumbers)
            {
                string texFile = Path.Combine(_gameTexPath, $"tex_{texNumber}.bin");
                if (File.Exists(texFile))
                {
                    File.Delete(texFile);
                    ModLogger.LogDebug($"Removed tex_{texNumber}.bin from main directory");
                }
            }
        }

        private void RemoveAllThemeDirectories()
        {
            // Remove any subdirectories that might contain tex files
            // The game searches subdirectories, so we need to clean them all
            string[] themeDirectories = { "themes", "active_theme", "white_heretic", "black_variant", "red_variant", "test_variant" };

            foreach (var dirName in themeDirectories)
            {
                string themeDir = Path.Combine(_gameTexPath, dirName);
                if (Directory.Exists(themeDir))
                {
                    Directory.Delete(themeDir, true);
                    ModLogger.LogDebug($"Removed {dirName} directory");
                }
            }
        }

        public void RestoreOriginalTexFiles()
        {
            // For original theme, remove all tex files to let game use built-in textures
            ModLogger.LogDebug($"Restoring original textures by removing all mod tex files");

            // Remove files from main directory
            RemoveMainDirectoryTexFiles();

            // Remove ALL theme directories that might contain Ramza tex files
            RemoveAllThemeDirectories();

            ModLogger.Log($"Restored original textures - using game's built-in files (restart required)");
        }
    }
}