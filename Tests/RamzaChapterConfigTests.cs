using Xunit;
using FFTColorCustomizer.Configuration;
using FluentAssertions;
using System.Reflection;

namespace FFTColorCustomizer.Tests
{
    public class RamzaChapterConfigTests
    {
        [Fact]
        public void Config_Should_Have_RamzaChapter1_Property()
        {
            // Arrange
            var config = new Config();

            // Act
            var property = config.GetType().GetProperty("RamzaChapter1");

            // Assert
            property.Should().NotBeNull("Config should have RamzaChapter1 property");
            property.PropertyType.Should().Be(typeof(string), "RamzaChapter1 should be a string property");
        }

        [Fact]
        public void Config_Should_Have_RamzaChapter23_Property()
        {
            // Arrange
            var config = new Config();

            // Act
            var property = config.GetType().GetProperty("RamzaChapter23");

            // Assert
            property.Should().NotBeNull("Config should have RamzaChapter23 property");
            property.PropertyType.Should().Be(typeof(string), "RamzaChapter23 should be a string property");
        }

        [Fact]
        public void Config_Should_Have_RamzaChapter4_Property()
        {
            // Arrange
            var config = new Config();

            // Act
            var property = config.GetType().GetProperty("RamzaChapter4");

            // Assert
            property.Should().NotBeNull("Config should have RamzaChapter4 property");
            property.PropertyType.Should().Be(typeof(string), "RamzaChapter4 should be a string property");
        }
    }
}