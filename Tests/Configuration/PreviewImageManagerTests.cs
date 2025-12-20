using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration.UI;
using FluentAssertions;
using Xunit;

namespace FFTColorCustomizer.Tests.Configuration
{
    public class PreviewImageManagerTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testPreviewsPath;
        private readonly PreviewImageManager _manager;

        public PreviewImageManagerTests()
        {
            // Create test directory structure matching production layout
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTColorCustomizerTest_" + Guid.NewGuid());

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
        public void UpdateStoryCharacterPreview_Should_Clear_Preview()
        {
            // Arrange
            var pictureBox = new PictureBox();
            // Set an initial image to verify it gets cleared
            pictureBox.Image = new Bitmap(1, 1);

            // Act
            _manager.UpdateStoryCharacterPreview(pictureBox, "agrias", "ash_dark");

            // Assert - our simplified version always clears the preview
            pictureBox.Image.Should().BeNull("Preview should be cleared since we no longer load PNG files");
            pictureBox.BackColor.Should().Be(Color.FromArgb(45, 45, 45), "Background should be set to dark gray");
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
        public void PreviewImageManager_Should_Clear_Preview_Box()
        {
            // Arrange
            var manager = new PreviewImageManager(_testModPath);
            var pictureBox = new PictureBox();

            // Set an initial image to verify it gets cleared
            using (var bitmap = new Bitmap(1, 1))
            {
                pictureBox.Image = bitmap;
                pictureBox.BackColor = Color.White;

                // Act
                manager.UpdateStoryCharacterPreview(pictureBox, "agrias", "ash_dark");

                // Assert - should clear the preview since we no longer load PNG files
                pictureBox.Image.Should().BeNull("Preview should be cleared");
                pictureBox.BackColor.Should().Be(Color.FromArgb(45, 45, 45), "Background should be set to dark gray");
            }
        }

        [Fact]
        public void PreviewImageManager_Should_Check_FFTIVC_Path()
        {
            // Arrange
            var manager = new PreviewImageManager(_testModPath);

            // Act
            var hasValidPath = manager.HasValidModPath();

            // Assert - should return false since our test path doesn't have FFTIVC folder
            hasValidPath.Should().BeFalse("Test path doesn't have FFTIVC folder");

            // Now create the FFTIVC folder structure
            var fftivcPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(fftivcPath);

            // Act again
            hasValidPath = manager.HasValidModPath();

            // Assert - should now return true
            hasValidPath.Should().BeTrue("FFTIVC folder now exists");
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
