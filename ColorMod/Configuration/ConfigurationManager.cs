using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace FFTColorMod.Configuration
{
    public class ConfigurationManager
    {
        private readonly string _configPath;
        private Config _cachedConfig;
        private readonly object _lockObject = new object();

        public ConfigurationManager(string configPath)
        {
            _configPath = configPath;
        }

        public Config LoadConfig()
        {
            lock (_lockObject)
            {
                if (_cachedConfig != null)
                    return _cachedConfig;

                if (!File.Exists(_configPath))
                {
                    _cachedConfig = new Config();
                    return _cachedConfig;
                }

                try
                {
                    var json = File.ReadAllText(_configPath);
                    _cachedConfig = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
                    return _cachedConfig;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFT Color Mod] Error loading config: {ex.Message}");
                    _cachedConfig = new Config();
                    return _cachedConfig;
                }
            }
        }

        public void SaveConfig(Config config)
        {
            lock (_lockObject)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                    var directory = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.WriteAllText(_configPath, json);
                    _cachedConfig = config;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFT Color Mod] Error saving config: {ex.Message}");
                }
            }
        }

        public string GetColorSchemeForSprite(string spriteName)
        {
            var config = LoadConfig();
            return config.GetColorForSprite(spriteName);
        }

        public void SetColorSchemeForJob(string jobProperty, string colorScheme)
        {
            var config = LoadConfig();
            var propertyInfo = typeof(Config).GetProperty(jobProperty);

            if (propertyInfo != null && propertyInfo.PropertyType == typeof(ColorScheme))
            {
                // Parse the string to ColorScheme enum
                if (Enum.TryParse<ColorScheme>(colorScheme, true, out var colorSchemeEnum))
                {
                    propertyInfo.SetValue(config, colorSchemeEnum);
                    SaveConfig(config);
                }
            }
        }

        public List<string> GetAvailableColorSchemes()
        {
            // Return the list of known color schemes
            return new List<string>
            {
                "original",
                "corpse_brigade",
                "lucavi",
                "northern_sky",
                "southern_sky",
                "crimson_red",
                "royal_purple",
                "phoenix_flame",
                "frost_knight",
                "silver_knight",
                "shadow_assassin",
                "emerald_dragon",
                "rose_gold",
                "ocean_depths",
                "golden_templar",
                "blood_moon",
                "celestial",
                "volcanic",
                "amethyst"
            };
        }

        public void ResetToDefaults()
        {
            var defaultConfig = new Config();
            SaveConfig(defaultConfig);
        }

        public string GetJobPropertyForSprite(string spriteName)
        {
            // Map sprite names to property names
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
                // Thieves
                ["thief_m"] = "Thief_Male",
                ["thief_w"] = "Thief_Female",
                // Ninjas
                ["ninja_m"] = "Ninja_Male",
                ["ninja_w"] = "Ninja_Female",
                // Squires (mina)
                ["mina_m"] = "Squire_Male",
                ["mina_w"] = "Squire_Female",
                // Time Mages (toki)
                ["toki_m"] = "TimeMage_Male",
                ["toki_w"] = "TimeMage_Female",
                // Summoners (syou)
                ["syou_m"] = "Summoner_Male",
                ["syou_w"] = "Summoner_Female",
                // Samurai (samu)
                ["samu_m"] = "Samurai_Male",
                ["samu_w"] = "Samurai_Female",
                // Dragoons (ryu)
                ["ryu_m"] = "Dragoon_Male",
                ["ryu_w"] = "Dragoon_Female",
                // Geomancers (fusui)
                ["fusui_m"] = "Geomancer_Male",
                ["fusui_w"] = "Geomancer_Female",
                // Oracles/Mystics (onmyo)
                ["onmyo_m"] = "Mystic_Male",
                ["onmyo_w"] = "Mystic_Female",
                // Mediators/Orators (waju)
                ["waju_m"] = "Mediator_Male",
                ["waju_w"] = "Mediator_Female",
                // Dancers (odori - female only)
                ["odori_w"] = "Dancer_Female",
                // Bards (gin - male only)
                ["gin_m"] = "Bard_Male",
                // Mimes (mono)
                ["mono_m"] = "Mime_Male",
                ["mono_w"] = "Mime_Female",
                // Calculators (san)
                ["san_m"] = "Calculator_Male",
                ["san_w"] = "Calculator_Female"
            };

            foreach (var mapping in mappings)
            {
                if (spriteName.Contains(mapping.Key))
                {
                    return mapping.Value;
                }
            }

            return null;
        }
    }
}