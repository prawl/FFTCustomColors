using Xunit;
using FFTColorCustomizer.Services;
using FluentAssertions;
using System.IO;
using System;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class RamzaPreviewTests
    {
        [Fact]
        public void ThemeManager_Should_Handle_TexFiles_For_Ramza()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RamzaTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var texFileManager = new TexFileManager();

                // Setup source theme files
                var themePath = Path.Combine(tempDir, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/themes/white_knight");
                Directory.CreateDirectory(themePath);
                File.WriteAllText(Path.Combine(themePath, "tex_834.bin"), "white_knight_data");
                File.WriteAllText(Path.Combine(themePath, "tex_835.bin"), "white_knight_palette");

                // Act - Check if RamzaChapter4 uses tex files
                var usesTexFiles = texFileManager.UsesTexFiles("RamzaChapter4");

                // Assert
                usesTexFiles.Should().BeTrue("RamzaChapter4 should use tex files");

                // Act - Apply theme for RamzaChapter4
                texFileManager.CopyTexFilesForTheme("RamzaChapter4", "white_knight", tempDir);

                // Verify tex files were copied
                var destPath = Path.Combine(tempDir, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d");
                File.Exists(Path.Combine(destPath, "tex_834.bin")).Should().BeTrue("tex_834.bin should be copied");
                File.Exists(Path.Combine(destPath, "tex_835.bin")).Should().BeTrue("tex_835.bin should be copied");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}