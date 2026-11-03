using System;
using System.IO;
using System.Diagnostics;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>Initializes the mod's services from the mod and source paths.</summary>
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
                ModLogger.Error(LogVerb.Startup, $"Initialization failed: {ex.Message}");
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
            ModLogger.Debug(LogVerb.Trace, $"Game base: 0x{process.MainModule?.BaseAddress.ToInt64():X}");
            return process;
        }

        private SpriteFileManager InitializeSpriteFileManager()
        {
            return new SpriteFileManager(_modPath, _sourcePath);
        }

        private string DetermineConfigPath()
        {
            var envConfigPath = Environment.GetEnvironmentVariable("FFT_CONFIG_PATH");
            ModLogger.Debug(LogVerb.Config, $"FFT_CONFIG_PATH env var: '{envConfigPath}'");

            if (!string.IsNullOrEmpty(envConfigPath))
            {
                ModLogger.Debug(LogVerb.Config, $"Using config path from env var: {envConfigPath}");
                return envConfigPath;
            }

            // Navigate from Mods/FFTColorCustomizer to User/Mods/paxtrick.fft.colorcustomizer
            var parent = Directory.GetParent(_modPath);
            if (parent != null)
            {
                var grandParent = Directory.GetParent(parent.FullName);
                if (grandParent != null)
                {
                    var reloadedRoot = grandParent.FullName;
                    var configPath = Path.Combine(reloadedRoot, ColorModConstants.UserDirectory, ColorModConstants.ModsDirectory, ColorModConstants.ModNamespace, ColorModConstants.ConfigFileName);

                    // Fallback if User config doesn't exist
                    if (!File.Exists(configPath))
                    {
                        ModLogger.Debug(LogVerb.Config, $"User config not found at: {configPath}");
                        configPath = Path.Combine(_modPath, ColorModConstants.ConfigFileName);
                    }

                    return configPath;
                }
            }

            return Path.Combine(_modPath, ColorModConstants.ConfigFileName);
        }

        private ConfigurationManager? InitializeConfigurationManager(string configPath)
        {
            ModLogger.Debug(LogVerb.Config, $"Final config path: '{configPath}'");

            if (!string.IsNullOrEmpty(configPath))
            {
                ModLogger.Debug(LogVerb.Config, $"Creating ConfigurationManager with path: {configPath}");
                return new ConfigurationManager(configPath);
            }

            ModLogger.Warn(LogVerb.Startup, "Config path is null or empty");
            return null;
        }

        private ConfigBasedSpriteManager? InitializeConfigBasedSpriteManager(ConfigurationManager? configManager)
        {
            if (configManager != null)
            {
                ModLogger.Debug(LogVerb.Sprite, "Creating ConfigBasedSpriteManager...");
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
            ModLogger.Debug(LogVerb.Config, $"Loaded config - Knight_Male: {loadedConfig.Knight_Male}");

            LogConfiguredJobColors(loadedConfig);

            // Prepare sprites based on configuration
            ModLogger.Debug(LogVerb.Sprite, "Preparing sprites based on configuration...");
            result.DynamicSpriteLoader?.PrepareSpritesForConfig();
            result.ConfigBasedSpriteManager?.ApplyConfiguration();
        }

        private void LogConfiguredJobColors(Config config)
        {
            // Method no longer needed, ColorScheme enum was removed
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
