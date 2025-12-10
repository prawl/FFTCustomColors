using System;
using System.IO;
using Newtonsoft.Json;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// Manages loading and saving of Reloaded-II configuration
    /// </summary>
    public static class ReloadedConfigManager
    {
        public static ModConfig LoadModConfig(string configPath)
        {
            if (!File.Exists(configPath))
            {
                return new ModConfig();
            }

            try
            {
                var json = File.ReadAllText(configPath);
                return JsonConvert.DeserializeObject<ModConfig>(json) ?? new ModConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FFT Color Mod] Error loading Reloaded config: {ex.Message}");
                return new ModConfig();
            }
        }

        public static void SaveModConfig(string configPath, ModConfig config)
        {
            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FFT Color Mod] Error saving Reloaded config: {ex.Message}");
            }
        }
    }
}