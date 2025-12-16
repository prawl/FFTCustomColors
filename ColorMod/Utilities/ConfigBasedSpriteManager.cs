using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FFTColorMod.Configuration;
using FFTColorMod.Services;

namespace FFTColorMod.Utilities
{
    /// <summary>
    /// Refactored sprite manager that uses CharacterDefinitionService for centralized character management
    /// This reduces coupling and makes it easier to add/remove characters
    /// </summary>
    public class ConfigBasedSpriteManager
    {
        private readonly string _modPath;
        private readonly string _sourcePath;
        private readonly ConfigurationManager _configManager;
        private readonly CharacterDefinitionService _characterService;
        private readonly string _unitPath;
        private readonly string _sourceUnitPath;

        public ConfigBasedSpriteManager(
            string modPath,
            ConfigurationManager configManager,
            CharacterDefinitionService characterService,
            string sourcePath = null)
        {
            _modPath = modPath;
            _sourcePath = sourcePath ?? @"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod";
            _configManager = configManager;
            _characterService = characterService;
            _unitPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            _sourceUnitPath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
        }

        // Backward compatibility constructor using singleton
        public ConfigBasedSpriteManager(
            string modPath,
            ConfigurationManager configManager,
            string sourcePath = null)
            : this(modPath, configManager, CharacterServiceSingleton.Instance, sourcePath)
        {
        }

        public void ApplyConfiguration()
        {
            var config = _configManager.LoadConfig();

            // Apply generic job class themes
            ApplyGenericJobThemes(config);

            // Apply story character themes using CharacterDefinitionService
            ApplyStoryCharacterThemes(config);
        }

        private void ApplyGenericJobThemes(Config config)
        {
            // Get all properties of Config that represent job colors
            var properties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(Configuration.ColorScheme) &&
                           (p.Name.EndsWith("_Male") || p.Name.EndsWith("_Female")));

            foreach (var property in properties)
            {
                var colorSchemeEnum = property.GetValue(config) as Configuration.ColorScheme?;
                var colorScheme = colorSchemeEnum?.ToString().ToLower() ?? "original";

                // Log what we're applying
                ModLogger.Log($"Applying {property.Name}: {colorScheme}");

                // Get the sprite name for this job/gender
                var spriteName = GetSpriteNameForJob(property.Name);
                if (spriteName != null)
                {
                    CopySpriteForJob(spriteName, colorScheme);
                }
            }
        }

        private void ApplyStoryCharacterThemes(Config config)
        {
            foreach (var character in _characterService.GetAllCharacters())
            {
                if (string.IsNullOrEmpty(character.EnumType) || character.SpriteNames.Length == 0)
                    continue;

                // Get the config property for this character
                var configProperty = typeof(Config).GetProperty(character.Name);
                if (configProperty == null)
                    continue;

                // Get the current theme value
                var themeValue = configProperty.GetValue(config);
                if (themeValue == null)
                    continue;

                var themeName = themeValue.ToString()?.ToLower() ?? "original";

                // Skip if it's set to original
                if (themeName == "original")
                {
                    ModLogger.Log($"Skipping {character.Name} - theme is original");
                    continue;
                }

                // Apply theme for each sprite name
                foreach (var spriteName in character.SpriteNames)
                {
                    ApplyStoryCharacterTheme(character.Name.ToLower(), spriteName, themeName);
                }
            }
        }

        private void ApplyStoryCharacterTheme(string characterName, string spriteName, string themeName)
        {
            ModLogger.Log($"Applying theme for character: {characterName}, sprite: {spriteName}, theme: {themeName}");

            // First try the directory-based structure
            var themeDir = $"sprites_{characterName}_{themeName}";
            var sourceDirPath = Path.Combine(_sourceUnitPath, themeDir, $"battle_{spriteName}_spr.bin");

            // Also check the flat file structure for backward compatibility
            var sourceFlatPath = Path.Combine(_sourceUnitPath, $"battle_{spriteName}_{themeName}_spr.bin");

            string sourceFile;
            if (File.Exists(sourceDirPath))
            {
                sourceFile = sourceDirPath;
                ModLogger.Log($"Using directory-based theme: {themeDir}");
            }
            else if (File.Exists(sourceFlatPath))
            {
                sourceFile = sourceFlatPath;
                ModLogger.Log($"Using flat file theme: battle_{spriteName}_{themeName}_spr.bin");
            }
            else
            {
                ModLogger.Log($"Warning: Theme file not found for {characterName} - {themeName}");
                ModLogger.Log($"  Tried: {sourceDirPath}");
                ModLogger.Log($"  Tried: {sourceFlatPath}");
                return;
            }

            // Destination file should be the BASE sprite name
            var destFile = Path.Combine(_unitPath, $"battle_{spriteName}_spr.bin");

            try
            {
                File.Copy(sourceFile, destFile, true);
                ModLogger.LogSuccess($"Applied {characterName} theme: {themeName} - copied to {Path.GetFileName(destFile)}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Error copying {characterName} sprite: {ex.Message}");
            }
        }

        // Backward compatibility methods for old interface
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
                ModLogger.LogWarning($"Property not found: {jobProperty}");
                return originalPath;
            }

            var colorSchemeEnum = propertyInfo.GetValue(config);
            if (colorSchemeEnum == null || !(colorSchemeEnum is Configuration.ColorScheme))
            {
                ModLogger.LogWarning($"No color scheme for: {jobProperty}");
                return originalPath;
            }

            var colorSchemeValue = ((Configuration.ColorScheme)colorSchemeEnum);
            var colorScheme = colorSchemeValue.ToString().ToLower();
            ModLogger.Log($"{jobProperty} configured as: {colorScheme}");

            if (colorScheme == "original")
            {
                return originalPath;
            }

            // Build new path with color scheme
            var sourceVariantPath = Path.Combine(_sourceUnitPath, $"sprites_{colorScheme}", fileName);

            if (File.Exists(sourceVariantPath))
            {
                var deploymentPath = Path.Combine(_unitPath, fileName);
                try
                {
                    File.Copy(sourceVariantPath, deploymentPath, true);
                    ModLogger.Log($"Copied theme file from {sourceVariantPath} to {deploymentPath}");
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"Failed to copy theme file: {ex.Message}");
                }
                return deploymentPath;
            }

            var variantPath = Path.Combine(_unitPath, $"sprites_{colorScheme}", fileName);
            if (File.Exists(variantPath))
            {
                return variantPath;
            }

            if (originalPath.Contains("sprites_"))
            {
                var pattern = @"sprites_[^\\\/]*";
                return System.Text.RegularExpressions.Regex.Replace(originalPath, pattern, $"sprites_{colorScheme}");
            }

            return originalPath.Replace(fileName, Path.Combine($"sprites_{colorScheme}", fileName));
        }

        public string GetActiveColorForJob(string jobProperty)
        {
            var config = _configManager.LoadConfig();
            var propertyInfo = typeof(Config).GetProperty(jobProperty);
            if (propertyInfo == null)
                return "Original";

            var colorSchemeEnum = propertyInfo.GetValue(config) as Configuration.ColorScheme?;
            return colorSchemeEnum?.GetDescription() ?? "Original";
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

        public void UpdateConfiguration(Config newConfig)
        {
            _configManager.SaveConfig(newConfig);
            ApplyConfiguration();
        }

        public void ResetAllToOriginal()
        {
            _configManager.ResetToDefaults();
            var resetConfig = _configManager.ReloadConfig();
            ApplyConfiguration();
        }

        public string GetJobFromSpriteName(string spriteName)
        {
            return _configManager.GetJobPropertyForSprite(spriteName);
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

        private void CopySpriteForJob(string spriteName, string colorScheme)
        {
            var sourceDir = Path.Combine(_sourceUnitPath, $"sprites_{colorScheme}");
            var sourceFile = Path.Combine(sourceDir, spriteName);
            var destFile = Path.Combine(_unitPath, spriteName);

            if (File.Exists(sourceFile))
            {
                try
                {
                    File.Copy(sourceFile, destFile, true);
                    ModLogger.LogSuccess($"Applied {colorScheme} to {spriteName}");
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"Error copying sprite: {ex.Message}");
                }
            }
            else
            {
                ModLogger.LogWarning($"Source sprite not found at {sourceFile}");
            }
        }
    }
}