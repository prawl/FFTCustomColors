using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Utilities
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

        public string GetModPath() => _modPath;

        public ConfigBasedSpriteManager(
            string modPath,
            ConfigurationManager configManager,
            CharacterDefinitionService characterService,
            string sourcePath = null)
        {
            _modPath = modPath;
            // In deployment, source path should be the mod path
            _sourcePath = sourcePath ?? _modPath;
            _configManager = configManager;
            _characterService = characterService;

            // Try to find FFTIVC folder - it might be in a versioned directory
            _sourceUnitPath = FindFFTIVCPath(_modPath);

            // Destination path: where we copy themes to (can be the same for now since we're intercepting)
            _unitPath = _sourceUnitPath;

            ModLogger.Log("ConfigBasedSpriteManager initialized:");
            ModLogger.Log($"  Mod path: {_modPath}");
            ModLogger.Log($"  Source path: {_sourcePath}");
            ModLogger.Log($"  Unit path (destination): {_unitPath}");
            ModLogger.Log($"  Source unit path: {_sourceUnitPath}");

            // Check if the FFTIVC folder exists
            if (Directory.Exists(_sourceUnitPath))
            {
                ModLogger.Log($"  FFTIVC folder EXISTS at: {_sourceUnitPath}");
                var themeDirs = Directory.GetDirectories(_sourceUnitPath, "sprites_*");
                ModLogger.Log($"  Found {themeDirs.Length} theme directories");
            }
            else
            {
                ModLogger.LogError($"  FFTIVC folder MISSING at: {_sourceUnitPath}");
                ModLogger.Log($"  Checking parent directory: {_modPath}");
                if (Directory.Exists(_modPath))
                {
                    var contents = Directory.GetDirectories(_modPath);
                    ModLogger.Log($"  Contents of mod directory ({contents.Length} items):");
                    foreach (var dir in contents.Take(10))
                    {
                        ModLogger.Log($"    - {Path.GetFileName(dir)}");
                    }
                }
            }
        }

        private string FindFFTIVCPath(string modPath)
        {
            // First try the direct path
            var directPath = Path.Combine(modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            if (Directory.Exists(directPath))
            {
                ModLogger.Log($"Found FFTIVC at direct path: {directPath}");
                return directPath;
            }

            // If not found, check if we're in a subdirectory and FFTIVC is in a versioned parent
            var parentDir = Path.GetDirectoryName(modPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                // Look for FFTColorCustomizer_v* directories and use the highest version
                try
                {
                    var versionedDirs = Directory.GetDirectories(parentDir, "FFTColorCustomizer_v*")
                        .OrderByDescending(dir =>
                        {
                            var dirName = Path.GetFileName(dir);
                            var versionStr = dirName.Substring(dirName.LastIndexOf('v') + 1);
                            if (int.TryParse(versionStr, out int version))
                                return version;
                            return 0;
                        })
                        .ToArray();

                    foreach (var versionedDir in versionedDirs)
                    {
                        var versionedPath = Path.Combine(versionedDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                        if (Directory.Exists(versionedPath))
                        {
                            ModLogger.Log($"Found FFTIVC in versioned directory: {versionedPath}");
                            return versionedPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogWarning($"Error searching for versioned directories: {ex.Message}");
                }
            }

            // As a fallback, return the expected path even if it doesn't exist
            ModLogger.LogWarning($"FFTIVC not found, using expected path: {directPath}");
            return directPath;
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
            ModLogger.Log("ApplyConfiguration called");
            var config = _configManager.LoadConfig();

            // Apply generic job class themes
            ApplyGenericJobThemes(config);

            // Apply story character themes using CharacterDefinitionService
            ApplyStoryCharacterThemes(config);
            ModLogger.Log("ApplyConfiguration completed");
        }

        private void ApplyGenericJobThemes(Config config)
        {
            // Get all properties of Config that represent job colors
            var properties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(string) &&
                           (p.Name.EndsWith("_Male") || p.Name.EndsWith("_Female")));

            // Debug: Log all properties found
            var propList = properties.ToList();
            ModLogger.Log($"[DEBUG] Found {propList.Count} job properties to process");
            foreach (var prop in propList)
            {
                ModLogger.Log($"[DEBUG]   - Property: {prop.Name}, Type: {prop.PropertyType.Name}");
            }

            // Additional debug: Check specifically for Archer properties
            var allProps = typeof(Config).GetProperties();
            var archerProps = allProps.Where(p => p.Name.Contains("Archer")).ToList();
            ModLogger.Log($"[DEBUG] Archer-specific properties found: {archerProps.Count}");
            foreach (var ap in archerProps)
            {
                ModLogger.Log($"[DEBUG]   - Archer prop: {ap.Name}, CanRead: {ap.CanRead}, CanWrite: {ap.CanWrite}");
            }

            foreach (var property in propList)
            {
                try
                {
                    var colorScheme = property.GetValue(config) as string ?? "original";

                    // Log what we're applying
                    ModLogger.Log($"Applying {property.Name}: {colorScheme}");

                    // Special debug for Archer
                    if (property.Name.Contains("Archer"))
                    {
                        ModLogger.Log($"[DEBUG] Processing Archer property: {property.Name}");
                        ModLogger.Log($"[DEBUG]   Value retrieved: {colorScheme}");
                    }

                    // Get the sprite name for this job/gender
                    var spriteName = GetSpriteNameForJob(property.Name);

                    // Special debug for Archer
                    if (property.Name.Contains("Archer"))
                    {
                        ModLogger.Log($"[DEBUG]   Sprite name returned: {spriteName ?? "NULL"}");
                    }

                    if (spriteName != null)
                    {
                        // Extract job type from property name (e.g., "Mediator_Male" -> "mediator")
                        var jobType = property.Name.Replace("_Male", "").Replace("_Female", "").ToLower();

                        // Special debug for Archer
                        if (property.Name.Contains("Archer"))
                        {
                            ModLogger.Log($"[DEBUG]   Job type: {jobType}");
                            ModLogger.Log($"[DEBUG]   Calling CopySpriteForJobWithType({spriteName}, {colorScheme}, {jobType})");
                        }

                        CopySpriteForJobWithType(spriteName, colorScheme, jobType);
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"[DEBUG] Error processing property {property.Name}: {ex.Message}");
                    ModLogger.LogError($"[DEBUG] Stack trace: {ex.StackTrace}");
                }
            }
        }

        private void ApplyStoryCharacterThemes(Config config)
        {
            foreach (var character in _characterService.GetAllCharacters())
            {
                if (character.SpriteNames.Length == 0)
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

                // Apply theme for each sprite name (including original)
                foreach (var spriteName in character.SpriteNames)
                {
                    ApplyStoryCharacterTheme(character.Name.ToLower(), spriteName, themeName);
                }
            }
        }

        private void ApplyStoryCharacterTheme(string characterName, string spriteName, string themeName)
        {
            ModLogger.Log($"Applying theme for character: {characterName}, sprite: {spriteName}, theme: {themeName}");

            // For "original" theme, restore the original sprite by copying from sprites_original directory
            if (themeName.ToLower() == "original")
            {
                ModLogger.Log($"Restoring original sprite for {characterName}/{spriteName}");

                var originalDir = Path.Combine(_sourceUnitPath, "sprites_original");
                var originalFile = Path.Combine(originalDir, $"battle_{spriteName}_spr.bin");
                var storyDestFile = Path.Combine(_unitPath, $"battle_{spriteName}_spr.bin");

                if (File.Exists(originalFile))
                {
                    try
                    {
                        File.Copy(originalFile, storyDestFile, true);
                        ModLogger.Log($"✓ Restored original sprite for {characterName}: {spriteName}");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.LogError($"Failed to restore original sprite for {characterName}/{spriteName}: {ex.Message}");
                    }
                }
                else
                {
                    ModLogger.LogWarning($"Original sprite not found for {characterName}: {originalFile}");
                }
                return;
            }

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
                // Ensure the destination directory exists
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    ModLogger.LogDebug($"Created destination directory: {destDir}");
                }

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

            var colorSchemeValue = propertyInfo.GetValue(config);
            if (colorSchemeValue == null || !(colorSchemeValue is string))
            {
                ModLogger.LogWarning($"No color scheme for: {jobProperty}");
                return originalPath;
            }

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

            var colorSchemeValue = propertyInfo.GetValue(config) as string;
            var themeName = colorSchemeValue ?? "original";

            // Convert to display name for backward compatibility with tests
            return ConvertThemeNameToDisplayName(themeName);
        }

        /// <summary>
        /// Converts internal theme name (e.g., "lucavi", "corpse_brigade") to display name (e.g., "Lucavi", "Corpse Brigade")
        /// </summary>
        private string ConvertThemeNameToDisplayName(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
                return "Original";

            // Replace underscores with spaces and convert to title case
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                themeName.Replace('_', ' ')
            );
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

        private void CopySpriteForJobWithType(string spriteName, string colorScheme, string jobType)
        {
            // For "original" theme, we need to restore the original sprite by copying from sprites_original directory
            if (colorScheme.ToLower() == "original")
            {
                ModLogger.Log($"Restoring original sprite for {spriteName}");

                // Copy from sprites_original directory to ensure we overwrite any custom theme
                var originalDir = Path.Combine(_sourceUnitPath, "sprites_original");
                var originalFile = Path.Combine(originalDir, spriteName);
                var originalDestFile = Path.Combine(_unitPath, spriteName);

                if (File.Exists(originalFile))
                {
                    try
                    {
                        File.Copy(originalFile, originalDestFile, true);
                        ModLogger.Log($"✓ Restored original sprite: {spriteName}");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.LogError($"Failed to restore original sprite {spriteName}: {ex.Message}");
                    }
                }
                else
                {
                    ModLogger.LogWarning($"Original sprite not found: {originalFile}");
                }
                return;
            }

            // Log the paths being used
            ModLogger.Log($"CopySpriteForJobWithType: Looking for {spriteName} with theme {colorScheme}");
            ModLogger.Log($"  _sourceUnitPath: {_sourceUnitPath}");

            // First check for job-specific theme directory (e.g., sprites_mediator_holy_knight)
            var jobSpecificDir = Path.Combine(_sourceUnitPath, $"sprites_{jobType}_{colorScheme}");
            var jobSpecificFile = Path.Combine(jobSpecificDir, spriteName);

            // Then check for generic theme directory (e.g., sprites_corpse_brigade)
            var genericDir = Path.Combine(_sourceUnitPath, $"sprites_{colorScheme}");
            var genericFile = Path.Combine(genericDir, spriteName);

            var destFile = Path.Combine(_unitPath, spriteName);

            ModLogger.Log($"  Checking job-specific: {jobSpecificFile}");
            ModLogger.Log($"  Checking generic: {genericFile}");
            ModLogger.Log($"  Destination: {destFile}");

            string sourceFile;
            if (File.Exists(jobSpecificFile))
            {
                sourceFile = jobSpecificFile;
                ModLogger.Log($"Using job-specific theme: sprites_{jobType}_{colorScheme}");
            }
            else if (File.Exists(genericFile))
            {
                sourceFile = genericFile;
                ModLogger.Log($"Using generic theme: sprites_{colorScheme}");
            }
            else
            {
                ModLogger.LogWarning($"Theme not found: tried sprites_{jobType}_{colorScheme} and sprites_{colorScheme}");
                // Log what files actually exist in the source directory for debugging
                if (Directory.Exists(_sourceUnitPath))
                {
                    var dirs = Directory.GetDirectories(_sourceUnitPath, "sprites_*");
                    ModLogger.LogDebug($"  Available theme directories: {dirs.Length} found");
                    foreach (var dir in dirs.Take(5))
                    {
                        ModLogger.LogDebug($"    - {Path.GetFileName(dir)}");
                    }
                }
                else
                {
                    ModLogger.LogError($"  Source unit path does not exist: {_sourceUnitPath}");
                }
                return;
            }

            try
            {
                // Ensure the destination directory exists
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    ModLogger.LogDebug($"Created destination directory: {destDir}");
                }

                File.Copy(sourceFile, destFile, true);
                ModLogger.LogSuccess($"Copied {colorScheme} theme for {jobType} to {destFile}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"ERROR copying sprite: {ex.Message}");
            }
        }

        private void CopySpriteForJob(string spriteName, string colorScheme)
        {
            // Skip copying for "original" theme - it uses the game's default sprites
            if (colorScheme.ToLower() == "original")
            {
                ModLogger.Log($"Skipping copy for {spriteName} - using original game sprites");
                return;
            }

            var sourceDir = Path.Combine(_sourceUnitPath, $"sprites_{colorScheme}");
            var sourceFile = Path.Combine(sourceDir, spriteName);
            var destFile = Path.Combine(_unitPath, spriteName);

            if (File.Exists(sourceFile))
            {
                try
                {
                    File.Copy(sourceFile, destFile, true);
                    ModLogger.LogSuccess($"Copied {colorScheme} theme to {destFile}");
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"ERROR copying sprite: {ex.Message}");
                }
            }
            else
            {
                ModLogger.LogWarning($"Source sprite not found at {sourceFile}");
            }
        }
    }
}
