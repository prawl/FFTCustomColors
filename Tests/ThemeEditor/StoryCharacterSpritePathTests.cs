using System;
using System.IO;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    public class StoryCharacterSpritePathTests
    {
        [Fact]
        public void ResolveStoryCharacterSpritePath_WithCharacterSpecificFolder_ReturnsCharacterFolderPath()
        {
            // Arrange - create temp directory structure with character-specific folder
            var tempDir = Path.Combine(Path.GetTempPath(), "SpritePathTest_" + Guid.NewGuid().ToString("N"));
            var characterFolder = Path.Combine(tempDir, "sprites_rapha_original");
            Directory.CreateDirectory(characterFolder);
            File.WriteAllText(Path.Combine(characterFolder, "battle_h79_spr.bin"), "dummy");

            try
            {
                // Act
                var result = StoryCharacterSpritePathResolver.ResolveSpritePath(
                    tempDir,
                    "Rapha",
                    "battle_h79_spr.bin");

                // Assert
                var expected = Path.Combine(characterFolder, "battle_h79_spr.bin");
                Assert.Equal(expected, result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ResolveStoryCharacterSpritePath_WithoutCharacterSpecificFolder_FallsBackToSpritesOriginal()
        {
            // Arrange - create temp directory structure with only sprites_original folder
            var tempDir = Path.Combine(Path.GetTempPath(), "SpritePathTest_" + Guid.NewGuid().ToString("N"));
            var originalFolder = Path.Combine(tempDir, "sprites_original");
            Directory.CreateDirectory(originalFolder);
            File.WriteAllText(Path.Combine(originalFolder, "battle_oru_spr.bin"), "dummy");

            try
            {
                // Act
                var result = StoryCharacterSpritePathResolver.ResolveSpritePath(
                    tempDir,
                    "Orlandeau",
                    "battle_oru_spr.bin");

                // Assert - should fall back to sprites_original since sprites_orlandeau_original doesn't exist
                var expected = Path.Combine(originalFolder, "battle_oru_spr.bin");
                Assert.Equal(expected, result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ResolveStoryCharacterSpritePath_WithBothFolders_PrefersCharacterSpecificFolder()
        {
            // Arrange - create temp directory structure with both folders
            var tempDir = Path.Combine(Path.GetTempPath(), "SpritePathTest_" + Guid.NewGuid().ToString("N"));
            var characterFolder = Path.Combine(tempDir, "sprites_cloud_original");
            var originalFolder = Path.Combine(tempDir, "sprites_original");
            Directory.CreateDirectory(characterFolder);
            Directory.CreateDirectory(originalFolder);
            File.WriteAllText(Path.Combine(characterFolder, "battle_cloud_spr.bin"), "character-specific");
            File.WriteAllText(Path.Combine(originalFolder, "battle_cloud_spr.bin"), "original");

            try
            {
                // Act
                var result = StoryCharacterSpritePathResolver.ResolveSpritePath(
                    tempDir,
                    "Cloud",
                    "battle_cloud_spr.bin");

                // Assert - should prefer character-specific folder
                var expected = Path.Combine(characterFolder, "battle_cloud_spr.bin");
                Assert.Equal(expected, result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ResolveWotLSpritePath_ShouldReturnUnitPspPath()
        {
            // Arrange - create temp directory structure with unit_psp folder
            var tempDir = Path.Combine(Path.GetTempPath(), "SpritePathTest_" + Guid.NewGuid().ToString("N"));
            var unitPspFolder = Path.Combine(tempDir, "sprites_original");
            Directory.CreateDirectory(unitPspFolder);
            File.WriteAllText(Path.Combine(unitPspFolder, "spr_dst_bchr_ankoku_m_spr.bin"), "dummy");

            try
            {
                // Act - WotL sprites should use unit_psp/sprites_original path
                var result = StoryCharacterSpritePathResolver.ResolveWotLSpritePath(
                    tempDir,
                    "spr_dst_bchr_ankoku_m_spr.bin");

                // Assert
                var expected = Path.Combine(unitPspFolder, "spr_dst_bchr_ankoku_m_spr.bin");
                Assert.Equal(expected, result);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
