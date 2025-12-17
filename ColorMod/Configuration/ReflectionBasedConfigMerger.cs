using System;
using System.Reflection;

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
                var incomingValue = incomingConfig.GetJobTheme(jobKey);
                var existingValue = existingConfig.GetJobTheme(jobKey);

                // If incoming value is different from default (original), it was explicitly changed
                if (incomingValue != "original")
                {
                    mergedConfig.SetJobTheme(jobKey, incomingValue);
                }
                else
                {
                    // Otherwise preserve the existing value
                    mergedConfig.SetJobTheme(jobKey, existingValue);
                }
            }

            // Handle story characters using reflection
            var properties = typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Skip non-enum properties and FilePath
                if (!property.PropertyType.IsEnum || property.Name == "FilePath")
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