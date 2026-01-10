using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Detection state for the GenericJobs mod.
    /// </summary>
    public enum GenericJobsState
    {
        /// <summary>Mod folder not found in Mods directory.</summary>
        NotInstalled,
        /// <summary>Mod folder exists but mod is not enabled in Reloaded-II.</summary>
        InstalledButDisabled,
        /// <summary>Mod is installed and enabled in Reloaded-II.</summary>
        InstalledAndEnabled
    }

    /// <summary>
    /// Detects whether the GenericJobs mod is installed and enabled in Reloaded-II.
    /// </summary>
    public class GenericJobsDetector
    {
        private const string GenericJobsModId = "ffttic.jobs.genericjobs";
        private readonly string _modsPath;
        private GenericJobsState? _state;

        public GenericJobsDetector(string modsPath)
        {
            _modsPath = modsPath;
        }

        /// <summary>
        /// Gets the current state of the GenericJobs mod.
        /// Result is cached after first check.
        /// </summary>
        public GenericJobsState State
        {
            get
            {
                if (_state == null)
                    _state = DetectGenericJobsState();
                return _state.Value;
            }
        }

        /// <summary>
        /// Gets whether the GenericJobs mod is installed (regardless of enabled state).
        /// For backwards compatibility.
        /// </summary>
        public bool IsGenericJobsInstalled => State != GenericJobsState.NotInstalled;

        /// <summary>
        /// Gets whether the GenericJobs mod is installed AND enabled.
        /// </summary>
        public bool IsGenericJobsEnabled => State == GenericJobsState.InstalledAndEnabled;

        private GenericJobsState DetectGenericJobsState()
        {
            try
            {
                if (string.IsNullOrEmpty(_modsPath) || !Directory.Exists(_modsPath))
                    return GenericJobsState.NotInstalled;

                // Check if mod folder exists
                if (!Directory.GetDirectories(_modsPath, "GenericJobs*").Any())
                    return GenericJobsState.NotInstalled;

                // Mod is installed, now check if it's enabled
                // The Apps folder should be at the same level as Mods folder
                var reloadedRoot = Path.GetDirectoryName(_modsPath);
                if (string.IsNullOrEmpty(reloadedRoot))
                    return GenericJobsState.InstalledButDisabled;

                var appsPath = Path.Combine(reloadedRoot, "Apps");
                if (!Directory.Exists(appsPath))
                    return GenericJobsState.InstalledButDisabled;

                // Look for FFT app config
                var appConfigPath = FindFftAppConfig(appsPath);
                if (string.IsNullOrEmpty(appConfigPath) || !File.Exists(appConfigPath))
                    return GenericJobsState.InstalledButDisabled;

                // Read and parse AppConfig.json
                var json = File.ReadAllText(appConfigPath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("EnabledMods", out var enabledMods))
                {
                    foreach (var mod in enabledMods.EnumerateArray())
                    {
                        if (mod.GetString()?.Equals(GenericJobsModId, StringComparison.OrdinalIgnoreCase) == true)
                            return GenericJobsState.InstalledAndEnabled;
                    }
                }

                return GenericJobsState.InstalledButDisabled;
            }
            catch
            {
                // If we can't determine the state, assume not installed
                return GenericJobsState.NotInstalled;
            }
        }

        private string FindFftAppConfig(string appsPath)
        {
            try
            {
                // Look for fft_enhanced.exe folder
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
