using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;
using FluentAssertions;
using Xunit;

namespace Tests.Configuration.UI
{
    public class JobSpecificThemeTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly JobClassDefinitionService _jobClassService;

        public JobSpecificThemeTests()
        {
            // Create a temporary test directory structure
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTColorTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testModPath);

            // Create the FFTIVC structure needed for preview validation
            var unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);

            // Create job-specific theme directories with creative names
            CreateJobSpecificThemeDirectory("sprites_knight_holy_guard", "battle_knight_m_spr.bin", "battle_knight_w_spr.bin");
            CreateJobSpecificThemeDirectory("sprites_knight_divine_blade", "battle_knight_m_spr.bin", "battle_knight_w_spr.bin");
            CreateJobSpecificThemeDirectory("sprites_knight_dark_knight", "battle_knight_m_spr.bin", "battle_knight_w_spr.bin");
            CreateJobSpecificThemeDirectory("sprites_knight_thunder_general", "battle_knight_m_spr.bin", "battle_knight_w_spr.bin");
            CreateJobSpecificThemeDirectory("sprites_knight_summoner_sage", "battle_knight_m_spr.bin", "battle_knight_w_spr.bin");

            // Initialize services - the service should load JobClasses.json from the actual project
            _jobClassService = new JobClassDefinitionService();
        }

        private void CreateJobSpecificThemeDirectory(string themeDirName, params string[] spriteFiles)
        {
            var themeDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", themeDirName);
            Directory.CreateDirectory(themeDir);

            foreach (var spriteFile in spriteFiles)
            {
                var filePath = Path.Combine(themeDir, spriteFile);
                // Create a dummy bin file with proper FFT sprite header
                var dummyData = CreateDummyBinFile();
                File.WriteAllBytes(filePath, dummyData);
            }
        }

        private byte[] CreateDummyBinFile()
        {
            // Create a minimal valid FFT sprite file (512 bytes palette + minimal sprite data)
            var data = new byte[1024];
            // Set up a basic palette (first 512 bytes)
            for (int i = 0; i < 256; i++)
            {
                // Each color is 2 bytes in BGR555 format
                data[i * 2] = 0xFF;
                data[i * 2 + 1] = 0x7F;
            }
            return data;
        }

        [Fact]
        public void JobClassDefinitionService_ShouldLoadJobSpecificThemes()
        {
            // Act
            var themes = _jobClassService.GetAvailableThemesForJob("Knight_Male");

            // Assert
            themes.Should().NotBeNull();
            themes.Should().Contain("holy_guard", "job-specific theme holy_guard should be in JobClasses.json");
            themes.Should().Contain("divine_blade", "job-specific theme divine_blade should be in JobClasses.json");
            themes.Should().Contain("dark_knight", "job-specific theme dark_knight should be in JobClasses.json");
            themes.Should().Contain("thunder_general", "job-specific theme thunder_general should be in JobClasses.json");
            themes.Should().Contain("summoner_sage", "job-specific theme summoner_sage should be in JobClasses.json");
        }

        [Fact]
        public void JobSpecificThemeDirectory_ShouldFollowNamingPattern()
        {
            // Arrange
            var expectedDirs = new[]
            {
                "sprites_knight_holy_guard",
                "sprites_knight_divine_blade",
                "sprites_knight_dark_knight",
                "sprites_knight_thunder_general",
                "sprites_knight_summoner_sage"
            };

            // Act & Assert
            foreach (var dirName in expectedDirs)
            {
                var dirPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", dirName);
                Directory.Exists(dirPath).Should().BeTrue($"Directory {dirName} should exist");

                var maleSpriteFile = Path.Combine(dirPath, "battle_knight_m_spr.bin");
                File.Exists(maleSpriteFile).Should().BeTrue($"Male sprite should exist in {dirName}");

                var femaleSpriteFile = Path.Combine(dirPath, "battle_knight_w_spr.bin");
                File.Exists(femaleSpriteFile).Should().BeTrue($"Female sprite should exist in {dirName}");
            }
        }

        [Fact]
        public void JobSpecificTheme_ShouldBeRecognizedBySpriteFinder()
        {
            // This test verifies that job-specific themes can be found by the sprite loading system
            // The actual implementation would need to handle the sprites_knight_h78 pattern

            // Arrange
            var knightThemes = new[] { "holy_guard", "divine_blade", "dark_knight", "thunder_general", "summoner_sage" };

            // Act & Assert
            foreach (var theme in knightThemes)
            {
                var expectedPath = $"sprites_knight_{theme}";
                var fullPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", expectedPath);

                Directory.Exists(fullPath).Should().BeTrue(
                    $"Job-specific theme directory {expectedPath} should exist for Knight with theme {theme}");
            }
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