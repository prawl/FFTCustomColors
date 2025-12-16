using System;
using System.IO;
using FFTColorMod.Utilities;

namespace FFTColorMod.Services
{
    public class ThemeManager
    {
        private readonly StoryCharacterThemeManager _storyCharacterManager;
        private readonly string _sourcePath;
        private readonly string _modPath;

        public ThemeManager(string sourcePath, string modPath)
        {
            _storyCharacterManager = new StoryCharacterThemeManager(sourcePath);
            _sourcePath = sourcePath;
            _modPath = modPath;
        }

        public StoryCharacterThemeManager GetStoryCharacterManager()
        {
            return _storyCharacterManager;
        }

        public void ApplyInitialThemes()
        {
            ApplyInitialOrlandeauTheme();
            ApplyInitialAgriasTheme();
            ApplyInitialCloudTheme();
        }

        public void CycleOrlandeauTheme()
        {
            var nextTheme = _storyCharacterManager.CycleTheme("Orlandeau");
            ModLogger.Log($"Orlandeau theme: {nextTheme}");
            ApplyOrlandeauTheme(nextTheme);
        }

        public void CycleAgriasTheme()
        {
            var nextTheme = _storyCharacterManager.CycleTheme("Agrias");
            ModLogger.Log($"================================================");
            ModLogger.Log($"    AGRIAS THEME CHANGED TO: {nextTheme}");
            ModLogger.Log($"================================================");
            ApplyAgriasTheme(nextTheme);
        }

        public void CycleCloudTheme()
        {
            var nextTheme = _storyCharacterManager.CycleTheme("Cloud");
            ModLogger.Log($"================================================");
            ModLogger.Log($"    CLOUD THEME CHANGED TO: {nextTheme}");
            ModLogger.Log($"================================================");
            ApplyCloudTheme(nextTheme);
        }

        private void ApplyInitialOrlandeauTheme()
        {
            try
            {
                var currentTheme = _storyCharacterManager.GetCurrentTheme("Orlandeau");
                ModLogger.Log($"Applying initial Orlandeau theme: {currentTheme}");
                ApplyOrlandeauTheme(currentTheme);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"applying initial Orlandeau theme: {ex.Message}");
            }
        }

        private void ApplyOrlandeauTheme(string theme)
        {
            string themeDir = $"sprites_orlandeau_{theme.ToLower()}";
            var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", themeDir, "battle_oru_spr.bin");
            var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_oru_spr.bin");

            ModLogger.LogDebug($"Looking for Orlandeau sprite at: {sourceFile}");
            if (File.Exists(sourceFile))
            {
                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(sourceFile, destFile, true);
                ModLogger.LogSuccess($"Successfully copied Orlandeau theme: {theme}");

                // Also copy the other Orlandeau variants
                string[] variants = { "battle_goru_spr.bin", "battle_voru_spr.bin" };
                foreach (var variant in variants)
                {
                    var variantSource = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", themeDir, variant);
                    var variantDest = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", variant);
                    if (File.Exists(variantSource))
                    {
                        // Ensure destination directory exists
                        var variantDestDir = Path.GetDirectoryName(variantDest);
                        if (!string.IsNullOrEmpty(variantDestDir) && !Directory.Exists(variantDestDir))
                        {
                            Directory.CreateDirectory(variantDestDir);
                        }
                        File.Copy(variantSource, variantDest, true);
                        ModLogger.Log($"Applied theme to {variant}");
                    }
                }
            }
            else
            {
                ModLogger.LogWarning($"Orlandeau theme file not found at: {sourceFile}");
            }
        }

        private void ApplyInitialAgriasTheme()
        {
            try
            {
                var currentTheme = _storyCharacterManager.GetCurrentTheme("Agrias");
                ModLogger.Log($"Applying initial Agrias theme: {currentTheme}");
                ApplyAgriasTheme(currentTheme);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"applying initial Agrias theme: {ex.Message}");
            }
        }

        private void ApplyAgriasTheme(string theme)
        {
            string themeDir = $"sprites_agrias_{theme.ToLower()}";
            string[] agriasSprites = { "battle_aguri_spr.bin", "battle_kanba_spr.bin" };

            foreach (var sprite in agriasSprites)
            {
                var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", themeDir, sprite);
                var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", sprite);

                ModLogger.LogDebug($"Looking for Agrias sprite at: {sourceFile}");
                if (File.Exists(sourceFile))
                {
                    // Ensure destination directory exists
                    var destDir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    File.Copy(sourceFile, destFile, true);
                    ModLogger.LogSuccess($"Successfully copied Agrias theme for {sprite}: {theme}");
                }
                else
                {
                    ModLogger.LogWarning($"Agrias theme file not found at: {sourceFile}");
                }
            }
        }

        private void ApplyInitialCloudTheme()
        {
            try
            {
                var currentTheme = _storyCharacterManager.GetCurrentTheme("Cloud");
                ModLogger.Log($"Applying initial Cloud theme: {currentTheme}");
                ApplyCloudTheme(currentTheme);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"applying initial Cloud theme: {ex.Message}");
            }
        }

        private void ApplyCloudTheme(string theme)
        {
            string themeDir = $"sprites_cloud_{theme.ToLower()}";
            var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", themeDir, "battle_cloud_spr.bin");
            var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_cloud_spr.bin");

            ModLogger.LogDebug($"Looking for Cloud sprite at: {sourceFile}");
            if (File.Exists(sourceFile))
            {
                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(sourceFile, destFile, true);
                ModLogger.LogSuccess($"Successfully copied Cloud theme: {theme}");
            }
            else
            {
                ModLogger.LogWarning($"Cloud theme file not found at: {sourceFile}");
            }
        }

    }
}