using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Detection state for the WotL Characters mod.
    /// </summary>
    public enum WotlCharactersState
    {
        /// <summary>Mod folder not found in Mods directory.</summary>
        NotInstalled,
        /// <summary>Mod folder exists but mod is not enabled in Reloaded-II.</summary>
        InstalledButDisabled,
        /// <summary>Mod is installed and enabled in Reloaded-II.</summary>
        InstalledAndEnabled
    }

    /// <summary>
    /// Detects whether the WotL Characters mod is installed and enabled in Reloaded-II.
    /// </summary>
    public class WotlCharactersDetector
    {
        private const string WotlCharactersModId = "ffttic.wotlcharacters";
        private readonly string _modsPath;
        private WotlCharactersState? _state;

        public WotlCharactersDetector(string modsPath)
        {
            _modsPath = modsPath;
        }

        /// <summary>
        /// Gets the current state of the WotL Characters mod.
        /// Result is cached after first check.
        /// </summary>
        public WotlCharactersState State
        {
            get
            {
                if (_state == null)
                    _state = DetectState();
                return _state.Value;
            }
        }

        /// <summary>
        /// Gets whether the WotL Characters mod is installed (regardless of enabled state).
        /// </summary>
        public bool IsWotlCharactersInstalled => State != WotlCharactersState.NotInstalled;

        /// <summary>
        /// Gets whether the WotL Characters mod is installed AND enabled.
        /// </summary>
        public bool IsWotlCharactersEnabled => State == WotlCharactersState.InstalledAndEnabled;

        private WotlCharactersState DetectState()
        {
            try
            {
                if (string.IsNullOrEmpty(_modsPath) || !Directory.Exists(_modsPath))
                    return WotlCharactersState.NotInstalled;

                // Check if mod folder exists
                if (!Directory.GetDirectories(_modsPath, "WotLCharacters*").Any())
                    return WotlCharactersState.NotInstalled;

                // Mod is installed, now check if it's enabled
                var reloadedRoot = Path.GetDirectoryName(_modsPath);
                if (string.IsNullOrEmpty(reloadedRoot))
                    return WotlCharactersState.InstalledButDisabled;

                var appsPath = Path.Combine(reloadedRoot, "Apps");
                if (!Directory.Exists(appsPath))
                    return WotlCharactersState.InstalledButDisabled;

                // Look for FFT app config
                var appConfigPath = FindFftAppConfig(appsPath);
                if (string.IsNullOrEmpty(appConfigPath) || !File.Exists(appConfigPath))
                    return WotlCharactersState.InstalledButDisabled;

                // Read and parse AppConfig.json
                var json = File.ReadAllText(appConfigPath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("EnabledMods", out var enabledMods))
                {
                    foreach (var mod in enabledMods.EnumerateArray())
                    {
                        if (mod.GetString()?.Equals(WotlCharactersModId, StringComparison.OrdinalIgnoreCase) == true)
                            return WotlCharactersState.InstalledAndEnabled;
                    }
                }

                return WotlCharactersState.InstalledButDisabled;
            }
            catch
            {
                return WotlCharactersState.NotInstalled;
            }
        }

        private string FindFftAppConfig(string appsPath)
        {
            try
            {
                var fftAppDirs = Directory.GetDirectories(appsPath, "fft*");
                foreach (var dir in fftAppDirs)
                {
                    var configPath = Path.Combine(dir, "AppConfig.json");
                    if (File.Exists(configPath))
                        return configPath;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
