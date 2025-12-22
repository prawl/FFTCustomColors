using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class ConfigUIDisplayNameTests
    {
        [Fact]
        public void CharacterRowBuilder_Should_Use_Display_Name_For_RamzaChapter1()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"UIDisplayTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var sourcePath = Path.Combine(tempDir, "ColorMod");
                var dataPath = Path.Combine(sourcePath, "Data");
                Directory.CreateDirectory(dataPath);

                var storyCharactersJson = @"{
                  ""characters"": [
                    {
                      ""name"": ""RamzaChapter1"",
                      ""spriteNames"": [""ramuza""],
                      ""defaultTheme"": ""original"",
                      ""availableThemes"": [""original"", ""white_heretic""]
                    }
                  ]
                }";
                File.WriteAllText(Path.Combine(dataPath, "StoryCharacters.json"), storyCharactersJson);

                var manager = new StoryCharacterThemeManager(sourcePath);

                // Act
                var displayName = manager.GetCharacterDisplayName("RamzaChapter1");

                // Assert
                displayName.Should().Be("Ramza (Chapter 1)");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}