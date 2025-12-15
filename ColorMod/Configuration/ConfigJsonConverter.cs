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

            writer.WritePropertyName("Cloud");
            writer.WriteValue(value.Cloud.ToString());

            writer.WritePropertyName("Mustadio");
            writer.WriteValue(value.Mustadio.ToString());

            writer.WritePropertyName("Reis");
            writer.WriteValue(value.Reis.ToString());

            writer.WritePropertyName("Malak");
            writer.WriteValue(value.Malak.ToString());

            writer.WritePropertyName("Rafa");
            writer.WriteValue(value.Rafa.ToString());

            writer.WritePropertyName("Delita");
            writer.WriteValue(value.Delita.ToString());

            writer.WritePropertyName("Alma");
            writer.WriteValue(value.Alma.ToString());

            writer.WritePropertyName("Wiegraf");
            writer.WriteValue(value.Wiegraf.ToString());

            writer.WritePropertyName("Celia");
            writer.WriteValue(value.Celia.ToString());

            writer.WritePropertyName("Lettie");
            writer.WriteValue(value.Lettie.ToString());

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
    }
}