using System;
using System.IO;
using System.Linq;
using FFTColorMod.Configuration;

namespace FFTColorMod.Utilities
{
    public class ConfigBasedSpriteManager
    {
        private readonly string _modPath;
        private readonly string _sourcePath;  // Git repo path for themes
        private readonly ConfigurationManager _configManager;
        private readonly string _unitPath;
        private readonly string _sourceUnitPath;  // Git repo unit path

        public ConfigBasedSpriteManager(string modPath, ConfigurationManager configManager, string sourcePath = null)
        {
            _modPath = modPath;
            _sourcePath = sourcePath ?? @"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod";  // Default to git repo
            _configManager = configManager;
            _unitPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            _sourceUnitPath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
        }

        public string InterceptFilePath(string originalPath)
        {
            // Extract sprite filename from path
            var fileName = Path.GetFileName(originalPath);

            // Get the job property for this sprite
            var jobProperty = GetJobFromSpriteName(fileName);
            if (jobProperty == null)
                return originalPath;

            // Get the configured color for this job
            var config = _configManager.LoadConfig();
            var propertyInfo = typeof(Config).GetProperty(jobProperty);
            if (propertyInfo == null)
            {
                Console.WriteLine($"[ConfigBasedSpriteManager] Property not found: {jobProperty}");
                return originalPath;
            }

            var colorSchemeEnum = propertyInfo.GetValue(config);
            if (colorSchemeEnum == null || !(colorSchemeEnum is Configuration.ColorScheme))
            {
                Console.WriteLine($"[ConfigBasedSpriteManager] No color scheme for: {jobProperty}");
                return originalPath;
            }

            var colorSchemeValue = ((Configuration.ColorScheme)colorSchemeEnum);
            // Convert enum to lowercase (matching directory names)
            var colorScheme = colorSchemeValue.ToString().ToLower();
            Console.WriteLine($"[ConfigBasedSpriteManager] {jobProperty} configured as: {colorScheme}");

            if (colorScheme == "original")
            {
                // For "original" scheme, return the path unchanged
                return originalPath;
            }

            // Build new path with color scheme - look in GIT REPO first
            var directory = Path.GetDirectoryName(originalPath);
            var sourceVariantPath = Path.Combine(_sourceUnitPath, $"sprites_{colorScheme}", fileName);

            // Check if the variant exists in our GIT REPO
            if (File.Exists(sourceVariantPath))
            {
                // Copy from git repo to deployment path
                var deploymentPath = Path.Combine(_unitPath, fileName);
                try
                {
                    File.Copy(sourceVariantPath, deploymentPath, true);
                    Console.WriteLine($"[FFT Color Mod] Copied theme file from {sourceVariantPath} to {deploymentPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFT Color Mod] Failed to copy theme file: {ex.Message}");
                }
                return deploymentPath;
            }

            // Fallback: check deployment directory
            var variantPath = Path.Combine(_unitPath, $"sprites_{colorScheme}", fileName);
            if (File.Exists(variantPath))
            {
                return variantPath;
            }

            // If path already contains a sprites_ folder, replace it
            if (originalPath.Contains("sprites_"))
            {
                var pattern = @"sprites_[^\\\/]*";
                return System.Text.RegularExpressions.Regex.Replace(originalPath, pattern, $"sprites_{colorScheme}");
            }

            // Default: add the scheme to the path
            return originalPath.Replace(fileName, Path.Combine($"sprites_{colorScheme}", fileName));
        }

        public void ApplyConfiguration()
        {
            var config = _configManager.LoadConfig();

            // Get all properties of Config that represent job colors
            var properties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(Configuration.ColorScheme) &&
                           (p.Name.EndsWith("_Male") || p.Name.EndsWith("_Female")));

            foreach (var property in properties)
            {
                var colorSchemeEnum = property.GetValue(config) as Configuration.ColorScheme?;
                // Convert enum to lowercase with underscores (matching directory names)
                var colorScheme = colorSchemeEnum?.ToString().ToLower() ?? "original";

                // Log what we're applying
                Console.WriteLine($"[FFT Color Mod] Applying {property.Name}: {colorScheme}");

                // Get the sprite name for this job/gender
                var spriteName = GetSpriteNameForJob(property.Name);
                if (spriteName != null)
                {
                    CopySpriteForJob(spriteName, colorScheme);
                }
            }
        }

        private void CopySpriteForJob(string spriteName, string colorScheme)
        {
            // Use _sourceUnitPath (git repo) for source files, not _unitPath (deployment)
            var sourceDir = Path.Combine(_sourceUnitPath, $"sprites_{colorScheme}");
            var sourceFile = Path.Combine(sourceDir, spriteName);
            var destFile = Path.Combine(_unitPath, spriteName);

            if (File.Exists(sourceFile))
            {
                try
                {
                    File.Copy(sourceFile, destFile, true);
                    Console.WriteLine($"[FFT Color Mod] Applied {colorScheme} to {spriteName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFT Color Mod] Error copying sprite: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[FFT Color Mod] Warning: Source sprite not found at {sourceFile}");
            }
        }

        private string GetSpriteNameForJob(string jobProperty)
        {
            return jobProperty switch
            {
                "Knight_Male" => "battle_knight_m_spr.bin",
                "Knight_Female" => "battle_knight_w_spr.bin",
                "Archer_Male" => "battle_yumi_m_spr.bin",
                "Archer_Female" => "battle_yumi_w_spr.bin",
                "Chemist_Male" => "battle_item_m_spr.bin",
                "Chemist_Female" => "battle_item_w_spr.bin",
                "Monk_Male" => "battle_monk_m_spr.bin",
                "Monk_Female" => "battle_monk_w_spr.bin",
                "WhiteMage_Male" => "battle_siro_m_spr.bin",
                "WhiteMage_Female" => "battle_siro_w_spr.bin",
                "BlackMage_Male" => "battle_kuro_m_spr.bin",
                "BlackMage_Female" => "battle_kuro_w_spr.bin",
                "Thief_Male" => "battle_thief_m_spr.bin",
                "Thief_Female" => "battle_thief_w_spr.bin",
                "Ninja_Male" => "battle_ninja_m_spr.bin",
                "Ninja_Female" => "battle_ninja_w_spr.bin",
                "Squire_Male" => "battle_mina_m_spr.bin",
                "Squire_Female" => "battle_mina_w_spr.bin",
                "TimeMage_Male" => "battle_toki_m_spr.bin",
                "TimeMage_Female" => "battle_toki_w_spr.bin",
                "Summoner_Male" => "battle_syou_m_spr.bin",
                "Summoner_Female" => "battle_syou_w_spr.bin",
                "Samurai_Male" => "battle_samu_m_spr.bin",
                "Samurai_Female" => "battle_samu_w_spr.bin",
                "Dragoon_Male" => "battle_ryu_m_spr.bin",
                "Dragoon_Female" => "battle_ryu_w_spr.bin",
                "Geomancer_Male" => "battle_fusui_m_spr.bin",
                "Geomancer_Female" => "battle_fusui_w_spr.bin",
                "Mystic_Male" => "battle_onmyo_m_spr.bin",
                "Mystic_Female" => "battle_onmyo_w_spr.bin",
                "Mediator_Male" => "battle_waju_m_spr.bin",
                "Mediator_Female" => "battle_waju_w_spr.bin",
                "Dancer_Female" => "battle_odori_w_spr.bin",
                "Bard_Male" => "battle_gin_m_spr.bin",
                "Mime_Male" => "battle_mono_m_spr.bin",
                "Mime_Female" => "battle_mono_w_spr.bin",
                "Calculator_Male" => "battle_san_m_spr.bin",
                "Calculator_Female" => "battle_san_w_spr.bin",
                _ => null
            };
        }

        public string GetActiveColorForJob(string jobProperty)
        {
            var config = _configManager.LoadConfig();
            var propertyInfo = typeof(Config).GetProperty(jobProperty);
            if (propertyInfo == null)
                return "Original";

            var colorSchemeEnum = propertyInfo.GetValue(config) as Configuration.ColorScheme?;
            return colorSchemeEnum?.GetDescription() ?? "Original"; // This method returns display name, not file name
        }

        public void SetColorForJob(string jobProperty, string colorScheme)
        {
            _configManager.SetColorSchemeForJob(jobProperty, colorScheme);

            // Apply the change immediately
            var spriteName = GetSpriteNameForJob(jobProperty);
            if (spriteName != null)
            {
                CopySpriteForJob(spriteName, colorScheme);
            }
        }

        public void ResetAllToOriginal()
        {
            _configManager.ResetToDefaults();

            // Force a reload to ensure we have the reset config
            var resetConfig = _configManager.ReloadConfig();

            // Apply the reset configuration
            ApplyConfiguration();
        }

        public void UpdateConfiguration(Config newConfig)
        {
            // Save the new configuration and reapply all sprite changes
            _configManager.SaveConfig(newConfig);
            ApplyConfiguration();
        }

        public string GetJobFromSpriteName(string spriteName)
        {
            return _configManager.GetJobPropertyForSprite(spriteName);
        }
    }
}