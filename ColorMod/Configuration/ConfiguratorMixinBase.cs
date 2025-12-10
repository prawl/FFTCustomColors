using System.IO;
using Reloaded.Mod.Interfaces;

namespace FFTColorMod.Configuration
{
    public class ConfiguratorMixinBase
    {
        /// <summary>
        /// Creates a set of configurations from a given directory.
        /// </summary>
        public virtual IUpdatableConfigurable[] MakeConfigurations(string configFolder)
        {
            var configPath = Path.Combine(configFolder, "Config.json");
            var config = Configurable<Config>.FromFile(configPath, "FFT Color Mod Configuration");
            return new IUpdatableConfigurable[] { config };
        }
    }

    /// <summary>
    /// Allows you to override certain aspects of the configuration creation process.
    /// </summary>
    public class ConfiguratorMixin : ConfiguratorMixinBase
    {
        // Override MakeConfigurations if you need multiple configs
    }
}