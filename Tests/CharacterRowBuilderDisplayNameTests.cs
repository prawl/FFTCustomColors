using Xunit;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace FFTColorCustomizer.Tests
{
    public class CharacterRowBuilderDisplayNameTests
    {
        [Fact]
        public void CharacterRowBuilder_Should_Create_Label_With_Display_Name_Chapter1()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RowBuilderTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var mainPanel = new TableLayoutPanel();
                var previewManager = new PreviewImageManager(tempDir);
                var isInitializing = new System.Func<bool>(() => false);
                var genericControls = new List<Control>();
                var storyControls = new List<Control>();

                var rowBuilder = new CharacterRowBuilder(
                    mainPanel,
                    previewManager,
                    isInitializing,
                    genericControls,
                    storyControls
                );

                var characterConfig = new FFTColorCustomizer.Configuration.UI.StoryCharacterRegistry.StoryCharacterConfig
                {
                    Name = "RamzaChapter1",
                    EnumType = typeof(object),
                    SetValue = (s) => { },
                    GetValue = () => "original"
                };

                // Act
                rowBuilder.AddStoryCharacterRow(1, characterConfig);

                // Assert
                // For Ramza characters, a Panel is added that contains the label
                var control = mainPanel.GetControlFromPosition(0, 1);
                control.Should().NotBeNull();

                if (control is Panel panel)
                {
                    // Find the label within the panel
                    var label = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Ramza (Chapter 1)");
                    label.Should().NotBeNull();
                    label.Text.Should().Be("Ramza (Chapter 1)");
                }
                else if (control is Label label)
                {
                    label.Text.Should().Be("Ramza (Chapter 1)");
                }
                else
                {
                    throw new InvalidOperationException($"Expected Panel or Label, but got {control?.GetType().Name}");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void CharacterRowBuilder_Should_Create_Label_With_Display_Name_Chapter23()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RowBuilderTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var mainPanel = new TableLayoutPanel();
                var previewManager = new PreviewImageManager(tempDir);
                var isInitializing = new System.Func<bool>(() => false);
                var genericControls = new List<Control>();
                var storyControls = new List<Control>();

                var rowBuilder = new CharacterRowBuilder(
                    mainPanel,
                    previewManager,
                    isInitializing,
                    genericControls,
                    storyControls
                );

                var characterConfig = new FFTColorCustomizer.Configuration.UI.StoryCharacterRegistry.StoryCharacterConfig
                {
                    Name = "RamzaChapter23",
                    EnumType = typeof(object),
                    SetValue = (s) => { },
                    GetValue = () => "original"
                };

                // Act
                rowBuilder.AddStoryCharacterRow(1, characterConfig);

                // Assert
                // For Ramza characters, a Panel is added that contains the label
                var control = mainPanel.GetControlFromPosition(0, 1);
                control.Should().NotBeNull();

                if (control is Panel panel)
                {
                    // Find the label within the panel
                    var label = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Ramza (Chapter 2 & 3)");
                    label.Should().NotBeNull();
                    label.Text.Should().Be("Ramza (Chapter 2 & 3)");
                }
                else if (control is Label label)
                {
                    label.Text.Should().Be("Ramza (Chapter 2 & 3)");
                }
                else
                {
                    throw new InvalidOperationException($"Expected Panel or Label, but got {control?.GetType().Name}");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void CharacterRowBuilder_Should_Create_Label_With_Display_Name_Chapter4()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"RowBuilderTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var mainPanel = new TableLayoutPanel();
                var previewManager = new PreviewImageManager(tempDir);
                var isInitializing = new System.Func<bool>(() => false);
                var genericControls = new List<Control>();
                var storyControls = new List<Control>();

                var rowBuilder = new CharacterRowBuilder(
                    mainPanel,
                    previewManager,
                    isInitializing,
                    genericControls,
                    storyControls
                );

                var characterConfig = new FFTColorCustomizer.Configuration.UI.StoryCharacterRegistry.StoryCharacterConfig
                {
                    Name = "RamzaChapter4",
                    EnumType = typeof(object),
                    SetValue = (s) => { },
                    GetValue = () => "original"
                };

                // Act
                rowBuilder.AddStoryCharacterRow(1, characterConfig);

                // Assert
                // For Ramza characters, a Panel is added that contains the label
                var control = mainPanel.GetControlFromPosition(0, 1);
                control.Should().NotBeNull();

                if (control is Panel panel)
                {
                    // Find the label within the panel
                    var label = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Ramza (Chapter 4)");
                    label.Should().NotBeNull();
                    label.Text.Should().Be("Ramza (Chapter 4)");
                }
                else if (control is Label label)
                {
                    label.Text.Should().Be("Ramza (Chapter 4)");
                }
                else
                {
                    throw new InvalidOperationException($"Expected Panel or Label, but got {control?.GetType().Name}");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}