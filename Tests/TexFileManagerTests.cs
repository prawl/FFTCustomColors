using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Tests
{
    public class TexFileManagerTests
    {
        [Fact]
        public void GetTexFilesForCharacter_RamzaChapter4_ReturnsCorrectTexFiles()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var texFiles = texFileManager.GetTexFilesForCharacter("RamzaChapter4");

            // Assert
            Assert.NotNull(texFiles);
            Assert.Contains("tex_834.bin", texFiles);
            Assert.Contains("tex_835.bin", texFiles);
        }

        [Fact]
        public void GetTexFilePathForTheme_RamzaChapter4WhiteKnight_ReturnsCorrectPath()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var path = texFileManager.GetTexFilePathForTheme("RamzaChapter4", "white_knight", "tex_834.bin");

            // Assert
            Assert.Equal("system/ffto/g2d/themes/white_knight/tex_834.bin", path);
        }

        [Fact]
        public void UsesTexFiles_RamzaChapter4_ReturnsTrue()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var usesTexFiles = texFileManager.UsesTexFiles("RamzaChapter4");

            // Assert
            Assert.True(usesTexFiles);
        }

        [Fact]
        public void UsesTexFiles_Cloud_ReturnsFalse()
        {
            // Arrange
            var texFileManager = new TexFileManager();

            // Act
            var usesTexFiles = texFileManager.UsesTexFiles("Cloud");

            // Assert
            Assert.False(usesTexFiles);
        }

        [Fact]
        public void CopyTexFilesForTheme_RamzaChapter4WhiteKnight_CopiesAllTexFiles()
        {
            // Arrange
            var texFileManager = new TexFileManager();
            var modPath = Path.GetTempPath() + "test_mod";
            var sourcePath = Path.Combine(modPath, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/themes/white_knight");
            var destPath = Path.Combine(modPath, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d");

            // Create source files
            Directory.CreateDirectory(sourcePath);
            File.WriteAllText(Path.Combine(sourcePath, "tex_834.bin"), "test");
            File.WriteAllText(Path.Combine(sourcePath, "tex_835.bin"), "test");

            // Act
            texFileManager.CopyTexFilesForTheme("RamzaChapter4", "white_knight", modPath);

            // Assert
            Assert.True(File.Exists(Path.Combine(destPath, "tex_834.bin")));
            Assert.True(File.Exists(Path.Combine(destPath, "tex_835.bin")));

            // Cleanup
            Directory.Delete(modPath, true);
        }
    }
}
