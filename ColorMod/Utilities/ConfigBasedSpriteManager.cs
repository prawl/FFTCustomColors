using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;

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
        private readonly UserThemeService _userThemeService;

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

            // Initialize user theme service
            _userThemeService = new UserThemeService(_modPath);

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
            if (_characterService == null)
            {
                ModLogger.LogWarning("CharacterDefinitionService is null, skipping story character themes");
                return;
            }

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

                // Keep original case for user theme lookup, but also pass lowercase for directory lookups
                var themeNameOriginal = themeValue.ToString() ?? "original";

                // Apply theme for each sprite name (including original)
                foreach (var spriteName in character.SpriteNames)
                {
                    ApplyStoryCharacterTheme(character.Name.ToLower(), spriteName, themeNameOriginal);
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
                    catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
                    {
                        // File is locked by the game - this is expected during runtime
                        // The InterceptFilePath will handle the redirection
                        ModLogger.LogDebug($"Sprite {spriteName} is in use, skipping restoration (will use path redirection)");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Permission issue - likely the file is read-only or locked
                        ModLogger.LogDebug($"Cannot overwrite {spriteName} (access denied), using path redirection");
                    }
                    catch (Exception ex)
                    {
                        // Other unexpected errors should still be logged as errors
                        ModLogger.LogError($"Failed to restore original sprite for {characterName}/{spriteName}: {ex.Message}");
                    }
                }
                else
                {
                    ModLogger.LogWarning($"Original sprite not found for {characterName}: {originalFile}");
                }
                return;
            }

            // Check if this is a user-created theme (story characters use character name as job name)
            // Convert characterName to proper case for user theme lookup (e.g., "agrias" -> "Agrias", "ramzachapter4" -> "RamzaChapter4")
            var properCharacterName = NormalizeCharacterName(characterName);

            // For Ramza chapters, check if this is a built-in theme (dark_knight, white_heretic, crimson_blade)
            // Built-in Ramza themes work via NXD palette patching, not sprite files
            if (IsRamzaChapter(properCharacterName) && IsBuiltInRamzaTheme(themeName))
            {
                ModLogger.Log($"[STORY_CHAR_THEME] Applying built-in Ramza theme: {properCharacterName}/{themeName}");
                ApplyBuiltInRamzaThemeToNxd(properCharacterName, themeName);
                return;
            }

            var isUserTheme = _userThemeService.IsUserTheme(properCharacterName, themeName);
            ModLogger.Log($"[STORY_CHAR_THEME] Checking user theme: character={properCharacterName}, theme={themeName}, isUserTheme={isUserTheme}");

            if (isUserTheme)
            {
                ModLogger.Log($"[STORY_CHAR_THEME] Applying user theme for {properCharacterName}/{themeName}");
                ApplyStoryCharacterUserTheme($"battle_{spriteName}_spr.bin", themeName, properCharacterName);
                return;
            }

            // First try the directory-based structure (use lowercase for directory names)
            var themeNameLower = themeName.ToLower();
            var themeDir = $"sprites_{characterName}_{themeNameLower}";
            var sourceDirPath = Path.Combine(_sourceUnitPath, themeDir, $"battle_{spriteName}_spr.bin");

            // Also check the flat file structure for backward compatibility
            var sourceFlatPath = Path.Combine(_sourceUnitPath, $"battle_{spriteName}_{themeNameLower}_spr.bin");

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
            catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
            {
                // File is locked by the game - this is expected during runtime
                ModLogger.LogDebug($"Sprite {spriteName} is in use, theme will be applied via path redirection");
            }
            catch (UnauthorizedAccessException)
            {
                ModLogger.LogDebug($"Cannot overwrite {spriteName} (access denied), using path redirection");
            }
            catch (Exception ex)
            {
                // Log other unexpected errors
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

            var colorSchemeOriginal = colorSchemeValue.ToString();
            var colorScheme = colorSchemeOriginal.ToLower();
            ModLogger.Log($"{jobProperty} configured as: {colorScheme}");

            if (colorScheme == "original")
            {
                return originalPath;
            }

            // User themes are now written directly to base unit folder by ApplyUserTheme,
            // so no special interception is needed - the file is already in the right place.
            // Just log for debugging purposes.
            var isUserTheme = _userThemeService.IsUserTheme(jobProperty, colorSchemeOriginal);
            if (isUserTheme)
            {
                ModLogger.Log($"[USER_THEME_CHECK] {jobProperty} uses user theme '{colorSchemeOriginal}' - file should be in base folder");
                // Return original path - ApplyConfiguration already placed the themed sprite there
                return originalPath;
            }

            // Build path to the themed sprite
            // DO NOT copy files during interception - this causes "Access Denied" errors
            // Files should already be copied when configuration is applied
            var variantPath = Path.Combine(_unitPath, $"sprites_{colorScheme}", fileName);
            if (File.Exists(variantPath))
            {
                ModLogger.LogDebug($"Redirecting {fileName} to themed variant: {colorScheme}");
                return variantPath;
            }

            // If the themed file doesn't exist in the expected location,
            // check if it exists in the source location (but don't copy it)
            var sourceVariantPath = Path.Combine(_sourceUnitPath, $"sprites_{colorScheme}", fileName);
            if (File.Exists(sourceVariantPath))
            {
                ModLogger.LogDebug($"Theme file exists in source but not deployed: {colorScheme}/{fileName}");
                // Return original path since we can't copy during interception
                return originalPath;
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
                // WotL Jobs - Dark Knight (ankoku)
                "DarkKnight_Male" => "spr_dst_bchr_ankoku_m_spr.bin",
                "DarkKnight_Female" => "spr_dst_bchr_ankoku_w_spr.bin",
                // WotL Jobs - Onion Knight (tama)
                "OnionKnight_Male" => "spr_dst_bchr_tama_m_spr.bin",
                "OnionKnight_Female" => "spr_dst_bchr_tama_w_spr.bin",
                _ => null
            };
        }

        private void CopySpriteForJobWithType(string spriteName, string colorScheme, string jobType)
        {
            // Convert jobType to job name format for getting correct unit path
            var jobName = ConvertJobTypeToJobName(jobType, spriteName);

            // Get the correct unit path for this job (WotL jobs use unit_psp)
            var unitPath = GetUnitPathForJob(jobName);
            ModLogger.Log($"[COPY_SPRITE] Using unit path: {unitPath} (IsWotL: {IsWotLJob(jobName)})");

            // For "original" theme, we need to restore the original sprite by copying from sprites_original directory
            if (colorScheme.ToLower() == "original")
            {
                ModLogger.Log($"Restoring original sprite for {spriteName}");

                // Copy from sprites_original directory to ensure we overwrite any custom theme
                var originalDir = Path.Combine(unitPath, "sprites_original");
                var originalFile = Path.Combine(originalDir, spriteName);
                var originalDestFile = Path.Combine(unitPath, spriteName);

                if (File.Exists(originalFile))
                {
                    try
                    {
                        File.Copy(originalFile, originalDestFile, true);
                        ModLogger.Log($"✓ Restored original sprite: {spriteName}");
                    }
                    catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
                    {
                        // File is locked by the game - this is expected during runtime
                        // The InterceptFilePath will handle the redirection
                        ModLogger.LogDebug($"Sprite {spriteName} is in use, skipping restoration (will use path redirection)");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Permission issue - likely the file is read-only or locked
                        ModLogger.LogDebug($"Cannot overwrite {spriteName} (access denied), using path redirection");
                    }
                    catch (Exception ex)
                    {
                        // Other unexpected errors should still be logged as errors
                        ModLogger.LogError($"Failed to restore original sprite {spriteName}: {ex.Message}");
                    }
                }
                else
                {
                    ModLogger.LogWarning($"Original sprite not found: {originalFile}");
                }
                return;
            }

            ModLogger.Log($"[COPY_SPRITE] jobType={jobType}, spriteName={spriteName} -> jobName={jobName}");

            // Check if this is a user-created theme
            var isUserTheme = _userThemeService.IsUserTheme(jobName, colorScheme);
            ModLogger.Log($"[COPY_SPRITE] Checking user theme: jobName={jobName}, colorScheme={colorScheme}, isUserTheme={isUserTheme}");

            if (isUserTheme)
            {
                ModLogger.Log($"[COPY_SPRITE] Calling ApplyUserTheme for {jobName}/{colorScheme}");
                ApplyUserTheme(spriteName, colorScheme, jobName);
                return;
            }

            // Log the paths being used
            ModLogger.Log($"CopySpriteForJobWithType: Looking for {spriteName} with theme {colorScheme}");
            ModLogger.Log($"  unitPath: {unitPath}");

            // First check for job-specific theme directory (e.g., sprites_mediator_holy_knight)
            var jobSpecificDir = Path.Combine(unitPath, $"sprites_{jobType}_{colorScheme}");
            var jobSpecificFile = Path.Combine(jobSpecificDir, spriteName);

            // Then check for generic theme directory (e.g., sprites_corpse_brigade)
            var genericDir = Path.Combine(unitPath, $"sprites_{colorScheme}");
            var genericFile = Path.Combine(genericDir, spriteName);

            var destFile = Path.Combine(unitPath, spriteName);

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
                if (Directory.Exists(unitPath))
                {
                    var dirs = Directory.GetDirectories(unitPath, "sprites_*");
                    ModLogger.LogDebug($"  Available theme directories: {dirs.Length} found");
                    foreach (var dir in dirs.Take(5))
                    {
                        ModLogger.LogDebug($"    - {Path.GetFileName(dir)}");
                    }
                }
                else
                {
                    ModLogger.LogError($"  Unit path does not exist: {unitPath}");
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
            catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
            {
                // File is locked by the game - this is expected during runtime
                ModLogger.LogDebug($"Sprite {spriteName} is in use, theme will be applied via path redirection");
            }
            catch (UnauthorizedAccessException)
            {
                ModLogger.LogDebug($"Cannot overwrite {spriteName} (access denied), using path redirection");
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

        /// <summary>
        /// Normalizes character names to proper case for user theme lookups.
        /// Handles multi-word names like "ramzachapter4" → "RamzaChapter4"
        /// </summary>
        private string NormalizeCharacterName(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
                return characterName;

            // Handle Ramza chapter variations
            var lowerName = characterName.ToLower();
            if (lowerName.StartsWith("ramzachapter") || lowerName.StartsWith("ramzach"))
            {
                // Extract the chapter number
                if (lowerName.Contains("chapter1") || lowerName.EndsWith("ch1"))
                    return "RamzaChapter1";
                if (lowerName.Contains("chapter2") || lowerName.Contains("chapter23") || lowerName.EndsWith("ch2"))
                    return "RamzaChapter23";
                if (lowerName.Contains("chapter4") || lowerName.EndsWith("ch4"))
                    return "RamzaChapter4";
                // Default to Chapter1 if we can't determine
                return "RamzaChapter1";
            }

            // For simple names, just capitalize the first letter
            return char.ToUpper(characterName[0]) + characterName.Substring(1);
        }

        /// <summary>
        /// Converts job type (e.g., "knight") to job name format (e.g., "Knight_Male") based on sprite name
        /// </summary>
        private string ConvertJobTypeToJobName(string jobType, string spriteName)
        {
            // Determine gender from sprite name (_m_ = male, _w_ = female)
            var gender = spriteName.Contains("_m_") ? "Male" : "Female";

            // Map compound job names that need special casing
            // ToTitleCase only capitalizes the first letter, so "blackmage" becomes "Blackmage"
            // but we need "BlackMage" to match the registry keys
            var properJobName = jobType.ToLower() switch
            {
                "blackmage" => "BlackMage",
                "whitemage" => "WhiteMage",
                "timemage" => "TimeMage",
                "darkknight" => "DarkKnight",
                "onionknight" => "OnionKnight",
                _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobType)
            };

            return $"{properJobName}_{gender}";
        }

        /// <summary>
        /// Checks if a job is a War of the Lions exclusive job (Dark Knight, Onion Knight)
        /// </summary>
        private bool IsWotLJob(string jobName)
        {
            return jobName.StartsWith("DarkKnight") || jobName.StartsWith("OnionKnight");
        }

        /// <summary>
        /// Gets the unit path for a job. WotL jobs use unit_psp, regular jobs use unit.
        /// </summary>
        private string GetUnitPathForJob(string jobName)
        {
            if (IsWotLJob(jobName))
            {
                return Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit_psp");
            }
            return _sourceUnitPath;
        }

        /// <summary>
        /// Applies a user-created theme by combining the original sprite with the user's palette
        /// </summary>
        private void ApplyUserTheme(string spriteName, string themeName, string jobName)
        {
            ModLogger.Log($"[APPLY_USER_THEME] START - sprite={spriteName}, theme={themeName}, job={jobName}");

            // Get the user theme palette path
            var palettePath = _userThemeService.GetUserThemePalettePath(jobName, themeName);
            ModLogger.Log($"[APPLY_USER_THEME] Palette path: {palettePath}");

            if (string.IsNullOrEmpty(palettePath))
            {
                ModLogger.LogWarning($"[APPLY_USER_THEME] Palette path is null/empty for {jobName}/{themeName}");
                return;
            }

            if (!File.Exists(palettePath))
            {
                ModLogger.LogWarning($"[APPLY_USER_THEME] Palette file does not exist: {palettePath}");
                return;
            }

            // Get the correct unit path for this job (WotL jobs use unit_psp)
            var unitPath = GetUnitPathForJob(jobName);
            ModLogger.Log($"[APPLY_USER_THEME] Using unit path: {unitPath} (IsWotL: {IsWotLJob(jobName)})");

            // Get the original sprite
            var originalDir = Path.Combine(unitPath, "sprites_original");
            var originalFile = Path.Combine(originalDir, spriteName);
            ModLogger.Log($"[APPLY_USER_THEME] Original sprite path: {originalFile}");
            ModLogger.Log($"[APPLY_USER_THEME] Original sprite exists: {File.Exists(originalFile)}");

            if (!File.Exists(originalFile))
            {
                ModLogger.LogWarning($"[APPLY_USER_THEME] Original sprite not found: {originalFile}");
                return;
            }

            try
            {
                // Read original sprite and user palette
                var originalSprite = File.ReadAllBytes(originalFile);
                var userPalette = File.ReadAllBytes(palettePath);
                ModLogger.Log($"[APPLY_USER_THEME] Original sprite size: {originalSprite.Length}, Palette size: {userPalette.Length}");

                // Validate palette size (should be 512 bytes)
                if (userPalette.Length != 512)
                {
                    ModLogger.LogWarning($"[APPLY_USER_THEME] Invalid palette size: {userPalette.Length} (expected 512)");
                    return;
                }

                // Replace palette in sprite (first 512 bytes)
                Array.Copy(userPalette, 0, originalSprite, 0, 512);

                // Write directly to base unit folder (same as regular themes)
                // This is required because FFTPack registers files at startup and uses the base folder
                var destFile = Path.Combine(unitPath, spriteName);
                ModLogger.Log($"[APPLY_USER_THEME] Writing to base unit folder: {destFile}");
                File.WriteAllBytes(destFile, originalSprite);

                ModLogger.LogSuccess($"[APPLY_USER_THEME] SUCCESS - Created: {destFile}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[APPLY_USER_THEME] FAILED: {ex.Message}");
                ModLogger.LogError($"[APPLY_USER_THEME] Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Applies a user-created theme for a story character by combining the original sprite with the user's palette
        /// </summary>
        private void ApplyStoryCharacterUserTheme(string spriteName, string themeName, string characterName)
        {
            ModLogger.Log($"[APPLY_STORY_USER_THEME] START - sprite={spriteName}, theme={themeName}, character={characterName}");

            // Get the user theme palette path (using character name as job name)
            var palettePath = _userThemeService.GetUserThemePalettePath(characterName, themeName);
            ModLogger.Log($"[APPLY_STORY_USER_THEME] Palette path: {palettePath}");

            if (string.IsNullOrEmpty(palettePath))
            {
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME] Palette path is null/empty for {characterName}/{themeName}");
                return;
            }

            if (!File.Exists(palettePath))
            {
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME] Palette file does not exist: {palettePath}");
                return;
            }

            // For story characters, original sprites may be in character-specific folders
            // e.g., sprites_rapha_original/ or sprites_meliadoul_original/
            // Fall back to sprites_original/ if character-specific folder doesn't have the file
            var characterOriginalDir = Path.Combine(_sourceUnitPath, $"sprites_{characterName.ToLower()}_original");
            var characterOriginalFile = Path.Combine(characterOriginalDir, spriteName);
            var genericOriginalDir = Path.Combine(_sourceUnitPath, "sprites_original");
            var genericOriginalFile = Path.Combine(genericOriginalDir, spriteName);

            string originalFile;
            if (File.Exists(characterOriginalFile))
            {
                originalFile = characterOriginalFile;
                ModLogger.Log($"[APPLY_STORY_USER_THEME] Using character-specific original: {originalFile}");
            }
            else if (File.Exists(genericOriginalFile))
            {
                originalFile = genericOriginalFile;
                ModLogger.Log($"[APPLY_STORY_USER_THEME] Using generic original: {originalFile}");
            }
            else
            {
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME] Original sprite not found in either location:");
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME]   Character-specific: {characterOriginalFile}");
                ModLogger.LogWarning($"[APPLY_STORY_USER_THEME]   Generic: {genericOriginalFile}");
                return;
            }

            try
            {
                // Read original sprite and user palette
                var originalSprite = File.ReadAllBytes(originalFile);
                var userPalette = File.ReadAllBytes(palettePath);
                ModLogger.Log($"[APPLY_STORY_USER_THEME] Original sprite size: {originalSprite.Length}, Palette size: {userPalette.Length}");

                // Validate palette size (should be 512 bytes)
                if (userPalette.Length != 512)
                {
                    ModLogger.LogWarning($"[APPLY_STORY_USER_THEME] Invalid palette size: {userPalette.Length} (expected 512)");
                    return;
                }

                // Replace palette in sprite (first 512 bytes)
                Array.Copy(userPalette, 0, originalSprite, 0, 512);

                // Write directly to base unit folder (same as regular themes)
                var destFile = Path.Combine(_unitPath, spriteName);
                ModLogger.Log($"[APPLY_STORY_USER_THEME] Writing to base unit folder: {destFile}");
                File.WriteAllBytes(destFile, originalSprite);

                ModLogger.LogSuccess($"[APPLY_STORY_USER_THEME] SUCCESS - Created: {destFile}");

                // For Ramza chapters, also patch the NXD with the user theme palette
                if (IsRamzaChapter(characterName))
                {
                    ApplyRamzaUserThemeToNxd(characterName, userPalette);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[APPLY_STORY_USER_THEME] FAILED: {ex.Message}");
                ModLogger.LogError($"[APPLY_STORY_USER_THEME] Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Checks if the character name is a Ramza chapter.
        /// </summary>
        private bool IsRamzaChapter(string characterName)
        {
            var lower = characterName.ToLower();
            return lower == "ramzachapter1" || lower == "ramzachapter23" || lower == "ramzachapter4";
        }

        /// <summary>
        /// Checks if the theme is a built-in Ramza theme (dark_knight, white_heretic, crimson_blade).
        /// </summary>
        private bool IsBuiltInRamzaTheme(string themeName)
        {
            var lower = themeName.ToLower();
            return lower == "dark_knight" || lower == "white_heretic" || lower == "crimson_blade";
        }

        /// <summary>
        /// Applies a built-in Ramza theme by patching the NXD with pre-computed palettes.
        /// </summary>
        private void ApplyBuiltInRamzaThemeToNxd(string characterName, string themeName)
        {
            try
            {
                ModLogger.Log($"[RAMZA_BUILTIN] Applying built-in theme '{themeName}' for {characterName}");

                var builtInPalettes = new RamzaBuiltInThemePalettes();
                var themeSaver = new RamzaThemeSaver();

                // Get chapter number from character name
                int chapter = characterName.ToLower() switch
                {
                    "ramzachapter1" => 1,
                    "ramzachapter23" => 2,
                    "ramzachapter4" => 4,
                    _ => throw new ArgumentException($"Invalid Ramza chapter: {characterName}")
                };

                // Get the pre-computed palette for this theme and chapter
                var clutData = builtInPalettes.GetThemePalette(themeName.ToLower(), chapter);
                if (clutData == null)
                {
                    ModLogger.LogWarning($"[RAMZA_BUILTIN] No palette found for {themeName}/{chapter}");
                    return;
                }

                // Log the armor colors being applied (indices 3-6)
                ModLogger.Log($"[RAMZA_BUILTIN] Armor colors for {themeName}/Ch{chapter}:");
                for (int i = 3; i <= 6; i++)
                {
                    int offset = i * 3;
                    ModLogger.Log($"[RAMZA_BUILTIN]   Index {i}: RGB({clutData[offset]}, {clutData[offset + 1]}, {clutData[offset + 2]})");
                }

                var success = themeSaver.ApplyClutData(chapter, clutData, _modPath);

                if (success)
                {
                    ModLogger.LogSuccess($"[RAMZA_BUILTIN] Successfully patched charclut.nxd for {characterName}/{themeName}");
                }
                else
                {
                    ModLogger.LogWarning($"[RAMZA_BUILTIN] Failed to patch charclut.nxd for {characterName}/{themeName}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[RAMZA_BUILTIN] Error applying built-in theme: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a Ramza user theme palette to the charclut.nxd file.
        /// </summary>
        private void ApplyRamzaUserThemeToNxd(string characterName, byte[] paletteData)
        {
            try
            {
                ModLogger.Log($"[RAMZA_NXD] Applying user theme NXD palette for {characterName}");

                var themeSaver = new RamzaThemeSaver();

                // Get chapter number from character name
                int chapter = characterName.ToLower() switch
                {
                    "ramzachapter1" => 1,
                    "ramzachapter23" => 2,
                    "ramzachapter4" => 4,
                    _ => throw new ArgumentException($"Invalid Ramza chapter: {characterName}")
                };

                // Convert palette to CLUTData and apply to NXD
                var clutData = themeSaver.ConvertPaletteToClutData(paletteData);
                var success = themeSaver.ApplyClutData(chapter, clutData, _modPath);

                if (success)
                {
                    ModLogger.LogSuccess($"[RAMZA_NXD] Successfully patched charclut.nxd for {characterName}");
                }
                else
                {
                    ModLogger.LogWarning($"[RAMZA_NXD] Failed to patch charclut.nxd for {characterName}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[RAMZA_NXD] Error patching NXD: {ex.Message}");
            }
        }
    }
}
