using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Tests.Helpers;
using FluentAssertions;

namespace FFTColorCustomizer.Tests
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
            //   FFTIVC/
            //     data/enhanced/fftpack/unit/
            //       battle_aguri_spr.bin (Agrias)
            //       battle_oru_spr.bin (Orlandeau)
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTColorCustomizerTest_{Guid.NewGuid()}");
            var configDir = Path.Combine(_testModPath, "Config");
            _testConfigPath = Path.Combine(configDir, "Config.json");

            Directory.CreateDirectory(configDir);

            // Create FFTIVC directory structure for BIN files (new lazy loading system)
            var unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);

            // Create minimal valid BIN files for sprites
            // FFT sprite BIN format: 512 bytes palette + sprite data
            // Create a minimal valid BIN file with palette and sprite data
            var binData = new byte[512 + (256 * 40 * 8)]; // Palette + sprite sheet

            // Set up a basic palette (first 512 bytes)
            for (int i = 0; i < 16; i++) // 16 colors in first palette
            {
                int offset = i * 2;
                binData[offset] = 0xFF; // Color data
                binData[offset + 1] = 0x7F;
            }

            // Add some sprite data (just enough to not crash)
            for (int i = 512; i < Math.Min(binData.Length, 2000); i++)
            {
                binData[i] = 0x11; // Some pixel data
            }

            // Create sprite files for story characters
            File.WriteAllBytes(Path.Combine(unitPath, "battle_aguri_spr.bin"), binData);
            File.WriteAllBytes(Path.Combine(unitPath, "battle_oru_spr.bin"), binData);
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
            var form = new TestConfigurationForm(config, _testConfigPath);

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

            // With lazy loading, we need to trigger the load callback
            if (agriasBox is FFTColorCustomizer.Configuration.UI.PreviewCarousel agriasCarousel)
            {
                agriasCarousel.LoadImagesCallback?.Invoke(agriasCarousel);
            }
            if (orlandeauBox is FFTColorCustomizer.Configuration.UI.PreviewCarousel orlandeauCarousel)
            {
                orlandeauCarousel.LoadImagesCallback?.Invoke(orlandeauCarousel);
            }

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

            // The BIN files should exist at our test mod path
            var testBinPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_aguri_spr.bin");
            File.Exists(testBinPath).Should().BeTrue(
                "BIN files SHOULD exist at test mod path");

            // Act - Create PreviewImageManager with the test mod path
            var testManager = new PreviewImageManager(_testModPath);

            // Assert - With test mod path, manager should recognize valid mod structure
            testManager.HasValidModPath().Should().BeTrue(
                "PreviewImageManager should recognize valid mod path with FFTIVC directory");

            // The point of the test is that ConfigurationForm should use the mod path parameter
            // not derive it from the assembly location or config path
            // This ensures resources are found in the correct location in a deployed environment
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
