using Xunit;
using FFTColorMod.Configuration;
using System.Reflection;

namespace FFTColorMod.Tests.Configuration
{
    public class OrlandeauPropertyTest
    {
        [Fact]
        public void Config_Should_Have_Orlandeau_Property()
        {
            // Arrange
            var config = new Config();

            // Act
            var property = typeof(Config).GetProperty("Orlandeau");

            // Assert
            Assert.NotNull(property);
            Assert.Equal(typeof(string), property.PropertyType);
        }

        [Fact]
        public void Config_Orlandeau_Should_Be_Settable()
        {
            // Arrange
            var config = new Config();

            // Act
            config.Orlandeau = "thunder_god";

            // Assert
            Assert.Equal("thunder_god", config.Orlandeau);

            // Also check via reflection
            var property = typeof(Config).GetProperty("Orlandeau");
            var value = property.GetValue(config);
            Assert.Equal("thunder_god", value);
        }

        [Fact]
        public void SpriteNameMapper_Should_Read_Orlandeau_Property()
        {
            // Arrange
            var config = new Config();
            config.Orlandeau = "thunder_god";

            // Act
            var mapper = new SpriteNameMapper(config);
            var characterKey = mapper.GetCharacterKeyForSprite("battle_oru_spr.bin");

            // Assert
            Assert.Equal("Orlandeau", characterKey);

            // Check the actual color mapping
            var result = mapper.GetColorForSprite("battle_oru_spr.bin");
            Assert.Equal("sprites_orlandeau_thunder_god", result);
        }
    }
}