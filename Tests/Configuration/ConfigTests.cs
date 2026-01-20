using Xunit;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
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
        // WotL Jobs - Dark Knight (ankoku)
        [InlineData("spr_dst_bchr_ankoku_m_spr.bin", "DarkKnight_Male")]
        [InlineData("spr_dst_bchr_ankoku_w_spr.bin", "DarkKnight_Female")]
        // WotL Jobs - Onion Knight (tama)
        [InlineData("spr_dst_bchr_tama_m_spr.bin", "OnionKnight_Male")]
        [InlineData("spr_dst_bchr_tama_w_spr.bin", "OnionKnight_Female")]
        public void GetColorForSprite_ShouldMapCorrectly(string spriteName, string expectedProperty)
        {
            // Arrange
            var config = new Config();

            // Set a unique color for each property to verify correct mapping
            var propertyInfo = typeof(Config).GetProperty(expectedProperty);
            Assert.NotNull(propertyInfo); // Verify the property exists
            propertyInfo.SetValue(config, "corpse_brigade"); // Use string enum value

            // Act
            var mapper = new SpriteNameMapper(config);
            var result = mapper.GetColorForSprite(spriteName);

            // Assert
            Assert.Equal("sprites_corpse_brigade", result); // SpriteNameMapper returns sprite path format
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
            var mapper = new SpriteNameMapper(config);
            var result = mapper.GetColorForSprite(spriteName);

            // Assert
            Assert.Equal("sprites_original", result); // SpriteNameMapper returns sprite path format
        }

        [Fact]
        public void Config_ShouldInitializeAllPropertiesWithOriginal()
        {
            // Arrange & Act
            var config = new Config();

            // Assert - verify all job properties are initialized to "original"
            Assert.Equal("original", config.Knight_Male);
            Assert.Equal("original", config.Knight_Female);
            Assert.Equal("original", config.Archer_Male);
            Assert.Equal("original", config.Archer_Female);
            Assert.Equal("original", config.Monk_Male);
            Assert.Equal("original", config.Monk_Female);
            Assert.Equal("original", config.WhiteMage_Male);
            Assert.Equal("original", config.WhiteMage_Female);
            Assert.Equal("original", config.BlackMage_Male);
            Assert.Equal("original", config.BlackMage_Female);
            Assert.Equal("original", config.Thief_Male);
            Assert.Equal("original", config.Thief_Female);
            Assert.Equal("original", config.Ninja_Male);
            Assert.Equal("original", config.Ninja_Female);
            Assert.Equal("original", config.Squire_Male);
            Assert.Equal("original", config.Squire_Female);
            Assert.Equal("original", config.TimeMage_Male);
            Assert.Equal("original", config.TimeMage_Female);
            Assert.Equal("original", config.Summoner_Male);
            Assert.Equal("original", config.Summoner_Female);
            Assert.Equal("original", config.Samurai_Male);
            Assert.Equal("original", config.Samurai_Female);
            Assert.Equal("original", config.Dragoon_Male);
            Assert.Equal("original", config.Dragoon_Female);
            Assert.Equal("original", config.Chemist_Male);
            Assert.Equal("original", config.Chemist_Female);
            Assert.Equal("original", config.Geomancer_Male);
            Assert.Equal("original", config.Geomancer_Female);
            Assert.Equal("original", config.Mystic_Male);
            Assert.Equal("original", config.Mystic_Female);
            Assert.Equal("original", config.Mediator_Male);
            Assert.Equal("original", config.Mediator_Female);
            Assert.Equal("original", config.Dancer_Female);
            Assert.Equal("original", config.Bard_Male);
            Assert.Equal("original", config.Mime_Male);
            Assert.Equal("original", config.Mime_Female);
            Assert.Equal("original", config.Calculator_Male);
            Assert.Equal("original", config.Calculator_Female);

            // WotL Jobs
            Assert.Equal("original", config.DarkKnight_Male);
            Assert.Equal("original", config.DarkKnight_Female);
            Assert.Equal("original", config.OnionKnight_Male);
            Assert.Equal("original", config.OnionKnight_Female);
        }

        [Fact]
        public void Config_ShouldHaveDarkKnightMaleMetadata()
        {
            // Arrange
            var config = new Config();

            // Act
            var metadata = config.GetJobMetadata("DarkKnight_Male");

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal("WotL Jobs", metadata.Category);
            Assert.Equal("Dark Knight (Male)", metadata.DisplayName);
            Assert.Equal("DarkKnightMale", metadata.JsonPropertyName);
        }

        [Fact]
        public void Config_ShouldHaveDarkKnightFemaleMetadata()
        {
            // Arrange
            var config = new Config();

            // Act
            var metadata = config.GetJobMetadata("DarkKnight_Female");

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal("WotL Jobs", metadata.Category);
            Assert.Equal("Dark Knight (Female)", metadata.DisplayName);
            Assert.Equal("DarkKnightFemale", metadata.JsonPropertyName);
        }

        [Fact]
        public void Config_ShouldHaveOnionKnightMaleMetadata()
        {
            // Arrange
            var config = new Config();

            // Act
            var metadata = config.GetJobMetadata("OnionKnight_Male");

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal("WotL Jobs", metadata.Category);
            Assert.Equal("Onion Knight (Male)", metadata.DisplayName);
            Assert.Equal("OnionKnightMale", metadata.JsonPropertyName);
        }

        [Fact]
        public void Config_ShouldHaveOnionKnightFemaleMetadata()
        {
            // Arrange
            var config = new Config();

            // Act
            var metadata = config.GetJobMetadata("OnionKnight_Female");

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal("WotL Jobs", metadata.Category);
            Assert.Equal("Onion Knight (Female)", metadata.DisplayName);
            Assert.Equal("OnionKnightFemale", metadata.JsonPropertyName);
        }

        [Fact]
        public void Config_ShouldHaveDarkKnightMaleProperty()
        {
            // Arrange
            var config = new Config();

            // Assert - property exists and is initialized to original
            Assert.Equal("original", config.DarkKnight_Male);

            // Act - set and get works
            config.DarkKnight_Male = "crimson";
            Assert.Equal("crimson", config.DarkKnight_Male);
        }

        [Fact]
        public void Config_ShouldHaveDarkKnightFemaleProperty()
        {
            // Arrange
            var config = new Config();

            // Assert - property exists and is initialized to original
            Assert.Equal("original", config.DarkKnight_Female);

            // Act - set and get works
            config.DarkKnight_Female = "crimson";
            Assert.Equal("crimson", config.DarkKnight_Female);
        }

        [Fact]
        public void Config_ShouldHaveOnionKnightMaleProperty()
        {
            // Arrange
            var config = new Config();

            // Assert - property exists and is initialized to original
            Assert.Equal("original", config.OnionKnight_Male);

            // Act - set and get works
            config.OnionKnight_Male = "azure";
            Assert.Equal("azure", config.OnionKnight_Male);
        }

        [Fact]
        public void Config_ShouldHaveOnionKnightFemaleProperty()
        {
            // Arrange
            var config = new Config();

            // Assert - property exists and is initialized to original
            Assert.Equal("original", config.OnionKnight_Female);

            // Act - set and get works
            config.OnionKnight_Female = "azure";
            Assert.Equal("azure", config.OnionKnight_Female);
        }

        [Fact]
        public void Config_ShouldHaveRamzaColorsProperty()
        {
            // Arrange & Act
            var config = new Config();

            // Assert
            Assert.NotNull(config.RamzaColors);
            Assert.NotNull(config.RamzaColors.Chapter1);
            Assert.NotNull(config.RamzaColors.Chapter2);
            Assert.NotNull(config.RamzaColors.Chapter4);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        public void Config_GetRamzaChapterSettings_ShouldReturnCorrectChapter(int chapter)
        {
            // Arrange
            var config = new Config();
            config.RamzaColors.Chapter1.HueShift = 10;
            config.RamzaColors.Chapter2.HueShift = 20;
            config.RamzaColors.Chapter4.HueShift = 40;

            // Act
            var settings = config.GetRamzaChapterSettings(chapter);

            // Assert
            Assert.Equal(chapter * 10, settings.HueShift);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        public void Config_SetRamzaChapterSettings_ShouldUpdateCorrectChapter(int chapter)
        {
            // Arrange
            var config = new Config();
            var newSettings = new RamzaChapterHslSettings
            {
                HueShift = 99,
                SaturationShift = 50,
                LightnessShift = -25,
                Enabled = true
            };

            // Act
            config.SetRamzaChapterSettings(chapter, newSettings);

            // Assert
            var result = config.GetRamzaChapterSettings(chapter);
            Assert.Equal(99, result.HueShift);
            Assert.Equal(50, result.SaturationShift);
            Assert.Equal(-25, result.LightnessShift);
            Assert.True(result.Enabled);
        }

        [Fact]
        public void Config_RamzaColors_ShouldSerializeAndDeserialize()
        {
            // Arrange
            var config = new Config();
            config.RamzaColors.Chapter1.HueShift = 45;
            config.RamzaColors.Chapter1.SaturationShift = 20;
            config.RamzaColors.Chapter1.Enabled = true;
            config.RamzaColors.Chapter4.LightnessShift = -30;

            // Act
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(config);
            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(json);

            // Assert
            Assert.NotNull(deserialized.RamzaColors);
            Assert.Equal(45, deserialized.RamzaColors.Chapter1.HueShift);
            Assert.Equal(20, deserialized.RamzaColors.Chapter1.SaturationShift);
            Assert.True(deserialized.RamzaColors.Chapter1.Enabled);
            Assert.Equal(-30, deserialized.RamzaColors.Chapter4.LightnessShift);
        }

        [Fact]
        public void Config_ShouldDeserializeOldConfigWithoutRamzaColors()
        {
            // Arrange - JSON from old config without RamzaColors
            var oldJson = @"{""KnightMale"":""crimson"",""ArcherFemale"":""azure""}";

            // Act
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(oldJson);

            // Assert - RamzaColors should exist with defaults
            Assert.NotNull(config.RamzaColors);
            Assert.Equal(0, config.RamzaColors.Chapter1.HueShift);
            Assert.False(config.RamzaColors.Chapter1.Enabled);
            // And the old values should be preserved
            Assert.Equal("crimson", config.Knight_Male);
            Assert.Equal("azure", config.Archer_Female);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(-1)]
        [InlineData(100)]
        public void Config_GetRamzaChapterSettings_WithInvalidChapter_ShouldReturnChapter1(int invalidChapter)
        {
            // Arrange
            var config = new Config();
            config.RamzaColors.Chapter1.HueShift = 42;

            // Act
            var settings = config.GetRamzaChapterSettings(invalidChapter);

            // Assert - should default to Chapter1
            Assert.Equal(42, settings.HueShift);
        }
    }
}
