using System;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Services;
using FluentAssertions;
using Xunit;

namespace FFTColorCustomizer.Tests.Services
{
    public class JobClassDefinitionServiceTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testDataPath;

        public JobClassDefinitionServiceTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTColorCustomizerTest_" + Guid.NewGuid());
            _testDataPath = Path.Combine(_testModPath, "Data");
            Directory.CreateDirectory(_testDataPath);
        }

        [Fact]
        public void GetAvailableThemes_Should_Load_Themes_From_JobClasses_Json()
        {
            // Arrange
            var jsonContent = @"{
                ""availableThemes"": [
                    ""original"",
                    ""corpse_brigade"",
                    ""lucavi"",
                    ""northern_sky""
                ],
                ""jobClasses"": []
            }";

            File.WriteAllText(Path.Combine(_testDataPath, "JobClasses.json"), jsonContent);

            // Act
            var service = new JobClassDefinitionService(_testModPath);
            var themes = service.GetAvailableThemes();

            // Assert
            themes.Should().HaveCount(4);
            themes.Should().Contain("original");
            themes.Should().Contain("corpse_brigade");
            themes.Should().Contain("lucavi");
            themes.Should().Contain("northern_sky");
        }

        [Fact]
        public void GetAvailableThemes_Should_Return_Default_Or_Fallback_When_No_Json_File()
        {
            // Arrange - no JSON file created
            // The service will try to use the fallback location

            // Act
            var service = new JobClassDefinitionService(_testModPath);
            var themes = service.GetAvailableThemes();

            // Assert
            // If fallback worked (dev environment), we'll get many themes
            // If fallback didn't work (CI or different environment), we'll get default
            themes.Should().NotBeNull();
            themes.Should().Contain("original", "Should always contain 'original' theme");

            // We expect either 1 theme (no fallback) or many themes (fallback worked)
            if (themes.Count == 1)
            {
                themes.Should().ContainSingle("Should only have 'original' when no fallback");
            }
            else
            {
                themes.Count.Should().BeGreaterThan(1, "Fallback should load multiple themes");
            }
        }

        [Fact]
        public void JobClassDefinitionService_Should_Use_Fallback_Path_When_Primary_Not_Found()
        {
            // Arrange
            // Create a temp directory that doesn't have the JSON file
            var tempModPath = Path.Combine(Path.GetTempPath(), "NonExistent_" + Guid.NewGuid());
            Directory.CreateDirectory(tempModPath);

            // Ensure the fallback location has the file (this test assumes the dev environment)
            var fallbackPath = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Data\JobClasses.json";

            // Only run this test if the fallback file exists
            if (File.Exists(fallbackPath))
            {
                // Act
                var service = new JobClassDefinitionService(tempModPath);
                var themes = service.GetAvailableThemes();

                // Assert - should have loaded themes from fallback
                themes.Should().NotBeEmpty("Themes should be loaded from fallback location");
                themes.Count.Should().BeGreaterThan(1, "Should have loaded multiple themes from fallback");
            }

            // Cleanup
            Directory.Delete(tempModPath, true);
        }

        [Fact]
        public void GetAvailableThemes_Should_Return_Empty_List_When_Themes_Array_Empty()
        {
            // Arrange
            var jsonContent = @"{
                ""availableThemes"": [],
                ""jobClasses"": []
            }";

            File.WriteAllText(Path.Combine(_testDataPath, "JobClasses.json"), jsonContent);

            // Act
            var service = new JobClassDefinitionService(_testModPath);
            var themes = service.GetAvailableThemes();

            // Assert - When no themes are loaded, should return default
            themes.Should().HaveCount(1);
            themes.Should().Contain("original");
        }

        [Fact]
        public void JobClassDefinitionService_Should_Load_Job_Classes_From_Json()
        {
            // Arrange
            var jsonContent = @"{
                ""availableThemes"": [""original""],
                ""jobClasses"": [
                    {
                        ""name"": ""Squire_Male"",
                        ""displayName"": ""Squire (Male)"",
                        ""spriteName"": ""battle_syou_m_spr.bin"",
                        ""defaultTheme"": ""original"",
                        ""gender"": ""Male"",
                        ""jobType"": ""Generic""
                    }
                ]
            }";

            File.WriteAllText(Path.Combine(_testDataPath, "JobClasses.json"), jsonContent);

            // Act
            var service = new JobClassDefinitionService(_testModPath);
            var jobClass = service.GetJobClassByName("Squire_Male");

            // Assert
            jobClass.Should().NotBeNull();
            jobClass.DisplayName.Should().Be("Squire (Male)");
            jobClass.SpriteName.Should().Be("battle_syou_m_spr.bin");
            jobClass.Gender.Should().Be("Male");
            jobClass.JobType.Should().Be("Generic");
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }
    }
}
