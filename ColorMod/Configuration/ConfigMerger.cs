using System.ComponentModel;
using System.Linq;
using System.Reflection;
using FFTColorMod.Configuration;

namespace FFTColorMod.Configuration
{
    public static class ConfigMerger
    {
        public static Config MergeConfigs(Config existingConfig, Config incomingConfig)
        {
            // Use the reflection-based merger
            return ReflectionBasedConfigMerger.MergeConfigs(existingConfig, incomingConfig);
        }
    }
}