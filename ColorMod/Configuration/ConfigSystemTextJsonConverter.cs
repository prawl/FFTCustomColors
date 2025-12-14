using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// System.Text.Json converter for the Config class
    /// Handles serialization and deserialization to maintain backward compatibility
    /// </summary>
    public class ConfigSystemTextJsonConverter : JsonConverter<Config>
    {
        public override Config Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var config = new Config();

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

            // Read the JSON object
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected PropertyName token");
                }

                string propertyName = reader.GetString();
                reader.Read(); // Move to the value

                string propertyValue = reader.GetString();

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

        public override void Write(Utf8JsonWriter writer, Config value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // Write all generic character color schemes using their JSON property names
            foreach (var key in value.GetAllJobKeys())
            {
                var metadata = value.GetJobMetadata(key);
                if (metadata != null)
                {
                    writer.WriteString(metadata.JsonPropertyName, value.GetColorScheme(key).ToString());
                }
            }

            // Write story characters
            writer.WriteString("Agrias", value.Agrias.ToString());
            writer.WriteString("Orlandeau", value.Orlandeau.ToString());

            writer.WriteEndObject();
        }
    }
}