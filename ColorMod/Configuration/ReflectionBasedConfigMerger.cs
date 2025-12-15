using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using FFTColorMod.Configuration;

namespace FFTColorMod.Configuration
{
    public static class ReflectionBasedConfigMerger
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

            // Handle story characters using reflection
            var properties = typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Skip non-enum properties and FilePath
                if (!property.PropertyType.IsEnum || property.Name == "FilePath")
                    continue;

                // Skip properties that are ColorScheme (handled above)
                if (property.PropertyType == typeof(ColorScheme))
                    continue;

                // This is a story character property
                var incomingValue = property.GetValue(incomingConfig);
                var existingValue = property.GetValue(existingConfig);

                if (incomingValue != null && existingValue != null)
                {
                    // Get the "original" value for this enum type
                    var originalValue = Enum.Parse(property.PropertyType, "original");

                    // If incoming value is different from default (original), it was explicitly changed
                    if (!incomingValue.Equals(originalValue))
                    {
                        property.SetValue(mergedConfig, incomingValue);
                    }
                    else
                    {
                        // Otherwise preserve the existing value
                        property.SetValue(mergedConfig, existingValue);
                    }
                }
            }

            return mergedConfig;
        }
    }
}