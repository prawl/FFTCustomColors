using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Dictionary-based System.Text.Json converter that serializes Config using
    /// the underlying job and story character theme dictionaries directly.
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
                            var storyCharacters = config.GetAllStoryCharacters();
                            if (storyCharacters.Contains(propertyName))
                            {
                                config.SetStoryCharacterTheme(propertyName, value);
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
