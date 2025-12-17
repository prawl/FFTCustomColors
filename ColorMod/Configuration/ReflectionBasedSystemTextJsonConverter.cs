using System;
using System.Reflection;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Reflection-based System.Text.Json converter that automatically handles all story character properties
    /// without requiring manual updates for each new character
    /// </summary>
    public class ReflectionBasedSystemTextJsonConverter : JsonConverter<Config>
    {
        public override Config Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var config = new Config();

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            // Get non-ignored properties for story characters (they're directly serializable)
            var storyCharacterProperties = typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<Newtonsoft.Json.JsonIgnoreAttribute>() == null &&
                           p.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() == null)
                .ToArray();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return config;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                var propertyName = reader.GetString();
                reader.Read();

                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        // First, try to find a direct story character property
                        var storyCharProperty = storyCharacterProperties.FirstOrDefault(p => p.Name == propertyName);
                        if (storyCharProperty != null)
                        {
                            storyCharProperty.SetValue(config, value);
                            continue;
                        }

                        // Then, try to map to job themes using metadata
                        var jobKeys = config.GetAllJobKeys();
                        foreach (var jobKey in jobKeys)
                        {
                            var metadata = config.GetJobMetadata(jobKey);
                            if (metadata != null && metadata.JsonPropertyName == propertyName)
                            {
                                config.SetJobTheme(jobKey, value);
                                break;
                            }
                        }
                    }
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Config value, JsonSerializerOptions options)
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
                    writer.WriteStringValue(theme);
                }
            }

            // Write all story character themes
            foreach (var characterName in value.GetAllStoryCharacters())
            {
                var theme = value.GetStoryCharacterTheme(characterName);
                writer.WritePropertyName(characterName);
                writer.WriteStringValue(theme);
            }

            writer.WriteEndObject();
        }
    }
}
