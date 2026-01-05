using Xunit;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.IO;
using System;
using Xunit.Abstractions;

namespace FFTColorCustomizer.Tests
{
    public class RamzaThemeDebugTest
    {
        private readonly ITestOutputHelper _output;

        public RamzaThemeDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Debug_What_Theme_Is_Being_Applied()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"DebugTheme_{Guid.NewGuid()}");
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
                      ""name"": ""RamzaChapter4"",
                      ""spriteNames"": [""ramuza3""],
                      ""defaultTheme"": ""original"",
                      ""availableThemes"": [""original"", ""white_heretic""]
                    }
                  ]
                }";
                File.WriteAllText(Path.Combine(dataPath, "StoryCharacters.json"), storyCharactersJson);

                // Setup white_heretic theme tex files in the location where TexFileManager expects them
                var themePath = Path.Combine(tempDir, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/themes/white_heretic");
                Directory.CreateDirectory(themePath);
                File.WriteAllText(Path.Combine(themePath, "tex_834.bin"), "white_heretic_data");
                File.WriteAllText(Path.Combine(themePath, "tex_835.bin"), "white_heretic_palette");

                // Create managers
                var storyManager = new StoryCharacterThemeManager(sourcePath);

                // Get initial theme
                var initialTheme = storyManager.GetCurrentTheme("RamzaChapter4");
                _output.WriteLine($"Initial theme: {initialTheme}");

                // Get available themes and cycle manually
                var themes = storyManager.GetAvailableThemes("RamzaChapter4");
                var currentIndex = themes.IndexOf(initialTheme);
                var nextIndex = (currentIndex + 1) % themes.Count;
                var nextTheme = themes[nextIndex];
                _output.WriteLine($"Next theme calculated: {nextTheme}");

                // Update story manager
                storyManager.SetCurrentTheme("RamzaChapter4", nextTheme);
                _output.WriteLine($"Current theme after set: {storyManager.GetCurrentTheme("RamzaChapter4")}");

                // Now apply the theme using TexFileManager
                var texFileManager = new TexFileManager();
                _output.WriteLine($"Applying theme '{nextTheme}' with modPath: {tempDir}");

                // Check source files exist
                var sourceFile834 = Path.Combine(tempDir, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/themes", nextTheme, "tex_834.bin");
                _output.WriteLine($"Source tex_834.bin exists: {File.Exists(sourceFile834)} at {sourceFile834}");

                texFileManager.CopyTexFilesForTheme("RamzaChapter4", nextTheme, tempDir);

                // Check if files were copied
                var destPath = Path.Combine(tempDir, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d");
                var destFile834 = Path.Combine(destPath, "tex_834.bin");
                _output.WriteLine($"Destination tex_834.bin exists: {File.Exists(destFile834)} at {destFile834}");

                // Assert
                File.Exists(destFile834).Should().BeTrue("tex_834.bin should be copied");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}