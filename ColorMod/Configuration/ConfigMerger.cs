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

            // Get all properties that are ColorScheme enums (including variants)
            var properties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(ColorScheme) ||
                           p.PropertyType == typeof(AgriasColorScheme) ||
                           p.PropertyType == typeof(OrlandeauColorScheme));

            foreach (var prop in properties)
            {
                // Get the default value from the DefaultValue attribute
                var defaultValueAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
                var defaultValue = defaultValueAttr?.Value;

                var incomingValue = prop.GetValue(incomingConfig);
                var existingValue = prop.GetValue(existingConfig);

                // If incoming value is different from default, it was explicitly changed
                if (incomingValue != null && !incomingValue.Equals(defaultValue))
                {
                    prop.SetValue(mergedConfig, incomingValue);
                }
                else
                {
                    // Otherwise preserve the existing value
                    prop.SetValue(mergedConfig, existingValue);
                }
            }

            return mergedConfig;
        }
    }
}