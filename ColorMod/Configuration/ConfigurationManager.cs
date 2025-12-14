using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FFTColorMod.Utilities;
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
                ModLogger.Log($"Using cached config (already in lock)");
                return _cachedConfig;
            }

            lock (_lockObject)
            {
                ModLogger.Log($"Loading config from: {_configPath}");

                // If we have a cached config, return it
                if (_cachedConfig != null)
                {
                    ModLogger.Log($"Using cached config");
                    return _cachedConfig;
                }

                // No cache - need to load or create
                if (!File.Exists(_configPath))
                {
                    ModLogger.Log($"Config file does not exist, creating new config");
                    _cachedConfig = new Config();
                    return _cachedConfig;
                }

                // Load from disk
                try
                {
                    var json = File.ReadAllText(_configPath);
                    ModLogger.Log($"Read JSON: {json.Substring(0, Math.Min(200, json.Length))}...");
                    _cachedConfig = JsonConvert.DeserializeObject<Config>(json, _jsonSettings) ?? new Config();
                    ModLogger.Log($"Loaded config - Squire_Male: {_cachedConfig.Squire_Male} (value: {(int)_cachedConfig.Squire_Male})");
                    _lastFileWriteTime = File.GetLastWriteTime(_configPath);
                    return _cachedConfig;
                }
                catch (Exception ex)
                {
                    ModLogger.Log($"Error loading config: {ex.Message}");
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
                    ModLogger.Log($"Saving config - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}");
                    var json = JsonConvert.SerializeObject(config, _jsonSettings);
                    ModLogger.Log($"Serialized JSON (first 500 chars): {json.Substring(0, Math.Min(500, json.Length))}");
                    var directory = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    ModLogger.Log($"Writing config to: {_configPath}");
                    File.WriteAllText(_configPath, json);

                    // Update cache and timestamp
                    _cachedConfig = config;
                    _lastFileWriteTime = DateTime.Now;

                    ModLogger.Log($"Config saved successfully");
                }
                catch (Exception ex)
                {
                    ModLogger.Log($"Error saving config: {ex.Message}");
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
            ModLogger.Log($"SetColorSchemeForJob START: {jobProperty} = {colorScheme}");

            // Add thread info to debug potential race conditions
            ModLogger.Log($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");

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
                    ModLogger.Log($"Created deep copy from cache");
                    ModLogger.Log($"Deep copy initial state - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, Knight_Female: {config.Knight_Female}");
                }
                else
                {
                    // Load from disk if no cache
                    config = LoadConfig();
                    ModLogger.Log($"Loaded from disk");
                    ModLogger.Log($"Loaded config state - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, Knight_Female: {config.Knight_Female}");
                }

                // Log current config state BEFORE change
                ModLogger.Log($"BEFORE - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, WhiteMage_Male: {config.WhiteMage_Male}");

                // Debug: List all ColorScheme properties
                var allProperties = typeof(Config).GetProperties()
                    .Where(p => p.PropertyType == typeof(Configuration.ColorScheme))
                    .Select(p => p.Name)
                    .ToList();
                ModLogger.Log($"Available ColorScheme properties (first 10): {string.Join(", ", allProperties.Take(10))}");

                ModLogger.Log($"Looking for property: '{jobProperty}'");
                var propertyInfo = typeof(Config).GetProperty(jobProperty);

                if (propertyInfo != null)
                {
                    ModLogger.Log($"Property found: {propertyInfo.Name}, Type: {propertyInfo.PropertyType}");

                    if (propertyInfo.PropertyType == typeof(Configuration.ColorScheme))
                    {
                        // Parse the string to ColorScheme enum
                        if (Enum.TryParse<Configuration.ColorScheme>(colorScheme, true, out var colorSchemeEnum))
                        {
                            ModLogger.Log($"About to set property '{propertyInfo.Name}' on config object");
                            ModLogger.Log($"Config object type: {config.GetType().FullName}");
                            ModLogger.Log($"Setting value to: {colorSchemeEnum} (enum value: {(int)colorSchemeEnum})");

                            // Get value before setting
                            var beforeValue = propertyInfo.GetValue(config);
                            ModLogger.Log($"Property value BEFORE: {beforeValue}");

                            propertyInfo.SetValue(config, colorSchemeEnum);

                            // Get value after setting
                            var afterValue = propertyInfo.GetValue(config);
                            ModLogger.Log($"Property value AFTER: {afterValue}");

                            // Debug: Check if other properties were accidentally modified
                            ModLogger.Log($"After setting {jobProperty}, checking other properties:");
                            ModLogger.Log($"Squire_Male: {config.Squire_Male}");
                            ModLogger.Log($"Archer_Female: {config.Archer_Female}");
                            ModLogger.Log($"Knight_Female: {config.Knight_Female}");

                            // Log state AFTER setting property but BEFORE save
                            ModLogger.Log($"AFTER SET - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, WhiteMage_Male: {config.WhiteMage_Male}");

                            SaveConfig(config);

                            // Verify it was set (use cached config which was updated by SaveConfig)
                            var verifyValue = propertyInfo.GetValue(_cachedConfig);
                            ModLogger.Log($"AFTER SAVE - Squire_Male: {_cachedConfig.Squire_Male}, Archer_Female: {_cachedConfig.Archer_Female}, WhiteMage_Male: {_cachedConfig.WhiteMage_Male}");
                            ModLogger.Log($"Verification - {jobProperty} is now: {verifyValue}");
                        }
                        else
                        {
                            ModLogger.Log($"Failed to parse color scheme: {colorScheme}");
                        }
                    }
                    else
                    {
                        ModLogger.Log($"Property type mismatch. Expected: {typeof(Configuration.ColorScheme)}, Got: {propertyInfo.PropertyType}");
                    }
                }
                else
                {
                    ModLogger.Log($"Property not found: {jobProperty}");
                    ModLogger.Log($"Did you mean one of: {string.Join(", ", allProperties.Take(5))}...");
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
            ModLogger.Log($"ResetToDefaults called");

            // Create a new default config with all properties explicitly set to original
            var defaultConfig = new Config();

            // Explicitly verify all properties are set to original (ColorScheme.original = 0)
            var properties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(ColorScheme));

            foreach (var prop in properties)
            {
                // Ensure each property is set to ColorScheme.original
                prop.SetValue(defaultConfig, ColorScheme.original);
            }

            // Log what we're about to save
            ModLogger.Log($"Default config - Knight_Male: {defaultConfig.Knight_Male}, Archer_Female: {defaultConfig.Archer_Female}");

            // Now save to disk using SaveConfig which properly updates the cache
            // SaveConfig has its own lock, so we don't need to lock here
            SaveConfig(defaultConfig);

            ModLogger.Log($"Reset to defaults completed");
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