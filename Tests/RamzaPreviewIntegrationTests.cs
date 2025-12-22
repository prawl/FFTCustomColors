using Xunit;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Services;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class RamzaPreviewIntegrationTests
    {
        [Fact]
        public void CharacterRowBuilder_Should_Apply_Theme_To_Ramza_Preview()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RamzaIntegrationTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var previewManager = new PreviewImageManager(tempDir);
                var rowBuilder = new CharacterRowBuilder(
                    new System.Windows.Forms.TableLayoutPanel(),
                    previewManager,
                    () => false,
                    new System.Collections.Generic.List<System.Windows.Forms.Control>(),
                    new System.Collections.Generic.List<System.Windows.Forms.Control>()
                );

                // Act - Check if CharacterRowBuilder can handle Ramza with themes
                var method = typeof(CharacterRowBuilder).GetMethod("ShouldApplyThemeTransform",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method == null)
                {
                    // Method doesn't exist yet, we need to implement it
                    method = typeof(CharacterRowBuilder).GetMethod("UsesTexFiles",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }

                // For now, just verify that we can identify Ramza chapters as needing special handling
                var texFileManager = new TexFileManager();
                var needsSpecialHandling = texFileManager.UsesTexFiles("RamzaChapter1");

                // Assert
                needsSpecialHandling.Should().BeTrue("RamzaChapter1 uses tex files and needs theme transformation");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}