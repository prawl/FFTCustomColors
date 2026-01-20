using Xunit;
using FFTColorCustomizer.Configuration;
using FluentAssertions;

namespace FFTColorCustomizer.Tests
{
    public class RamzaChapterConfigTests
    {
        [Fact]
        public void Config_Should_Have_RamzaChapter1_StoryCharacter()
        {
            // Arrange
            var config = new Config();

            // Act
            var storyCharacters = config.GetAllStoryCharacters();
            var theme = config.GetStoryCharacterTheme("RamzaChapter1");

            // Assert
            storyCharacters.Should().Contain("RamzaChapter1", "Config should have RamzaChapter1 story character");
            theme.Should().NotBeNull("RamzaChapter1 theme should have a default value");
        }

        [Fact]
        public void Config_Should_Have_RamzaChapter23_StoryCharacter()
        {
            // Arrange
            var config = new Config();

            // Act
            var storyCharacters = config.GetAllStoryCharacters();
            var theme = config.GetStoryCharacterTheme("RamzaChapter23");

            // Assert
            storyCharacters.Should().Contain("RamzaChapter23", "Config should have RamzaChapter23 story character");
            theme.Should().NotBeNull("RamzaChapter23 theme should have a default value");
        }

        [Fact]
        public void Config_Should_Have_RamzaChapter4_StoryCharacter()
        {
            // Arrange
            var config = new Config();

            // Act
            var storyCharacters = config.GetAllStoryCharacters();
            var theme = config.GetStoryCharacterTheme("RamzaChapter4");

            // Assert
            storyCharacters.Should().Contain("RamzaChapter4", "Config should have RamzaChapter4 story character");
            theme.Should().NotBeNull("RamzaChapter4 theme should have a default value");
        }

        [Fact]
        public void Config_RamzaChapters_CanBeSetViaIndexer()
        {
            // Arrange
            var config = new Config();

            // Act
            config["RamzaChapter1"] = "dark_knight";
            config["RamzaChapter23"] = "white_heretic";
            config["RamzaChapter4"] = "crimson_blade";

            // Assert
            config["RamzaChapter1"].Should().Be("dark_knight");
            config["RamzaChapter23"].Should().Be("white_heretic");
            config["RamzaChapter4"].Should().Be("crimson_blade");
        }
    }
}
