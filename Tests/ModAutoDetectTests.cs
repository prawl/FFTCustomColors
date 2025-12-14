using System.IO;
using FFTColorMod.Configuration;
using Xunit;
using FluentAssertions;
using FFTColorMod;

namespace Tests
{
    public class ModAutoDetectTests
    {
        [Fact]
        public void ColorSchemeCycler_WithNonExistentPath_ShouldUseFallbackList()
        {
            // This test verifies fallback behavior when path doesn't exist

            // Arrange
            var tempPath = Path.Combine(Path.GetTempPath(), "test_sprites_nonexistent");

            // Act
            var cycler = new ColorSchemeCycler(tempPath);
            var schemes = cycler.GetAvailableSchemes();

            // Assert
            schemes.Should().NotBeNull();
            schemes.Should().HaveCount(5); // Fallback list has 5 schemes (smoke removed)
            schemes.Should().Contain("original");
        }
    }
}