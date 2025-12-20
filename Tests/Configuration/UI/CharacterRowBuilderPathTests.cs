using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;
using Xunit;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Configuration;

namespace Tests.Configuration.UI
{
    /// <summary>
    /// Tests for versioned directory path detection in CharacterRowBuilder
    /// </summary>
    public class CharacterRowBuilderPathTests : IDisposable
    {
        private string _testRootPath;
        private CharacterDefinitionService _characterService;

        public CharacterRowBuilderPathTests()
        {
            // Reset singleton to avoid test pollution
            CharacterServiceSingleton.Reset();

            // Create temporary test directories
            _testRootPath = Path.Combine(Path.GetTempPath(), "FFTCharacterRowBuilderTest_" + Guid.NewGuid());
            Directory.CreateDirectory(_testRootPath);

            // Create character service
            _characterService = new CharacterDefinitionService();
        }

        public void Dispose()
        {
            // Clean up test directories
            if (Directory.Exists(_testRootPath))
            {
                Directory.Delete(_testRootPath, true);
            }

            // Reset singleton after tests
            CharacterServiceSingleton.Reset();
        }

        [Fact]
        public void FindActualUnitPath_WithDirectPath_ReturnsDirectPath()
        {
            // Arrange
            var modPath = Path.Combine(_testRootPath, "FFTColorCustomizer");
            var expectedPath = Path.Combine(modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(expectedPath);

            // Create a test sprite file
            var testSprite = Path.Combine(expectedPath, "sprites_lucavi", "battle_knight_m_spr.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(testSprite));
            File.WriteAllBytes(testSprite, new byte[] { 0x01, 0x02, 0x03 });

            // Act - Use reflection to test private method
            // Create minimal required objects for constructor
            var tablePanel = new TableLayoutPanel();
            var previewManager = new PreviewImageManager(expectedPath);
            Func<bool> isInitializing = () => false;
            var genericControls = new List<Control>();
            var storyControls = new List<Control>();

            var builder = new CharacterRowBuilder(tablePanel, previewManager, isInitializing, genericControls, storyControls);
            var builderType = typeof(CharacterRowBuilder);
            var method = builderType.GetMethod("FindActualUnitPath", BindingFlags.NonPublic | BindingFlags.Instance);
            var actualPath = (string)method.Invoke(builder, new object[] { modPath });

            // Assert
            Assert.Equal(expectedPath, actualPath);
        }

        [Fact]
        public void FindActualUnitPath_WithVersionedDirectory_FindsHighestVersion()
        {
            // Arrange
            var modsPath = Path.Combine(_testRootPath, "Mods");
            Directory.CreateDirectory(modsPath);

            // Create multiple versioned directories
            var v105Path = Path.Combine(modsPath, "FFTColorCustomizer_v105", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var v109Path = Path.Combine(modsPath, "FFTColorCustomizer_v109", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var v110Path = Path.Combine(modsPath, "FFTColorCustomizer_v110", "FFTIVC", "data", "enhanced", "fftpack", "unit");

            Directory.CreateDirectory(v105Path);
            Directory.CreateDirectory(v109Path);
            Directory.CreateDirectory(v110Path);

            // Create test sprite in v110
            var testSprite = Path.Combine(v110Path, "sprites_lucavi", "battle_knight_m_spr.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(testSprite));
            File.WriteAllBytes(testSprite, new byte[] { 0x01, 0x02, 0x03 });

            // Mod reports it's in non-versioned path (but it doesn't exist)
            var modPath = Path.Combine(modsPath, "FFTColorCustomizer");

            // Act
            var builderType = typeof(CharacterRowBuilder);
            // Create minimal required objects for constructor
            var tablePanel = new TableLayoutPanel();
            var previewManager = new PreviewImageManager(modPath);
            Func<bool> isInitializing = () => false;
            var genericControls = new List<Control>();
            var storyControls = new List<Control>();

            var builder = new CharacterRowBuilder(tablePanel, previewManager, isInitializing, genericControls, storyControls);
            var method = builderType.GetMethod("FindActualUnitPath", BindingFlags.NonPublic | BindingFlags.Instance);
            var actualPath = (string)method.Invoke(builder, new object[] { modPath });

            // Assert - should find v110 (highest version)
            Assert.Equal(v110Path, actualPath);
        }

        [Fact]
        public void FindActualUnitPath_WithNoValidPaths_ReturnsFallback()
        {
            // Arrange
            var modPath = Path.Combine(_testRootPath, "NonExistentMod");

            // Act
            var builderType = typeof(CharacterRowBuilder);
            // Create minimal required objects for constructor
            var tablePanel = new TableLayoutPanel();
            var previewManager = new PreviewImageManager(modPath);
            Func<bool> isInitializing = () => false;
            var genericControls = new List<Control>();
            var storyControls = new List<Control>();

            var builder = new CharacterRowBuilder(tablePanel, previewManager, isInitializing, genericControls, storyControls);
            var method = builderType.GetMethod("FindActualUnitPath", BindingFlags.NonPublic | BindingFlags.Instance);
            var actualPath = (string)method.Invoke(builder, new object[] { modPath });

            // Assert - should return fallback path even if it doesn't exist
            var expectedFallback = Path.Combine(modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Assert.Equal(expectedFallback, actualPath);
        }

        [Fact]
        public void LoadStoryCharacterSprites_UsesVersionedPath()
        {
            // Arrange
            var modsPath = Path.Combine(_testRootPath, "Mods");
            Directory.CreateDirectory(modsPath);

            // Create versioned directory
            var versionedPath = Path.Combine(modsPath, "FFTColorCustomizer_v110", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(versionedPath);

            // Create sprite file
            var spriteDir = Path.Combine(versionedPath, "sprites_lucavi");
            Directory.CreateDirectory(spriteDir);
            var spriteFile = Path.Combine(spriteDir, "battle_cloud_spr.bin");

            // Create a minimal BIN file (just headers)
            using (var stream = File.Create(spriteFile))
            using (var writer = new BinaryWriter(stream))
            {
                // Minimal BIN structure
                writer.Write(new byte[1024]); // Placeholder data
            }

            var modPath = Path.Combine(modsPath, "FFTColorCustomizer");

            // Add a story character
            _characterService.AddCharacter(new CharacterDefinition
            {
                Name = "Cloud",
                SpriteNames = new[] { "cloud" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "lucavi" },
                EnumType = "StoryCharacter"
            });

            // Act - This should use the versioned path internally
            var exception = Record.Exception(() =>
            {
                // CharacterRowBuilder constructor requires more parameters
                // We're just testing path detection, not the full builder
                var builderType = typeof(CharacterRowBuilder);
                // Create minimal required objects for constructor
                var tablePanel = new TableLayoutPanel();
                var previewManager = new PreviewImageManager(modPath);
                Func<bool> isInitializing = () => false;
                var genericControls = new List<Control>();
                var storyControls = new List<Control>();

                var builder = new CharacterRowBuilder(tablePanel, previewManager, isInitializing, genericControls, storyControls);
                var method = builderType.GetMethod("FindActualUnitPath", BindingFlags.NonPublic | BindingFlags.Instance);
                var actualPath = (string)method.Invoke(builder, new object[] { modPath });
                Assert.Equal(versionedPath, actualPath);
            });

            // Assert - should not throw
            Assert.Null(exception);
        }

        [Fact]
        public void LoadGenericSprites_UsesVersionedPath()
        {
            // Arrange
            var modsPath = Path.Combine(_testRootPath, "Mods");
            Directory.CreateDirectory(modsPath);

            // Create versioned directory
            var versionedPath = Path.Combine(modsPath, "FFTColorCustomizer_v110", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(versionedPath);

            // Create sprite files
            var spriteDir = Path.Combine(versionedPath, "sprites_lucavi");
            Directory.CreateDirectory(spriteDir);

            var knightSprite = Path.Combine(spriteDir, "battle_knight_m_spr.bin");
            using (var stream = File.Create(knightSprite))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new byte[1024]); // Placeholder data
            }

            var modPath = Path.Combine(modsPath, "FFTColorCustomizer");

            // Act
            var exception = Record.Exception(() =>
            {
                // CharacterRowBuilder constructor requires more parameters
                // We're just testing path detection, not the full builder
                var builderType = typeof(CharacterRowBuilder);
                // Create minimal required objects for constructor
                var tablePanel = new TableLayoutPanel();
                var previewManager = new PreviewImageManager(modPath);
                Func<bool> isInitializing = () => false;
                var genericControls = new List<Control>();
                var storyControls = new List<Control>();

                var builder = new CharacterRowBuilder(tablePanel, previewManager, isInitializing, genericControls, storyControls);
                var method = builderType.GetMethod("FindActualUnitPath", BindingFlags.NonPublic | BindingFlags.Instance);
                var actualPath = (string)method.Invoke(builder, new object[] { modPath });
                Assert.Equal(versionedPath, actualPath);
            });

            // Assert - should not throw
            Assert.Null(exception);
        }

        [Fact]
        public void VersionSorting_HandlesVariousFormats()
        {
            // Arrange
            var modsPath = Path.Combine(_testRootPath, "Mods");
            Directory.CreateDirectory(modsPath);

            // Create directories with various version formats
            var versions = new[] { "v1", "v10", "v2", "v100", "v99", "v101" };
            foreach (var version in versions)
            {
                var versionPath = Path.Combine(modsPath, $"FFTColorCustomizer_{version}", "FFTIVC", "data", "enhanced", "fftpack", "unit");
                Directory.CreateDirectory(versionPath);
            }

            // Get the directories and sort them as the code does
            var versionedDirs = Directory.GetDirectories(modsPath, "FFTColorCustomizer_v*")
                .OrderByDescending(dir =>
                {
                    var dirName = Path.GetFileName(dir);
                    var versionStr = dirName.Substring(dirName.LastIndexOf('v') + 1);
                    if (int.TryParse(versionStr, out int version))
                        return version;
                    return 0;
                })
                .ToArray();

            // Assert - should be sorted in descending order by version number
            Assert.Equal("FFTColorCustomizer_v101", Path.GetFileName(versionedDirs[0]));
            Assert.Equal("FFTColorCustomizer_v100", Path.GetFileName(versionedDirs[1]));
            Assert.Equal("FFTColorCustomizer_v99", Path.GetFileName(versionedDirs[2]));
            Assert.Equal("FFTColorCustomizer_v10", Path.GetFileName(versionedDirs[3]));
            Assert.Equal("FFTColorCustomizer_v2", Path.GetFileName(versionedDirs[4]));
            Assert.Equal("FFTColorCustomizer_v1", Path.GetFileName(versionedDirs[5]));
        }

        [Fact]
        public void FindActualUnitPath_WithMixedVersionedAndNonVersioned_PrefersDirect()
        {
            // Arrange
            var modsPath = Path.Combine(_testRootPath, "Mods");
            Directory.CreateDirectory(modsPath);

            // Create both direct and versioned paths
            var directPath = Path.Combine(modsPath, "FFTColorCustomizer", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var versionedPath = Path.Combine(modsPath, "FFTColorCustomizer_v200", "FFTIVC", "data", "enhanced", "fftpack", "unit");

            Directory.CreateDirectory(directPath);
            Directory.CreateDirectory(versionedPath);

            var modPath = Path.Combine(modsPath, "FFTColorCustomizer");

            // Act
            var builderType = typeof(CharacterRowBuilder);
            // Create minimal required objects for constructor
            var tablePanel = new TableLayoutPanel();
            var previewManager = new PreviewImageManager(modPath);
            Func<bool> isInitializing = () => false;
            var genericControls = new List<Control>();
            var storyControls = new List<Control>();

            var builder = new CharacterRowBuilder(tablePanel, previewManager, isInitializing, genericControls, storyControls);
            var method = builderType.GetMethod("FindActualUnitPath", BindingFlags.NonPublic | BindingFlags.Instance);
            var actualPath = (string)method.Invoke(builder, new object[] { modPath });

            // Assert - should prefer the direct path when it exists
            Assert.Equal(directPath, actualPath);
        }

        [Fact]
        public void FindActualUnitPath_HandlesPathsWithSpaces()
        {
            // Arrange
            var modsPath = Path.Combine(_testRootPath, "Mods With Spaces");
            Directory.CreateDirectory(modsPath);

            var versionedPath = Path.Combine(modsPath, "FFTColorCustomizer_v110", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(versionedPath);

            var modPath = Path.Combine(modsPath, "FFTColorCustomizer");

            // Act
            var builderType = typeof(CharacterRowBuilder);
            // Create minimal required objects for constructor
            var tablePanel = new TableLayoutPanel();
            var previewManager = new PreviewImageManager(modPath);
            Func<bool> isInitializing = () => false;
            var genericControls = new List<Control>();
            var storyControls = new List<Control>();

            var builder = new CharacterRowBuilder(tablePanel, previewManager, isInitializing, genericControls, storyControls);
            var method = builderType.GetMethod("FindActualUnitPath", BindingFlags.NonPublic | BindingFlags.Instance);
            var actualPath = (string)method.Invoke(builder, new object[] { modPath });

            // Assert
            Assert.Equal(versionedPath, actualPath);
        }

        [Fact]
        public void FindActualUnitPath_IgnoresNonVersionedSimilarDirectories()
        {
            // Arrange
            var modsPath = Path.Combine(_testRootPath, "Mods");
            Directory.CreateDirectory(modsPath);

            // Create directories that shouldn't match
            Directory.CreateDirectory(Path.Combine(modsPath, "FFTColorCustomizer_backup", "FFTIVC", "data", "enhanced", "fftpack", "unit"));
            Directory.CreateDirectory(Path.Combine(modsPath, "FFTColorCustomizer_old", "FFTIVC", "data", "enhanced", "fftpack", "unit"));
            Directory.CreateDirectory(Path.Combine(modsPath, "FFTColorCustomizerTest", "FFTIVC", "data", "enhanced", "fftpack", "unit"));

            // Create one valid versioned directory
            var validPath = Path.Combine(modsPath, "FFTColorCustomizer_v110", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(validPath);

            var modPath = Path.Combine(modsPath, "FFTColorCustomizer");

            // Act
            var builderType = typeof(CharacterRowBuilder);
            // Create minimal required objects for constructor
            var tablePanel = new TableLayoutPanel();
            var previewManager = new PreviewImageManager(modPath);
            Func<bool> isInitializing = () => false;
            var genericControls = new List<Control>();
            var storyControls = new List<Control>();

            var builder = new CharacterRowBuilder(tablePanel, previewManager, isInitializing, genericControls, storyControls);
            var method = builderType.GetMethod("FindActualUnitPath", BindingFlags.NonPublic | BindingFlags.Instance);
            var actualPath = (string)method.Invoke(builder, new object[] { modPath });

            // Assert - should only find the valid versioned directory
            Assert.Equal(validPath, actualPath);
        }
    }
}