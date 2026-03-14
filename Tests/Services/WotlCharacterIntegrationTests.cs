using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Configuration;
using System;
using System.IO;
using System.Linq;

namespace FFTColorCustomizer.Tests.Services
{
    /// <summary>
    /// Tests for Balthier and Luso (WotL Characters) integration across the codebase.
    /// </summary>
    public class WotlCharacterIntegrationTests : IDisposable
    {
        private readonly string _testModPath;

        public WotlCharacterIntegrationTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), $"WotlCharTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testModPath);

            // Create required directory structure
            var unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var unitPspPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit_psp");
            Directory.CreateDirectory(unitPath);
            Directory.CreateDirectory(unitPspPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }

        // --- CharacterDefinition Tests ---

        [Fact]
        public void CharacterDefinition_Should_Have_IsWotLCharacter_Property()
        {
            var character = new CharacterDefinition
            {
                Name = "Balthier",
                IsWotLCharacter = true
            };

            character.IsWotLCharacter.Should().BeTrue();
        }

        [Fact]
        public void CharacterDefinition_IsWotLCharacter_Should_Default_To_False()
        {
            var character = new CharacterDefinition { Name = "Agrias" };
            character.IsWotLCharacter.Should().BeFalse();
        }

        // --- CharacterDefinitionService Tests ---

        [Fact]
        public void LoadWotLCharactersFromJson_Should_Load_Characters_With_IsWotLCharacter_True()
        {
            var service = new CharacterDefinitionService();
            var jsonPath = Path.Combine(_testModPath, "WotLCharacters.json");
            File.WriteAllText(jsonPath, @"{
                ""characters"": [
                    {
                        ""name"": ""Balthier"",
                        ""spriteNames"": [""spr_dst_bchr_bulechange_m_spr""],
                        ""defaultTheme"": ""original"",
                        ""availableThemes"": [""original""]
                    },
                    {
                        ""name"": ""Luso"",
                        ""spriteNames"": [""spr_dst_bchr_kaito_m_spr""],
                        ""defaultTheme"": ""original"",
                        ""availableThemes"": [""original""]
                    }
                ]
            }");

            service.LoadWotLCharactersFromJson(jsonPath);

            var characters = service.GetAllCharacters();
            characters.Should().HaveCount(2);
            characters.Should().OnlyContain(c => c.IsWotLCharacter);
        }

        [Fact]
        public void LoadWotLCharactersFromJson_Should_Add_To_Existing_Characters()
        {
            var service = new CharacterDefinitionService();

            // Load regular story characters first
            var storyPath = Path.Combine(_testModPath, "StoryCharacters.json");
            File.WriteAllText(storyPath, @"{
                ""characters"": [
                    {
                        ""name"": ""Agrias"",
                        ""spriteNames"": [""aguri""],
                        ""defaultTheme"": ""original"",
                        ""availableThemes"": [""original""]
                    }
                ]
            }");
            service.LoadFromJson(storyPath);

            // Then load WotL characters
            var wotlPath = Path.Combine(_testModPath, "WotLCharacters.json");
            File.WriteAllText(wotlPath, @"{
                ""characters"": [
                    {
                        ""name"": ""Balthier"",
                        ""spriteNames"": [""spr_dst_bchr_bulechange_m_spr""],
                        ""defaultTheme"": ""original"",
                        ""availableThemes"": [""original""]
                    }
                ]
            }");
            service.LoadWotLCharactersFromJson(wotlPath);

            var characters = service.GetAllCharacters();
            characters.Should().HaveCount(2);
            characters.First(c => c.Name == "Agrias").IsWotLCharacter.Should().BeFalse();
            characters.First(c => c.Name == "Balthier").IsWotLCharacter.Should().BeTrue();
        }

        [Fact]
        public void LoadWotLCharactersFromJson_Should_Handle_Missing_File()
        {
            var service = new CharacterDefinitionService();
            service.LoadWotLCharactersFromJson("/nonexistent/path.json");
            service.GetAllCharacters().Should().BeEmpty();
        }

        // --- SpritePathResolver Tests ---

        [Fact]
        public void SpritePathResolver_IsWotLJob_Should_Return_True_For_Balthier()
        {
            var resolver = new SpritePathResolver(_testModPath);
            resolver.IsWotLJob("Balthier").Should().BeTrue();
        }

        [Fact]
        public void SpritePathResolver_IsWotLJob_Should_Return_True_For_Luso()
        {
            var resolver = new SpritePathResolver(_testModPath);
            resolver.IsWotLJob("Luso").Should().BeTrue();
        }

        [Theory]
        [InlineData("Balthier")]
        [InlineData("Luso")]
        [InlineData("DarkKnight_Male")]
        [InlineData("OnionKnight_Female")]
        public void SpritePathResolver_GetUnitPathForJob_Should_Return_UnitPsp_For_WotL(string jobName)
        {
            var resolver = new SpritePathResolver(_testModPath);
            var path = resolver.GetUnitPathForJob(jobName);
            path.Should().Contain("unit_psp");
        }

        [Theory]
        [InlineData("Knight_Male")]
        [InlineData("Agrias")]
        public void SpritePathResolver_GetUnitPathForJob_Should_Not_Return_UnitPsp_For_Regular(string jobName)
        {
            var resolver = new SpritePathResolver(_testModPath);
            var path = resolver.GetUnitPathForJob(jobName);
            path.Should().NotContain("unit_psp");
        }

        [Fact]
        public void SpritePathResolver_GetSpriteNameForJob_Should_Return_Correct_Sprite_For_Balthier()
        {
            var resolver = new SpritePathResolver(_testModPath);
            var spriteName = resolver.GetSpriteNameForJob("Balthier");
            spriteName.Should().Be("spr_dst_bchr_bulechange_m_spr.bin");
        }

        [Fact]
        public void SpritePathResolver_GetSpriteNameForJob_Should_Return_Correct_Sprite_For_Luso()
        {
            var resolver = new SpritePathResolver(_testModPath);
            var spriteName = resolver.GetSpriteNameForJob("Luso");
            spriteName.Should().Be("spr_dst_bchr_kaito_m_spr.bin");
        }

        // --- DynamicSpriteLoader Tests ---

        [Theory]
        [InlineData("Balthier", true)]
        [InlineData("Luso", true)]
        [InlineData("DarkKnight_Male", true)]
        [InlineData("OnionKnight_Female", true)]
        [InlineData("Knight_Male", false)]
        [InlineData("Agrias", false)]
        public void DynamicSpriteLoader_IsWotLJob_Should_Identify_WotL_Characters(string jobProperty, bool expected)
        {
            var configPath = Path.Combine(_testModPath, "Config.json");
            File.WriteAllText(configPath, "{}");
            var configManager = new ConfigurationManagerAdapter(configPath);
            var loader = new DynamicSpriteLoader(_testModPath, configManager);

            loader.IsWotLJob(jobProperty).Should().Be(expected);
        }

        [Theory]
        [InlineData("Balthier", "unit_psp")]
        [InlineData("Luso", "unit_psp")]
        [InlineData("Knight_Male", "unit")]
        public void DynamicSpriteLoader_GetUnitDirectory_Should_Route_Correctly(string jobProperty, string expectedDir)
        {
            var configPath = Path.Combine(_testModPath, "Config.json");
            File.WriteAllText(configPath, "{}");
            var configManager = new ConfigurationManagerAdapter(configPath);
            var loader = new DynamicSpriteLoader(_testModPath, configManager);

            loader.GetUnitDirectory(jobProperty).Should().EndWith(expectedDir);
        }

        // --- SpriteFileManager Tests ---

        [Theory]
        [InlineData("spr_dst_bchr_bulechange_m_spr.bin", "unit_psp")]
        [InlineData("spr_dst_bchr_kaito_m_spr.bin", "unit_psp")]
        [InlineData("spr_dst_bchr_ankoku_m_spr.bin", "unit_psp")]
        [InlineData("battle_knight_m_spr.bin", "unit")]
        public void SpriteFileManager_GetTargetUnitDirectory_Should_Route_WotL_Sprites(string spriteFileName, string expectedDir)
        {
            var manager = new SpriteFileManager(_testModPath);
            var result = manager.GetTargetUnitDirectory(spriteFileName);
            result.Should().EndWith(expectedDir);
        }

        // --- ConfigurationManagerAdapter Tests ---

        [Theory]
        [InlineData("spr_dst_bchr_bulechange_m_spr.bin", "Balthier")]
        [InlineData("spr_dst_bchr_kaito_m_spr.bin", "Luso")]
        [InlineData("spr_dst_bchr_ankoku_m_spr.bin", "DarkKnight_Male")]
        [InlineData("spr_dst_bchr_tama_w_spr.bin", "OnionKnight_Female")]
        public void ConfigurationManagerAdapter_GetJobPropertyForSprite_Should_Map_WotL_Characters(
            string spriteName, string expectedProperty)
        {
            var configPath = Path.Combine(_testModPath, "Config.json");
            File.WriteAllText(configPath, "{}");
            var adapter = new ConfigurationManagerAdapter(configPath);

            var result = adapter.GetJobPropertyForSprite(spriteName);
            result.Should().Be(expectedProperty);
        }

        // --- Config Tests ---

        [Fact]
        public void Config_Should_Include_Balthier_And_Luso_In_StoryCharacters()
        {
            var config = new Config();
            var allCharacters = config.GetAllStoryCharacters().ToList();

            allCharacters.Should().Contain("Balthier");
            allCharacters.Should().Contain("Luso");
        }

        [Fact]
        public void Config_Should_Default_Balthier_And_Luso_To_Original()
        {
            var config = new Config();

            config.GetStoryCharacterTheme("Balthier").Should().Be("original");
            config.GetStoryCharacterTheme("Luso").Should().Be("original");
        }

        [Fact]
        public void Config_Indexer_Should_Access_Balthier_And_Luso()
        {
            var config = new Config();

            config["Balthier"].Should().Be("original");
            config["Luso"].Should().Be("original");

            config["Balthier"] = "custom_theme";
            config["Balthier"].Should().Be("custom_theme");
        }
    }
}
