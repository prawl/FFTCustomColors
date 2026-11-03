using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>The old file backed configuration manager, kept for callers not yet on the service.</summary>
    public class ConfigurationManagerLegacy
    {
        private readonly string _configPath;
        private Config _cachedConfig;
        private readonly object _lockObject = new object();

        // Use consistent JSON settings with reflection-based converter
        // Use default naming (not camelCase) to match property names exactly
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter> { new ReflectionBasedConfigJsonConverter() },
            ContractResolver = new DefaultContractResolver() // Use exact property names
        };

        private readonly JobClassDefinitionService? _jobClassService;

        public ConfigurationManagerLegacy(string configPath)
        {
            _configPath = configPath;
        }

        public ConfigurationManagerLegacy(string configPath, JobClassDefinitionService jobClassService)
        {
            _configPath = configPath;
            _jobClassService = jobClassService;
        }

        public virtual Config LoadConfig()
        {
            // If we already have the lock (recursive call), use cache directly
            if (Monitor.IsEntered(_lockObject) && _cachedConfig != null)
            {
                ModLogger.LogDebug(LogVerb.Config, $"Using cached config (already in lock)");
                return _cachedConfig;
            }

            lock (_lockObject)
            {
                ModLogger.LogDebug(LogVerb.Config, $"Loading config from: {_configPath}");

                // If we have a cached config, return it
                if (_cachedConfig != null)
                {
                    ModLogger.LogDebug(LogVerb.Config, $"Using cached config");
                    return _cachedConfig;
                }

                // No cache, need to load or create
                if (!File.Exists(_configPath))
                {
                    ModLogger.LogDebug(LogVerb.Config, $"Config file does not exist, creating new config");
                    _cachedConfig = new Config();
                    return _cachedConfig;
                }

                // Load from disk
                try
                {
                    var json = File.ReadAllText(_configPath);
                    ModLogger.LogDebug(LogVerb.Config, $"Read JSON: {json.Substring(0, Math.Min(200, json.Length))}...");
                    _cachedConfig = JsonConvert.DeserializeObject<Config>(json, _jsonSettings) ?? new Config();
                    ModLogger.LogDebug(LogVerb.Config, $"Loaded config - Squire_Male: {_cachedConfig.Squire_Male}");
                    return _cachedConfig;
                }
                catch (Exception ex)
                {
                    ModLogger.LogError(LogVerb.Config, $"Error loading config: {ex.Message}");
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
                    ModLogger.LogDebug(LogVerb.Config, $"Saving config - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}");
                    var json = JsonConvert.SerializeObject(config, _jsonSettings);
                    ModLogger.LogDebug(LogVerb.Config, $"Serialized JSON (first 500 chars): {json.Substring(0, Math.Min(500, json.Length))}");
                    var directory = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    ModLogger.LogDebug(LogVerb.Config, $"Writing config to: {_configPath}");
                    File.WriteAllText(_configPath, json);

                    // Update cache and timestamp
                    _cachedConfig = config;

                    ModLogger.LogDebug(LogVerb.Config, $"Config saved successfully");
                }
                catch (Exception ex)
                {
                    ModLogger.LogError(LogVerb.Config, $"Error saving config: {ex.Message}");
                }
            }
        }

        public string GetColorSchemeForSprite(string spriteName)
        {
            var config = LoadConfig();
            var mapper = new SpriteNameMapper(config);
            return mapper.GetColorForSprite(spriteName);
        }

        public void SetColorSchemeForJob(string jobProperty, string colorScheme)
        {
            ModLogger.LogDebug(LogVerb.Config, $"SetColorSchemeForJob START: {jobProperty} = {colorScheme}");

            // Add thread info to debug potential race conditions
            ModLogger.LogDebug(LogVerb.Config, $"Thread ID: {Thread.CurrentThread.ManagedThreadId}");

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
                    ModLogger.LogDebug(LogVerb.Config, $"Created deep copy from cache");
                    ModLogger.LogDebug(LogVerb.Config, $"Deep copy initial state - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, Knight_Female: {config.Knight_Female}");
                }
                else
                {
                    // Load from disk if no cache
                    config = LoadConfig();
                    ModLogger.LogDebug(LogVerb.Config, $"Loaded from disk");
                    ModLogger.LogDebug(LogVerb.Config, $"Loaded config state - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, Knight_Female: {config.Knight_Female}");
                }

                // Log current config state BEFORE change
                ModLogger.LogDebug(LogVerb.Config, $"BEFORE - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, WhiteMage_Male: {config.WhiteMage_Male}");

                // List all theme properties
                var allProperties = typeof(Config).GetProperties()
                    .Where(p => p.PropertyType == typeof(string) &&
                               (p.Name.EndsWith("_Male") || p.Name.EndsWith("_Female")))
                    .Select(p => p.Name)
                    .ToList();
                ModLogger.LogDebug(LogVerb.Config, $"Available theme properties (first 10): {string.Join(", ", allProperties.Take(10))}");

                ModLogger.LogDebug(LogVerb.Config, $"Looking for property: '{jobProperty}'");
                var propertyInfo = typeof(Config).GetProperty(jobProperty);

                if (propertyInfo != null)
                {
                    ModLogger.LogDebug(LogVerb.Config, $"Property found: {propertyInfo.Name}, Type: {propertyInfo.PropertyType}");

                    if (propertyInfo.PropertyType == typeof(string))
                    {
                        ModLogger.LogDebug(LogVerb.Config, $"About to set property '{propertyInfo.Name}' on config object");
                        ModLogger.LogDebug(LogVerb.Config, $"Config object type: {config.GetType().FullName}");
                        ModLogger.LogDebug(LogVerb.Config, $"Setting value to: {colorScheme}");

                        // Get value before setting
                        var beforeValue = propertyInfo.GetValue(config);
                        ModLogger.LogDebug(LogVerb.Config, $"Property value BEFORE: {beforeValue}");

                        propertyInfo.SetValue(config, colorScheme);

                        // Get value after setting
                        var afterValue = propertyInfo.GetValue(config);
                        ModLogger.LogDebug(LogVerb.Config, $"Property value AFTER: {afterValue}");

                        // Check if other properties were accidentally modified
                        ModLogger.LogDebug(LogVerb.Config, $"After setting {jobProperty}, checking other properties:");
                        ModLogger.LogDebug(LogVerb.Config, $"Squire_Male: {config.Squire_Male}");
                        ModLogger.LogDebug(LogVerb.Config, $"Archer_Female: {config.Archer_Female}");
                        ModLogger.LogDebug(LogVerb.Config, $"Knight_Female: {config.Knight_Female}");

                        // Log state AFTER setting property but BEFORE save
                        ModLogger.LogDebug(LogVerb.Config, $"AFTER SET - Squire_Male: {config.Squire_Male}, Archer_Female: {config.Archer_Female}, WhiteMage_Male: {config.WhiteMage_Male}");

                        SaveConfig(config);

                        // Verify it was set (use cached config which was updated by SaveConfig)
                        var verifyValue = propertyInfo.GetValue(_cachedConfig);
                        ModLogger.LogDebug(LogVerb.Config, $"AFTER SAVE - Squire_Male: {_cachedConfig.Squire_Male}, Archer_Female: {_cachedConfig.Archer_Female}, WhiteMage_Male: {_cachedConfig.WhiteMage_Male}");
                        ModLogger.LogDebug(LogVerb.Config, $"Verification - {jobProperty} is now: {verifyValue}");
                    }
                    else
                    {
                        ModLogger.LogDebug(LogVerb.Config, $"Property type mismatch. Expected: {typeof(string)}, Got: {propertyInfo.PropertyType}");
                    }
                }
                else
                {
                    ModLogger.LogDebug(LogVerb.Config, $"Property not found: {jobProperty}");
                    ModLogger.LogDebug(LogVerb.Config, $"Did you mean one of: {string.Join(", ", allProperties.Take(5))}...");
                }
            }
        }

        public List<string> GetAvailableColorSchemes()
        {
            // Get themes from the JobClassDefinitionService
            // This allows for easy configuration via JSON
            var jobClassService = _jobClassService ?? Services.JobClassServiceSingleton.Instance;
            return jobClassService.GetAvailableThemes();
        }

        public void ResetToDefaults()
        {
            ModLogger.LogDebug(LogVerb.Config, $"ResetToDefaults called");

            // Create a new default config with all properties explicitly set to original
            var defaultConfig = new Config();

            // Get all job class properties (ending with _Male or _Female)
            var jobProperties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(string) &&
                      (p.Name.EndsWith("_Male") || p.Name.EndsWith("_Female")));

            foreach (var prop in jobProperties)
            {
                // Ensure each property is set to "original"
                prop.SetValue(defaultConfig, "original");
            }

            // Also reset story character properties to original
            var storyCharacters = new[] { "Agrias", "Orlandeau", "Cloud", "Mustadio", "Reis", "Rapha", "Marach", "Beowulf", "Meliadoul", "Construct8",
                "Alma", "Argath", "Celia", "DelitaChapter1", "DelitaChapter23", "DelitaChapter4", "Elmdore", "Gaffgarion", "Isilud", "Lettie", "Orran", "Ovelia", "Simon", "Valmafra", "Zalmour" };
            foreach (var characterName in storyCharacters)
            {
                var prop = typeof(Config).GetProperty(characterName);
                if (prop != null && prop.PropertyType == typeof(string))
                {
                    prop.SetValue(defaultConfig, "original");
                    ModLogger.LogDebug(LogVerb.Config, $"Reset story character {characterName} to original");
                }
            }

            // Log what we're about to save
            ModLogger.LogDebug(LogVerb.Config, $"Default config - Knight_Male: {defaultConfig.Knight_Male}, Archer_Female: {defaultConfig.Archer_Female}");
            ModLogger.LogDebug(LogVerb.Config, $"Story characters - Mustadio: {defaultConfig.Mustadio}, Rapha: {defaultConfig.Rapha}, Meliadoul: {defaultConfig.Meliadoul}");

            // Now save to disk using SaveConfig which properly updates the cache
            // SaveConfig has its own lock, so we don't need to lock here
            SaveConfig(defaultConfig);

            ModLogger.LogDebug(LogVerb.Config, $"Reset to defaults completed");
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
                // Dancers (odori, female only)
                ["odori_w"] = "Dancer_Female",
                // Bards (gin, male only)
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
