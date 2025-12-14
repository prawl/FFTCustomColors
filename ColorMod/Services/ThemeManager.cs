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
            _storyCharacterManager = new StoryCharacterThemeManager();
            _sourcePath = sourcePath;
            _modPath = modPath;
        }

        public void ApplyInitialThemes()
        {
            ApplyInitialOrlandeauTheme();
            ApplyInitialAgriasTheme();
        }

        public void CycleOrlandeauTheme()
        {
            var nextTheme = _storyCharacterManager.CycleOrlandeauTheme();
            Console.WriteLine($"[FFT Color Mod] Orlandeau theme: {nextTheme}");
            ApplyOrlandeauTheme(nextTheme.ToString());
        }

        public void CycleAgriasTheme()
        {
            var nextTheme = _storyCharacterManager.CycleAgriasTheme();
            Console.WriteLine("================================================");
            Console.WriteLine($"    AGRIAS THEME CHANGED TO: {nextTheme}");
            Console.WriteLine("================================================");
            ApplyAgriasTheme(nextTheme.ToString());
        }

        private void ApplyInitialOrlandeauTheme()
        {
            try
            {
                var currentTheme = _storyCharacterManager.GetCurrentOrlandeauTheme();
                Console.WriteLine($"[FFT Color Mod] Applying initial Orlandeau theme: {currentTheme}");
                ApplyOrlandeauTheme(currentTheme.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FFT Color Mod] Error applying initial Orlandeau theme: {ex.Message}");
            }
        }

        private void ApplyOrlandeauTheme(string theme)
        {
            string themeDir = $"sprites_orlandeau_{theme.ToLower()}";
            var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", themeDir, "battle_oru_spr.bin");
            var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_oru_spr.bin");

            Console.WriteLine($"[FFT Color Mod] Looking for Orlandeau sprite at: {sourceFile}");
            if (File.Exists(sourceFile))
            {
                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(sourceFile, destFile, true);
                Console.WriteLine($"[FFT Color Mod] Successfully copied Orlandeau theme: {theme}");

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
                        Console.WriteLine($"[FFT Color Mod] Applied theme to {variant}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[FFT Color Mod] Warning: Orlandeau theme file not found at: {sourceFile}");
            }
        }

        private void ApplyInitialAgriasTheme()
        {
            try
            {
                var currentTheme = _storyCharacterManager.GetCurrentAgriasTheme();
                Console.WriteLine($"[FFT Color Mod] Applying initial Agrias theme: {currentTheme}");
                ApplyAgriasTheme(currentTheme.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FFT Color Mod] Error applying initial Agrias theme: {ex.Message}");
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

                Console.WriteLine($"[FFT Color Mod] Looking for Agrias sprite at: {sourceFile}");
                if (File.Exists(sourceFile))
                {
                    // Ensure destination directory exists
                    var destDir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    File.Copy(sourceFile, destFile, true);
                    Console.WriteLine($"[FFT Color Mod] Successfully copied Agrias theme for {sprite}: {theme}");
                }
                else
                {
                    Console.WriteLine($"[FFT Color Mod] Warning: Agrias theme file not found at: {sourceFile}");
                }
            }
        }
    }
}