using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Registry;

namespace Tests.Registry
{
    public class GenericCharacterRegistryTests
    {
        [Fact]
        public void GenericCharacterRegistry_Should_Register_All_Generic_Characters()
        {
            // Arrange & Act
            var registry = new GenericCharacterRegistry();

            // Assert - All generic characters should be registered
            registry.GetCharacter("Squire_Male").Should().NotBeNull();
            registry.GetCharacter("Knight_Male").Should().NotBeNull();
            registry.GetCharacter("Monk_Male").Should().NotBeNull();
            registry.GetCharacter("Archer_Male").Should().NotBeNull();
            registry.GetCharacter("Chemist_Female").Should().NotBeNull();
            registry.GetCharacter("Bard_Male").Should().NotBeNull();
            registry.GetCharacter("Dancer_Female").Should().NotBeNull();
            registry.GetCharacter("Mime_Male").Should().NotBeNull();

            // Should have all 38 generic characters (some jobs are gender-specific)
            registry.GetAllCharacters().Should().HaveCount(38);
        }

        [Fact]
        public void GenericCharacterRegistry_Should_Map_Sprite_Names_Correctly()
        {
            // Arrange
            var registry = new GenericCharacterRegistry();

            // Act & Assert - Test various sprite name mappings
            registry.GetCharacterBySpriteName("knight_m").Should().Be("Knight_Male");
            registry.GetCharacterBySpriteName("knight_w").Should().Be("Knight_Female");
            registry.GetCharacterBySpriteName("yumi_m").Should().Be("Archer_Male");
            registry.GetCharacterBySpriteName("yumi_w").Should().Be("Archer_Female");
            registry.GetCharacterBySpriteName("item_m").Should().Be("Chemist_Male");
            registry.GetCharacterBySpriteName("monk_m").Should().Be("Monk_Male");
            registry.GetCharacterBySpriteName("siro_m").Should().Be("WhiteMage_Male");
            registry.GetCharacterBySpriteName("kuro_w").Should().Be("BlackMage_Female");
            registry.GetCharacterBySpriteName("odori_w").Should().Be("Dancer_Female");
            registry.GetCharacterBySpriteName("gin_m").Should().Be("Bard_Male");
        }

        [Fact]
        public void GenericCharacterRegistry_Should_Return_Null_For_Unknown_Sprite()
        {
            // Arrange
            var registry = new GenericCharacterRegistry();

            // Act & Assert
            registry.GetCharacterBySpriteName("unknown_sprite").Should().BeNull();
            registry.GetCharacterBySpriteName("").Should().BeNull();
        }

        [Fact]
        public void GenericCharacterDefinition_Should_Have_Required_Properties()
        {
            // Arrange
            var registry = new GenericCharacterRegistry();

            // Act
            var squireMale = registry.GetCharacter("Squire_Male");

            // Assert
            squireMale.Should().NotBeNull();
            squireMale.Key.Should().Be("Squire_Male");
            squireMale.DisplayName.Should().Be("Squire (Male)");
            squireMale.Category.Should().Be("Generic Characters");
            squireMale.JsonPropertyName.Should().Be("SquireMale");
            squireMale.SpritePatterns.Should().Contain("mina_m");
            squireMale.Description.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void GenericCharacterRegistry_Should_Support_Gender_Specific_Characters()
        {
            // Arrange
            var registry = new GenericCharacterRegistry();

            // Act
            var bard = registry.GetCharacter("Bard_Male");
            var dancer = registry.GetCharacter("Dancer_Female");

            // Assert - Bard is male only
            bard.Should().NotBeNull();
            bard.DisplayName.Should().Be("Bard");
            registry.GetCharacter("Bard_Female").Should().BeNull();

            // Dancer is female only
            dancer.Should().NotBeNull();
            dancer.DisplayName.Should().Be("Dancer");
            registry.GetCharacter("Dancer_Male").Should().BeNull();
        }

        [Theory]
        [InlineData("Knight_Male", "knight_m_pld")]
        [InlineData("Archer_Male", "yumi_m")]
        [InlineData("Chemist_Male", "item_m")]
        [InlineData("WhiteMage_Male", "siro_m")]
        [InlineData("BlackMage_Male", "kuro_m")]
        public void GenericCharacterRegistry_Should_Match_Partial_Sprite_Names(string characterKey, string spritePattern)
        {
            // Arrange
            var registry = new GenericCharacterRegistry();
            var fullSpriteName = $"battle_{spritePattern}_spr.bin";

            // Act
            var character = registry.GetCharacterBySpriteName(fullSpriteName);

            // Assert
            character.Should().Be(characterKey);
        }

        [Fact]
        public void GenericCharacterRegistry_Should_Be_Singleton()
        {
            // Arrange & Act
            var registry1 = GenericCharacterRegistry.Instance;
            var registry2 = GenericCharacterRegistry.Instance;

            // Assert
            registry1.Should().BeSameAs(registry2);
        }

        [Fact]
        public void Config_Should_Use_GenericCharacterRegistry_For_Properties()
        {
            // Arrange
            var config = new Config();
            var registry = GenericCharacterRegistry.Instance;

            // Act - Set value through property
            config.Squire_Male = "corpse_brigade";

            // Assert - Value should be in dictionary
            var squireDefinition = registry.GetCharacter("Squire_Male");
            config.GetJobTheme("Squire_Male").Should().Be("corpse_brigade");
        }
    }
}
