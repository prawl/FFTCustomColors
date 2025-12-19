using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Services;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Tests.Configuration
{
    public class HybridThemeSystemTests
    {
        private readonly string _testJobClassesJson = @"{
            ""sharedThemes"": [
                ""original"",
                ""corpse_brigade"",
                ""lucavi"",
                ""frost_knight"",
                ""blood_moon""
            ],
            ""jobClasses"": [
                {
                    ""name"": ""Knight_Male"",
                    ""displayName"": ""Knight (Male)"",
                    ""spriteName"": ""battle_knight_m_spr.bin"",
                    ""defaultTheme"": ""original"",
                    ""gender"": ""Male"",
                    ""jobType"": ""Knight"",
                    ""jobSpecificThemes"": [
                        ""knight_aguri"",
                        ""knight_cloud"",
                        ""knight_simon""
                    ]
                },
                {
                    ""name"": ""Archer_Male"",
                    ""displayName"": ""Archer (Male)"",
                    ""spriteName"": ""battle_yumi_m_spr.bin"",
                    ""defaultTheme"": ""original"",
                    ""gender"": ""Male"",
                    ""jobType"": ""Archer"",
                    ""jobSpecificThemes"": [
                        ""archer_daisu"",
                        ""archer_oran""
                    ]
                },
                {
                    ""name"": ""Chemist_Male"",
                    ""displayName"": ""Chemist (Male)"",
                    ""spriteName"": ""battle_item_m_spr.bin"",
                    ""defaultTheme"": ""original"",
                    ""gender"": ""Male"",
                    ""jobType"": ""Chemist"",
                    ""jobSpecificThemes"": []
                }
            ]
        }";

        [Fact]
        public void JobClassConfig_Should_Have_SharedThemes_Property()
        {
            // Arrange & Act
            var config = JsonConvert.DeserializeObject<JobClassConfig>(_testJobClassesJson);

            // Assert
            config.Should().NotBeNull();
            config.SharedThemes.Should().NotBeNull();
            config.SharedThemes.Should().HaveCount(5);
            config.SharedThemes.Should().Contain("original");
            config.SharedThemes.Should().Contain("frost_knight");
        }

        [Fact]
        public void JobClass_Should_Have_JobSpecificThemes_Property()
        {
            // Arrange & Act
            var config = JsonConvert.DeserializeObject<JobClassConfig>(_testJobClassesJson);
            var knight = config.JobClasses.First(j => j.JobType == "Knight");

            // Assert
            knight.JobSpecificThemes.Should().NotBeNull();
            knight.JobSpecificThemes.Should().HaveCount(3);
            knight.JobSpecificThemes.Should().Contain("knight_aguri");
            knight.JobSpecificThemes.Should().Contain("knight_cloud");
        }

        [Fact]
        public void GetAllAvailableThemes_Should_Combine_Shared_And_JobSpecific()
        {
            // Arrange
            var config = JsonConvert.DeserializeObject<JobClassConfig>(_testJobClassesJson);
            var knight = config.JobClasses.First(j => j.JobType == "Knight");

            // Act
            var allThemes = knight.GetAllAvailableThemes(config.SharedThemes);

            // Assert
            allThemes.Should().HaveCount(8); // 5 shared + 3 job-specific
            allThemes.Should().Contain("original");
            allThemes.Should().Contain("frost_knight");
            allThemes.Should().Contain("knight_aguri");
        }

        [Fact]
        public void GetAllAvailableThemes_Should_Return_Only_Shared_When_No_JobSpecific()
        {
            // Arrange
            var config = JsonConvert.DeserializeObject<JobClassConfig>(_testJobClassesJson);
            var chemist = config.JobClasses.First(j => j.JobType == "Chemist");

            // Act
            var allThemes = chemist.GetAllAvailableThemes(config.SharedThemes);

            // Assert
            allThemes.Should().HaveCount(5); // Only shared themes
            allThemes.Should().BeEquivalentTo(config.SharedThemes);
        }

        [Fact]
        public void IsThemeAvailableForJob_Should_Return_True_For_Shared_Theme()
        {
            // Arrange
            var config = JsonConvert.DeserializeObject<JobClassConfig>(_testJobClassesJson);
            var knight = config.JobClasses.First(j => j.JobType == "Knight");

            // Act
            var isAvailable = knight.IsThemeAvailable("frost_knight", config.SharedThemes);

            // Assert
            isAvailable.Should().BeTrue();
        }

        [Fact]
        public void IsThemeAvailableForJob_Should_Return_True_For_JobSpecific_Theme()
        {
            // Arrange
            var config = JsonConvert.DeserializeObject<JobClassConfig>(_testJobClassesJson);
            var knight = config.JobClasses.First(j => j.JobType == "Knight");

            // Act
            var isAvailable = knight.IsThemeAvailable("knight_aguri", config.SharedThemes);

            // Assert
            isAvailable.Should().BeTrue();
        }

        [Fact]
        public void IsThemeAvailableForJob_Should_Return_False_For_Other_Job_Theme()
        {
            // Arrange
            var config = JsonConvert.DeserializeObject<JobClassConfig>(_testJobClassesJson);
            var knight = config.JobClasses.First(j => j.JobType == "Knight");

            // Act
            var isAvailable = knight.IsThemeAvailable("archer_daisu", config.SharedThemes);

            // Assert
            isAvailable.Should().BeFalse();
        }

        [Fact]
        public void JobSpecificThemes_Should_Default_To_Empty_List_If_Null()
        {
            // Arrange
            var jsonWithoutJobSpecific = @"{
                ""sharedThemes"": [""original""],
                ""jobClasses"": [{
                    ""name"": ""Test_Job"",
                    ""spriteName"": ""test.bin"",
                    ""jobType"": ""Test""
                }]
            }";

            // Act
            var config = JsonConvert.DeserializeObject<JobClassConfig>(jsonWithoutJobSpecific);
            var testJob = config.JobClasses.First();

            // Assert
            testJob.JobSpecificThemes.Should().NotBeNull();
            testJob.JobSpecificThemes.Should().BeEmpty();
        }

        [Fact]
        public void Backward_Compatibility_AvailableThemes_Should_Map_To_SharedThemes()
        {
            // Arrange - using old format
            var oldFormatJson = @"{
                ""availableThemes"": [
                    ""original"",
                    ""corpse_brigade"",
                    ""lucavi""
                ],
                ""jobClasses"": []
            }";

            // Act
            var config = JsonConvert.DeserializeObject<JobClassConfig>(oldFormatJson);

            // Assert
            config.SharedThemes.Should().NotBeNull();
            config.SharedThemes.Should().HaveCount(3);
            config.SharedThemes.Should().Contain("original");
            config.SharedThemes.Should().Contain("corpse_brigade");
            config.SharedThemes.Should().Contain("lucavi");
        }
    }
}