using Xunit;
using FFTColorCustomizer.Services;
using FluentAssertions;

namespace FFTColorCustomizer.Tests
{
    public class TexBasedCharacterPreviewTests
    {
        [Fact]
        public void Should_Identify_RamzaChapter1_As_TexBased_Character()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var usesTexFiles = texFileManager.UsesTexFiles("RamzaChapter1");

            // Assert
            usesTexFiles.Should().BeTrue("RamzaChapter1 should be identified as using tex files");
        }

        [Fact]
        public void Should_Identify_RamzaChapter23_As_TexBased_Character()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var usesTexFiles = texFileManager.UsesTexFiles("RamzaChapter23");

            // Assert
            usesTexFiles.Should().BeTrue("RamzaChapter23 should be identified as using tex files");
        }

        [Fact]
        public void Should_Identify_RamzaChapter4_As_TexBased_Character()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var usesTexFiles = texFileManager.UsesTexFiles("RamzaChapter4");

            // Assert
            usesTexFiles.Should().BeTrue("RamzaChapter4 should be identified as using tex files");
        }

        [Fact]
        public void Should_Not_Identify_Cloud_As_TexBased_Character()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var usesTexFiles = texFileManager.UsesTexFiles("Cloud");

            // Assert
            usesTexFiles.Should().BeFalse("Cloud should not be identified as using tex files");
        }
    }
}