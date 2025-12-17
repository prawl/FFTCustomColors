using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorMod.Core;
using FFTColorMod.Interfaces;
using FFTColorMod.Services;
using FFTColorMod.Utilities;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// Adapter class that maintains backward compatibility with ConfigurationManager
    /// while using the new ConfigurationService internally
    /// </summary>
    public class ConfigurationManagerAdapter
    {
        private readonly IConfigurationService _configurationService;
        private readonly string _configPath;
        private readonly JobClassDefinitionService? _jobClassService;

        public ConfigurationManagerAdapter(string configPath)
        {
            _configPath = configPath;
            var pathResolver = new PathResolverAdapter(configPath);
            _configurationService = new ConfigurationService(pathResolver);
        }

        public ConfigurationManagerAdapter(string configPath, JobClassDefinitionService jobClassService)
        {
            _configPath = configPath;
            _jobClassService = jobClassService;
            var pathResolver = new PathResolverAdapter(configPath);
            _configurationService = new ConfigurationService(pathResolver);
        }

        public virtual Config LoadConfig()
        {
            return _configurationService.LoadConfig();
        }

        public virtual void SaveConfig(Config config)
        {
            _configurationService.SaveConfig(config);
        }

        public Config GetDefaultConfiguration()
        {
            return _configurationService.GetDefaultConfig();
        }

        public void ResetToDefaults()
        {
            _configurationService.ResetToDefaults();
        }

        public List<string> GetAvailableColorSchemes()
        {
            // This method was specific to ConfigurationManager
            // We'll need to get this from somewhere else or hardcode for now
            var schemes = new List<string>
            {
                "original", "corpse_brigade", "lucavi", "northern_sky", "southern_sky",
                "crimson_red", "royal_purple", "phoenix_flame", "frost_knight", "silver_knight",
                "emerald_dragon", "rose_gold", "ocean_depths", "golden_templar", "blood_moon",
                "celestial", "volcanic", "amethyst"
            };

            if (_jobClassService != null)
            {
                var jobClassThemes = _jobClassService.GetAvailableThemes();
                foreach (var theme in jobClassThemes)
                {
                    if (!schemes.Contains(theme))
                        schemes.Add(theme);
                }
            }

            return schemes;
        }

        public Config ReloadConfig()
        {
            // Force reload from disk
            return _configurationService.LoadConfig();
        }

        public void SetColorSchemeForJob(string jobProperty, string colorScheme)
        {
            var config = _configurationService.LoadConfig();
            var property = typeof(Config).GetProperty(jobProperty);
            if (property != null && property.PropertyType == typeof(string))
            {
                property.SetValue(config, colorScheme);
                _configurationService.SaveConfig(config);
            }
        }

        public string GetJobPropertyForSprite(string spriteName)
        {
            // Map sprite names to property names
            if (string.IsNullOrEmpty(spriteName))
                return null;

            // Remove common prefixes/suffixes
            var normalized = spriteName
                .Replace("battle_", "")
                .Replace("_spr", "")
                .Replace(".bin", "");

            // Map specific sprite names to job properties
            var mappings = new Dictionary<string, string>
            {
                // Knights
                ["knight_m"] = "Knight_Male",
                ["knight_w"] = "Knight_Female",
                // Archers (yumi)
                ["yumi_m"] = "Archer_Male",
                ["yumi_w"] = "Archer_Female",
                // Chemists (item)
                ["item_m"] = "Chemist_Male",
                ["item_w"] = "Chemist_Female",
                // Monks
                ["monk_m"] = "Monk_Male",
                ["monk_w"] = "Monk_Female",
                // White Mages (siro)
                ["siro_m"] = "WhiteMage_Male",
                ["siro_w"] = "WhiteMage_Female",
                // Black Mages (kuro)
                ["kuro_m"] = "BlackMage_Male",
                ["kuro_w"] = "BlackMage_Female",
                // Squires
                ["squire_m"] = "Squire_Male",
                ["squire_w"] = "Squire_Female"
            };

            return mappings.TryGetValue(normalized, out var result) ? result : null;
        }

        public string GetColorSchemeForSprite(string spriteName)
        {
            var config = _configurationService.LoadConfig();
            var mapper = new SpriteNameMapper(config);
            return mapper.GetColorForSprite(spriteName);
        }

        // Private helper class for path resolution
        private class PathResolverAdapter : IPathResolver
        {
            private readonly string _configPath;
            private readonly string _modRootPath;

            public PathResolverAdapter(string configPath)
            {
                _configPath = configPath;
                var configDir = Path.GetDirectoryName(configPath);
                _modRootPath = configDir ?? Environment.CurrentDirectory;
            }

            public string ModRootPath => _modRootPath;
            public string SourcePath => @"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod";
            public string UserConfigPath => Path.GetDirectoryName(_configPath) ?? _modRootPath;

            public string GetDataPath(string relativePath)
            {
                return Path.Combine(_modRootPath, "Data", relativePath);
            }

            public string GetSpritePath(string characterName, string themeName, string spriteFileName)
            {
                return Path.Combine(SourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit",
                    $"sprites_{themeName}", spriteFileName);
            }

            public string GetThemeDirectory(string characterName, string themeName)
            {
                return Path.Combine(SourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit",
                    $"sprites_{themeName}");
            }

            public string GetConfigPath()
            {
                return _configPath;
            }

            public string GetPreviewImagePath(string characterName, string themeName)
            {
                return Path.Combine(_modRootPath, "Resources", "Previews",
                    $"{characterName}_{themeName}.png");
            }

            public string ResolveFirstExistingPath(params string[] candidates)
            {
                foreach (var path in candidates)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                        return path;
                }
                return candidates.FirstOrDefault() ?? string.Empty;
            }

            public IEnumerable<string> GetAvailableThemes(string characterName)
            {
                var themesPath = Path.Combine(SourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                if (Directory.Exists(themesPath))
                {
                    var dirs = Directory.GetDirectories(themesPath, "sprites_*");
                    foreach (var dir in dirs)
                    {
                        yield return Path.GetFileName(dir).Replace("sprites_", "");
                    }
                }
            }
        }
    }
}