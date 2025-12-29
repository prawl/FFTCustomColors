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
    }
}
