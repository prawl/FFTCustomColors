using Xunit;
using FFTColorCustomizer.Services;
using FluentAssertions;
using System.Collections.Generic;

namespace FFTColorCustomizer.Tests
{
    public class TexFileManagerChapterTests
    {
        [Fact]
        public void GetTexFilesForCharacter_RamzaChapter1_ReturnsCorrectTexFiles()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var texFiles = texFileManager.GetTexFilesForCharacter("RamzaChapter1");

            // Assert
            texFiles.Should().NotBeNull();
            texFiles.Should().Contain("tex_830.bin");
            texFiles.Should().Contain("tex_831.bin");
        }

        [Fact]
        public void GetTexFilesForCharacter_RamzaChapter2_ReturnsCorrectTexFiles()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var texFiles = texFileManager.GetTexFilesForCharacter("RamzaChapter2");

            // Assert
            texFiles.Should().NotBeNull();
            texFiles.Should().Contain("tex_832.bin");
            texFiles.Should().Contain("tex_833.bin");
        }

        [Fact]
        public void GetTexFilesForCharacter_RamzaChapter34_ReturnsCorrectTexFiles()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var texFiles = texFileManager.GetTexFilesForCharacter("RamzaChapter34");

            // Assert
            texFiles.Should().NotBeNull();
            texFiles.Should().Contain("tex_834.bin");
            texFiles.Should().Contain("tex_835.bin");
        }
    }
}