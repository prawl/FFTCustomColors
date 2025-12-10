using System;
using System.IO;
using System.Linq;
using FFTColorMod.Configuration;

namespace FFTColorMod.Utilities
{
    public class ConfigBasedSpriteManager
    {
        private readonly string _modPath;
        private readonly ConfigurationManager _configManager;
        private readonly string _unitPath;

        public ConfigBasedSpriteManager(string modPath, ConfigurationManager configManager)
        {
            _modPath = modPath;
            _configManager = configManager;
            _unitPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
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
                return originalPath;

            var colorScheme = propertyInfo.GetValue(config) as string;
            if (string.IsNullOrEmpty(colorScheme) || colorScheme == "original")
            {
                // For "original" scheme, return the path unchanged
                return originalPath;
            }

            // Build new path with color scheme
            var directory = Path.GetDirectoryName(originalPath);
            var variantPath = Path.Combine(_unitPath, $"sprites_{colorScheme}", fileName);

            // Check if the variant exists in our mod directory
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
                .Where(p => p.PropertyType == typeof(string) &&
                           (p.Name.EndsWith("Male") || p.Name.EndsWith("Female")));

            foreach (var property in properties)
            {
                var colorScheme = property.GetValue(config) as string;
                if (string.IsNullOrEmpty(colorScheme) || colorScheme == "original")
                    colorScheme = "original";

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
            var sourceDir = Path.Combine(_unitPath, $"sprites_{colorScheme}");
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
        }

        private string GetSpriteNameForJob(string jobProperty)
        {
            return jobProperty switch
            {
                "KnightMale" => "battle_knight_m_spr.bin",
                "KnightFemale" => "battle_knight_w_spr.bin",
                "ArcherMale" => "battle_yumi_m_spr.bin",
                "ArcherFemale" => "battle_yumi_w_spr.bin",
                "ChemistMale" => "battle_item_m_spr.bin",
                "ChemistFemale" => "battle_item_w_spr.bin",
                "MonkMale" => "battle_monk_m_spr.bin",
                "MonkFemale" => "battle_monk_w_spr.bin",
                "WhiteMageMale" => "battle_siro_m_spr.bin",
                "WhiteMageFemale" => "battle_siro_w_spr.bin",
                "BlackMageMale" => "battle_kuro_m_spr.bin",
                "BlackMageFemale" => "battle_kuro_w_spr.bin",
                "ThiefMale" => "battle_thief_m_spr.bin",
                "ThiefFemale" => "battle_thief_w_spr.bin",
                "NinjaMale" => "battle_ninja_m_spr.bin",
                "NinjaFemale" => "battle_ninja_w_spr.bin",
                "SquireMale" => "battle_mina_m_spr.bin",
                "SquireFemale" => "battle_mina_w_spr.bin",
                "TimeMageMale" => "battle_toki_m_spr.bin",
                "TimeMageFemale" => "battle_toki_w_spr.bin",
                "SummonerMale" => "battle_syou_m_spr.bin",
                "SummonerFemale" => "battle_syou_w_spr.bin",
                "SamuraiMale" => "battle_samu_m_spr.bin",
                "SamuraiFemale" => "battle_samu_w_spr.bin",
                "DragoonMale" => "battle_ryu_m_spr.bin",
                "DragoonFemale" => "battle_ryu_w_spr.bin",
                "GeomancerMale" => "battle_fusui_m_spr.bin",
                "GeomancerFemale" => "battle_fusui_w_spr.bin",
                "MysticMale" => "battle_onmyo_m_spr.bin",
                "MysticFemale" => "battle_onmyo_w_spr.bin",
                "MediatorMale" => "battle_waju_m_spr.bin",
                "MediatorFemale" => "battle_waju_w_spr.bin",
                "DancerFemale" => "battle_odori_w_spr.bin",
                "BardMale" => "battle_gin_m_spr.bin",
                "MimeMale" => "battle_mono_m_spr.bin",
                "MimeFemale" => "battle_mono_w_spr.bin",
                "CalculatorMale" => "battle_san_m_spr.bin",
                "CalculatorFemale" => "battle_san_w_spr.bin",
                _ => null
            };
        }

        public string GetActiveColorForJob(string jobProperty)
        {
            var config = _configManager.LoadConfig();
            var propertyInfo = typeof(Config).GetProperty(jobProperty);
            if (propertyInfo == null)
                return "original";

            var colorScheme = propertyInfo.GetValue(config) as string;
            return string.IsNullOrEmpty(colorScheme) ? "original" : colorScheme;
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
            ApplyConfiguration();
        }

        public string GetJobFromSpriteName(string spriteName)
        {
            return _configManager.GetJobPropertyForSprite(spriteName);
        }
    }
}