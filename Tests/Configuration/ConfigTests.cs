using Xunit;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class ConfigTests
    {
        [Theory]
        // Knights
        [InlineData("battle_knight_m_spr.bin", "Knight_Male")]
        [InlineData("battle_knight_w_spr.bin", "Knight_Female")]
        // Archers (yumi = bow)
        [InlineData("battle_yumi_m_spr.bin", "Archer_Male")]
        [InlineData("battle_yumi_w_spr.bin", "Archer_Female")]
        // Chemists (item)
        [InlineData("battle_item_m_spr.bin", "Chemist_Male")]
        [InlineData("battle_item_w_spr.bin", "Chemist_Female")]
        // Monks
        [InlineData("battle_monk_m_spr.bin", "Monk_Male")]
        [InlineData("battle_monk_w_spr.bin", "Monk_Female")]
        // White Mages (siro)
        [InlineData("battle_siro_m_spr.bin", "WhiteMage_Male")]
        [InlineData("battle_siro_w_spr.bin", "WhiteMage_Female")]
        // Black Mages (kuro)
        [InlineData("battle_kuro_m_spr.bin", "BlackMage_Male")]
        [InlineData("battle_kuro_w_spr.bin", "BlackMage_Female")]
        // Thieves
        [InlineData("battle_thief_m_spr.bin", "Thief_Male")]
        [InlineData("battle_thief_w_spr.bin", "Thief_Female")]
        // Ninjas
        [InlineData("battle_ninja_m_spr.bin", "Ninja_Male")]
        [InlineData("battle_ninja_w_spr.bin", "Ninja_Female")]
        // Squires (mina)
        [InlineData("battle_mina_m_spr.bin", "Squire_Male")]
        [InlineData("battle_mina_w_spr.bin", "Squire_Female")]
        // Time Mages (toki)
        [InlineData("battle_toki_m_spr.bin", "TimeMage_Male")]
        [InlineData("battle_toki_w_spr.bin", "TimeMage_Female")]
        // Summoners (syou)
        [InlineData("battle_syou_m_spr.bin", "Summoner_Male")]
        [InlineData("battle_syou_w_spr.bin", "Summoner_Female")]
        // Samurai (samu)
        [InlineData("battle_samu_m_spr.bin", "Samurai_Male")]
        [InlineData("battle_samu_w_spr.bin", "Samurai_Female")]
        // Dragoons (ryu)
        [InlineData("battle_ryu_m_spr.bin", "Dragoon_Male")]
        [InlineData("battle_ryu_w_spr.bin", "Dragoon_Female")]
        // Geomancers (fusui)
        [InlineData("battle_fusui_m_spr.bin", "Geomancer_Male")]
        [InlineData("battle_fusui_w_spr.bin", "Geomancer_Female")]
        // Oracles/Mystics (onmyo)
        [InlineData("battle_onmyo_m_spr.bin", "Mystic_Male")]
        [InlineData("battle_onmyo_w_spr.bin", "Mystic_Female")]
        // Mediators/Orators (waju)
        [InlineData("battle_waju_m_spr.bin", "Mediator_Male")]
        [InlineData("battle_waju_w_spr.bin", "Mediator_Female")]
        // Dancers (odori - female only)
        [InlineData("battle_odori_w_spr.bin", "Dancer_Female")]
        // Bards (gin - male only)
        [InlineData("battle_gin_m_spr.bin", "Bard_Male")]
        // Mimes (mono)
        [InlineData("battle_mono_m_spr.bin", "Mime_Male")]
        [InlineData("battle_mono_w_spr.bin", "Mime_Female")]
        // Calculators/Arithmeticians (san)
        [InlineData("battle_san_m_spr.bin", "Calculator_Male")]
        [InlineData("battle_san_w_spr.bin", "Calculator_Female")]
        public void GetColorForSprite_ShouldMapCorrectly(string spriteName, string expectedProperty)
        {
            // Arrange
            var config = new Config();

            // Set a unique color for each property to verify correct mapping
            var propertyInfo = typeof(Config).GetProperty(expectedProperty);
            Assert.NotNull(propertyInfo); // Verify the property exists
            propertyInfo.SetValue(config, (Configuration.ColorScheme)1); // Use ColorScheme enum value

            // Act
            var result = config.GetColorForSprite(spriteName);

            // Assert
            Assert.Equal("Corpse Brigade", result); // ColorScheme.ToString() returns Description attribute
        }

        [Theory]
        [InlineData("unknown_sprite.bin")]
        [InlineData("some_other_file.bin")]
        [InlineData("")]
        public void GetColorForSprite_ShouldReturnOriginalForUnknownSprites(string spriteName)
        {
            // Arrange
            var config = new Config();

            // Act
            var result = config.GetColorForSprite(spriteName);

            // Assert
            Assert.Equal("Original", result); // ColorScheme.original.ToString() returns "Original"
        }

        [Fact]
        public void Config_ShouldInitializeAllPropertiesWithOriginal()
        {
            // Arrange & Act
            var config = new Config();

            // Assert - verify all job properties are initialized to ColorScheme.original
            Assert.Equal((Configuration.ColorScheme)0, config.Knight_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Knight_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Archer_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Archer_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Monk_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Monk_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.WhiteMage_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.WhiteMage_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.BlackMage_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.BlackMage_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Thief_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Thief_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Ninja_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Ninja_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Squire_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Squire_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.TimeMage_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.TimeMage_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Summoner_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Summoner_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Samurai_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Samurai_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Dragoon_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Dragoon_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Chemist_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Chemist_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Geomancer_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Geomancer_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Mystic_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Mystic_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Mediator_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Mediator_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Dancer_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Bard_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Mime_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Mime_Female);
            Assert.Equal((Configuration.ColorScheme)0, config.Calculator_Male);
            Assert.Equal((Configuration.ColorScheme)0, config.Calculator_Female);
        }
    }
}