using Xunit;
using FFTColorCustomizer.Services;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class TexFileManagerChapterCopyTests
    {
        [Fact]
        public void CopyTexFilesForTheme_RamzaChapter1_Should_Copy_Chapter1_Files()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"TexCopyTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var texFileManager = new TexFileManager();

                // Setup white_heretic theme tex files for Chapter 1
                var themePath = Path.Combine(tempDir, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d/themes/white_heretic");
                Directory.CreateDirectory(themePath);

                // Create tex files for Chapter 1
                File.WriteAllText(Path.Combine(themePath, "tex_830.bin"), "white_heretic_ch1_sprite");
                File.WriteAllText(Path.Combine(themePath, "tex_831.bin"), "white_heretic_ch1_palette");

                // Act - Call CopyTexFilesForTheme for RamzaChapter1
                texFileManager.CopyTexFilesForTheme("RamzaChapter1", "white_heretic", tempDir);

                // Assert - Verify only Chapter 1 tex files were copied
                var destPath = Path.Combine(tempDir, "ColorMod/FFTIVC/data/enhanced/system/ffto/g2d");

                File.Exists(Path.Combine(destPath, "tex_830.bin")).Should().BeTrue("tex_830.bin should be copied for Chapter 1");
                File.Exists(Path.Combine(destPath, "tex_831.bin")).Should().BeTrue("tex_831.bin should be copied for Chapter 1");

                var content830 = File.ReadAllText(Path.Combine(destPath, "tex_830.bin"));
                content830.Should().Be("white_heretic_ch1_sprite");

                var content831 = File.ReadAllText(Path.Combine(destPath, "tex_831.bin"));
                content831.Should().Be("white_heretic_ch1_palette");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}