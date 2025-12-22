using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class ConfigDisplayNameTests
    {
        [Fact]
        public void Config_Should_Have_Display_Name_For_RamzaChapter1()
        {
            // Arrange
            var config = new Config();

            // Act
            var displayName = config.GetDisplayName("RamzaChapter1");

            // Assert
            displayName.Should().Be("Ramza (Chapter 1)");
        }

        [Fact]
        public void Config_Should_Have_Display_Name_For_RamzaChapter2()
        {
            // Arrange
            var config = new Config();

            // Act
            var displayName = config.GetDisplayName("RamzaChapter2");

            // Assert
            displayName.Should().Be("Ramza (Chapter 2)");
        }

        [Fact]
        public void Config_Should_Have_Display_Name_For_RamzaChapter34()
        {
            // Arrange
            var config = new Config();

            // Act
            var displayName = config.GetDisplayName("RamzaChapter34");

            // Assert
            displayName.Should().Be("Ramza (Chapter 3 & 4)");
        }

        [Fact]
        public void StoryCharacterThemeManager_Should_Provide_Display_Name()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"DisplayNameTest_{Guid.NewGuid()}");
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