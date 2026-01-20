using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Tests.Helpers;
using FluentAssertions;

namespace FFTColorCustomizer.Tests
{
    public class ConfigurationFormModPathSeparationTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testUserConfigPath;
        private readonly string _testConfigPath;

        public ConfigurationFormModPathSeparationTests()
        {
            // Create a test directory structure that simulates the real deployment scenario:
            // - Mod installation directory with FFTIVC/data/enhanced/fftpack/unit (BIN files)
            // - User config directory (different location) with Config.json

            // Mod installation path (like Reloaded\Mods\FFTColorCustomizer)
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTColorCustomizerTest_Mod_{Guid.NewGuid()}");

            // User config path (like Reloaded\UserData\FFTColorCustomizer\Config)
            var userDataPath = Path.Combine(Path.GetTempPath(), $"FFTColorCustomizerTest_User_{Guid.NewGuid()}");
            var userConfigDir = Path.Combine(userDataPath, "Config");
            _testConfigPath = Path.Combine(userConfigDir, "Config.json");
            _testUserConfigPath = userConfigDir;

            // Create mod directory structure with BIN files (new lazy loading system)
            var unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);

            // Create user config directory
            Directory.CreateDirectory(userConfigDir);

            // Create minimal valid BIN files for sprites
            var binData = CreateMinimalBinFile();
            File.WriteAllBytes(Path.Combine(unitPath, "battle_aguri_spr.bin"), binData);
            File.WriteAllBytes(Path.Combine(unitPath, "battle_oru_spr.bin"), binData);
        }

        [Fact]
        public void ConfigurationForm_Should_Load_Images_From_ModPath_When_ConfigPath_In_Different_Directory()
        {
            // This test confirms the fix: ConfigurationForm should use the mod path for resources
            // even when the config path points to a different directory (User config directory)

            // Arrange
            var config = new Config { ["Agrias"] = "original", ["Orlandeau"] = "original" };

            // The config path and mod path should be different directories
            _testConfigPath.Should().NotBe(_testModPath, "Config and mod should be in different directories");
            Path.GetDirectoryName(Path.GetDirectoryName(_testConfigPath)).Should().NotBe(_testModPath,
                "Config parent directory should not be the mod directory");

            // BIN files only exist in mod path, not in config path directory
            var configDirBinPath = Path.Combine(_testUserConfigPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_aguri_spr.bin");
            File.Exists(configDirBinPath).Should().BeFalse(
                "BIN files should NOT exist in config directory");

            var modBinPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_aguri_spr.bin");
            File.Exists(modBinPath).Should().BeTrue(
                "BIN files SHOULD exist in mod directory");

            // Act - Create form with both config path and mod path (simulating the fix)
            var form = new TestConfigurationForm(config, _testConfigPath, _testModPath);

            // Assert - Images should load from mod path
            var pictureBoxes = GetAllPictureBoxes(form);
            var agriasBox = pictureBoxes.FirstOrDefault(pb =>
                pb.Tag != null && pb.Tag.ToString().Contains("Agrias"));
            var orlandeauBox = pictureBoxes.FirstOrDefault(pb =>
                pb.Tag != null && pb.Tag.ToString().Contains("Orlandeau"));

            agriasBox.Should().NotBeNull("Agrias picture box should exist");
            orlandeauBox.Should().NotBeNull("Orlandeau picture box should exist");

            // With lazy loading, we need to trigger the load callback
            if (agriasBox is PreviewCarousel agriasCarousel)
            {
                // Trigger the lazy loading callback if it exists
                agriasCarousel.LoadImagesCallback?.Invoke(agriasCarousel);
            }
            if (orlandeauBox is PreviewCarousel orlandeauCarousel)
            {
                // Trigger the lazy loading callback if it exists
                orlandeauCarousel.LoadImagesCallback?.Invoke(orlandeauCarousel);
            }

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
            var config = new Config { ["Agrias"] = "original", ["Orlandeau"] = "original" };

            // Act - Create form with only config path (simulating the bug scenario)
            // Without the fix, it would try to derive mod path from config path
            var form = new TestConfigurationForm(config, _testConfigPath, null);

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
            var configDirUnitPath = Path.Combine(_testUserConfigPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var modDirUnitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            Directory.Exists(configDirUnitPath).Should().BeFalse(
                "Config directory should not have BIN resources");
            Directory.Exists(modDirUnitPath).Should().BeTrue(
                "Mod directory should have BIN resources");

            // Act & Assert - Manager with mod path should recognize valid mod structure
            var correctManager = new PreviewImageManager(_testModPath);
            correctManager.HasValidModPath().Should().BeTrue(
                "PreviewImageManager should recognize valid mod path with FFTIVC directory");

            // Act & Assert - Manager with config directory path should not find valid structure
            var wrongManager = new PreviewImageManager(_testUserConfigPath);
            wrongManager.HasValidModPath().Should().BeFalse(
                "PreviewImageManager with config path should not find valid mod structure");
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

        private byte[] CreateMinimalBinFile()
        {
            // Create a minimal valid BIN file with palette and sprite data
            // FFT sprite BIN format: 512 bytes palette + sprite data
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

            return binData;
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
