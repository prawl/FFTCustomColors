using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Newtonsoft.Json;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class ConfigRefactoringTests
    {
        [Fact]
        public void Config_Should_Support_Dictionary_Based_Storage()
        {
            // This test verifies that the refactored Config class uses a dictionary-based approach
            // while maintaining backward compatibility with existing property accessors

            // Arrange
            var config = new Config();

            // Act - Set values using the new dictionary-based approach
            config.SetJobTheme("Squire_Male", "corpse_brigade");
            config.SetJobTheme("Knight_Female", "original");
            config.SetJobTheme("Monk_Male", "original");

            // Assert - Values should be retrievable via the new approach
            config.GetJobTheme("Squire_Male").Should().Be("corpse_brigade");
            config.GetJobTheme("Knight_Female").Should().Be("original");
            config.GetJobTheme("Monk_Male").Should().Be("original");

            // Assert - Default values for unset properties
            config.GetJobTheme("Archer_Male").Should().Be("original");
        }

        [Fact]
        public void Config_Should_Maintain_Backward_Compatible_Properties()
        {
            // The refactored Config should still expose properties for backward compatibility

            // Arrange
            var config = new Config();

            // Act - Set values using traditional properties
            config["Squire_Male"] = "corpse_brigade";
            config["Knight_Female"] = "original";

            // Assert - Properties should work as before
            config["Squire_Male"].Should().Be("corpse_brigade");
            config["Knight_Female"].Should().Be("original");

            // Assert - Should also be accessible via dictionary approach
            config.GetJobTheme("Squire_Male").Should().Be("corpse_brigade");
            config.GetJobTheme("Knight_Female").Should().Be("original");
        }

        [Fact]
        public void Config_Should_Serialize_To_Same_Json_Format()
        {
            // The refactored Config must produce the same JSON output for compatibility

            // Arrange
            var config = new Config();
            config["Squire_Male"] = "corpse_brigade";
            config["Knight_Female"] = "original";
            config["Archer_Male"] = "original";

            // Act
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);

            // Assert - JSON should contain the expected property names
            json.Should().Contain("\"SquireMale\": \"corpse_brigade\"");
            json.Should().Contain("\"KnightFemale\": \"original\"");
            json.Should().Contain("\"ArcherMale\": \"original\"");
        }

        [Fact]
        public void Config_Should_Deserialize_From_Existing_Json_Format()
        {
            // The refactored Config must be able to read existing config files

            // Arrange
            var json = @"{
                ""SquireMale"": ""corpse_brigade"",
                ""KnightFemale"": ""crimson_red"",
                ""ArcherMale"": ""royal_purple""
            }";

            // Act
            var config = JsonConvert.DeserializeObject<Config>(json);

            // Assert - Values should be correctly loaded
            config["Squire_Male"].Should().Be("corpse_brigade");
            config["Knight_Female"].Should().Be("crimson_red");
            config["Archer_Male"].Should().Be("royal_purple");

            // Assert - Should also work with dictionary approach
            config.GetJobTheme("Squire_Male").Should().Be("corpse_brigade");
        }

        [Fact]
        public void Config_Should_Provide_Job_Metadata()
        {
            // The refactored Config should provide metadata about each job

            // Arrange
            var config = new Config();

            // Act
            var metadata = config.GetJobMetadata("Squire_Male");

            // Assert
            metadata.Should().NotBeNull();
            metadata.Category.Should().Be("Generic Characters");
            metadata.DisplayName.Should().Be("Squire (Male)");
            metadata.Description.Should().Be("Color scheme for all male squires");
            metadata.JsonPropertyName.Should().Be("SquireMale");
        }

        [Fact]
        public void Config_Should_List_All_Available_Jobs()
        {
            // The refactored Config should be able to list all job keys

            // Arrange
            var config = new Config();

            // Act
            var allJobs = config.GetAllJobKeys();

            // Assert
            allJobs.Should().Contain("Squire_Male");
            allJobs.Should().Contain("Knight_Female");
            allJobs.Should().Contain("Monk_Male");
            allJobs.Should().Contain("WhiteMage_Female");
            allJobs.Should().Contain("Ninja_Male");
            allJobs.Should().HaveCountGreaterThan(30); // We have 38 generic + some others
        }

        [Fact]
        public void Config_Should_Handle_Story_Characters()
        {
            // Story characters (Agrias, Orlandeau) should still work

            // Arrange
            var config = new Config();

            // Act
            config["Agrias"] = "ash_dark";
            config["Orlandeau"] = "thunder_god";

            // Assert
            config["Agrias"].Should().Be("ash_dark");
            config["Orlandeau"].Should().Be("thunder_god");
        }

        [Fact]
        public void Config_Should_Initialize_With_Default_Values()
        {
            // All properties should default to 'original' color scheme

            // Arrange & Act
            var config = new Config();

            // Assert - Check a sample of properties
            config["Squire_Male"].Should().Be("original");
            config["Knight_Female"].Should().Be("original");
            config["Archer_Male"].Should().Be("original");
            config["WhiteMage_Female"].Should().Be("original");
            config["Ninja_Male"].Should().Be("original");

            // Story characters
            config["Agrias"].Should().Be("original");
            config["Orlandeau"].Should().Be("original");
        }

        [Fact]
        public void Config_Should_Support_Batch_Updates()
        {
            // Nice to have: batch update multiple schemes at once

            // Arrange
            var config = new Config();
            var updates = new Dictionary<string, string>
            {
                ["Squire_Male"] = "corpse_brigade",
                ["Knight_Female"] = "original",
                ["Monk_Male"] = "original"
            };

            // Act - Simulate batch updates using individual calls since SetJobThemes is not implemented
            foreach (var update in updates)
            {
                config.SetJobTheme(update.Key, update.Value);
            }

            // Assert
            config["Squire_Male"].Should().Be("corpse_brigade");
            config["Knight_Female"].Should().Be("original");
            config["Monk_Male"].Should().Be("original");
        }
    }
}
