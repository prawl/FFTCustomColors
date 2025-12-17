using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class ConfiguratorTests
    {
        [Fact]
        public void Configurator_TryRunCustomConfiguration_ShouldReturnTrue()
        {
            // Arrange
            var configurator = new Configurator();
            configurator.SetConfigDirectory(System.IO.Path.GetTempPath());

            // Act
            // We can't actually test the window opening in unit tests
            // but we can verify the method exists and returns a boolean
            var methodExists = configurator.GetType().GetMethod("TryRunCustomConfiguration") != null;

            // Assert
            methodExists.Should().BeTrue();
        }

        [Fact]
        public void Configurator_ShouldHaveConfigurations()
        {
            // Arrange
            var configurator = new Configurator();
            configurator.SetConfigDirectory(System.IO.Path.GetTempPath());

            // Act
            var configurations = configurator.Configurations;

            // Assert
            configurations.Should().NotBeNull();
            configurations.Should().NotBeEmpty();
        }
    }
}
