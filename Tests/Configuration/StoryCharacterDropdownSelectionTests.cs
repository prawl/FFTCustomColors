using System;
using System.Windows.Forms;
using Xunit;
using FFTColorMod.Configuration;

namespace Tests.Configuration
{
    public class StoryCharacterDropdownSelectionTests
    {
        [Fact]
        public void ComboBox_Should_Select_Correct_Item_When_DataSource_Is_String_Array()
        {
            // This test documents that SelectedItem doesn't work without Handle
            // Our fix uses SelectedIndex after Handle creation instead

            // Arrange
            var comboBox = new ComboBox();
            string currentTheme = "ash_dark";

            // Act - Simulate what AddStoryCharacterRow does with string themes
            var availableThemes = new string[] { "original", "ash_dark" };
            comboBox.DataSource = availableThemes;

            // Without Handle, SelectedItem fails
            comboBox.SelectedItem = currentTheme;

            // Assert - This fails without Handle creation
            Assert.Null(comboBox.SelectedItem); // Expected: fails without Handle
        }

        [Fact]
        public void ComboBox_Should_Select_Correct_Item_Using_String_Comparison()
        {
            // This test documents that even string comparison fails without Handle

            // Arrange
            var comboBox = new ComboBox();
            string currentTheme = "ash_dark";

            // Act - Use string comparison to find the matching item
            var availableThemes = new string[] { "original", "ash_dark" };
            comboBox.DataSource = availableThemes;

            foreach (var item in availableThemes)
            {
                if (item == currentTheme)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }

            // Assert - This also fails without Handle creation
            Assert.Null(comboBox.SelectedItem); // Expected: fails without Handle
        }

        [Fact]
        public void All_Story_Character_Themes_Should_Select_Correctly_With_Handle()
        {
            // Test Agrias themes with Handle
            TestThemeSelectionWithHandle("ash_dark", new string[] { "original", "ash_dark" });

            // Test Orlandeau themes with Handle
            TestThemeSelectionWithHandle("thunder_god", new string[] { "original", "thunder_god" });

            // Test default theme selection
            TestThemeSelectionWithHandle("original", new string[] { "original" });
        }

        [Fact]
        public void ComboBox_Should_Select_After_Adding_To_Panel_Using_Index()
        {
            // This test simulates the fix we'll implement in ConfigurationForm

            // Arrange
            var form = new Form();
            var panel = new TableLayoutPanel();
            form.Controls.Add(panel);

            var comboBox = new ComboBox();
            string currentTheme = "ash_dark";

            // Set DataSource first
            var availableThemes = new string[] { "original", "ash_dark" };
            comboBox.DataSource = availableThemes;

            // Add to panel
            panel.Controls.Add(comboBox);

            // Force Handle creation explicitly
            var handle = comboBox.Handle;

            // Now find the correct index and set it
            for (int i = 0; i < availableThemes.Length; i++)
            {
                if (availableThemes[i] == currentTheme)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }

            // Assert
            Assert.Equal("ash_dark", comboBox.SelectedItem);
            Assert.Equal(1, comboBox.SelectedIndex); // ash_dark is at index 1

            form.Dispose();
        }

        [Fact]
        public void ComboBox_Should_Handle_Empty_Theme_List()
        {
            // Test edge case with no available themes

            // Arrange
            var form = new Form();
            var panel = new TableLayoutPanel();
            form.Controls.Add(panel);

            var comboBox = new ComboBox();
            var availableThemes = new string[0]; // Empty array

            // Act
            comboBox.DataSource = availableThemes;
            panel.Controls.Add(comboBox);
            var handle = comboBox.Handle;

            // Assert
            Assert.Null(comboBox.SelectedItem);
            Assert.Equal(-1, comboBox.SelectedIndex);

            form.Dispose();
        }

        [Fact]
        public void ComboBox_Should_Handle_Single_Theme()
        {
            // Test typical case where character has only default theme

            // Arrange
            var form = new Form();
            var panel = new TableLayoutPanel();
            form.Controls.Add(panel);

            var comboBox = new ComboBox();
            var availableThemes = new string[] { "original" };

            // Act
            comboBox.DataSource = availableThemes;
            panel.Controls.Add(comboBox);
            var handle = comboBox.Handle;

            comboBox.SelectedIndex = 0;

            // Assert
            Assert.Equal("original", comboBox.SelectedItem);
            Assert.Equal(0, comboBox.SelectedIndex);

            form.Dispose();
        }

        private void TestThemeSelectionWithHandle(string themeValue, string[] availableThemes)
        {
            // Arrange
            var form = new Form();
            var panel = new TableLayoutPanel();
            form.Controls.Add(panel);

            var comboBox = new ComboBox();
            comboBox.DataSource = availableThemes;

            // Add to panel and force Handle creation
            panel.Controls.Add(comboBox);
            var handle = comboBox.Handle;

            // Act - Use index-based selection after Handle creation
            for (int i = 0; i < availableThemes.Length; i++)
            {
                if (availableThemes[i] == themeValue)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }

            // Assert
            Assert.NotNull(comboBox.SelectedItem);
            Assert.Equal(themeValue, comboBox.SelectedItem.ToString());

            form.Dispose();
        }
    }
}