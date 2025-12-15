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
                else if (propertyName == "Cloud")
                {
                    // Story character - Cloud
                    if (Enum.TryParse<CloudColorScheme>(propertyValue, out var cloudScheme))
                    {
                        config.Cloud = cloudScheme;
                    }
                }
                else if (propertyName == "Mustadio")
                {
                    // Story character - Mustadio
                    if (Enum.TryParse<MustadioColorScheme>(propertyValue, out var mustadioScheme))
                    {
                        config.Mustadio = mustadioScheme;
                    }
                }
                else if (propertyName == "Reis")
                {
                    // Story character - Reis
                    if (Enum.TryParse<ReisColorScheme>(propertyValue, out var reisScheme))
                    {
                        config.Reis = reisScheme;
                    }
                }
                else if (propertyName == "Malak")
                {
                    // Story character - Malak
                    if (Enum.TryParse<MalakColorScheme>(propertyValue, out var malakScheme))
                    {
                        config.Malak = malakScheme;
                    }
                }
                else if (propertyName == "Rafa")
                {
                    // Story character - Rafa
                    if (Enum.TryParse<RafaColorScheme>(propertyValue, out var rafaScheme))
                    {
                        config.Rafa = rafaScheme;
                    }
                }
                else if (propertyName == "Delita")
                {
                    // Story character - Delita
                    if (Enum.TryParse<DelitaColorScheme>(propertyValue, out var delitaScheme))
                    {
                        config.Delita = delitaScheme;
                    }
                }
                else if (propertyName == "Alma")
                {
                    // Story character - Alma
                    if (Enum.TryParse<AlmaColorScheme>(propertyValue, out var almaScheme))
                    {
                        config.Alma = almaScheme;
                    }
                }
                else if (propertyName == "Wiegraf")
                {
                    // Story character - Wiegraf
                    if (Enum.TryParse<WiegrafColorScheme>(propertyValue, out var wiegrafScheme))
                    {
                        config.Wiegraf = wiegrafScheme;
                    }
                }
                else if (propertyName == "Celia")
                {
                    // Story character - Celia
                    if (Enum.TryParse<CeliaColorScheme>(propertyValue, out var celiaScheme))
                    {
                        config.Celia = celiaScheme;
                    }
                }
                else if (propertyName == "Lettie")
                {
                    // Story character - Lettie
                    if (Enum.TryParse<LettieColorScheme>(propertyValue, out var lettieScheme))
                    {
                        config.Lettie = lettieScheme;
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
            writer.WriteString("Cloud", value.Cloud.ToString());
            writer.WriteString("Mustadio", value.Mustadio.ToString());
            writer.WriteString("Reis", value.Reis.ToString());
            writer.WriteString("Malak", value.Malak.ToString());
            writer.WriteString("Rafa", value.Rafa.ToString());
            writer.WriteString("Delita", value.Delita.ToString());
            writer.WriteString("Alma", value.Alma.ToString());
            writer.WriteString("Wiegraf", value.Wiegraf.ToString());
            writer.WriteString("Celia", value.Celia.ToString());
            writer.WriteString("Lettie", value.Lettie.ToString());

            writer.WriteEndObject();
        }
    }
}