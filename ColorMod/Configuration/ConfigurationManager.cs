using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace FFTColorMod.Configuration
{
    public class ConfigurationManager
    {
        private readonly string _configPath;
        private Config _cachedConfig;
        private readonly object _lockObject = new object();
        private DateTime _lastFileWriteTime = DateTime.MinValue;

        // Use consistent JSON settings with enum string conversion
        // IMPORTANT: Use default naming (not camelCase) to match property names exactly
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            ContractResolver = new DefaultContractResolver() // Use exact property names
        };

        public ConfigurationManager(string configPath)
        {
            _configPath = configPath;
        }

        public Config LoadConfig()
        {
            // If we already have the lock (recursive call), use cache directly
            if (Monitor.IsEntered(_lockObject) && _cachedConfig != null)
            {
                Console.WriteLine($"[FFT Color Mod] Using cached config (already in lock)");
                return _cachedConfig;
            }

            lock (_lockObject)
            {
                Console.WriteLine($"[FFT Color Mod] Loading config from: {_configPath}");

                // If we have a cached config, return it
                if (_cachedConfig != null)
                {
                    Console.WriteLine($"[FFT Color Mod] Using cached config");
                    return _cachedConfig;
                }

                // No cache - need to load or create
                if (!File.Exists(_configPath))
                {
                    Console.WriteLine($"[FFT Color Mod] Config file does not exist, creating new config");
                    _cachedConfig = new Config();
                    return _cachedConfig;
                }

                // Load from disk
                try
                {
                    var json = File.ReadAllText(_configPath);
                    Console.WriteLine($"[FFT Color Mod] Read JSON: {json.Substring(0, Math.Min(200, json.Length))}...");
                    _cachedConfig = JsonConvert.DeserializeObject<Config>(json, _jsonSettings) ?? new Config();
                    Console.WriteLine($"[FFT Color Mod] Loaded config - Squire_Male: {_cachedConfig.Squire_Male} (value: {(int)_cachedConfig.Squire_Male})");
                    _lastFileWriteTime = File.GetLastWriteTime(_configPath);
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

        // Add a method to force reload from disk when needed
        public Config ReloadConfig()
        {
            lock (_lockObject)
            {
                _cachedConfig = null;  // Force reload
                return LoadConfig();
            }
        }

        public void SaveConfig(Config config)
        {
            lock (_lockObject)
            {
                try
                {
                    Console.WriteLine($"[FFT Color Mod] Saving config - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}");
                    var json = JsonConvert.SerializeObject(config, _jsonSettings);
                    Console.WriteLine($"[FFT Color Mod] Serialized JSON (first 500 chars): {json.Substring(0, Math.Min(500, json.Length))}");
                    var directory = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    Console.WriteLine($"[FFT Color Mod] Writing config to: {_configPath}");
                    File.WriteAllText(_configPath, json);

                    // Update cache and timestamp
                    _cachedConfig = config;
                    _lastFileWriteTime = DateTime.Now;

                    Console.WriteLine($"[FFT Color Mod] Config saved successfully");
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
            Console.WriteLine($"[FFT Color Mod] SetColorSchemeForJob START: {jobProperty} = {colorScheme}");

            // Add thread info to debug potential race conditions
            Console.WriteLine($"[FFT Color Mod] Thread ID: {Thread.CurrentThread.ManagedThreadId}");

            // Use lock to prevent race conditions
            lock (_lockObject)
            {
                // Create a new config object based on current cache to avoid direct modification
                Config config;
                if (_cachedConfig != null)
                {
                    // Serialize and deserialize to create a deep copy
                    var json = JsonConvert.SerializeObject(_cachedConfig, _jsonSettings);
                    config = JsonConvert.DeserializeObject<Config>(json, _jsonSettings) ?? new Config();
                }
                else
                {
                    // Load from disk if no cache
                    config = LoadConfig();
                }

                // Log current config state BEFORE change
                Console.WriteLine($"[FFT Color Mod] BEFORE - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, WhiteMage_Male: {config.WhiteMage_Male}");

                // Debug: List all ColorScheme properties
                var allProperties = typeof(Config).GetProperties()
                    .Where(p => p.PropertyType == typeof(Configuration.ColorScheme))
                    .Select(p => p.Name)
                    .ToList();
                Console.WriteLine($"[FFT Color Mod] Available ColorScheme properties (first 10): {string.Join(", ", allProperties.Take(10))}");

                var propertyInfo = typeof(Config).GetProperty(jobProperty);

                if (propertyInfo != null)
                {
                    Console.WriteLine($"[FFT Color Mod] Property found: {propertyInfo.Name}, Type: {propertyInfo.PropertyType}");

                    if (propertyInfo.PropertyType == typeof(Configuration.ColorScheme))
                    {
                        // Parse the string to ColorScheme enum
                        if (Enum.TryParse<Configuration.ColorScheme>(colorScheme, true, out var colorSchemeEnum))
                        {
                            Console.WriteLine($"[FFT Color Mod] About to set property '{propertyInfo.Name}' on config object");
                            Console.WriteLine($"[FFT Color Mod] Config object type: {config.GetType().FullName}");
                            Console.WriteLine($"[FFT Color Mod] Setting value to: {colorSchemeEnum} (enum value: {(int)colorSchemeEnum})");

                            // Get value before setting
                            var beforeValue = propertyInfo.GetValue(config);
                            Console.WriteLine($"[FFT Color Mod] Property value BEFORE: {beforeValue}");

                            propertyInfo.SetValue(config, colorSchemeEnum);

                            // Get value after setting
                            var afterValue = propertyInfo.GetValue(config);
                            Console.WriteLine($"[FFT Color Mod] Property value AFTER: {afterValue}");

                            // Log state AFTER setting property but BEFORE save
                            Console.WriteLine($"[FFT Color Mod] AFTER SET - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, WhiteMage_Male: {config.WhiteMage_Male}");

                            SaveConfig(config);

                            // Verify it was set (use cached config which was updated by SaveConfig)
                            var verifyValue = propertyInfo.GetValue(_cachedConfig);
                            Console.WriteLine($"[FFT Color Mod] AFTER SAVE - Squire_Male: {_cachedConfig.Squire_Male}, Archer_Female: {_cachedConfig.Archer_Female}, WhiteMage_Male: {_cachedConfig.WhiteMage_Male}");
                            Console.WriteLine($"[FFT Color Mod] Verification - {jobProperty} is now: {verifyValue}");
                        }
                        else
                        {
                            Console.WriteLine($"[FFT Color Mod] Failed to parse color scheme: {colorScheme}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[FFT Color Mod] Property type mismatch. Expected: {typeof(Configuration.ColorScheme)}, Got: {propertyInfo.PropertyType}");
                    }
                }
                else
                {
                    Console.WriteLine($"[FFT Color Mod] Property not found: {jobProperty}");
                    Console.WriteLine($"[FFT Color Mod] Did you mean one of: {string.Join(", ", allProperties.Take(5))}...");
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
            lock (_lockObject)
            {
                // Create a new default config with all properties explicitly set to original
                var defaultConfig = new Config();

                // Manually update the cache to ensure it's replaced
                _cachedConfig = defaultConfig;

                // Now save to disk
                try
                {
                    var json = JsonConvert.SerializeObject(defaultConfig, _jsonSettings);
                    var directory = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.WriteAllText(_configPath, json);
                    _lastFileWriteTime = DateTime.Now;
                    Console.WriteLine($"[FFT Color Mod] Reset to defaults completed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFT Color Mod] Error resetting to defaults: {ex.Message}");
                }
            }
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