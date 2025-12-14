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
            var mergedConfig = new Config();

            // Handle generic job color schemes using dictionary approach
            foreach (var jobKey in mergedConfig.GetAllJobKeys())
            {
                var incomingValue = incomingConfig.GetColorScheme(jobKey);
                var existingValue = existingConfig.GetColorScheme(jobKey);

                // If incoming value is different from default (original), it was explicitly changed
                if (incomingValue != ColorScheme.original)
                {
                    mergedConfig.SetColorScheme(jobKey, incomingValue);
                }
                else
                {
                    // Otherwise preserve the existing value
                    mergedConfig.SetColorScheme(jobKey, existingValue);
                }
            }

            // Handle story characters separately (they still have properties)
            // Agrias
            if (incomingConfig.Agrias != AgriasColorScheme.original)
            {
                mergedConfig.Agrias = incomingConfig.Agrias;
            }
            else
            {
                mergedConfig.Agrias = existingConfig.Agrias;
            }

            // Orlandeau
            if (incomingConfig.Orlandeau != OrlandeauColorScheme.original)
            {
                mergedConfig.Orlandeau = incomingConfig.Orlandeau;
            }
            else
            {
                mergedConfig.Orlandeau = existingConfig.Orlandeau;
            }

            return mergedConfig;
        }
    }
}