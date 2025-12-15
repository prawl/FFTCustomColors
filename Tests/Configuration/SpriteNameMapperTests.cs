using Xunit;
using FluentAssertions;
using FFTColorMod.Configuration;

namespace Tests.Configuration
{
    public class SpriteNameMapperTests
    {
        [Theory]
        [InlineData("battle_knight_m_spr.bin", "Knight_Male")]
        [InlineData("battle_knight_w_spr.bin", "Knight_Female")]
        [InlineData("battle_yumi_m_spr.bin", "Archer_Male")]
        [InlineData("battle_item_m_spr.bin", "Chemist_Male")]
        [InlineData("battle_monk_m_spr.bin", "Monk_Male")]
        [InlineData("battle_siro_w_spr.bin", "WhiteMage_Female")]
        [InlineData("battle_kuro_m_spr.bin", "BlackMage_Male")]
        [InlineData("battle_odori_w_spr.bin", "Dancer_Female")]
        [InlineData("battle_gin_m_spr.bin", "Bard_Male")]
        [InlineData("battle_mono_m_spr.bin", "Mime_Male")]
        public void SpriteNameMapper_Should_Map_Generic_Character_Sprites(string spriteName, string expectedCharacterKey)
        {
            // Arrange
            var config = new Config();
            var mapper = new SpriteNameMapper(config);

            // Act
            var characterKey = mapper.GetCharacterKeyForSprite(spriteName);

            // Assert
            characterKey.Should().Be(expectedCharacterKey);
        }

        [Theory]
        [InlineData("battle_aguri_spr.bin", "Agrias")]
        [InlineData("battle_oru_spr.bin", "Orlandeau")]
        [InlineData("battle_cloud_spr.bin", "Cloud")]
        public void SpriteNameMapper_Should_Map_Story_Character_Sprites(string spriteName, string expectedCharacterKey)
        {
            // Arrange
            var config = new Config();
            var mapper = new SpriteNameMapper(config);

            // Act
            var characterKey = mapper.GetCharacterKeyForSprite(spriteName);

            // Assert
            characterKey.Should().Be(expectedCharacterKey);
        }

        [Fact]
        public void SpriteNameMapper_Should_Return_ColorScheme_Description_For_Generic_Characters()
        {
            // Arrange
            var config = new Config();
            config.Knight_Male = ColorScheme.corpse_brigade;
            var mapper = new SpriteNameMapper(config);

            // Act
            var colorDescription = mapper.GetColorForSprite("battle_knight_m_spr.bin");

            // Assert
            colorDescription.Should().Be("sprites_corpse_brigade");
        }

        [Fact]
        public void SpriteNameMapper_Should_Return_ColorScheme_Description_For_Story_Characters()
        {
            // Arrange
            var config = new Config();
            config.Agrias = AgriasColorScheme.ash_dark;

            // Create mapper AFTER setting config
            var mapper = new SpriteNameMapper(config);

            // Act
            var colorDescription = mapper.GetColorForSprite("battle_aguri_spr.bin");

            // Assert
            colorDescription.Should().Be("sprites_agrias_ash_dark");
        }

        [Fact]
        public void SpriteNameMapper_Should_Return_Original_For_Unknown_Sprites()
        {
            // Arrange
            var config = new Config();
            var mapper = new SpriteNameMapper(config);

            // Act
            var colorDescription = mapper.GetColorForSprite("unknown_sprite.bin");

            // Assert
            colorDescription.Should().Be("sprites_original");
        }

        [Theory]
        [InlineData("battle_oru_spr.bin", OrlandeauColorScheme.thunder_god, "sprites_orlandeau_thunder_god")]
        [InlineData("battle_oru_spr.bin", OrlandeauColorScheme.original, "sprites_original")]
        [InlineData("battle_cloud_spr.bin", CloudColorScheme.sephiroth_black, "sprites_cloud_sephiroth_black")]
        [InlineData("battle_cloud_spr.bin", CloudColorScheme.original, "sprites_original")]
        public void SpriteNameMapper_Should_Handle_Special_Story_Character_Formatting<T>(
            string spriteName, T colorScheme, string expectedDescription) where T : System.Enum
        {
            // Arrange
            var config = new Config();
            var mapper = new SpriteNameMapper(config);

            // Set the appropriate property based on sprite name
            if (spriteName.Contains("oru"))
            {
                config.Orlandeau = (OrlandeauColorScheme)(object)colorScheme;
            }
            else if (spriteName.Contains("cloud"))
            {
                config.Cloud = (CloudColorScheme)(object)colorScheme;
            }

            // Act
            var colorDescription = mapper.GetColorForSprite(spriteName);

            // Assert
            colorDescription.Should().Be(expectedDescription);
        }
    }
}