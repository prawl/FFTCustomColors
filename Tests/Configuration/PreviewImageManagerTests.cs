using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FFTColorMod.Configuration.UI;
using FluentAssertions;
using Xunit;

namespace FFTColorMod.Tests.Configuration
{
    public class PreviewImageManagerTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testPreviewsPath;
        private readonly PreviewImageManager _manager;

        public PreviewImageManagerTests()
        {
            // Create test directory structure matching production layout
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());

            // Create Resources/Previews directory to match production structure
            _testPreviewsPath = Path.Combine(_testModPath, "Resources", "Previews");
            Directory.CreateDirectory(_testPreviewsPath);

            // Also create fallback previews directory for compatibility
            var fallbackPreviewsPath = Path.Combine(_testModPath, "previews");
            Directory.CreateDirectory(fallbackPreviewsPath);

            // Create test preview images in the correct location
            CreateTestPreviewImage("agrias_ash_dark.png");
            CreateTestPreviewImage("orlandeau_thunder_god.png");
            CreateTestPreviewImage("cloud_original.png");

            _manager = new PreviewImageManager(_testModPath);
        }

        [Fact]
        public void UpdateStoryCharacterPreview_Should_Find_Preview_In_Mod_Previews_Directory()
        {
            // Arrange
            var pictureBox = new PictureBox();

            // Act
            _manager.UpdateStoryCharacterPreview(pictureBox, "agrias", "ash_dark");

            // Assert
            pictureBox.Image.Should().NotBeNull("Preview image should be loaded");
        }

        [Fact]
        public void UpdateStoryCharacterPreview_Should_Clear_Preview_When_Image_Not_Found()
        {
            // Arrange
            var pictureBox = new PictureBox();

            // Act
            _manager.UpdateStoryCharacterPreview(pictureBox, "nonexistent", "theme");

            // Assert
            pictureBox.Image.Should().BeNull("Preview should be cleared when image not found");
            pictureBox.BackColor.Should().Be(Color.FromArgb(45, 45, 45), "Background should be set to dark gray");
        }

        [Fact]
        public void GetStoryCharacterPreviewPath_Should_Return_Correct_Path()
        {
            // Arrange
            // The primary location is Resources/Previews as per production code
            var expectedPath = Path.Combine(_testModPath, "Resources", "Previews", "agrias_ash_dark.png");

            // Act
            var actualPath = GetStoryCharacterPreviewPathViaReflection("agrias", "ash_dark");

            // Assert
            actualPath.Should().Be(expectedPath);
        }

        [Fact]
        public void PreviewImageManager_Should_Look_In_Correct_Preview_Directory()
        {
            // Arrange
            var manager = new PreviewImageManager(_testModPath);

            // Act
            var path = GetStoryCharacterPreviewPathViaReflection("agrias", "ash_dark");

            // Assert
            // Should use Resources/Previews as the primary location when file doesn't exist
            var expectedPath = Path.Combine(_testModPath, "Resources", "Previews", "agrias_ash_dark.png");
            path.Should().Be(expectedPath, "Should look in Resources/Previews directory first");
        }

        private void CreateTestPreviewImage(string filename)
        {
            var filepath = Path.Combine(_testPreviewsPath, filename);

            // Create a simple 1x1 bitmap as a test image
            using (var bitmap = new Bitmap(1, 1))
            {
                bitmap.SetPixel(0, 0, Color.Red);
                bitmap.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private string GetStoryCharacterPreviewPathViaReflection(string characterName, string theme)
        {
            // Use reflection to call the private GetStoryCharacterPreviewPath method
            var method = typeof(PreviewImageManager).GetMethod(
                "GetStoryCharacterPreviewPath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (string)method.Invoke(_manager, new object[] { characterName, theme });
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }
    }
}