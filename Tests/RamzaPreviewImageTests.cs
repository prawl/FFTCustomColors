using Xunit;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class RamzaPreviewImageTests
    {
        [Fact]
        public void CharacterRowBuilder_Should_Map_RamzaChapter1_To_Ramuza_Sprite()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RamzaPreviewTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var unitPath = Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                Directory.CreateDirectory(unitPath);

                // Create a sprite file for ramuza (Chapter 1)
                var spritePath = Path.Combine(unitPath, "battle_ramuza_spr.bin");
                File.WriteAllText(spritePath, "test_sprite_data");

                var previewManager = new PreviewImageManager(tempDir);
                var rowBuilder = new CharacterRowBuilder(
                    new System.Windows.Forms.TableLayoutPanel(),
                    previewManager,
                    () => false,
                    new System.Collections.Generic.List<System.Windows.Forms.Control>(),
                    new System.Collections.Generic.List<System.Windows.Forms.Control>()
                );

                // Act - Use reflection to test the internal sprite name mapping
                var method = typeof(CharacterRowBuilder).GetMethod("GetInternalSpriteName",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var internalName = method.Invoke(rowBuilder, new object[] { "RamzaChapter1" });

                // Assert
                internalName.Should().Be("ramuza");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void CharacterRowBuilder_Should_Map_RamzaChapter2_To_Ramuza2_Sprite()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RamzaPreviewTest_{Guid.NewGuid()}");
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

                // Act - Use reflection to test the internal sprite name mapping
                var method = typeof(CharacterRowBuilder).GetMethod("GetInternalSpriteName",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var internalName = method.Invoke(rowBuilder, new object[] { "RamzaChapter2" });

                // Assert
                internalName.Should().Be("ramuza2");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void CharacterRowBuilder_Should_Map_RamzaChapter34_To_Ramuza3_Sprite()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RamzaPreviewTest_{Guid.NewGuid()}");
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

                // Act - Use reflection to test the internal sprite name mapping
                var method = typeof(CharacterRowBuilder).GetMethod("GetInternalSpriteName",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var internalName = method.Invoke(rowBuilder, new object[] { "RamzaChapter34" });

                // Assert
                internalName.Should().Be("ramuza3");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}