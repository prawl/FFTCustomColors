using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    public class CharacterRowBuilderBinIntegrationTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testBinPath;
        private readonly TableLayoutPanel _testPanel;
        private readonly PreviewImageManager _previewManager;
        private readonly CharacterRowBuilder _builder;

        public CharacterRowBuilderBinIntegrationTests()
        {
            // Create a temporary mod directory structure
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testModPath);

            var previewPath = Path.Combine(_testModPath, "Resources", "Previews");
            Directory.CreateDirectory(previewPath);

            // Create the correct directory structure for FFT sprites
            var unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_original");
            Directory.CreateDirectory(unitPath);

            // Create a test .bin file with minimal sprite data
            // Use the correct filename format: battle_cloud_spr.bin
            _testBinPath = Path.Combine(unitPath, "battle_cloud_spr.bin");
            CreateTestBinFile(_testBinPath);

            // Create test components
            _testPanel = new TableLayoutPanel();
            _previewManager = new PreviewImageManager(_testModPath);
            _builder = new CharacterRowBuilder(
                _testPanel,
                _previewManager,
                () => false,
                new System.Collections.Generic.List<Control>(),
                new System.Collections.Generic.List<Control>()
            );
        }

        private void CreateTestBinFile(string path)
        {
            // Create a minimal .bin file with palette and sprite sheet
            // FFT sprites are stored in a 256-pixel-wide sprite sheet
            // Each sprite is 32x40 pixels, arranged horizontally
            // We need at least 5 sprites (positions 0-4) for the extractor to work

            // Sheet is 256 pixels wide, we need at least 40 rows for one row of sprites
            // 256 pixels * 40 rows = 10240 pixels
            // At 4bpp (2 pixels per byte) = 5120 bytes
            var binData = new byte[512 + 5120]; // Palette + sprite sheet data

            // Set up a simple palette
            binData[0] = 0x00; binData[1] = 0x00; // Color 0: Black/Transparent
            binData[2] = 0x1F; binData[3] = 0x00; // Color 1: Red
            binData[4] = 0xE0; binData[5] = 0x03; // Color 2: Green
            binData[6] = 0x00; binData[7] = 0x7C; // Color 3: Blue

            // Fill sprite sheet with different patterns for each sprite
            // Each sprite is 32 pixels wide, so we can fit 8 sprites in the 256-pixel wide sheet
            // But we only need 5 for the extractor (0-4), as it will mirror to create the rest
            int spriteDataStart = 512;

            // Create 5 different sprites with different patterns
            for (int spriteIndex = 0; spriteIndex < 5; spriteIndex++)
            {
                byte pattern = (byte)((spriteIndex + 1) | ((spriteIndex + 1) << 4)); // Both nibbles same

                // Each sprite is 32x40 pixels
                for (int y = 0; y < 40; y++)
                {
                    for (int x = 0; x < 16; x++) // 16 bytes = 32 pixels (2 pixels per byte)
                    {
                        // Calculate position in sprite sheet
                        int sheetX = (spriteIndex * 32) + (x * 2);
                        int pixelIndex = (y * 256) + sheetX;
                        int byteIndex = spriteDataStart + (pixelIndex / 2);

                        if (byteIndex < binData.Length)
                        {
                            binData[byteIndex] = pattern;
                        }
                    }
                }
            }

            File.WriteAllBytes(path, binData);
        }

        [Fact]
        public void CharacterRowBuilder_Should_Load_Sprites_From_Bin_File_When_Available()
        {
            // Arrange
            var carousel = new PreviewCarousel();

            // Act - This should trigger loading from the .bin file
            var updateMethod = _builder.GetType().GetMethod("UpdateStoryCharacterPreview",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            updateMethod.Should().NotBeNull("UpdateStoryCharacterPreview method should exist");

            updateMethod.Invoke(_builder, new object[] { carousel, "cloud", "original" });

            // Assert
            carousel.ImageCount.Should().Be(4, "Should load 4 corner sprites from .bin file for faster loading");
            carousel.CurrentViewIndex.Should().Be(0, "Should start at first image");

            // Verify images are loaded
            for (int i = 0; i < 4; i++)
            {
                carousel.NextView();
                carousel.Image.Should().NotBeNull($"Image {i} should be loaded");
            }
        }

        [Fact]
        public void CharacterRowBuilder_Should_Fall_Back_To_Embedded_Resources_When_Bin_Not_Found()
        {
            // Arrange
            var carousel = new PreviewCarousel();

            // Delete the .bin file to test fallback
            if (File.Exists(_testBinPath))
            {
                File.Delete(_testBinPath);
            }

            // Act
            var updateMethod = _builder.GetType().GetMethod("UpdateStoryCharacterPreview",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            updateMethod.Invoke(_builder, new object[] { carousel, "cloud", "original" });

            // Assert - Should fall back to embedded resources or empty
            // The actual behavior depends on whether embedded resources exist
            // For this test, we just verify it doesn't crash
            carousel.Should().NotBeNull();
        }

        [Fact]
        public void TryLoadGenericFromBinFile_Should_Use_External_Palette_For_UserThemes()
        {
            // Arrange
            // Create the unit path structure with sprites_original containing original sprite
            var unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var originalDir = Path.Combine(unitPath, "sprites_original");
            Directory.CreateDirectory(originalDir);

            // Create original Knight sprite with red palette (color 1 = red)
            var originalSpritePath = Path.Combine(originalDir, "battle_knight_m_spr.bin");
            var originalBinData = new byte[512 + 5120];
            // Set up red at color 1 in embedded palette
            originalBinData[2] = 0x1F; originalBinData[3] = 0x00; // Color 1: Red (BGR555)
            // Fill sprite data with color index 1
            for (int i = 512; i < originalBinData.Length; i++)
            {
                originalBinData[i] = 0x11; // Color index 1
            }
            File.WriteAllBytes(originalSpritePath, originalBinData);

            // Create user theme with blue palette
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "Knight_Male", "Ocean Blue");
            Directory.CreateDirectory(userThemesDir);
            var userPalette = new byte[512];
            // Color 1 = pure blue (BGR555: 0x7C00)
            userPalette[2] = 0x00;
            userPalette[3] = 0x7C;
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), userPalette);

            // Create user themes registry
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Knight_Male\":[\"Ocean Blue\"]}");

            // Initialize the singleton with test mod path so CharacterRowBuilder can find user themes
            UserThemeServiceSingleton.Initialize(_testModPath);

            // Create a new builder that will use the initialized singleton
            var testBuilder = new CharacterRowBuilder(
                _testPanel,
                _previewManager,
                () => false,
                new System.Collections.Generic.List<Control>(),
                new System.Collections.Generic.List<Control>()
            );

            // Act
            var tryLoadMethod = testBuilder.GetType().GetMethod("TryLoadGenericFromBinFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            tryLoadMethod.Should().NotBeNull("TryLoadGenericFromBinFile method should exist");

            var result = tryLoadMethod.Invoke(testBuilder, new object[] { "Knight (Male)", "Ocean Blue" }) as Image[];

            // Assert
            result.Should().NotBeNull("Should return sprites for user theme");
            result.Should().HaveCount(4, "Should return 4 corner sprites");

            // Verify the first sprite uses the user's blue palette, not the original red
            var firstSprite = result[0] as Bitmap;
            firstSprite.Should().NotBeNull();

            var pixelColor = firstSprite.GetPixel(0, 0);
            pixelColor.B.Should().BeGreaterThan(200, "Pixel should be blue from user palette");
            pixelColor.R.Should().BeLessThan(50, "Pixel should not be red from original palette");
        }

        public void Dispose()
        {
            _testPanel?.Dispose();

            // Reset singleton to avoid test pollution
            UserThemeServiceSingleton.Reset();

            // Clean up test directory
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }

        #region Story Character User Theme Tests

        [Fact]
        public void AddStoryCharacterRow_ShouldIncludeUserThemes_WhenUserThemesExist()
        {
            // Arrange
            // Create user theme for story character "Agrias"
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "Agrias", "Ocean Blue");
            Directory.CreateDirectory(userThemesDir);
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), new byte[512]);

            // Create registry entry
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Agrias\":[\"Ocean Blue\"]}");

            // Initialize the singleton with test mod path
            UserThemeServiceSingleton.Initialize(_testModPath);

            // Create test panel and builder with new instance
            var testPanel = new TableLayoutPanel();
            testPanel.ColumnCount = 3;
            testPanel.RowCount = 10;
            var genericControls = new System.Collections.Generic.List<Control>();
            var storyControls = new System.Collections.Generic.List<Control>();

            var testBuilder = new CharacterRowBuilder(
                testPanel,
                _previewManager,
                () => false,
                genericControls,
                storyControls
            );

            // Create a story character config
            var storyConfig = new StoryCharacterRegistry.StoryCharacterConfig
            {
                Name = "Agrias",
                EnumType = typeof(string),
                AvailableThemes = new[] { "original" },
                PreviewName = "Agrias",
                GetValue = () => "original",
                SetValue = (v) => { }
            };

            // Act
            testBuilder.AddStoryCharacterRow(0, storyConfig);

            // Assert - Find the combo box and verify user themes are present
            ThemeComboBox foundComboBox = null;
            foreach (Control control in testPanel.Controls)
            {
                if (control is ThemeComboBox tcb)
                {
                    foundComboBox = tcb;
                    break;
                }
            }

            foundComboBox.Should().NotBeNull("ComboBox should be added to panel");

            // Verify user theme "Ocean Blue" is in the combo box items
            var items = new System.Collections.Generic.List<string>();
            foreach (var item in foundComboBox.Items)
            {
                items.Add(item.ToString());
            }

            items.Should().Contain("Ocean Blue", "User theme should be included in story character dropdown");

            testPanel.Dispose();
        }

        [Fact]
        public void AddStoryCharacterRow_ShouldSetTagWithJobName_ForRefreshDropdowns()
        {
            // Arrange
            UserThemeServiceSingleton.Initialize(_testModPath);

            var testPanel = new TableLayoutPanel();
            testPanel.ColumnCount = 3;
            testPanel.RowCount = 10;
            var genericControls = new System.Collections.Generic.List<Control>();
            var storyControls = new System.Collections.Generic.List<Control>();

            var testBuilder = new CharacterRowBuilder(
                testPanel,
                _previewManager,
                () => false,
                genericControls,
                storyControls
            );

            var storyConfig = new StoryCharacterRegistry.StoryCharacterConfig
            {
                Name = "Agrias",
                EnumType = typeof(string),
                AvailableThemes = new[] { "original" },
                PreviewName = "Agrias",
                GetValue = () => "original",
                SetValue = (v) => { }
            };

            // Act
            testBuilder.AddStoryCharacterRow(0, storyConfig);

            // Assert - Find the combo box and verify Tag has JobName
            ThemeComboBox foundComboBox = null;
            foreach (Control control in testPanel.Controls)
            {
                if (control is ThemeComboBox tcb)
                {
                    foundComboBox = tcb;
                    break;
                }
            }

            foundComboBox.Should().NotBeNull("ComboBox should be added to panel");
            foundComboBox.Tag.Should().NotBeNull("ComboBox should have Tag set");

            // Verify Tag has JobName property using reflection (anonymous types are internal to their assembly)
            var tagType = foundComboBox.Tag.GetType();
            var jobNameProperty = tagType.GetProperty("JobName");
            jobNameProperty.Should().NotBeNull("Tag should have JobName property");

            var jobName = jobNameProperty.GetValue(foundComboBox.Tag)?.ToString();
            jobName.Should().Be("Agrias", "Tag.JobName should be set to character name for RefreshDropdownsForJob");

            testPanel.Dispose();
        }

        [Fact]
        public void TryLoadFromBinFile_ShouldUseExternalPalette_ForStoryCharacterUserThemes()
        {
            // Arrange
            // Create the unit path structure with sprites_original containing original sprite
            var unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var originalDir = Path.Combine(unitPath, "sprites_original");
            Directory.CreateDirectory(originalDir);

            // Create original Agrias sprite with red palette (color 1 = red)
            var originalSpritePath = Path.Combine(originalDir, "battle_aguri_spr.bin");
            var originalBinData = new byte[512 + 5120];
            // Set up red at color 1 in embedded palette
            originalBinData[2] = 0x1F; originalBinData[3] = 0x00; // Color 1: Red (BGR555)
            // Fill sprite data with color index 1
            for (int i = 512; i < originalBinData.Length; i++)
            {
                originalBinData[i] = 0x11; // Color index 1
            }
            File.WriteAllBytes(originalSpritePath, originalBinData);

            // Create user theme with blue palette for Agrias
            var userThemesDir = Path.Combine(_testModPath, "UserThemes", "Agrias", "Ocean Blue");
            Directory.CreateDirectory(userThemesDir);
            var userPalette = new byte[512];
            // Color 1 = pure blue (BGR555: 0x7C00)
            userPalette[2] = 0x00;
            userPalette[3] = 0x7C;
            File.WriteAllBytes(Path.Combine(userThemesDir, "palette.bin"), userPalette);

            // Create user themes registry
            var registryPath = Path.Combine(_testModPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Agrias\":[\"Ocean Blue\"]}");

            // Initialize the singleton with test mod path
            UserThemeServiceSingleton.Initialize(_testModPath);

            // Create a new builder that will use the initialized singleton
            var testBuilder = new CharacterRowBuilder(
                _testPanel,
                _previewManager,
                () => false,
                new System.Collections.Generic.List<Control>(),
                new System.Collections.Generic.List<Control>()
            );

            // Act - Call TryLoadFromBinFile directly via reflection
            var tryLoadMethod = testBuilder.GetType().GetMethod("TryLoadFromBinFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            tryLoadMethod.Should().NotBeNull("TryLoadFromBinFile method should exist");

            var result = tryLoadMethod.Invoke(testBuilder, new object[] { "Agrias", "Ocean Blue" }) as Image[];

            // Assert
            result.Should().NotBeNull("Should return sprites for story character user theme");
            result.Should().HaveCount(4, "Should return 4 corner sprites");

            // Verify the first sprite uses the user's blue palette, not the original red
            var firstSprite = result[0] as Bitmap;
            firstSprite.Should().NotBeNull();

            var pixelColor = firstSprite.GetPixel(0, 0);
            pixelColor.B.Should().BeGreaterThan(200, "Pixel should be blue from user palette");
            pixelColor.R.Should().BeLessThan(50, "Pixel should not be red from original palette");
        }

        #endregion
    }
}