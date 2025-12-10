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

            if (propertyInfo != null && propertyInfo.PropertyType == typeof(string))
            {
                propertyInfo.SetValue(config, colorScheme);
                SaveConfig(config);
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
                "smoke",
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
                ["knight_m"] = "KnightMale",
                ["knight_w"] = "KnightFemale",
                // Archers (yumi)
                ["yumi_m"] = "ArcherMale",
                ["yumi_w"] = "ArcherFemale",
                // Chemists (item)
                ["item_m"] = "ChemistMale",
                ["item_w"] = "ChemistFemale",
                // Monks
                ["monk_m"] = "MonkMale",
                ["monk_w"] = "MonkFemale",
                // White Mages (siro)
                ["siro_m"] = "WhiteMageMale",
                ["siro_w"] = "WhiteMageFemale",
                // Black Mages (kuro)
                ["kuro_m"] = "BlackMageMale",
                ["kuro_w"] = "BlackMageFemale",
                // Thieves
                ["thief_m"] = "ThiefMale",
                ["thief_w"] = "ThiefFemale",
                // Ninjas
                ["ninja_m"] = "NinjaMale",
                ["ninja_w"] = "NinjaFemale",
                // Squires (mina)
                ["mina_m"] = "SquireMale",
                ["mina_w"] = "SquireFemale",
                // Time Mages (toki)
                ["toki_m"] = "TimeMageMale",
                ["toki_w"] = "TimeMageFemale",
                // Summoners (syou)
                ["syou_m"] = "SummonerMale",
                ["syou_w"] = "SummonerFemale",
                // Samurai (samu)
                ["samu_m"] = "SamuraiMale",
                ["samu_w"] = "SamuraiFemale",
                // Dragoons (ryu)
                ["ryu_m"] = "DragoonMale",
                ["ryu_w"] = "DragoonFemale",
                // Geomancers (fusui)
                ["fusui_m"] = "GeomancerMale",
                ["fusui_w"] = "GeomancerFemale",
                // Oracles/Mystics (onmyo)
                ["onmyo_m"] = "MysticMale",
                ["onmyo_w"] = "MysticFemale",
                // Mediators/Orators (waju)
                ["waju_m"] = "MediatorMale",
                ["waju_w"] = "MediatorFemale",
                // Dancers (odori - female only)
                ["odori_w"] = "DancerFemale",
                // Bards (gin - male only)
                ["gin_m"] = "BardMale",
                // Mimes (mono)
                ["mono_m"] = "MimeMale",
                ["mono_w"] = "MimeFemale",
                // Calculators (san)
                ["san_m"] = "CalculatorMale",
                ["san_w"] = "CalculatorFemale"
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