using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FFTColorCustomizer.Utilities
{

    public class SpriteFileManager
    {
        private readonly string _modPath;        // Deployment path
        private readonly string _sourcePath;     // Git repo path for themes
        private readonly Dictionary<string, string> _pacFileMapping = new();

        public SpriteFileManager(string modPath, string sourcePath = null)
        {
            _modPath = modPath;
            // If no source path provided, use mod path (backward compatibility)
            _sourcePath = sourcePath ?? modPath;
            InitializePacMappings();
        }

        // TODO, REVIEW, This .pac file mapping may no longer be needed
        // The mod now uses direct file swapping instead of PAC file manipulation
        // Consider removing this entire mapping system if confirmed unused
        private void InitializePacMappings()
        {
            _pacFileMapping["original"] = "pack000.pac";
            _pacFileMapping["corpse_brigade"] = "pack001.pac";
            _pacFileMapping["lucavi"] = "pack002.pac";
            _pacFileMapping["northern_sky"] = "pack003.pac";
            _pacFileMapping["southern_sky"] = "pack004.pac";
            _pacFileMapping["crimson_red"] = "pack005.pac";
            _pacFileMapping["royal_purple"] = "pack006.pac";
            _pacFileMapping["phoenix_flame"] = "pack007.pac";
            _pacFileMapping["frost_knight"] = "pack008.pac";
            _pacFileMapping["silver_knight"] = "pack009.pac";
            _pacFileMapping["emerald_dragon"] = "pack011.pac";
            _pacFileMapping["rose_gold"] = "pack012.pac";
            _pacFileMapping["ocean_depths"] = "pack013.pac";
            _pacFileMapping["golden_templar"] = "pack014.pac";
            _pacFileMapping["blood_moon"] = "pack015.pac";
            _pacFileMapping["celestial"] = "pack016.pac";
            _pacFileMapping["volcanic"] = "pack017.pac";
            _pacFileMapping["amethyst"] = "pack018.pac";
        }

        public void SwitchColorScheme(string colorScheme)
        {
            ModLogger.LogDebug(LogVerb.Sprite, $"Switching to {colorScheme} color scheme");

            // Read from source (git repo), write to deployment
            string sourceDir = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", $"sprites_{colorScheme}");
            string targetDir = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            ModLogger.LogDebug(LogVerb.Sprite, $"Looking for modified sprites in: {sourceDir}");

            if (!Directory.Exists(sourceDir))
            {
                // If it's the original scheme and the directory doesn't exist, that's OK, we just don't swap anything
                if (colorScheme == "original")
                {
                    ModLogger.LogDebug(LogVerb.Sprite, $"Original scheme selected - no sprite swapping needed");
                    return;
                }

                ModLogger.LogWarning(LogVerb.Sprite, $"Variant directory does not exist: {sourceDir}");
                return;
            }

            try
            {
                var spriteFiles = Directory.GetFiles(sourceDir, "*.bin")
                    .ToList();

                if (spriteFiles.Count == 0)
                {
                    ModLogger.LogDebug(LogVerb.Sprite, $"No modified sprites found for {colorScheme}");
                    return;
                }

                ModLogger.LogDebug(LogVerb.Sprite, $"Found {spriteFiles.Count} modified sprite(s) for {colorScheme}");

                foreach (var sourceFile in spriteFiles)
                {
                    string fileName = Path.GetFileName(sourceFile);
                    string targetFile = Path.Combine(targetDir, fileName);

                    // Log if this is a story character sprite
                    if (fileName.Contains("musu") || fileName.Contains("aguri") || fileName.Contains("oran"))
                    {
                        ModLogger.LogDebug(LogVerb.Sprite, $"STORY CHARACTER: Applying {colorScheme} variant for {fileName}");
                        ModLogger.LogDebug(LogVerb.Sprite, $"Source: {sourceFile}");
                        ModLogger.LogDebug(LogVerb.Sprite, $"Target: {targetFile}");

                        // Verify the file exists before copying
                        if (!File.Exists(sourceFile))
                        {
                            ModLogger.LogError(LogVerb.Sprite, $"Source file does not exist: {sourceFile}");
                            continue;
                        }
                    }

                    File.Copy(sourceFile, targetFile, true);
                    ModLogger.LogDebug(LogVerb.Sprite, $"Applied {colorScheme} variant: {fileName}");
                }

                ModLogger.Log(LogVerb.Sprite, $"Successfully applied {colorScheme} color scheme");
            }
            catch (Exception ex)
            {
                ModLogger.LogError(LogVerb.Sprite, $"Switching sprites failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the target unit directory for a sprite file.
        /// WotL sprites (ankoku, tama) go to unit_psp, regular sprites go to unit.
        /// </summary>
        /// <param name="spriteFileName">The sprite file name</param>
        /// <returns>The full path to the target unit directory</returns>
        public string GetTargetUnitDirectory(string spriteFileName)
        {
            var isWotLSprite = spriteFileName.Contains("ankoku_") || spriteFileName.Contains("tama_");
            var unitFolder = isWotLSprite ? "unit_psp" : "unit";
            return Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", unitFolder);
        }

        public string InterceptFilePath(string originalPath, string currentScheme)
        {
            // Don't intercept if using original scheme
            if (currentScheme == "original" || string.IsNullOrEmpty(currentScheme))
                return originalPath;

            // Check if this is a sprite file (ends with _spr.bin)
            if (!originalPath.EndsWith("_spr.bin", StringComparison.OrdinalIgnoreCase))
                return originalPath;

            // For sprite files, redirect to the color scheme directory
            // The path should be redirected to the mod's sprite variant folder
            var fileName = Path.GetFileName(originalPath);
            var modSpriteDir = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", $"sprites_{currentScheme}");
            var redirectedPath = Path.Combine(modSpriteDir, fileName);

            // If the variant sprite exists, return that path; otherwise return original
            if (File.Exists(redirectedPath))
            {
                // Extra logging for story characters
                if (fileName.Contains("musu") || fileName.Contains("aguri"))
                {
                    ModLogger.LogDebug(LogVerb.Sprite, $"STORY CHARACTER: Redirecting {fileName} to {currentScheme} variant");
                    ModLogger.LogDebug(LogVerb.Sprite, $"From: {originalPath}");
                    ModLogger.LogDebug(LogVerb.Sprite, $"To: {redirectedPath}");
                }
                else
                {
                    ModLogger.LogDebug(LogVerb.Sprite, $"Redirecting {fileName} to {currentScheme} variant");
                }
                return redirectedPath;
            }

            return originalPath;
        }
    }
}
