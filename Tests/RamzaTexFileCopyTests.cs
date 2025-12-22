using Xunit;
using FFTColorCustomizer.Services;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class RamzaTexFileCopyTests
    {
        [Fact]
        public void TexFileManager_CopyTexFilesForTheme_Should_Copy_Files_To_Correct_Location()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"TexCopyTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var texFileManager = new TexFileManager();

                // Setup white_heretic theme tex files
                var themePath = Path.Combine(tempDir, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/themes/white_heretic");
                Directory.CreateDirectory(themePath);

                // Create tex files for white_heretic theme (only Chapter 4 for now)
                File.WriteAllText(Path.Combine(themePath, "tex_834.bin"), "white_heretic_ch4_sprite");
                File.WriteAllText(Path.Combine(themePath, "tex_835.bin"), "white_heretic_ch4_palette");

                // Act - Call CopyTexFilesForTheme directly
                texFileManager.CopyTexFilesForTheme("RamzaChapter34", "white_heretic", tempDir);

                // Assert - Verify tex files were copied to the g2d directory
                var destPath = Path.Combine(tempDir, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d");

                File.Exists(Path.Combine(destPath, "tex_834.bin")).Should().BeTrue("tex_834.bin should be copied");
                File.Exists(Path.Combine(destPath, "tex_835.bin")).Should().BeTrue("tex_835.bin should be copied");

                var content834 = File.ReadAllText(Path.Combine(destPath, "tex_834.bin"));
                content834.Should().Be("white_heretic_ch4_sprite", "Content should match source theme file");

                var content835 = File.ReadAllText(Path.Combine(destPath, "tex_835.bin"));
                content835.Should().Be("white_heretic_ch4_palette", "Content should match source theme file");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}