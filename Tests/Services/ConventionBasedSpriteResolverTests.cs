using System;
using System.IO;
using System.Linq;
using Xunit;
using FFTColorCustomizer.Services;

namespace Tests.Services
{
    public class ConventionBasedSpriteResolverTests : IDisposable
    {
        private readonly string _testPath;

        public ConventionBasedSpriteResolverTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), "SpriteResolverTest_" + Guid.NewGuid());
            Directory.CreateDirectory(_testPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
                Directory.Delete(_testPath, true);
        }

        [Fact]
        public void ResolveSpriteTheme_Should_Follow_Directory_Convention()
        {
            // Arrange
            var resolver = new ConventionBasedSpriteResolver(_testPath);

            // Create test directory structure following convention
            var themeDir = Path.Combine(_testPath, "sprites_agrias_ash_dark");
            Directory.CreateDirectory(themeDir);
            var spriteFile = Path.Combine(themeDir, "battle_aguri_spr.bin");
            File.WriteAllText(spriteFile, "test");

            // Act
            var resolvedPath = resolver.ResolveSpriteTheme("agrias", "aguri", "ash_dark");

            // Assert
            Assert.Equal(spriteFile, resolvedPath);
            Assert.True(File.Exists(resolvedPath));
        }

        [Fact]
        public void ResolveSpriteTheme_Should_Fallback_To_Flat_Convention()
        {
            // Arrange
            var resolver = new ConventionBasedSpriteResolver(_testPath);

            // Create flat file structure (backward compatibility)
            var flatFile = Path.Combine(_testPath, "battle_aguri_ash_dark_spr.bin");
            File.WriteAllText(flatFile, "test");

            // Act
            var resolvedPath = resolver.ResolveSpriteTheme("agrias", "aguri", "ash_dark");

            // Assert
            Assert.Equal(flatFile, resolvedPath);
            Assert.True(File.Exists(resolvedPath));
        }

        [Fact]
        public void ResolveSpriteTheme_Should_Prefer_Directory_Over_Flat()
        {
            // Arrange
            var resolver = new ConventionBasedSpriteResolver(_testPath);

            // Create both structures
            var themeDir = Path.Combine(_testPath, "sprites_agrias_ash_dark");
            Directory.CreateDirectory(themeDir);
            var dirFile = Path.Combine(themeDir, "battle_aguri_spr.bin");
            File.WriteAllText(dirFile, "directory");

            var flatFile = Path.Combine(_testPath, "battle_aguri_ash_dark_spr.bin");
            File.WriteAllText(flatFile, "flat");

            // Act
            var resolvedPath = resolver.ResolveSpriteTheme("agrias", "aguri", "ash_dark");

            // Assert
            Assert.Equal(dirFile, resolvedPath);
            var content = File.ReadAllText(resolvedPath);
            Assert.Equal("directory", content);
        }

        [Fact]
        public void DiscoverAvailableThemes_Should_Find_All_Themes()
        {
            // Arrange
            var resolver = new ConventionBasedSpriteResolver(_testPath);

            // Create multiple theme directories
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_agrias_ash_dark"));
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_agrias_blackguard_gold"));
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_cloud_dark"));

            // Also create a flat file
            File.WriteAllText(Path.Combine(_testPath, "battle_aguri_light_spr.bin"), "test");

            // Act
            var agriasThemes = resolver.DiscoverAvailableThemes("agrias", "aguri");
            var cloudThemes = resolver.DiscoverAvailableThemes("cloud", "cloud");

            // Assert
            Assert.Contains("ash_dark", agriasThemes);
            Assert.Contains("blackguard_gold", agriasThemes);
            Assert.Contains("light", agriasThemes); // From flat file
            Assert.Contains("dark", cloudThemes);
        }

        [Fact]
        public void ResolveSpriteTheme_Should_Return_Null_For_Missing_File()
        {
            // Arrange
            var resolver = new ConventionBasedSpriteResolver(_testPath);

            // Act
            var resolvedPath = resolver.ResolveSpriteTheme("nonexistent", "sprite", "theme");

            // Assert
            Assert.Null(resolvedPath);
        }
    }
}
