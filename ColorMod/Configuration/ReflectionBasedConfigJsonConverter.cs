using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Dictionary-based JSON converter that serializes Config using the underlying
    /// job and story character theme dictionaries directly.
    /// </summary>
    public class ReflectionBasedConfigJsonConverter : JsonConverter<Config>
    {
        public override void WriteJson(JsonWriter writer, Config value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            // Write all job themes using their JSON property names
            foreach (var jobKey in value.GetAllJobKeys())
            {
                var metadata = value.GetJobMetadata(jobKey);
                if (metadata != null)
                {
                    var theme = value.GetJobTheme(jobKey);
                    writer.WritePropertyName(metadata.JsonPropertyName);
                    writer.WriteValue(theme);
                }
            }

            // Write all story character themes
            foreach (var characterName in value.GetAllStoryCharacters())
            {
                var theme = value.GetStoryCharacterTheme(characterName);
                writer.WritePropertyName(characterName);
                writer.WriteValue(theme);
            }

            // Write RamzaColors
            writer.WritePropertyName("RamzaColors");
            serializer.Serialize(writer, value.RamzaColors);

            writer.WriteEndObject();
        }

        public override Config ReadJson(JsonReader reader, Type objectType, Config existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var config = existingValue ?? new Config();
            var jo = JObject.Load(reader);

            foreach (var property in jo.Properties())
            {
                var propertyName = property.Name;

                // Handle RamzaColors specially
                if (propertyName == "RamzaColors")
                {
                    try
                    {
                        var ramzaSettings = property.Value.ToObject<RamzaHslSettings>(serializer);
                        if (ramzaSettings != null)
                        {
                            config.RamzaColors = ramzaSettings;
                        }
                    }
                    catch
                    {
                        // If parsing fails, leave as default
                    }
                    continue;
                }

                if (property.Value.Type != JTokenType.String)
                    continue;

                var value = property.Value.ToString();
                if (string.IsNullOrEmpty(value))
                    continue;

                // Try to map to job themes using metadata (JsonPropertyName like "KnightMale")
                bool matched = false;
                foreach (var jobKey in config.GetAllJobKeys())
                {
                    var metadata = config.GetJobMetadata(jobKey);
                    if (metadata != null && metadata.JsonPropertyName == propertyName)
                    {
                        config.SetJobTheme(jobKey, value);
                        matched = true;
                        break;
                    }
                }

                // If not a job theme, try story character
                if (!matched)
                {
                    // Check if it's a known story character
                    var storyCharacters = config.GetAllStoryCharacters();
                    foreach (var character in storyCharacters)
                    {
                        if (character == propertyName)
                        {
                            config.SetStoryCharacterTheme(character, value);
                            break;
                        }
                    }
                }
            }

            return config;
        }
    }
}
