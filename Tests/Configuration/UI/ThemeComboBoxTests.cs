using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    [Collection("WinForms")]
    public class ThemeComboBoxTests : IDisposable
    {
        private readonly ThemeComboBox _comboBox;
        private readonly List<string> _testThemes;
        private readonly Form _form;

        public ThemeComboBoxTests()
        {
            // Create a form context for Windows Forms controls
            _form = new Form();
            _comboBox = new ThemeComboBox();
            _form.Controls.Add(_comboBox);

            _testThemes = new List<string>
            {
                "original",
                "corpse_brigade",
                "lucavi",
                "northern_sky",
                "crimson_red"
            };

            // Force handle creation for the control
            var handle = _form.Handle;
        }

        public void Dispose()
        {
            _comboBox?.Dispose();
            _form?.Dispose();
        }

        [Fact]
        public void SetThemes_Should_Display_Formatted_Names()
        {
            // Act
            _comboBox.SetThemes(_testThemes);

            // Assert
            _comboBox.Items.Count.Should().Be(5);
            _comboBox.Items[0].ToString().Should().Be("Original");
            _comboBox.Items[1].ToString().Should().Be("Corpse Brigade");
            _comboBox.Items[2].ToString().Should().Be("Lucavi");
            _comboBox.Items[3].ToString().Should().Be("Northern Sky");
            _comboBox.Items[4].ToString().Should().Be("Crimson Red");
        }

        [Fact]
        public void SelectedThemeValue_Should_Return_Internal_Format()
        {
            // Arrange
            _comboBox.SetThemes(_testThemes);

            // Act
            _comboBox.SelectedIndex = 1; // Select "Corpse Brigade"

            // Assert
            _comboBox.SelectedThemeValue.Should().Be("corpse_brigade");
            _comboBox.Text.Should().Be("Corpse Brigade");
        }

        [Fact]
        public void Setting_SelectedThemeValue_Should_Select_Correct_Item()
        {
            // Arrange
            _comboBox.SetThemes(_testThemes);

            // Act
            _comboBox.SelectedThemeValue = "northern_sky";

            // Assert
            _comboBox.SelectedIndex.Should().Be(3);
            _comboBox.Text.Should().Be("Northern Sky");
        }

        [Fact]
        public void SelectedThemeChanged_Should_Fire_With_Internal_Value()
        {
            // Arrange
            _comboBox.SetThemes(_testThemes);
            string receivedValue = null;
            _comboBox.SelectedThemeChanged += (sender, value) => receivedValue = value;

            // Act
            _comboBox.SelectedIndex = 2; // Select "Lucavi"

            // Assert
            receivedValue.Should().Be("lucavi");
        }

        [Fact]
        public void Setting_SelectedThemeValue_To_Unknown_Should_Add_Item()
        {
            // Arrange
            _comboBox.SetThemes(_testThemes);
            var initialCount = _comboBox.Items.Count;

            // Act
            _comboBox.SelectedThemeValue = "new_theme";

            // Assert
            _comboBox.Items.Count.Should().Be(initialCount + 1);
            _comboBox.SelectedThemeValue.Should().Be("new_theme");
            _comboBox.Text.Should().Be("New Theme");
        }

        [Fact]
        public void Setting_SelectedThemeValue_Should_Be_Case_Insensitive()
        {
            // Arrange
            _comboBox.SetThemes(_testThemes);

            // Act
            _comboBox.SelectedThemeValue = "CORPSE_BRIGADE";

            // Assert
            _comboBox.SelectedIndex.Should().Be(1);
            _comboBox.SelectedThemeValue.Should().Be("corpse_brigade");
        }

        [Fact]
        public void Setting_Null_SelectedThemeValue_Should_Clear_Selection()
        {
            // Arrange
            _comboBox.SetThemes(_testThemes);
            _comboBox.SelectedIndex = 1;

            // Act
            _comboBox.SelectedThemeValue = null;

            // Assert
            _comboBox.SelectedIndex.Should().Be(-1);
            _comboBox.SelectedThemeValue.Should().BeNull();
        }

        [Fact]
        public void SetThemesWithUserThemes_Should_Add_Separator_Before_UserThemes()
        {
            // Arrange
            var builtInThemes = new List<string> { "original", "corpse_brigade" };
            var userThemes = new List<string> { "my_custom_theme", "ocean_blue" };

            // Act
            _comboBox.SetThemesWithUserThemes(builtInThemes, userThemes);

            // Assert - Should have: 2 built-in + 1 separator + 2 user themes = 5 items
            _comboBox.Items.Count.Should().Be(5);
            _comboBox.Items[0].ToString().Should().Be("Original");
            _comboBox.Items[1].ToString().Should().Be("Corpse Brigade");
            _comboBox.Items[2].ToString().Should().Be("── My Themes ──");
            _comboBox.Items[3].ToString().Should().Be("My Custom Theme");
            _comboBox.Items[4].ToString().Should().Be("Ocean Blue");
        }

        [Fact]
        public void SetThemesWithUserThemes_Should_Not_Add_Separator_When_No_UserThemes()
        {
            // Arrange
            var builtInThemes = new List<string> { "original", "corpse_brigade" };
            var userThemes = new List<string>(); // Empty

            // Act
            _comboBox.SetThemesWithUserThemes(builtInThemes, userThemes);

            // Assert - Should have only built-in themes, no separator
            _comboBox.Items.Count.Should().Be(2);
            _comboBox.Items[0].ToString().Should().Be("Original");
            _comboBox.Items[1].ToString().Should().Be("Corpse Brigade");
        }

        [Fact]
        public void SelectedThemeValue_Should_Return_UserTheme_Value_After_Separator()
        {
            // Arrange
            var builtInThemes = new List<string> { "original", "corpse_brigade" };
            var userThemes = new List<string> { "my_custom_theme", "ocean_blue" };
            _comboBox.SetThemesWithUserThemes(builtInThemes, userThemes);

            // Act - Select user theme (index 3, after separator at index 2)
            _comboBox.SelectedIndex = 3;

            // Assert
            _comboBox.SelectedThemeValue.Should().Be("my_custom_theme");
        }

        [Fact]
        public void IsSelectedItemSeparator_Should_Return_True_When_Separator_Selected()
        {
            // Arrange
            var builtInThemes = new List<string> { "original", "corpse_brigade" };
            var userThemes = new List<string> { "my_custom_theme" };
            _comboBox.SetThemesWithUserThemes(builtInThemes, userThemes);

            // Act - Select separator (index 2)
            _comboBox.SelectedIndex = 2;

            // Assert
            _comboBox.IsSelectedItemSeparator.Should().BeTrue();
        }

        [Fact]
        public void IsSelectedItemSeparator_Should_Return_False_For_Regular_Themes()
        {
            // Arrange
            var builtInThemes = new List<string> { "original", "corpse_brigade" };
            var userThemes = new List<string> { "my_custom_theme" };
            _comboBox.SetThemesWithUserThemes(builtInThemes, userThemes);

            // Act & Assert - Built-in theme
            _comboBox.SelectedIndex = 0;
            _comboBox.IsSelectedItemSeparator.Should().BeFalse();

            // Act & Assert - User theme
            _comboBox.SelectedIndex = 3;
            _comboBox.IsSelectedItemSeparator.Should().BeFalse();
        }

        [Fact]
        public void Setting_SelectedThemeValue_Should_Find_UserTheme_After_Separator()
        {
            // Arrange
            var builtInThemes = new List<string> { "original", "corpse_brigade" };
            var userThemes = new List<string> { "my_custom_theme", "ocean_blue" };
            _comboBox.SetThemesWithUserThemes(builtInThemes, userThemes);

            // Act - Set to user theme by value
            _comboBox.SelectedThemeValue = "ocean_blue";

            // Assert - Should find and select the user theme at index 4
            _comboBox.SelectedIndex.Should().Be(4);
            _comboBox.Text.Should().Be("Ocean Blue");
        }

        [Fact]
        public void SelectedThemeChanged_Should_Fire_With_UserTheme_Value()
        {
            // Arrange
            var builtInThemes = new List<string> { "original", "corpse_brigade" };
            var userThemes = new List<string> { "my_custom_theme" };
            _comboBox.SetThemesWithUserThemes(builtInThemes, userThemes);
            string receivedValue = null;
            _comboBox.SelectedThemeChanged += (sender, value) => receivedValue = value;

            // Act - Select user theme
            _comboBox.SelectedIndex = 3;

            // Assert
            receivedValue.Should().Be("my_custom_theme");
        }

        [Fact]
        public void RefreshUserThemes_Should_Add_New_UserTheme_And_Preserve_Selection()
        {
            // Arrange
            var builtInThemes = new List<string> { "original", "corpse_brigade" };
            var userThemes = new List<string> { "my_custom_theme" };
            _comboBox.SetThemesWithUserThemes(builtInThemes, userThemes);
            _comboBox.SelectedThemeValue = "original"; // Select a built-in theme

            // Act - Refresh with a new user theme added
            var updatedUserThemes = new List<string> { "my_custom_theme", "new_user_theme" };
            _comboBox.RefreshUserThemes(updatedUserThemes);

            // Assert - Should have: 2 built-in + 1 separator + 2 user themes = 5 items
            _comboBox.Items.Count.Should().Be(5);
            _comboBox.Items[4].ToString().Should().Be("New User Theme");
            _comboBox.SelectedThemeValue.Should().Be("original"); // Selection preserved
        }
    }
}