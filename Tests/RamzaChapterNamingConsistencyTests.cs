using Xunit;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using FluentAssertions;

namespace FFTColorCustomizer.Tests
{
    /// <summary>
    /// Tests to ensure consistent naming convention for Ramza chapters across all services.
    ///
    /// The correct naming convention is:
    /// - RamzaChapter1  -> Chapter 1 armor (tex_830, tex_831)
    /// - RamzaChapter23 -> Chapter 2 & 3 armor (tex_832, tex_833) - same outfit for both chapters
    /// - RamzaChapter4  -> Chapter 4 armor (tex_834, tex_835)
    ///
    /// This matches the Config.cs property names and StoryCharacters.json definitions.
    /// </summary>
    public class RamzaChapterNamingConsistencyTests
    {
        #region TexFileManager Tests - Consistent Naming

        [Fact]
        public void TexFileManager_GetTexFilesForCharacter_RamzaChapter23_ReturnsChapter2And3TexFiles()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act - Using the CORRECT name "RamzaChapter23" (not "RamzaChapter2")
            var texFiles = texFileManager.GetTexFilesForCharacter("RamzaChapter23");

            // Assert - Should return tex_832 and tex_833 for Chapter 2 & 3
            texFiles.Should().NotBeNull();
            texFiles.Should().HaveCount(2);
            texFiles.Should().Contain("tex_832.bin");
            texFiles.Should().Contain("tex_833.bin");
        }

        [Fact]
        public void TexFileManager_GetTexFilesForCharacter_RamzaChapter4_ReturnsChapter4TexFiles()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act - Using the CORRECT name "RamzaChapter4" (not "RamzaChapter34")
            var texFiles = texFileManager.GetTexFilesForCharacter("RamzaChapter4");

            // Assert - Should return tex_834 and tex_835 for Chapter 4
            texFiles.Should().NotBeNull();
            texFiles.Should().HaveCount(2);
            texFiles.Should().Contain("tex_834.bin");
            texFiles.Should().Contain("tex_835.bin");
        }

        [Fact]
        public void TexFileManager_UsesTexFiles_RamzaChapter23_ReturnsTrue()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act - Using the CORRECT name "RamzaChapter23"
            var usesTexFiles = texFileManager.UsesTexFiles("RamzaChapter23");

            // Assert
            usesTexFiles.Should().BeTrue();
        }

        [Fact]
        public void TexFileManager_UsesTexFiles_RamzaChapter4_ReturnsTrue()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act - Using the CORRECT name "RamzaChapter4"
            var usesTexFiles = texFileManager.UsesTexFiles("RamzaChapter4");

            // Assert
            usesTexFiles.Should().BeTrue();
        }

        [Fact]
        public void TexFileManager_OldNaming_RamzaChapter2_ShouldNotWork()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act - Using the OLD incorrect name "RamzaChapter2"
            var texFiles = texFileManager.GetTexFilesForCharacter("RamzaChapter2");

            // Assert - Should return empty list because this name is incorrect
            texFiles.Should().BeEmpty("because 'RamzaChapter2' is not a valid name - use 'RamzaChapter23' instead");
        }

        [Fact]
        public void TexFileManager_OldNaming_RamzaChapter34_ShouldNotWork()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act - Using the OLD incorrect name "RamzaChapter34"
            var texFiles = texFileManager.GetTexFilesForCharacter("RamzaChapter34");

            // Assert - Should return empty list because this name is incorrect
            texFiles.Should().BeEmpty("because 'RamzaChapter34' is not a valid name - use 'RamzaChapter4' instead");
        }

        #endregion

        #region StoryCharacterThemeManager Tests - Display Names

        [Fact]
        public void StoryCharacterThemeManager_GetCharacterDisplayName_RamzaChapter23_ReturnsCorrectDisplayName()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act
            var displayName = manager.GetCharacterDisplayName("RamzaChapter23");

            // Assert - Should show "Chapter 2 & 3" not just "Chapter 2"
            displayName.Should().Be("Ramza (Chapter 2 & 3)");
        }

        [Fact]
        public void StoryCharacterThemeManager_GetCharacterDisplayName_RamzaChapter4_ReturnsCorrectDisplayName()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act
            var displayName = manager.GetCharacterDisplayName("RamzaChapter4");

            // Assert - Should show "Chapter 4" not "Chapter 3 & 4"
            displayName.Should().Be("Ramza (Chapter 4)");
        }

        [Fact]
        public void StoryCharacterThemeManager_GetCharacterDisplayName_RamzaChapter1_ReturnsCorrectDisplayName()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act
            var displayName = manager.GetCharacterDisplayName("RamzaChapter1");

            // Assert
            displayName.Should().Be("Ramza (Chapter 1)");
        }

        #endregion

        #region Cross-Service Consistency Tests

        [Theory]
        [InlineData("RamzaChapter1")]
        [InlineData("RamzaChapter23")]
        [InlineData("RamzaChapter4")]
        public void AllServices_ShouldRecognize_CorrectRamzaChapterNames(string chapterName)
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act & Assert - TexFileManager should recognize these names
            texFileManager.UsesTexFiles(chapterName).Should().BeTrue(
                $"TexFileManager should recognize '{chapterName}' as a valid Ramza chapter");

            texFileManager.GetTexFilesForCharacter(chapterName).Should().NotBeEmpty(
                $"TexFileManager should return tex files for '{chapterName}'");
        }

        #endregion
    }
}
