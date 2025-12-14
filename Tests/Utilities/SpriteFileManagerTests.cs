using Xunit;
using FFTColorMod.Configuration;
using FluentAssertions;
using System.IO;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests
{
    public class SpriteFileManagerTests
    {
        [Fact]
        public void SwitchColorScheme_Should_Find_Sprites_In_Variant_Directory()
        {
            // TLDR: Ensure sprite files are found in variant directories like sprites_corpse_brigade
            // This test catches the bug where we were filtering out files containing "sprites_"

            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            var unitDir = Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var variantDir = Path.Combine(unitDir, "sprites_corpse_brigade");
            Directory.CreateDirectory(variantDir);

            // Create a test sprite file in the variant directory
            var testSpriteFile = Path.Combine(variantDir, "battle_knight_m_spr.bin");
            File.WriteAllBytes(testSpriteFile, new byte[] { 0x01, 0x02, 0x03 });

            var manager = new SpriteFileManager(tempDir);

            try
            {
                // Act - This should copy the sprite from variant to main directory
                manager.SwitchColorScheme("corpse_brigade");

                // Assert - The sprite should be copied to the main unit directory
                var copiedFile = Path.Combine(unitDir, "battle_knight_m_spr.bin");
                File.Exists(copiedFile).Should().BeTrue("sprite file should be copied to main directory");

                // Verify the content was copied correctly
                var copiedContent = File.ReadAllBytes(copiedFile);
                copiedContent.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03 });
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void SwitchColorScheme_Should_Handle_Multiple_Sprites_In_Variant()
        {
            // TLDR: Ensure all sprite files in a variant directory are copied

            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            var unitDir = Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var variantDir = Path.Combine(unitDir, "sprites_lucavi");
            Directory.CreateDirectory(variantDir);

            // Create multiple test sprite files
            File.WriteAllBytes(Path.Combine(variantDir, "battle_knight_m_spr.bin"), new byte[] { 0x01 });
            File.WriteAllBytes(Path.Combine(variantDir, "battle_archer_m_spr.bin"), new byte[] { 0x02 });
            File.WriteAllBytes(Path.Combine(variantDir, "battle_wizard_m_spr.bin"), new byte[] { 0x03 });

            var manager = new SpriteFileManager(tempDir);

            try
            {
                // Act
                manager.SwitchColorScheme("lucavi");

                // Assert - All sprites should be copied
                File.Exists(Path.Combine(unitDir, "battle_knight_m_spr.bin")).Should().BeTrue();
                File.Exists(Path.Combine(unitDir, "battle_archer_m_spr.bin")).Should().BeTrue();
                File.Exists(Path.Combine(unitDir, "battle_wizard_m_spr.bin")).Should().BeTrue();
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void SwitchColorScheme_Original_Should_Not_Error_When_Directory_Missing()
        {
            // TLDR: Original scheme should handle missing directory gracefully

            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            var manager = new SpriteFileManager(tempDir);

            try
            {
                // Act & Assert - Should not throw
                var exception = Record.Exception(() => manager.SwitchColorScheme("original"));
                exception.Should().BeNull("original scheme should handle missing directory gracefully");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void SwitchColorScheme_Should_Not_Filter_Out_Files_In_Sprites_Directory()
        {
            // TLDR: Regression test for the bug where files were filtered if path contained "sprites_"
            // This was the exact issue we just fixed - files in sprites_corpse_brigade were being excluded

            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            var unitDir = Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var variantDir = Path.Combine(unitDir, "sprites_southern_sky");
            Directory.CreateDirectory(variantDir);

            // Create a sprite file - this would have been filtered out by the bug
            var spriteFile = Path.Combine(variantDir, "test_sprite.bin");
            File.WriteAllBytes(spriteFile, new byte[] { 0xFF });

            var manager = new SpriteFileManager(tempDir);

            try
            {
                // Act
                manager.SwitchColorScheme("southern_sky");

                // Assert - The file should NOT have been filtered out
                var targetFile = Path.Combine(unitDir, "test_sprite.bin");
                File.Exists(targetFile).Should().BeTrue(
                    "sprite file should be copied even though parent directory contains 'sprites_' in the path");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}