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
        private readonly int[] _ramzaTexNumbers = { 830, 831, 832, 833, 834, 835 };

        public RamzaTexSwapper(string modRootPath, string gameTexPath)
        {
            _modRootPath = modRootPath;
            _gameTexPath = gameTexPath;
            ModLogger.LogDebug($"RamzaTexSwapper initialized - ModRoot: {_modRootPath}, Game: {_gameTexPath}");
        }

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