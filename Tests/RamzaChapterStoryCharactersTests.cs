using Xunit;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class RamzaChapterStoryCharactersTests
    {
        [Fact]
        public void StoryCharacters_Should_Have_RamzaChapter1_Entry()
        {
            // Arrange - Create a temp directory with StoryCharacters.json to test
            var tempDir = Path.Combine(Path.GetTempPath(), $"StoryTest_{Guid.NewGuid()}");
            var dataPath = Path.Combine(tempDir, "Data");
            Directory.CreateDirectory(dataPath);

            // Copy the actual StoryCharacters.json from the project
            var sourceFile = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Data\StoryCharacters.json";
            var destFile = Path.Combine(dataPath, "StoryCharacters.json");
            File.Copy(sourceFile, destFile);

            try
            {
                // Act
                var manager = new StoryCharacterThemeManager(tempDir);
                var themes = manager.GetAvailableThemes("RamzaChapter1");

                // Assert
                themes.Should().NotBeNull("RamzaChapter1 should exist in StoryCharacters.json");
                themes.Should().Contain("original", "RamzaChapter1 should have original theme");
                themes.Should().Contain("white_heretic", "RamzaChapter1 should have white_heretic theme");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void StoryCharacters_Should_Have_RamzaChapter2_Entry()
        {
            // Arrange - Create a temp directory with StoryCharacters.json to test
            var tempDir = Path.Combine(Path.GetTempPath(), $"StoryTest_{Guid.NewGuid()}");
            var dataPath = Path.Combine(tempDir, "Data");
            Directory.CreateDirectory(dataPath);

            // Copy the actual StoryCharacters.json from the project
            var sourceFile = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Data\StoryCharacters.json";
            var destFile = Path.Combine(dataPath, "StoryCharacters.json");
            File.Copy(sourceFile, destFile);

            try
            {
                // Act
                var manager = new StoryCharacterThemeManager(tempDir);
                var themes = manager.GetAvailableThemes("RamzaChapter2");

                // Assert
                themes.Should().NotBeNull("RamzaChapter2 should exist in StoryCharacters.json");
                themes.Should().Contain("original", "RamzaChapter2 should have original theme");
                themes.Should().Contain("white_heretic", "RamzaChapter2 should have white_heretic theme");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void StoryCharacters_Should_Have_RamzaChapter34_Entry()
        {
            // Arrange - Create a temp directory with StoryCharacters.json to test
            var tempDir = Path.Combine(Path.GetTempPath(), $"StoryTest_{Guid.NewGuid()}");
            var dataPath = Path.Combine(tempDir, "Data");
            Directory.CreateDirectory(dataPath);

            // Copy the actual StoryCharacters.json from the project
            var sourceFile = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Data\StoryCharacters.json";
            var destFile = Path.Combine(dataPath, "StoryCharacters.json");
            File.Copy(sourceFile, destFile);

            try
            {
                // Act
                var manager = new StoryCharacterThemeManager(tempDir);
                var themes = manager.GetAvailableThemes("RamzaChapter34");

                // Assert
                themes.Should().NotBeNull("RamzaChapter34 should exist in StoryCharacters.json");
                themes.Should().Contain("original", "RamzaChapter34 should have original theme");
                themes.Should().Contain("white_heretic", "RamzaChapter34 should have white_heretic theme");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}