using System;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ColorMod.Registry;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Reflection-based JSON converter that automatically handles all story character properties
    /// without requiring manual updates for each new character
    /// </summary>
    public class ReflectionBasedConfigJsonConverter : JsonConverter<Config>
    {
        public override void WriteJson(JsonWriter writer, Config value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            // Use reflection to find all properties
            var properties = typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Skip FilePath and other non-serializable properties
                if (property.Name == "FilePath")
                    continue;

                var propertyValue = property.GetValue(value);
                if (propertyValue != null)
                {
                    // Write property name without underscores for compatibility
                    var jsonPropertyName = property.Name.Replace("_", "");
                    writer.WritePropertyName(jsonPropertyName);
                    writer.WriteValue(propertyValue.ToString());
                }
            }

            writer.WriteEndObject();
        }

        public override Config ReadJson(JsonReader reader, Type objectType, Config existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var config = existingValue ?? new Config();
            var jo = JObject.Load(reader);

            var properties = typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in jo.Properties())
            {
                // Handle both formats: with underscores (Archer_Female) and without (ArcherFemale)
                var configProperty = properties.FirstOrDefault(p =>
                    p.Name == property.Name ||
                    p.Name.Replace("_", "") == property.Name);

                if (configProperty != null && configProperty.CanWrite)
                {
                    var propertyType = configProperty.PropertyType;
                    if (propertyType == typeof(string))
                    {
                        try
                        {
                            var stringValue = property.Value.ToString();
                            configProperty.SetValue(config, stringValue);
                        }
                        catch
                        {
                            // If parsing fails, leave as default (original)
                        }
                    }
                    else if (propertyType.IsEnum)
                    {
                        try
                        {
                            var enumValue = Enum.Parse(propertyType, property.Value.ToString());
                            configProperty.SetValue(config, enumValue);
                        }
                        catch
                        {
                            // If parsing fails, leave as default (original)
                        }
                    }
                }
            }

            return config;
        }
    }
}
