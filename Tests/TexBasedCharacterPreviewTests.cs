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
        public void Should_Identify_RamzaChapter2_As_TexBased_Character()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var usesTexFiles = texFileManager.UsesTexFiles("RamzaChapter2");

            // Assert
            usesTexFiles.Should().BeTrue("RamzaChapter2 should be identified as using tex files");
        }

        [Fact]
        public void Should_Identify_RamzaChapter34_As_TexBased_Character()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var usesTexFiles = texFileManager.UsesTexFiles("RamzaChapter34");

            // Assert
            usesTexFiles.Should().BeTrue("RamzaChapter34 should be identified as using tex files");
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