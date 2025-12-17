using System;
using System.IO;
using System.Diagnostics;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;

namespace FFTColorMod.Services
{
    public class ModInitializer
    {
        private readonly string _modPath;
        private readonly string _sourcePath;

        public ModInitializer(string modPath, string sourcePath)
        {
            _modPath = modPath;
            _sourcePath = sourcePath;
        }

        public InitializationResult Initialize()
        {
            var result = new InitializationResult();

            try
            {
                LogInitialization();
                result.GameProcess = InitializeGameProcess();
                result.SpriteFileManager = InitializeSpriteFileManager();

                var configPath = DetermineConfigPath();
                result.ConfigurationManager = InitializeConfigurationManager(configPath);
                result.ConfigBasedSpriteManager = InitializeConfigBasedSpriteManager(result.ConfigurationManager);
                result.DynamicSpriteLoader = InitializeDynamicSpriteLoader(result.ConfigurationManager);

                LoadAndApplyConfiguration(result);

                result.Success = true;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"during initialization: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private void LogInitialization()
        {
            var logPath = Path.Combine(Path.GetTempPath(), "FFTColorMod.log");
            File.WriteAllText(logPath, $"[{DateTime.Now}] FFT Color Mod initializing\n");
        }

        private Process InitializeGameProcess()
        {
            var process = Process.GetCurrentProcess();
            ModLogger.LogDebug($"Game base: 0x{process.MainModule?.BaseAddress.ToInt64():X}");
            return process;
        }

        private SpriteFileManager InitializeSpriteFileManager()
        {
            return new SpriteFileManager(_modPath, _sourcePath);
        }

        private string DetermineConfigPath()
        {
            var envConfigPath = Environment.GetEnvironmentVariable("FFT_CONFIG_PATH");
            ModLogger.Log($"FFT_CONFIG_PATH env var: '{envConfigPath}'");

            if (!string.IsNullOrEmpty(envConfigPath))
            {
                ModLogger.Log($"Using config path from env var: {envConfigPath}");
                return envConfigPath;
            }

            // Navigate from Mods/FFT_Color_Mod to User/Mods/ptyra.fft.colormod
            var parent = Directory.GetParent(_modPath);
            if (parent != null)
            {
                var grandParent = Directory.GetParent(parent.FullName);
                if (grandParent != null)
                {
                    var reloadedRoot = grandParent.FullName;
                    var configPath = Path.Combine(reloadedRoot, "User", "Mods", "ptyra.fft.colormod", "Config.json");

                    // Fallback if User config doesn't exist
                    if (!File.Exists(configPath))
                    {
                        ModLogger.Log($"User config not found at: {configPath}");
                        configPath = Path.Combine(_modPath, "Config.json");
                    }

                    return configPath;
                }
            }

            return Path.Combine(_modPath, "Config.json");
        }

        private ConfigurationManager? InitializeConfigurationManager(string configPath)
        {
            ModLogger.Log($"Final config path: '{configPath}'");

            if (!string.IsNullOrEmpty(configPath))
            {
                ModLogger.Log($"Creating ConfigurationManager with path: {configPath}");
                return new ConfigurationManager(configPath);
            }

            ModLogger.LogWarning("Config path is null or empty");
            return null;
        }

        private ConfigBasedSpriteManager? InitializeConfigBasedSpriteManager(ConfigurationManager? configManager)
        {
            if (configManager != null)
            {
                ModLogger.Log("Creating ConfigBasedSpriteManager...");
                return new ConfigBasedSpriteManager(_modPath, configManager, _sourcePath);
            }

            return null;
        }

        private DynamicSpriteLoader? InitializeDynamicSpriteLoader(ConfigurationManager? configManager)
        {
            return new DynamicSpriteLoader(_modPath, configManager);
        }

        private void LoadAndApplyConfiguration(InitializationResult result)
        {
            if (result.ConfigurationManager == null) return;

            var loadedConfig = result.ConfigurationManager.LoadConfig();
            ModLogger.Log($"Loaded config - Knight_Male: {loadedConfig.Knight_Male}");

            LogConfiguredJobColors(loadedConfig);

            // Prepare sprites based on configuration
            ModLogger.Log("Preparing sprites based on configuration...");
            result.DynamicSpriteLoader?.PrepareSpritesForConfig();
            result.ConfigBasedSpriteManager?.ApplyConfiguration();
        }

        private void LogConfiguredJobColors(Config config)
        {
            // Method no longer needed - ColorScheme enum was removed
        }
    }

    public class InitializationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Process? GameProcess { get; set; }
        public SpriteFileManager? SpriteFileManager { get; set; }
        public ConfigurationManager? ConfigurationManager { get; set; }
        public ConfigBasedSpriteManager? ConfigBasedSpriteManager { get; set; }
        public DynamicSpriteLoader? DynamicSpriteLoader { get; set; }
    }
}