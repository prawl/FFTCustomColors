using System;
using System.Reflection;

namespace FFTColorCustomizer.Configuration
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

            // Handle story characters using reflection (skip indexers)
            var properties = typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Skip indexers, non-enum properties and FilePath
                if (property.GetIndexParameters().Length > 0)
                    continue;
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

            // Handle RamzaColors - preserve existing if incoming is default
            MergeRamzaColors(existingConfig, incomingConfig, mergedConfig);

            return mergedConfig;
        }

        private static void MergeRamzaColors(Config existingConfig, Config incomingConfig, Config mergedConfig)
        {
            MergeChapterSettings(existingConfig.RamzaColors.Chapter1, incomingConfig.RamzaColors.Chapter1, mergedConfig.RamzaColors.Chapter1);
            MergeChapterSettings(existingConfig.RamzaColors.Chapter2, incomingConfig.RamzaColors.Chapter2, mergedConfig.RamzaColors.Chapter2);
            MergeChapterSettings(existingConfig.RamzaColors.Chapter4, incomingConfig.RamzaColors.Chapter4, mergedConfig.RamzaColors.Chapter4);
        }

        private static void MergeChapterSettings(RamzaChapterHslSettings existing, RamzaChapterHslSettings incoming, RamzaChapterHslSettings merged)
        {
            // If incoming has non-default values, use them; otherwise preserve existing
            merged.HueShift = incoming.HueShift != 0 ? incoming.HueShift : existing.HueShift;
            merged.SaturationShift = incoming.SaturationShift != 0 ? incoming.SaturationShift : existing.SaturationShift;
            merged.LightnessShift = incoming.LightnessShift != 0 ? incoming.LightnessShift : existing.LightnessShift;
            merged.Enabled = incoming.Enabled || existing.Enabled;
        }
    }
}
