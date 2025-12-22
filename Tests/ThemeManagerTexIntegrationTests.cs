using Xunit;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Configuration;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class ThemeManagerTexIntegrationTests
    {
        [Fact]
        public void ThemeManagerAdapter_Should_Have_Ramza_Methods()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"ThemeTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var sourcePath = Path.Combine(tempDir, "ColorMod");
                var adapter = new ThemeManagerAdapter(sourcePath, tempDir);

                // Act - Check if public CycleRamzaTheme method exists
                var hasCycleMethod = adapter.GetType().GetMethod("CycleRamzaTheme") != null;

                // Assert
                hasCycleMethod.Should().BeTrue("ThemeManagerAdapter should have CycleRamzaTheme method");

                // Also verify it actually works by calling it (since Ramza exists in StoryCharacters.json)
                var exception = Record.Exception(() => adapter.CycleRamzaTheme());
                exception.Should().BeNull("CycleRamzaTheme should execute without errors");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}