using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FFTColorMod.Utilities;

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

    private void InitializePacMappings()
    {
        _pacFileMapping["original"] = "pack000.pac";
        _pacFileMapping["corpse_brigade"] = "pack001.pac";
        _pacFileMapping["lucavi"] = "pack002.pac";
        _pacFileMapping["northern_sky"] = "pack003.pac";
        _pacFileMapping["smoke"] = "pack004.pac";
        _pacFileMapping["southern_sky"] = "pack005.pac";
        _pacFileMapping["crimson_red"] = "pack006.pac";
        _pacFileMapping["royal_purple"] = "pack007.pac";
        _pacFileMapping["phoenix_flame"] = "pack008.pac";
        _pacFileMapping["frost_knight"] = "pack009.pac";
        _pacFileMapping["silver_knight"] = "pack010.pac";
        _pacFileMapping["shadow_assassin"] = "pack011.pac";
        _pacFileMapping["emerald_dragon"] = "pack012.pac";
        _pacFileMapping["rose_gold"] = "pack013.pac";
        _pacFileMapping["ocean_depths"] = "pack014.pac";
        _pacFileMapping["golden_templar"] = "pack015.pac";
        _pacFileMapping["blood_moon"] = "pack016.pac";
        _pacFileMapping["celestial"] = "pack017.pac";
        _pacFileMapping["volcanic"] = "pack018.pac";
        _pacFileMapping["amethyst"] = "pack019.pac";
    }

    public void SwitchColorScheme(string colorScheme)
    {
        Console.WriteLine($"[FFT Color Mod] Switching to {colorScheme} color scheme");

        // Read from source (git repo), write to deployment
        string sourceDir = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", $"sprites_{colorScheme}");
        string targetDir = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

        Console.WriteLine($"[FFT Color Mod] Looking for modified sprites in: {sourceDir}");

        if (!Directory.Exists(sourceDir))
        {
            // If it's the original scheme and the directory doesn't exist, that's OK - we just don't swap anything
            if (colorScheme == "original")
            {
                Console.WriteLine($"[FFT Color Mod] Original scheme selected - no sprite swapping needed");
                return;
            }

            Console.WriteLine($"[FFT Color Mod] WARNING: Variant directory does not exist: {sourceDir}");
            return;
        }

        try
        {
            var spriteFiles = Directory.GetFiles(sourceDir, "*.bin")
                .ToList();

            if (spriteFiles.Count == 0)
            {
                Console.WriteLine($"[FFT Color Mod] No modified sprites found for {colorScheme}");
                return;
            }

            Console.WriteLine($"[FFT Color Mod] Found {spriteFiles.Count} modified sprite(s) for {colorScheme}");

            foreach (var sourceFile in spriteFiles)
            {
                string fileName = Path.GetFileName(sourceFile);
                string targetFile = Path.Combine(targetDir, fileName);

                // Log if this is a story character sprite
                if (fileName.Contains("musu") || fileName.Contains("aguri") || fileName.Contains("oran"))
                {
                    Console.WriteLine($"[FFT Color Mod] STORY CHARACTER: Applying {colorScheme} variant for {fileName}");
                    Console.WriteLine($"[FFT Color Mod]   Source: {sourceFile}");
                    Console.WriteLine($"[FFT Color Mod]   Target: {targetFile}");

                    // Verify the file exists before copying
                    if (!File.Exists(sourceFile))
                    {
                        Console.WriteLine($"[FFT Color Mod] ERROR: Source file does not exist: {sourceFile}");
                        continue;
                    }
                }

                File.Copy(sourceFile, targetFile, true);
                Console.WriteLine($"[FFT Color Mod] Applied {colorScheme} variant: {fileName}");
            }

            Console.WriteLine($"[FFT Color Mod] Successfully applied {colorScheme} color scheme");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] ERROR switching sprites: {ex.Message}");
        }
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
            if (fileName.Contains("musu") || fileName.Contains("aguri") || fileName.Contains("beio"))
            {
                Console.WriteLine($"[FFT Color Mod] STORY CHARACTER: Redirecting {fileName} to {currentScheme} variant");
                Console.WriteLine($"[FFT Color Mod]   From: {originalPath}");
                Console.WriteLine($"[FFT Color Mod]   To: {redirectedPath}");
            }
            else
            {
                Console.WriteLine($"[FFT Color Mod] Redirecting {fileName} to {currentScheme} variant");
            }
            return redirectedPath;
        }

        return originalPath;
    }
}