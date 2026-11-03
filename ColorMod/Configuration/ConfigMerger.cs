using System.ComponentModel;
using System.Linq;
using System.Reflection;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>Merges an incoming config into an existing one through the reflection based merger.</summary>
    public static class ConfigMerger
    {
        public static Config MergeConfigs(Config existingConfig, Config incomingConfig)
        {
            // Use the reflection-based merger
            return ReflectionBasedConfigMerger.MergeConfigs(existingConfig, incomingConfig);
        }
    }
}
