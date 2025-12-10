using Xunit;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class ConfigTests
    {
        [Theory]
        // Knights
        [InlineData("battle_knight_m_spr.bin", "KnightMale")]
        [InlineData("battle_knight_w_spr.bin", "KnightFemale")]
        // Archers (yumi = bow)
        [InlineData("battle_yumi_m_spr.bin", "ArcherMale")]
        [InlineData("battle_yumi_w_spr.bin", "ArcherFemale")]
        // Chemists (item)
        [InlineData("battle_item_m_spr.bin", "ChemistMale")]
        [InlineData("battle_item_w_spr.bin", "ChemistFemale")]
        // Monks
        [InlineData("battle_monk_m_spr.bin", "MonkMale")]
        [InlineData("battle_monk_w_spr.bin", "MonkFemale")]
        // White Mages (siro)
        [InlineData("battle_siro_m_spr.bin", "WhiteMageMale")]
        [InlineData("battle_siro_w_spr.bin", "WhiteMageFemale")]
        // Black Mages (kuro)
        [InlineData("battle_kuro_m_spr.bin", "BlackMageMale")]
        [InlineData("battle_kuro_w_spr.bin", "BlackMageFemale")]
        // Thieves
        [InlineData("battle_thief_m_spr.bin", "ThiefMale")]
        [InlineData("battle_thief_w_spr.bin", "ThiefFemale")]
        // Ninjas
        [InlineData("battle_ninja_m_spr.bin", "NinjaMale")]
        [InlineData("battle_ninja_w_spr.bin", "NinjaFemale")]
        // Squires (mina)
        [InlineData("battle_mina_m_spr.bin", "SquireMale")]
        [InlineData("battle_mina_w_spr.bin", "SquireFemale")]
        // Time Mages (toki)
        [InlineData("battle_toki_m_spr.bin", "TimeMageMale")]
        [InlineData("battle_toki_w_spr.bin", "TimeMageFemale")]
        // Summoners (syou)
        [InlineData("battle_syou_m_spr.bin", "SummonerMale")]
        [InlineData("battle_syou_w_spr.bin", "SummonerFemale")]
        // Samurai (samu)
        [InlineData("battle_samu_m_spr.bin", "SamuraiMale")]
        [InlineData("battle_samu_w_spr.bin", "SamuraiFemale")]
        // Dragoons (ryu)
        [InlineData("battle_ryu_m_spr.bin", "DragoonMale")]
        [InlineData("battle_ryu_w_spr.bin", "DragoonFemale")]
        // Geomancers (fusui)
        [InlineData("battle_fusui_m_spr.bin", "GeomancerMale")]
        [InlineData("battle_fusui_w_spr.bin", "GeomancerFemale")]
        // Oracles/Mystics (onmyo)
        [InlineData("battle_onmyo_m_spr.bin", "MysticMale")]
        [InlineData("battle_onmyo_w_spr.bin", "MysticFemale")]
        // Mediators/Orators (waju)
        [InlineData("battle_waju_m_spr.bin", "MediatorMale")]
        [InlineData("battle_waju_w_spr.bin", "MediatorFemale")]
        // Dancers (odori - female only)
        [InlineData("battle_odori_w_spr.bin", "DancerFemale")]
        // Bards (gin - male only)
        [InlineData("battle_gin_m_spr.bin", "BardMale")]
        // Mimes (mono)
        [InlineData("battle_mono_m_spr.bin", "MimeMale")]
        [InlineData("battle_mono_w_spr.bin", "MimeFemale")]
        // Calculators/Arithmeticians (san)
        [InlineData("battle_san_m_spr.bin", "CalculatorMale")]
        [InlineData("battle_san_w_spr.bin", "CalculatorFemale")]
        public void GetColorForSprite_ShouldMapCorrectly(string spriteName, string expectedProperty)
        {
            // Arrange
            var config = new Config();

            // Set a unique color for each property to verify correct mapping
            var propertyInfo = typeof(Config).GetProperty(expectedProperty);
            Assert.NotNull(propertyInfo); // Verify the property exists
            propertyInfo.SetValue(config, "test_color");

            // Act
            var result = config.GetColorForSprite(spriteName);

            // Assert
            Assert.Equal("test_color", result);
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
            Assert.Equal("original", result);
        }

        [Fact]
        public void Config_ShouldInitializeAllPropertiesWithOriginal()
        {
            // Arrange & Act
            var config = new Config();

            // Assert - verify all job properties are initialized to "original"
            Assert.Equal("original", config.KnightMale);
            Assert.Equal("original", config.KnightFemale);
            Assert.Equal("original", config.ArcherMale);
            Assert.Equal("original", config.ArcherFemale);
            Assert.Equal("original", config.MonkMale);
            Assert.Equal("original", config.MonkFemale);
            Assert.Equal("original", config.WhiteMageMale);
            Assert.Equal("original", config.WhiteMageFemale);
            Assert.Equal("original", config.BlackMageMale);
            Assert.Equal("original", config.BlackMageFemale);
            Assert.Equal("original", config.ThiefMale);
            Assert.Equal("original", config.ThiefFemale);
            Assert.Equal("original", config.NinjaMale);
            Assert.Equal("original", config.NinjaFemale);
            Assert.Equal("original", config.SquireMale);
            Assert.Equal("original", config.SquireFemale);
            Assert.Equal("original", config.TimeMageMale);
            Assert.Equal("original", config.TimeMageFemale);
            Assert.Equal("original", config.SummonerMale);
            Assert.Equal("original", config.SummonerFemale);
            Assert.Equal("original", config.SamuraiMale);
            Assert.Equal("original", config.SamuraiFemale);
            Assert.Equal("original", config.DragoonMale);
            Assert.Equal("original", config.DragoonFemale);
            Assert.Equal("original", config.ChemistMale);
            Assert.Equal("original", config.ChemistFemale);
            Assert.Equal("original", config.GeomancerMale);
            Assert.Equal("original", config.GeomancerFemale);
            Assert.Equal("original", config.MysticMale);
            Assert.Equal("original", config.MysticFemale);
            Assert.Equal("original", config.MediatorMale);
            Assert.Equal("original", config.MediatorFemale);
            Assert.Equal("original", config.DancerFemale);
            Assert.Equal("original", config.BardMale);
            Assert.Equal("original", config.MimeMale);
            Assert.Equal("original", config.MimeFemale);
            Assert.Equal("original", config.CalculatorMale);
            Assert.Equal("original", config.CalculatorFemale);
        }
    }
}