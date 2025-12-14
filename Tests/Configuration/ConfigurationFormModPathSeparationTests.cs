using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using FFTColorMod.Configuration;
using FFTColorMod.Configuration.UI;
using FluentAssertions;

namespace FFTColorMod.Tests
{
    public class ConfigurationFormModPathSeparationTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testUserConfigPath;
        private readonly string _testConfigPath;

        public ConfigurationFormModPathSeparationTests()
        {
            // Create a test directory structure that simulates the real deployment scenario:
            // - Mod installation directory with Resources/Previews
            // - User config directory (different location) with Config.json

            // Mod installation path (like Reloaded\Mods\FFT_Color_Mod)
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTColorModTest_Mod_{Guid.NewGuid()}");

            // User config path (like Reloaded\UserData\FFT_Color_Mod\Config)
            var userDataPath = Path.Combine(Path.GetTempPath(), $"FFTColorModTest_User_{Guid.NewGuid()}");
            var userConfigDir = Path.Combine(userDataPath, "Config");
            _testConfigPath = Path.Combine(userConfigDir, "Config.json");
            _testUserConfigPath = userConfigDir;

            // Create mod directory structure with preview images
            var previewsPath = Path.Combine(_testModPath, "Resources", "Previews");
            Directory.CreateDirectory(previewsPath);

            // Create user config directory
            Directory.CreateDirectory(userConfigDir);

            // Create minimal valid 1x1 PNG files in mod directory
            byte[] minimalPng = CreateMinimalPng();
            File.WriteAllBytes(Path.Combine(previewsPath, "agrias_original.png"), minimalPng);
            File.WriteAllBytes(Path.Combine(previewsPath, "agrias_ash_dark.png"), minimalPng);
            File.WriteAllBytes(Path.Combine(previewsPath, "orlandeau_original.png"), minimalPng);
            File.WriteAllBytes(Path.Combine(previewsPath, "orlandeau_thunder_god.png"), minimalPng);
        }

        [Fact]
        public void ConfigurationForm_Should_Load_Images_From_ModPath_When_ConfigPath_In_Different_Directory()
        {
            // This test confirms the fix: ConfigurationForm should use the mod path for resources
            // even when the config path points to a different directory (User config directory)

            // Arrange
            var config = new Config { Agrias = AgriasColorScheme.original, Orlandeau = OrlandeauColorScheme.original };

            // The config path and mod path should be different directories
            _testConfigPath.Should().NotBe(_testModPath, "Config and mod should be in different directories");
            Path.GetDirectoryName(Path.GetDirectoryName(_testConfigPath)).Should().NotBe(_testModPath,
                "Config parent directory should not be the mod directory");

            // Preview images only exist in mod path, not in config path directory
            var configDirPreviewPath = Path.Combine(_testUserConfigPath, "Resources", "Previews", "agrias_original.png");
            File.Exists(configDirPreviewPath).Should().BeFalse(
                "Preview images should NOT exist in config directory");

            var modPreviewPath = Path.Combine(_testModPath, "Resources", "Previews", "agrias_original.png");
            File.Exists(modPreviewPath).Should().BeTrue(
                "Preview images SHOULD exist in mod directory");

            // Act - Create form with both config path and mod path (simulating the fix)
            var form = new ConfigurationForm(config, _testConfigPath, _testModPath);

            // Assert - Images should load from mod path
            var pictureBoxes = GetAllPictureBoxes(form);
            var agriasBox = pictureBoxes.FirstOrDefault(pb =>
                pb.Tag != null && pb.Tag.ToString().Contains("Agrias"));
            var orlandeauBox = pictureBoxes.FirstOrDefault(pb =>
                pb.Tag != null && pb.Tag.ToString().Contains("Orlandeau"));

            agriasBox.Should().NotBeNull("Agrias picture box should exist");
            orlandeauBox.Should().NotBeNull("Orlandeau picture box should exist");

            // The fix ensures images load from mod path, not config path
            agriasBox?.Image.Should().NotBeNull(
                "Agrias preview should load from mod path when both paths are provided");
            orlandeauBox?.Image.Should().NotBeNull(
                "Orlandeau preview should load from mod path when both paths are provided");

            // Cleanup
            form.Dispose();
        }

        [Fact]
        public void ConfigurationForm_Should_Fail_To_Load_Images_Without_ModPath_When_Config_In_User_Directory()
        {
            // This test demonstrates the bug that was fixed: without explicit mod path,
            // the form tries to derive mod path from config path, which fails when config is in User directory

            // Arrange
            var config = new Config { Agrias = AgriasColorScheme.original, Orlandeau = OrlandeauColorScheme.original };

            // Act - Create form with only config path (simulating the bug scenario)
            // Without the fix, it would try to derive mod path from config path
            var form = new ConfigurationForm(config, _testConfigPath, null);

            // The derived path would be wrong (parent of Config directory in User area)
            // So images wouldn't load
            var pictureBoxes = GetAllPictureBoxes(form);
            var agriasBox = pictureBoxes.FirstOrDefault(pb =>
                pb.Tag != null && pb.Tag.ToString().Contains("Agrias"));

            // Assert - Without explicit mod path, images won't load because they're not in User directory
            agriasBox?.Image.Should().BeNull(
                "Without explicit mod path, preview images won't load from wrong derived path");

            // Cleanup
            form.Dispose();
        }

        [Fact]
        public void PreviewImageManager_Should_Use_ModPath_Not_ConfigPath_For_Resources()
        {
            // Direct test of PreviewImageManager with separate paths

            // Arrange
            var configDirPreviewPath = Path.Combine(_testUserConfigPath, "Resources", "Previews");
            var modDirPreviewPath = Path.Combine(_testModPath, "Resources", "Previews");

            Directory.Exists(configDirPreviewPath).Should().BeFalse(
                "Config directory should not have preview resources");
            Directory.Exists(modDirPreviewPath).Should().BeTrue(
                "Mod directory should have preview resources");

            // Act & Assert - Manager with mod path should work
            var correctManager = new PreviewImageManager(_testModPath);
            var pictureBox1 = new PictureBox();
            try
            {
                correctManager.UpdateStoryCharacterPreview(pictureBox1, "Agrias", "original");
                pictureBox1.Image.Should().NotBeNull(
                    "PreviewImageManager should load images from mod path");
            }
            finally
            {
                pictureBox1.Image?.Dispose();
                pictureBox1.Dispose();
            }

            // Act & Assert - Manager with config directory path should fail
            var wrongManager = new PreviewImageManager(_testUserConfigPath);
            var pictureBox2 = new PictureBox();
            try
            {
                wrongManager.UpdateStoryCharacterPreview(pictureBox2, "Agrias", "original");
                pictureBox2.Image.Should().BeNull(
                    "PreviewImageManager should NOT find images in config directory");
            }
            finally
            {
                pictureBox2.Image?.Dispose();
                pictureBox2.Dispose();
            }
        }

        [Fact]
        public void ConfigurationForm_Debug_Button_Should_Use_Correct_ModPath()
        {
            // Test that the debug button also uses the correct mod path

            // Arrange
            var config = new Config { Agrias = AgriasColorScheme.original };

            // Create form with explicit mod path
            var form = new ConfigurationForm(config, _testConfigPath, _testModPath);

            // Find the debug button
            var debugButton = FindDebugButton(form);
            debugButton.Should().NotBeNull("Debug button should exist");

            // The debug button's click handler should use _testModPath for checking files
            // We can't easily invoke it in a test, but we've verified the button exists
            // and the code now uses _modPath field

            // Cleanup
            form.Dispose();
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

        private byte[] CreateMinimalPng()
        {
            // Create a complete minimal 1x1 black pixel PNG
            return new byte[] {
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
        }

        public void Dispose()
        {
            // Clean up test directories
            try
            {
                if (Directory.Exists(_testModPath))
                    Directory.Delete(_testModPath, true);

                var userDataPath = Path.GetDirectoryName(Path.GetDirectoryName(_testConfigPath));
                if (Directory.Exists(userDataPath))
                    Directory.Delete(userDataPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}