using System;
using System.IO;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>The old theme manager, kept for callers not yet on the theme service.</summary>
    public class ThemeManagerLegacy
    {
        private readonly StoryCharacterThemeManager _storyCharacterManager;
        private readonly string _sourcePath;
        private readonly string _modPath;

        public ThemeManagerLegacy(string sourcePath, string modPath)
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
            ModLogger.Log(LogVerb.Theme, $"Orlandeau theme: {nextTheme}");
            ApplyOrlandeauTheme(nextTheme);
        }

        public void CycleAgriasTheme()
        {
            var nextTheme = _storyCharacterManager.CycleTheme("Agrias");
            ModLogger.LogDebug(LogVerb.Theme, $"================================================");
            ModLogger.Log(LogVerb.Theme, $"Agrias theme changed to {nextTheme}");
            ModLogger.LogDebug(LogVerb.Theme, $"================================================");
            ApplyAgriasTheme(nextTheme);
        }

        public void CycleCloudTheme()
        {
            var nextTheme = _storyCharacterManager.CycleTheme("Cloud");
            ModLogger.LogDebug(LogVerb.Theme, $"================================================");
            ModLogger.Log(LogVerb.Theme, $"Cloud theme changed to {nextTheme}");
            ModLogger.LogDebug(LogVerb.Theme, $"================================================");
            ApplyCloudTheme(nextTheme);
        }

        private void ApplyInitialOrlandeauTheme()
        {
            try
            {
                var currentTheme = _storyCharacterManager.GetCurrentTheme("Orlandeau");
                ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Orlandeau theme: {currentTheme}");
                ApplyOrlandeauTheme(currentTheme);
            }
            catch (Exception ex)
            {
                ModLogger.LogError(LogVerb.Theme, $"Applying the initial Orlandeau theme failed: {ex.Message}");
            }
        }

        private void ApplyOrlandeauTheme(string theme)
        {
            string themeDir = $"sprites_orlandeau_{theme.ToLower()}";
            var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", themeDir, "battle_oru_spr.bin");
            var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_oru_spr.bin");

            ModLogger.LogDebug(LogVerb.Theme, $"Looking for Orlandeau sprite at: {sourceFile}");
            if (File.Exists(sourceFile))
            {
                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(sourceFile, destFile, true);
                ModLogger.LogDebug(LogVerb.Theme, $"Successfully copied Orlandeau theme: {theme}");

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
                        ModLogger.LogDebug(LogVerb.Theme, $"Applied theme to {variant}");
                    }
                }
            }
            else
            {
                ModLogger.LogWarning(LogVerb.Theme, $"Orlandeau theme file not found at: {sourceFile}");
            }
        }

        private void ApplyInitialAgriasTheme()
        {
            try
            {
                var currentTheme = _storyCharacterManager.GetCurrentTheme("Agrias");
                ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Agrias theme: {currentTheme}");
                ApplyAgriasTheme(currentTheme);
            }
            catch (Exception ex)
            {
                ModLogger.LogError(LogVerb.Theme, $"Applying the initial Agrias theme failed: {ex.Message}");
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

                ModLogger.LogDebug(LogVerb.Theme, $"Looking for Agrias sprite at: {sourceFile}");
                if (File.Exists(sourceFile))
                {
                    // Ensure destination directory exists
                    var destDir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    File.Copy(sourceFile, destFile, true);
                    ModLogger.LogDebug(LogVerb.Theme, $"Successfully copied Agrias theme for {sprite}: {theme}");
                }
                else
                {
                    ModLogger.LogWarning(LogVerb.Theme, $"Agrias theme file not found at: {sourceFile}");
                }
            }
        }

        private void ApplyInitialCloudTheme()
        {
            try
            {
                var currentTheme = _storyCharacterManager.GetCurrentTheme("Cloud");
                ModLogger.LogDebug(LogVerb.Theme, $"Applying initial Cloud theme: {currentTheme}");
                ApplyCloudTheme(currentTheme);
            }
            catch (Exception ex)
            {
                ModLogger.LogError(LogVerb.Theme, $"Applying the initial Cloud theme failed: {ex.Message}");
            }
        }

        private void ApplyCloudTheme(string theme)
        {
            string themeDir = $"sprites_cloud_{theme.ToLower()}";
            var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", themeDir, "battle_cloud_spr.bin");
            var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_cloud_spr.bin");

            ModLogger.LogDebug(LogVerb.Theme, $"Looking for Cloud sprite at: {sourceFile}");
            if (File.Exists(sourceFile))
            {
                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(sourceFile, destFile, true);
                ModLogger.LogDebug(LogVerb.Theme, $"Successfully copied Cloud theme: {theme}");
            }
            else
            {
                ModLogger.LogWarning(LogVerb.Theme, $"Cloud theme file not found at: {sourceFile}");
            }
        }

    }
}
