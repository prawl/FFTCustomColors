using System.ComponentModel;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// Main configuration class for the FFT Color Mod
    /// This is what Reloaded-II will serialize/deserialize
    /// </summary>
    public class ModConfig
    {
        [DisplayName("Job Color Settings")]
        [Description("Configure colors for each job and gender combination")]
        public ReloadedConfig JobColors { get; set; } = new ReloadedConfig();
    }
}