using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Xunit;
using FFTColorCustomizer.Configuration.UI;
using FluentAssertions;

namespace FFTColorCustomizer.Tests
{
    public class PreviewImageManagerTests
    {
        [Fact]
        public void UpdateStoryCharacterPreview_Should_Load_Agrias_Original_Image()
        {
            // TLDR: Preview images for story characters should load correctly from Resources/Previews folder
            // This test verifies the fix for preview images showing as empty boxes

            // Arrange
            var testModPath = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Resources";
            var manager = new PreviewImageManager(testModPath);
            var pictureBox = new PictureBox();

            // Verify test setup - image should exist
            var expectedImagePath = Path.Combine(testModPath, "Previews", "agrias_original.png");
            File.Exists(expectedImagePath).Should().BeTrue($"Test setup: Image should exist at {expectedImagePath}");

            try
            {
                // Act
                manager.UpdateStoryCharacterPreview(pictureBox, "agrias", "original");

                // Assert
                pictureBox.Image.Should().NotBeNull("Preview image should be loaded for Agrias original theme");
            }
            finally
            {
                // Cleanup
                pictureBox.Image?.Dispose();
                pictureBox.Dispose();
            }
        }

        [Fact]
        public void UpdateStoryCharacterPreview_Should_Load_Agrias_AshDark_Image()
        {
            // TLDR: Agrias ash_dark preview should load from the correct path

            // Arrange
            var testModPath = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Resources";
            var manager = new PreviewImageManager(testModPath);
            var pictureBox = new PictureBox();

            // Verify test setup
            var expectedImagePath = Path.Combine(testModPath, "Previews", "agrias_ash_dark.png");
            File.Exists(expectedImagePath).Should().BeTrue($"Test setup: Image should exist at {expectedImagePath}");

            try
            {
                // Act
                manager.UpdateStoryCharacterPreview(pictureBox, "agrias", "ash_dark");

                // Assert
                pictureBox.Image.Should().NotBeNull("Preview image should be loaded for Agrias ash_dark theme");
            }
            finally
            {
                // Cleanup
                pictureBox.Image?.Dispose();
                pictureBox.Dispose();
            }
        }

        [Fact]
        public void UpdateStoryCharacterPreview_Should_Load_Orlandeau_Original_Image()
        {
            // TLDR: Orlandeau original preview should load from the correct path

            // Arrange
            var testModPath = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Resources";
            var manager = new PreviewImageManager(testModPath);
            var pictureBox = new PictureBox();

            // Verify test setup - the file exists as orlandeau_original.png
            var expectedImagePath = Path.Combine(testModPath, "Previews", "orlandeau_original.png");
            File.Exists(expectedImagePath).Should().BeTrue($"Test setup: Image should exist at {expectedImagePath}");

            try
            {
                // Act
                manager.UpdateStoryCharacterPreview(pictureBox, "orlandeau", "original");

                // Assert
                pictureBox.Image.Should().NotBeNull("Preview image should be loaded for Orlandeau original theme");
            }
            finally
            {
                // Cleanup
                pictureBox.Image?.Dispose();
                pictureBox.Dispose();
            }
        }

        [Fact]
        public void UpdateStoryCharacterPreview_Should_Load_Orlandeau_ThunderGod_Image()
        {
            // TLDR: Orlandeau thunder_god preview should load from the correct path

            // Arrange
            var testModPath = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Resources";
            var manager = new PreviewImageManager(testModPath);
            var pictureBox = new PictureBox();

            // Verify test setup
            var expectedImagePath = Path.Combine(testModPath, "Previews", "orlandeau_thunder_god.png");
            File.Exists(expectedImagePath).Should().BeTrue($"Test setup: Image should exist at {expectedImagePath}");

            try
            {
                // Act
                manager.UpdateStoryCharacterPreview(pictureBox, "orlandeau", "thunder_god");

                // Assert
                pictureBox.Image.Should().NotBeNull("Preview image should be loaded for Orlandeau thunder_god theme");
            }
            finally
            {
                // Cleanup
                pictureBox.Image?.Dispose();
                pictureBox.Dispose();
            }
        }
    }
}
