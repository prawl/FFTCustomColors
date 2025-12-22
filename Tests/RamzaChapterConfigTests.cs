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
        public void Config_Should_Have_RamzaChapter2_Property()
        {
            // Arrange
            var config = new Config();

            // Act
            var property = config.GetType().GetProperty("RamzaChapter2");

            // Assert
            property.Should().NotBeNull("Config should have RamzaChapter2 property");
            property.PropertyType.Should().Be(typeof(string), "RamzaChapter2 should be a string property");
        }

        [Fact]
        public void Config_Should_Have_RamzaChapter34_Property()
        {
            // Arrange
            var config = new Config();

            // Act
            var property = config.GetType().GetProperty("RamzaChapter34");

            // Assert
            property.Should().NotBeNull("Config should have RamzaChapter34 property");
            property.PropertyType.Should().Be(typeof(string), "RamzaChapter34 should be a string property");
        }
    }
}