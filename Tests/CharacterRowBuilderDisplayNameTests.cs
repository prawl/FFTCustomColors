using Xunit;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System;

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
                var label = mainPanel.GetControlFromPosition(0, 1) as Label;
                label.Should().NotBeNull();
                label.Text.Should().Be("Ramza (Chapter 1)");
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
                var label = mainPanel.GetControlFromPosition(0, 1) as Label;
                label.Should().NotBeNull();
                label.Text.Should().Be("Ramza (Chapter 2 & 3)");
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
                var label = mainPanel.GetControlFromPosition(0, 1) as Label;
                label.Should().NotBeNull();
                label.Text.Should().Be("Ramza (Chapter 4)");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}