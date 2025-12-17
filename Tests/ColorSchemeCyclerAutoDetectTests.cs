using System.Collections.Generic;
using FFTColorCustomizer.Configuration;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer;

namespace Tests
{
    public class ColorSchemeCyclerAutoDetectTests
    {
        [Fact]
        public void GetAvailableSchemes_WhenSpritesDirectoryExists_ShouldReturnAllSpriteDirectories()
        {
            // Arrange
            var tempPath = Path.Combine(Path.GetTempPath(), $"test_sprites_{Path.GetRandomFileName()}");
            Directory.CreateDirectory(tempPath);
            Directory.CreateDirectory(Path.Combine(tempPath, "sprites_original"));
            Directory.CreateDirectory(Path.Combine(tempPath, "sprites_crimson_red"));
            Directory.CreateDirectory(Path.Combine(tempPath, "sprites_golden_sun"));

            try
            {
                var cycler = new ColorSchemeCycler(tempPath);

                // Act
                var schemes = cycler.GetAvailableSchemes();

                // Assert
                schemes.Should().Contain("original");
                schemes.Should().Contain("crimson_red");
                schemes.Should().Contain("golden_sun");
                schemes.Should().HaveCount(3);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        [Fact]
        public void ParameterlessConstructor_ShouldReturnFallbackList()
        {
            // Arrange & Act
            var cycler = new ColorSchemeCycler();
            var schemes = cycler.GetAvailableSchemes();

            // Assert - parameterless constructor should return fallback list
            schemes.Should().NotBeNull();
            schemes.Should().Contain("original");
            schemes.Should().Contain("corpse_brigade");
            schemes.Should().HaveCount(5); // Fallback list has 5 schemes (smoke removed)
        }
    }
}
