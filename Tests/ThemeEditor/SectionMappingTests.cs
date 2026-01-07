using System;
using System.IO;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    public class SectionMappingTests
    {
        [Fact]
        public void JobSection_CanBeCreated_WithNameIndicesAndRoles()
        {
            // Arrange & Act
            var section = new JobSection(
                name: "Cape",
                displayName: "Cape",
                indices: new[] { 3, 4, 5 },
                roles: new[] { "shadow", "base", "highlight" }
            );

            // Assert
            Assert.Equal("Cape", section.Name);
            Assert.Equal("Cape", section.DisplayName);
            Assert.Equal(new[] { 3, 4, 5 }, section.Indices);
            Assert.Equal(new[] { "shadow", "base", "highlight" }, section.Roles);
        }

        [Fact]
        public void SectionMapping_CanBeCreated_WithJobAndSections()
        {
            // Arrange
            var sections = new[]
            {
                new JobSection("Cape", "Cape", new[] { 3, 4, 5 }, new[] { "shadow", "base", "highlight" }),
                new JobSection("Boots", "Boots", new[] { 6, 7 }, new[] { "base", "highlight" })
            };

            // Act
            var mapping = new SectionMapping(
                job: "Knight_Male",
                sprite: "battle_knight_m_spr.bin",
                sections: sections
            );

            // Assert
            Assert.Equal("Knight_Male", mapping.Job);
            Assert.Equal("battle_knight_m_spr.bin", mapping.Sprite);
            Assert.Equal(2, mapping.Sections.Length);
        }

        [Fact]
        public void SectionMappingLoader_ParseJson_ReturnsValidMapping()
        {
            // Arrange
            var json = @"{
                ""job"": ""Knight_Male"",
                ""sprite"": ""battle_knight_m_spr.bin"",
                ""sections"": [
                    {
                        ""name"": ""Cape"",
                        ""displayName"": ""Cape"",
                        ""indices"": [3, 4, 5],
                        ""roles"": [""shadow"", ""base"", ""highlight""]
                    }
                ]
            }";

            // Act
            var mapping = SectionMappingLoader.ParseJson(json);

            // Assert
            Assert.Equal("Knight_Male", mapping.Job);
            Assert.Equal("battle_knight_m_spr.bin", mapping.Sprite);
            Assert.Single(mapping.Sections);
            Assert.Equal("Cape", mapping.Sections[0].Name);
        }

        [Fact]
        public void SectionMappingLoader_GetAvailableJobs_ReturnsJobsWithMappingFiles()
        {
            // Arrange - create temp directory with test mapping files
            var tempDir = Path.Combine(Path.GetTempPath(), "SectionMappingTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "Squire_Male.json"), "{}");
                File.WriteAllText(Path.Combine(tempDir, "Knight_Female.json"), "{}");

                // Act
                var jobs = SectionMappingLoader.GetAvailableJobs(tempDir);

                // Assert
                Assert.Equal(2, jobs.Length);
                Assert.Contains("Knight_Female", jobs);
                Assert.Contains("Squire_Male", jobs);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SectionMappingLoader_LoadFromFile_ReturnsValidMapping()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                var json = @"{
                    ""job"": ""Squire_Male"",
                    ""sprite"": ""battle_mina_m_spr.bin"",
                    ""sections"": [
                        {
                            ""name"": ""Hood"",
                            ""displayName"": ""Hood"",
                            ""indices"": [8, 9, 10],
                            ""roles"": [""shadow"", ""base"", ""highlight""]
                        }
                    ]
                }";
                File.WriteAllText(tempFile, json);

                // Act
                var mapping = SectionMappingLoader.LoadFromFile(tempFile);

                // Assert
                Assert.Equal("Squire_Male", mapping.Job);
                Assert.Equal("battle_mina_m_spr.bin", mapping.Sprite);
                Assert.Single(mapping.Sections);
                Assert.Equal("Hood", mapping.Sections[0].Name);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void SectionMappingLoader_GetAvailableJobs_SortsInJobClassOrder()
        {
            // Arrange - create temp directory with test mapping files in random order
            var tempDir = Path.Combine(Path.GetTempPath(), "SectionMappingOrderTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                // Create files in alphabetical order (which is NOT the desired order)
                File.WriteAllText(Path.Combine(tempDir, "Archer_Female.json"), "{}");
                File.WriteAllText(Path.Combine(tempDir, "Archer_Male.json"), "{}");
                File.WriteAllText(Path.Combine(tempDir, "Chemist_Female.json"), "{}");
                File.WriteAllText(Path.Combine(tempDir, "Chemist_Male.json"), "{}");
                File.WriteAllText(Path.Combine(tempDir, "Knight_Female.json"), "{}");
                File.WriteAllText(Path.Combine(tempDir, "Knight_Male.json"), "{}");
                File.WriteAllText(Path.Combine(tempDir, "Squire_Female.json"), "{}");
                File.WriteAllText(Path.Combine(tempDir, "Squire_Male.json"), "{}");

                // Act
                var jobs = SectionMappingLoader.GetAvailableJobs(tempDir);

                // Assert - should be sorted in job class order:
                // Squire Male, Squire Female, Chemist Male, Chemist Female,
                // Knight Male, Knight Female, Archer Male, Archer Female
                Assert.Equal(8, jobs.Length);
                Assert.Equal("Squire_Male", jobs[0]);
                Assert.Equal("Squire_Female", jobs[1]);
                Assert.Equal("Chemist_Male", jobs[2]);
                Assert.Equal("Chemist_Female", jobs[3]);
                Assert.Equal("Knight_Male", jobs[4]);
                Assert.Equal("Knight_Female", jobs[5]);
                Assert.Equal("Archer_Male", jobs[6]);
                Assert.Equal("Archer_Female", jobs[7]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SectionMappingLoader_LoadStoryCharacterMapping_ReturnsValidMapping()
        {
            // Arrange - create temp directory with Story subdirectory
            var tempDir = Path.Combine(Path.GetTempPath(), "StoryMappingTest_" + Guid.NewGuid().ToString("N"));
            var storyDir = Path.Combine(tempDir, "Story");
            Directory.CreateDirectory(storyDir);
            try
            {
                var json = @"{
                    ""job"": ""Cloud"",
                    ""sprite"": ""battle_cloud_spr.bin"",
                    ""sections"": [
                        {
                            ""name"": ""Outfit"",
                            ""displayName"": ""Outfit"",
                            ""indices"": [5, 6, 7, 8],
                            ""roles"": [""highlight"", ""base"", ""shadow"", ""outline""]
                        }
                    ]
                }";
                File.WriteAllText(Path.Combine(storyDir, "Cloud.json"), json);

                // Act
                var mapping = SectionMappingLoader.LoadStoryCharacterMapping("Cloud", tempDir);

                // Assert
                Assert.Equal("Cloud", mapping.Job);
                Assert.Equal("battle_cloud_spr.bin", mapping.Sprite);
                Assert.Single(mapping.Sections);
                Assert.Equal("Outfit", mapping.Sections[0].Name);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SectionMappingLoader_GetAvailableStoryCharacters_ReturnsCharacterNames()
        {
            // Arrange - create temp directory with Story subdirectory
            var tempDir = Path.Combine(Path.GetTempPath(), "StoryCharListTest_" + Guid.NewGuid().ToString("N"));
            var storyDir = Path.Combine(tempDir, "Story");
            Directory.CreateDirectory(storyDir);
            try
            {
                File.WriteAllText(Path.Combine(storyDir, "Cloud.json"), "{}");
                File.WriteAllText(Path.Combine(storyDir, "Orlandeau.json"), "{}");
                File.WriteAllText(Path.Combine(storyDir, "Beowulf.json"), "{}");

                // Act
                var characters = SectionMappingLoader.GetAvailableStoryCharacters(tempDir);

                // Assert
                Assert.Equal(3, characters.Length);
                Assert.Contains("Cloud", characters);
                Assert.Contains("Orlandeau", characters);
                Assert.Contains("Beowulf", characters);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Theory]
        [InlineData("Cloud", "battle_cloud_spr.bin")]
        [InlineData("Orlandeau", "battle_oru_spr.bin")]
        [InlineData("Rapha", "battle_h79_spr.bin")]
        [InlineData("Marach", "battle_mara_spr.bin")]
        [InlineData("Beowulf", "battle_beio_spr.bin")]
        [InlineData("Meliadoul", "battle_h80_spr.bin")]
        public void StoryCharacterMapping_ExistsAndLoads_ForEligibleCharacters(string characterName, string expectedSprite)
        {
            // Arrange - use the actual Data/SectionMappings path
            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "SectionMappings");

            // Act
            var mapping = SectionMappingLoader.LoadStoryCharacterMapping(characterName, basePath);

            // Assert
            Assert.Equal(characterName, mapping.Job);
            Assert.Equal(expectedSprite, mapping.Sprite);
            Assert.NotEmpty(mapping.Sections);
        }

        [Theory]
        [InlineData("Cloud")]
        [InlineData("Orlandeau")]
        [InlineData("Rapha")]
        [InlineData("Marach")]
        [InlineData("Beowulf")]
        [InlineData("Meliadoul")]
        public void StoryCharacterMapping_HasValidSectionStructure(string characterName)
        {
            // Arrange
            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "SectionMappings");

            // Act
            var mapping = SectionMappingLoader.LoadStoryCharacterMapping(characterName, basePath);

            // Assert - each section should have valid structure
            foreach (var section in mapping.Sections)
            {
                Assert.False(string.IsNullOrEmpty(section.Name), $"Section name should not be empty for {characterName}");
                Assert.False(string.IsNullOrEmpty(section.DisplayName), $"Section displayName should not be empty for {characterName}");
                Assert.NotEmpty(section.Indices);
                Assert.NotEmpty(section.Roles);
                Assert.Equal(section.Indices.Length, section.Roles.Length);

                // Validate indices are in valid palette range (0-15 for 16-color palettes)
                foreach (var index in section.Indices)
                {
                    Assert.InRange(index, 0, 15);
                }

                // Validate roles are known values
                var validRoles = new[] { "highlight", "base", "shadow", "outline", "accent", "accent_shadow" };
                foreach (var role in section.Roles)
                {
                    Assert.Contains(role, validRoles);
                }
            }
        }
    }
}
