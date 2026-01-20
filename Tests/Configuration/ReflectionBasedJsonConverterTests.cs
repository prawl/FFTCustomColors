using System;
using System.Reflection;
using Newtonsoft.Json;
using Xunit;
using FFTColorCustomizer.Configuration;
using System.Linq;

namespace FFTColorCustomizer.Tests
{
    public class ReflectionBasedJsonConverterTests
    {
        [Fact]
        public void ReflectionBasedConverter_Should_Automatically_Serialize_StoryCharacter_Properties()
        {
            // Arrange
            var config = new Config();
            // config.Alma removed in refactoring
            config["Agrias"] = "ash_dark";

            var converter = new ReflectionBasedConfigJsonConverter();
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(converter);

            // Act
            var json = JsonConvert.SerializeObject(config, settings);
            var deserializedConfig = JsonConvert.DeserializeObject<Config>(json, settings);

            // Assert
            Assert.Equal("ash_dark", deserializedConfig["Agrias"]);
        }

        [Fact]
        public void ReflectionBasedConverter_Should_Handle_ALL_StoryCharacters_Without_HardcodedNames()
        {
            // Arrange
            var config = new Config();

            // Set various story characters to non-default values using valid enum values
            config["Agrias"] = "ash_dark";
            config["Cloud"] = "sephiroth_black";
            // config.Alma removed in refactoring
            // config.Delita removed in refactoring

            var converter = new ReflectionBasedConfigJsonConverter();
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(converter);

            // Act
            var json = JsonConvert.SerializeObject(config, settings);
            var deserializedConfig = JsonConvert.DeserializeObject<Config>(json, settings);

            // Assert - All story characters should be deserialized correctly
            Assert.Equal("ash_dark", deserializedConfig["Agrias"]);
            Assert.Equal("sephiroth_black", deserializedConfig["Cloud"]);
        }

    }
}
