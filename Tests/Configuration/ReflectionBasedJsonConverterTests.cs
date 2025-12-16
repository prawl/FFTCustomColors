using System;
using System.Reflection;
using Newtonsoft.Json;
using Xunit;
using FFTColorMod.Configuration;
using System.Linq;

namespace FFTColorMod.Tests
{
    public class ReflectionBasedJsonConverterTests
    {
        [Fact]
        public void ReflectionBasedConverter_Should_Automatically_Serialize_StoryCharacter_Properties()
        {
            // Arrange
            var config = new Config();
            config.Alma = AlmaColorScheme.original;
            config.Agrias = AgriasColorScheme.ash_dark;

            var converter = new ReflectionBasedConfigJsonConverter();
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(converter);

            // Act
            var json = JsonConvert.SerializeObject(config, settings);
            var deserializedConfig = JsonConvert.DeserializeObject<Config>(json, settings);

            // Assert
            Assert.Equal(AlmaColorScheme.original, deserializedConfig.Alma);
            Assert.Equal(AgriasColorScheme.ash_dark, deserializedConfig.Agrias);
        }

        [Fact]
        public void ReflectionBasedConverter_Should_Handle_ALL_StoryCharacters_Without_HardcodedNames()
        {
            // Arrange
            var config = new Config();

            // Set various story characters to non-default values using valid enum values
            config.Agrias = AgriasColorScheme.ash_dark;
            config.Cloud = CloudColorScheme.sephiroth_black;
            config.Alma = AlmaColorScheme.golden_yellow;
            config.Delita = DelitaColorScheme.midnight_black;

            var converter = new ReflectionBasedConfigJsonConverter();
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(converter);

            // Act
            var json = JsonConvert.SerializeObject(config, settings);
            var deserializedConfig = JsonConvert.DeserializeObject<Config>(json, settings);

            // Assert - All story characters should be deserialized correctly
            Assert.Equal(AgriasColorScheme.ash_dark, deserializedConfig.Agrias);
            Assert.Equal(CloudColorScheme.sephiroth_black, deserializedConfig.Cloud);
            Assert.Equal(AlmaColorScheme.golden_yellow, deserializedConfig.Alma);
            Assert.Equal(DelitaColorScheme.midnight_black, deserializedConfig.Delita);
        }

    }
}