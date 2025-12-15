using System;
using System.Reflection;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FFTColorMod.Configuration
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

            var properties = typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance);

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

                // Handle both formats: with underscores (Archer_Female) and without (ArcherFemale)
                var configProperty = properties.FirstOrDefault(p =>
                    p.Name == propertyName ||
                    p.Name.Replace("_", "") == propertyName);

                if (configProperty != null && configProperty.CanWrite)
                {
                    var propertyType = configProperty.PropertyType;
                    if (propertyType.IsEnum && reader.TokenType == JsonTokenType.String)
                    {
                        var value = reader.GetString();
                        try
                        {
                            var enumValue = Enum.Parse(propertyType, value);
                            configProperty.SetValue(config, enumValue);
                        }
                        catch
                        {
                            // If parsing fails, leave as default (original)
                        }
                    }
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Config value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

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
                    writer.WriteStringValue(propertyValue.ToString());
                }
            }

            writer.WriteEndObject();
        }
    }
}