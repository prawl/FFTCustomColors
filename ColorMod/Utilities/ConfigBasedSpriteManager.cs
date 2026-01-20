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
    /// Orchestrates sprite theming operations by coordinating between specialized services.
    /// Refactored to follow Single Responsibility Principle - delegates to:
    /// - SpritePathResolver: Path resolution and job name mapping
    /// - SpriteFileCopier: File copy operations
    /// - UserThemeApplicator: User theme palette application
    /// - SpriteFileInterceptor: Runtime path interception
    /// - RamzaNxdService: Ramza NXD patching
    /// </summary>
    public class ConfigBasedSpriteManager
    {
        private readonly string _modPath;
        private readonly ConfigurationManager _configManager;
        private readonly CharacterDefinitionService _characterService;
        private readonly UserThemeService _userThemeService;

        // Extracted services
        private readonly SpritePathResolver _pathResolver;
        private readonly SpriteFileCopier _fileCopier;
        private readonly UserThemeApplicator _userThemeApplicator;
        private readonly SpriteFileInterceptor _fileInterceptor;
        private readonly RamzaNxdService _ramzaNxdService;

        public string GetModPath() => _modPath;

        public ConfigBasedSpriteManager(
            string modPath,
            ConfigurationManager configManager,
            CharacterDefinitionService characterService,
            string sourcePath = null)
        {
            _modPath = modPath;
            _configManager = configManager;
            _characterService = characterService;

            // Initialize user theme service
            _userThemeService = new UserThemeService(_modPath);

            // Initialize extracted services
            _pathResolver = new SpritePathResolver(_modPath);
            _fileCopier = new SpriteFileCopier(_pathResolver);
            _userThemeApplicator = new UserThemeApplicator(_pathResolver, _userThemeService);
            _fileInterceptor = new SpriteFileInterceptor(_pathResolver, _configManager, _userThemeService);
            _ramzaNxdService = new RamzaNxdService(_modPath);

            LogInitialization();
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

            ApplyGenericJobThemes(config);
            ApplyStoryCharacterThemes(config);

            ModLogger.Log("ApplyConfiguration completed");
        }

        private void ApplyGenericJobThemes(Config config)
        {
            var properties = typeof(Config).GetProperties()
                .Where(p => p.GetIndexParameters().Length == 0 &&
                           p.PropertyType == typeof(string) &&
                           (p.Name.EndsWith("_Male") || p.Name.EndsWith("_Female")));

            foreach (var property in properties)
            {
                try
                {
                    var colorScheme = property.GetValue(config) as string ?? "original";
                    ModLogger.LogDebug($"Applying {property.Name}: {colorScheme}");

                    var spriteName = _pathResolver.GetSpriteNameForJob(property.Name);
                    if (spriteName != null)
                    {
                        var jobType = property.Name.Replace("_Male", "").Replace("_Female", "").ToLower();
                        ApplyGenericJobTheme(spriteName, colorScheme, jobType, property.Name);
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"Error processing property {property.Name}: {ex.Message}");
                }
            }
        }

        private void ApplyGenericJobTheme(string spriteName, string colorScheme, string jobType, string jobName)
        {
            var unitPath = _pathResolver.GetUnitPathForJob(jobName);

            if (colorScheme.ToLower() == "original")
            {
                _fileCopier.RestoreOriginalSprite(spriteName, unitPath);
                return;
            }

            if (_userThemeApplicator.IsUserTheme(jobName, colorScheme))
            {
                _userThemeApplicator.ApplyUserTheme(spriteName, colorScheme, jobName);
                return;
            }

            _fileCopier.CopyThemedSprite(spriteName, colorScheme, jobType, jobName);
        }

        private void ApplyStoryCharacterThemes(Config config)
        {
            if (_characterService == null)
            {
                ModLogger.LogWarning("CharacterDefinitionService is null, skipping story character themes");
                return;
            }

            // Apply all Ramza chapters' NXD palettes together FIRST
            _ramzaNxdService.ApplyAllChaptersToNxd(config);

            foreach (var character in _characterService.GetAllCharacters())
            {
                if (character.SpriteNames.Length == 0)
                    continue;

                var configProperty = typeof(Config).GetProperty(character.Name);
                if (configProperty == null)
                    continue;

                var themeValue = configProperty.GetValue(config);
                if (themeValue == null)
                    continue;

                var themeName = themeValue.ToString() ?? "original";

                foreach (var spriteName in character.SpriteNames)
                {
                    ApplyStoryCharacterTheme(character.Name.ToLower(), spriteName, themeName);
                }
            }
        }

        private void ApplyStoryCharacterTheme(string characterName, string spriteName, string themeName)
        {
            ModLogger.Log($"Applying theme for character: {characterName}, sprite: {spriteName}, theme: {themeName}");

            var unitPath = _pathResolver.UnitPath;

            if (themeName.ToLower() == "original")
            {
                _fileCopier.RestoreStoryCharacterOriginalSprite(characterName, spriteName, unitPath);
                return;
            }

            var properCharacterName = _pathResolver.NormalizeCharacterName(characterName);

            // Built-in Ramza themes are handled via NXD patching (already done in ApplyAllChaptersToNxd)
            if (_pathResolver.IsRamzaChapter(properCharacterName) && _pathResolver.IsBuiltInRamzaTheme(themeName))
            {
                ModLogger.Log($"[STORY_CHAR_THEME] Skipping individual NXD patch for {properCharacterName}/{themeName} (handled by ApplyAllRamzaChaptersToNxd)");
                return;
            }

            if (_userThemeApplicator.IsUserTheme(properCharacterName, themeName))
            {
                var paletteData = _userThemeApplicator.ApplyStoryCharacterUserTheme(
                    $"battle_{spriteName}_spr.bin", themeName, properCharacterName, unitPath);

                // For Ramza, also patch the NXD with the user theme palette
                if (paletteData != null && _pathResolver.IsRamzaChapter(properCharacterName))
                {
                    _ramzaNxdService.ApplyUserThemeToNxd(properCharacterName, paletteData);
                }
                return;
            }

            _fileCopier.CopyStoryCharacterThemedSprite(characterName, spriteName, themeName, unitPath);
        }

        // Public API methods delegating to services

        public string InterceptFilePath(string originalPath)
        {
            return _fileInterceptor.InterceptFilePath(originalPath);
        }

        public string GetActiveColorForJob(string jobProperty)
        {
            return _fileInterceptor.GetActiveColorForJob(jobProperty);
        }

        public void SetColorForJob(string jobProperty, string colorScheme)
        {
            _configManager.SetColorSchemeForJob(jobProperty, colorScheme);

            var spriteName = _pathResolver.GetSpriteNameForJob(jobProperty);
            if (spriteName != null)
            {
                var jobType = jobProperty.Replace("_Male", "").Replace("_Female", "").ToLower();
                ApplyGenericJobTheme(spriteName, colorScheme, jobType, jobProperty);
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
            _configManager.ReloadConfig();
            ApplyConfiguration();
        }

        public string GetJobFromSpriteName(string spriteName)
        {
            return _configManager.GetJobPropertyForSprite(spriteName);
        }

        private void LogInitialization()
        {
            ModLogger.Log("ConfigBasedSpriteManager initialized:");
            ModLogger.Log($"  Mod path: {_modPath}");
            ModLogger.Log($"  Unit path: {_pathResolver.UnitPath}");

            if (Directory.Exists(_pathResolver.UnitPath))
            {
                ModLogger.Log($"  FFTIVC folder EXISTS at: {_pathResolver.UnitPath}");
                var themeDirs = Directory.GetDirectories(_pathResolver.UnitPath, "sprites_*");
                ModLogger.Log($"  Found {themeDirs.Length} theme directories");
            }
            else
            {
                ModLogger.LogError($"  FFTIVC folder MISSING at: {_pathResolver.UnitPath}");
            }
        }
    }
}
