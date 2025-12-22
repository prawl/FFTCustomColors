using Xunit;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class RamzaCycleDebugTest
    {
        [Fact]
        public void Debug_CycleRamzaTheme_Should_Return_Correct_Theme()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"DebugTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var sourcePath = Path.Combine(tempDir, "ColorMod");

                // Setup Data directory with StoryCharacters.json
                var dataPath = Path.Combine(sourcePath, "Data");
                Directory.CreateDirectory(dataPath);

                var storyCharactersJson = @"{
                  ""characters"": [
                    {
                      ""name"": ""Ramza"",
                      ""spriteNames"": [""ramuza"", ""ramuza2"", ""ramuza3""],
                      ""defaultTheme"": ""original"",
                      ""availableThemes"": [""original"", ""white_heretic""]
                    }
                  ]
                }";
                File.WriteAllText(Path.Combine(dataPath, "StoryCharacters.json"), storyCharactersJson);

                // Create StoryCharacterThemeManager directly to test
                var themeManager = new StoryCharacterThemeManager(sourcePath);

                // Act - Get current theme (should be "original")
                var currentTheme = themeManager.GetCurrentTheme("Ramza");

                // Cycle to next theme
                var themes = themeManager.GetAvailableThemes("Ramza");
                var currentIndex = themes.IndexOf(currentTheme);
                var nextIndex = (currentIndex + 1) % themes.Count;
                var nextTheme = themes[nextIndex];

                themeManager.SetCurrentTheme("Ramza", nextTheme);

                // Assert
                currentTheme.Should().Be("original", "Initial theme should be original");
                nextTheme.Should().Be("white_heretic", "Next theme should be white_heretic");
                themeManager.GetCurrentTheme("Ramza").Should().Be("white_heretic", "Theme should be updated");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}