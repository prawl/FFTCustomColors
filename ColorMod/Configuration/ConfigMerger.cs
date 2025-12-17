using System.ComponentModel;
using System.Linq;
using System.Reflection;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Configuration
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
