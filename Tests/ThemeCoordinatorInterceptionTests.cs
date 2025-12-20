using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Core.ModComponents;

namespace FFTColorCustomizer.Tests
{
    public class ThemeCoordinatorInterceptionTests
    {
        [Fact]
        public void InterceptFilePath_Should_Use_ModPath_Not_SourcePath()
        {
            // Arrange - use temp directories instead of Program Files
            var tempPath = Path.Combine(Path.GetTempPath(), $"FFTTest_{Guid.NewGuid()}");
            var sourcePath = Path.Combine(tempPath, "Dev", "FFTColorCustomizer", "ColorMod"); // Dev path
            var modPath = Path.Combine(tempPath, "Reloaded", "Mods", "FFTColorCustomizer"); // Deployed path

            // Create test directories to simulate deployed environment
            var testThemeDir = Path.Combine(modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_crimson_red");
            Directory.CreateDirectory(testThemeDir);

            var testBinFile = Path.Combine(testThemeDir, "battle_knight_m_spr.bin");
            File.WriteAllText(testBinFile, "test");

            try
            {
                var coordinator = new ThemeCoordinator(sourcePath, modPath);

                // Act
                coordinator.SetColorScheme("crimson_red");
                var result = coordinator.InterceptFilePath(@"D:\Games\FFT\battle_knight_m_spr.bin");

                // Assert
                result.Should().Contain(modPath, "should use deployed mod path");
                result.Should().NotContain(sourcePath, "should NOT use development source path");
                result.Should().EndWith(@"sprites_crimson_red\battle_knight_m_spr.bin");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        [Fact]
        public void InterceptFilePath_Should_Return_Original_When_Theme_Not_Found()
        {
            // Arrange
            var sourcePath = @"C:\Dev\FFTColorCustomizer\ColorMod";
            var modPath = @"C:\Program Files\Reloaded\Mods\FFTColorCustomizer";
            var coordinator = new ThemeCoordinator(sourcePath, modPath);
            var originalPath = @"D:\Games\FFT\battle_knight_m_spr.bin";

            // Act
            coordinator.SetColorScheme("non_existent_theme");
            var result = coordinator.InterceptFilePath(originalPath);

            // Assert
            result.Should().Be(originalPath, "should return original path when theme doesn't exist");
        }

        [Fact]
        public void InterceptFilePath_Should_Work_On_Deployed_System_Without_Source()
        {
            // Arrange - simulate a deployed system where source path doesn't exist
            var sourcePath = @"C:\NonExistent\Path\That\Does\Not\Exist";
            var modPath = Environment.CurrentDirectory; // Use current directory as mod path

            // Create test theme structure in "deployed" location
            var testThemeDir = Path.Combine(modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_dark_knight");
            Directory.CreateDirectory(testThemeDir);

            var testBinFile = Path.Combine(testThemeDir, "battle_knight_m_spr.bin");
            File.WriteAllText(testBinFile, "test");

            try
            {
                var coordinator = new ThemeCoordinator(sourcePath, modPath);

                // Act
                coordinator.SetColorScheme("dark_knight");
                var result = coordinator.InterceptFilePath(@"D:\Games\FFT\battle_knight_m_spr.bin");

                // Assert
                result.Should().Contain(modPath, "should use mod path even when source doesn't exist");
                result.Should().EndWith(@"sprites_dark_knight\battle_knight_m_spr.bin");
                File.Exists(result).Should().BeTrue("intercepted file should exist");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(Path.Combine(modPath, "FFTIVC")))
                    Directory.Delete(Path.Combine(modPath, "FFTIVC"), true);
            }
        }
    }
}