using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// Custom JSON converter for the Config class
    /// Handles serialization and deserialization to maintain backward compatibility
    /// </summary>
    public class ConfigJsonConverter : JsonConverter<Config>
    {
        public override void WriteJson(JsonWriter writer, Config value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            // Write all generic character color schemes using their JSON property names
            foreach (var key in value.GetAllJobKeys())
            {
                var metadata = value.GetJobMetadata(key);
                if (metadata != null)
                {
                    writer.WritePropertyName(metadata.JsonPropertyName);
                    writer.WriteValue(value.GetColorScheme(key).ToString());
                }
            }

            // Write story characters
            writer.WritePropertyName("Agrias");
            writer.WriteValue(value.Agrias.ToString());

            writer.WritePropertyName("Orlandeau");
            writer.WriteValue(value.Orlandeau.ToString());

            writer.WriteEndObject();
        }

        public override Config ReadJson(JsonReader reader, Type objectType, Config existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var config = existingValue ?? new Config();
            var jo = JObject.Load(reader);

            // Create a reverse mapping from JSON property names to job keys
            var jsonToJobKey = new Dictionary<string, string>();
            foreach (var key in config.GetAllJobKeys())
            {
                var metadata = config.GetJobMetadata(key);
                if (metadata != null)
                {
                    jsonToJobKey[metadata.JsonPropertyName] = key;
                }
            }

            // Read all properties
            foreach (var property in jo.Properties())
            {
                var propertyName = property.Name;
                var propertyValue = property.Value.ToString();

                if (jsonToJobKey.TryGetValue(propertyName, out var jobKey))
                {
                    // Generic character
                    if (Enum.TryParse<ColorScheme>(propertyValue, out var colorScheme))
                    {
                        config.SetColorScheme(jobKey, colorScheme);
                    }
                }
                else if (propertyName == "Agrias")
                {
                    // Story character - Agrias
                    if (Enum.TryParse<AgriasColorScheme>(propertyValue, out var agriasScheme))
                    {
                        config.Agrias = agriasScheme;
                    }
                }
                else if (propertyName == "Orlandeau")
                {
                    // Story character - Orlandeau
                    if (Enum.TryParse<OrlandeauColorScheme>(propertyValue, out var orlandeauScheme))
                    {
                        config.Orlandeau = orlandeauScheme;
                    }
                }
            }

            return config;
        }
    }
}