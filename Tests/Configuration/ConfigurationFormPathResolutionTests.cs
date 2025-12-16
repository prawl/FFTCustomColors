using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using FFTColorMod.Configuration;
using FFTColorMod.Configuration.UI;
using FluentAssertions;

namespace FFTColorMod.Tests
{
    public class ConfigurationFormPathResolutionTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testConfigPath;

        public ConfigurationFormPathResolutionTests()
        {
            // Create a test directory structure that simulates the deployed mod structure
            // ModRoot/
            //   Config/
            //     Config.json
            //   Resources/
            //     Previews/
            //       agrias_original.png
            //       orlandeau_original.png
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTColorModTest_{Guid.NewGuid()}");
            var configDir = Path.Combine(_testModPath, "Config");
            _testConfigPath = Path.Combine(configDir, "Config.json");

            Directory.CreateDirectory(configDir);

            // Create Resources/Previews directory with test images
            var previewsPath = Path.Combine(_testModPath, "Resources", "Previews");
            Directory.CreateDirectory(previewsPath);

            // Create minimal valid 1x1 PNG files
            // This is a complete 1x1 black pixel PNG
            byte[] minimalPng = new byte[] {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,  // PNG signature
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,  // IHDR chunk
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x00, 0x00, 0x00, 0x00, 0x3A, 0x7E, 0x9B,
                0x55, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,  // IDAT chunk
                0x54, 0x08, 0x1D, 0x01, 0x01, 0x00, 0x00, 0xFE,
                0xFF, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0xE5,
                0x5A, 0xDC, 0xE8, 0x00, 0x00, 0x00, 0x00, 0x49,  // IEND chunk
                0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
            };

            File.WriteAllBytes(Path.Combine(previewsPath, "agrias_original.png"), minimalPng);
            File.WriteAllBytes(Path.Combine(previewsPath, "agrias_ash_dark.png"), minimalPng);
            File.WriteAllBytes(Path.Combine(previewsPath, "orlandeau_original.png"), minimalPng);
            File.WriteAllBytes(Path.Combine(previewsPath, "orlandeau_thunder_god.png"), minimalPng);
        }

        [Fact]
        public void ConfigurationForm_Should_Load_Preview_Images_When_ConfigPath_Provided()
        {
            // This test demonstrates the bug: when ConfigurationForm is created with a config path,
            // it should derive the mod root path from that config path, not from Assembly.GetExecutingAssembly().Location

            // Arrange
            var config = new Config { Agrias = "original", Orlandeau = "original" };

            // The bug is in ConfigurationForm line 69:
            // string modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
            // It should instead derive the mod path from _configPath when provided

            // Act
            var form = new ConfigurationForm(config, _testConfigPath);

            // Check if preview images loaded for story characters
            var pictureBoxes = GetAllPictureBoxes(form);
            var agriasBox = pictureBoxes.FirstOrDefault(pb =>
                pb.Tag != null && pb.Tag.ToString().Contains("Agrias"));
            var orlandeauBox = pictureBoxes.FirstOrDefault(pb =>
                pb.Tag != null && pb.Tag.ToString().Contains("Orlandeau"));

            // Assert - This will FAIL because ConfigurationForm uses Assembly location instead of deriving from config path
            // The images exist at _testModPath/Resources/Previews/ but ConfigurationForm looks at Assembly location
            agriasBox.Should().NotBeNull("Agrias picture box should exist");
            orlandeauBox.Should().NotBeNull("Orlandeau picture box should exist");

            // The real bug: images won't load because form looks in wrong directory
            agriasBox?.Image.Should().NotBeNull(
                "Agrias preview image should be loaded when config path is provided");
            orlandeauBox?.Image.Should().NotBeNull(
                "Orlandeau preview image should be loaded when config path is provided");

            // Cleanup
            form.Dispose();
        }

        [Fact]
        public void PreviewImageManager_Should_Find_Images_At_ModRoot_Not_AssemblyLocation()
        {
            // Direct test of the path resolution issue

            // Arrange
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);

            // The assembly is running from bin/Debug or similar, not our test mod path
            assemblyDir.Should().NotBe(_testModPath,
                "Test setup: Assembly should be in different location than test mod");

            // If we use assembly location, we won't find the preview images
            var wrongPreviewPath = Path.Combine(assemblyDir, "Resources", "Previews", "agrias_original.png");
            File.Exists(wrongPreviewPath).Should().BeFalse(
                "Preview images should NOT exist at assembly location");

            // But they should exist at our test mod path
            var correctPreviewPath = Path.Combine(_testModPath, "Resources", "Previews", "agrias_original.png");
            File.Exists(correctPreviewPath).Should().BeTrue(
                "Preview images SHOULD exist at mod root path");

            // Act - Create PreviewImageManager with the CORRECT path (what ConfigurationForm should do)
            var correctManager = new PreviewImageManager(_testModPath);
            var pictureBox = new PictureBox();

            try
            {
                correctManager.UpdateStoryCharacterPreview(pictureBox, "agrias", "original");

                // Assert - With correct path, image should load
                pictureBox.Image.Should().NotBeNull(
                    "PreviewImageManager should load image when given correct mod path");
            }
            finally
            {
                pictureBox.Image?.Dispose();
                pictureBox.Dispose();
            }

            // Act - Create PreviewImageManager with WRONG path (what ConfigurationForm currently does)
            var wrongManager = new PreviewImageManager(assemblyDir);
            var pictureBox2 = new PictureBox();

            try
            {
                wrongManager.UpdateStoryCharacterPreview(pictureBox2, "agrias", "original");

                // Assert - With wrong path, image won't load
                pictureBox2.Image.Should().BeNull(
                    "PreviewImageManager should NOT load image when given assembly location path");
            }
            finally
            {
                pictureBox2.Image?.Dispose();
                pictureBox2.Dispose();
            }
        }

        private Button FindDebugButton(Form form)
        {
            foreach (Control control in form.Controls)
            {
                if (control is FlowLayoutPanel flowPanel)
                {
                    foreach (Control button in flowPanel.Controls)
                    {
                        if (button is Button btn && btn.Text == "Debug")
                        {
                            return btn;
                        }
                    }
                }
            }
            return null;
        }

        private System.Collections.Generic.List<PictureBox> GetAllPictureBoxes(Form form)
        {
            var pictureBoxes = new System.Collections.Generic.List<PictureBox>();

            void SearchControls(Control.ControlCollection controls)
            {
                foreach (Control control in controls)
                {
                    if (control is PictureBox pb)
                    {
                        pictureBoxes.Add(pb);
                    }
                    if (control.Controls.Count > 0)
                    {
                        SearchControls(control.Controls);
                    }
                }
            }

            SearchControls(form.Controls);
            return pictureBoxes;
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testModPath))
            {
                try
                {
                    Directory.Delete(_testModPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}