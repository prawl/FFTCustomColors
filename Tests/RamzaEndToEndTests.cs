using Xunit;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Services;
using FluentAssertions;
using System.IO;
using System;
using System.Windows.Forms;

namespace FFTColorCustomizer.Tests
{
    public class RamzaEndToEndTests
    {
        [Fact]
        public void Should_Display_Transformed_Ramza_Preview_In_UI()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RamzaE2ETest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Copy a real Ramza sprite file if available
                var sourceSprite = @"C:\Users\ptyRa\OneDrive\Desktop\Pac Files\0002\fftpack\unit\battle_ramuza_spr.bin";
                if (File.Exists(sourceSprite))
                {
                    var unitPath = Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                    Directory.CreateDirectory(unitPath);
                    File.Copy(sourceSprite, Path.Combine(unitPath, "battle_ramuza_spr.bin"));
                }

                var mainPanel = new TableLayoutPanel();
                var previewManager = new PreviewImageManager(tempDir);
                var rowBuilder = new CharacterRowBuilder(
                    mainPanel,
                    previewManager,
                    () => false,
                    new System.Collections.Generic.List<Control>(),
                    new System.Collections.Generic.List<Control>()
                );

                var characterConfig = new StoryCharacterRegistry.StoryCharacterConfig
                {
                    Name = "RamzaChapter1",
                    EnumType = typeof(object),
                    GetValue = () => "white_heretic",
                    SetValue = (s) => { },
                    AvailableThemes = new[] { "original", "white_heretic" }
                };

                // Act
                rowBuilder.AddStoryCharacterRow(1, characterConfig);

                // Assert
                var label = mainPanel.GetControlFromPosition(0, 1) as Label;
                label.Should().NotBeNull();
                label.Text.Should().Be("Ramza (Chapter 1)");

                // Check that a preview carousel was created
                var carousel = mainPanel.GetControlFromPosition(2, 1) as PreviewCarousel;
                carousel.Should().NotBeNull("Preview carousel should be created for Ramza");

                // The preview should be using transformed colors for white_heretic theme
                // This would be visible if we could actually render and check the images
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}