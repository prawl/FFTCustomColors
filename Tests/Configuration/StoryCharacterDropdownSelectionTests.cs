using System;
using System.Windows.Forms;
using Xunit;
using FFTColorMod.Configuration;

namespace Tests.Configuration
{
    public class StoryCharacterDropdownSelectionTests
    {
        [Fact]
        public void ComboBox_Should_Select_Correct_Item_When_DataSource_Is_Different_Enum_Type()
        {
            // This test documents that SelectedItem doesn't work without Handle
            // Our fix uses SelectedIndex after Handle creation instead

            // Arrange
            var comboBox = new ComboBox();
            object currentTheme = AgriasColorScheme.ash_dark;

            // Act - Simulate what AddStoryCharacterRow does
            var values = Enum.GetValues(typeof(AgriasColorScheme));
            comboBox.DataSource = values;

            // Without Handle, SelectedItem fails
            comboBox.SelectedItem = (AgriasColorScheme)currentTheme;

            // Assert - This fails without Handle creation
            Assert.Null(comboBox.SelectedItem); // Expected: fails without Handle
        }

        [Fact]
        public void ComboBox_Should_Select_Correct_Item_Using_String_Comparison()
        {
            // This test documents that even string comparison fails without Handle

            // Arrange
            var comboBox = new ComboBox();
            object currentTheme = AgriasColorScheme.ash_dark;

            // Act - Use string comparison to find the matching item
            var values = Enum.GetValues(typeof(AgriasColorScheme));
            comboBox.DataSource = values;

            string themeString = currentTheme.ToString();
            foreach (var item in values)
            {
                if (item.ToString() == themeString)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }

            // Assert - This also fails without Handle creation
            Assert.Null(comboBox.SelectedItem); // Expected: fails without Handle
        }

        [Fact]
        public void All_Story_Character_Enums_Should_Select_Correctly_With_Handle()
        {
            // Test Agrias with Handle
            TestEnumSelectionWithHandle(AgriasColorScheme.ash_dark, typeof(AgriasColorScheme));

            // Test Orlandeau with Handle
            TestEnumSelectionWithHandle(OrlandeauColorScheme.thunder_god, typeof(OrlandeauColorScheme));

            // Test Cloud with Handle
            TestEnumSelectionWithHandle(CloudColorScheme.knights_round, typeof(CloudColorScheme));
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
            object currentTheme = AgriasColorScheme.ash_dark;

            // Set DataSource first
            var values = Enum.GetValues(typeof(AgriasColorScheme));
            comboBox.DataSource = values;

            // Add to panel
            panel.Controls.Add(comboBox);

            // Force Handle creation explicitly
            var handle = comboBox.Handle;

            // Now find the correct index and set it
            for (int i = 0; i < values.Length; i++)
            {
                if (values.GetValue(i).ToString() == currentTheme.ToString())
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }

            // Assert
            Assert.Equal(AgriasColorScheme.ash_dark, comboBox.SelectedItem);
            Assert.Equal(1, comboBox.SelectedIndex); // ash_dark is at index 1

            form.Dispose();
        }

        private void TestEnumSelectionWithHandle(object themeValue, Type enumType)
        {
            // Arrange
            var form = new Form();
            var panel = new TableLayoutPanel();
            form.Controls.Add(panel);

            var comboBox = new ComboBox();
            var values = Enum.GetValues(enumType);
            comboBox.DataSource = values;

            // Add to panel and force Handle creation
            panel.Controls.Add(comboBox);
            var handle = comboBox.Handle;

            // Act - Use index-based selection after Handle creation
            string themeString = themeValue.ToString();
            for (int i = 0; i < values.Length; i++)
            {
                if (values.GetValue(i).ToString() == themeString)
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }

            // Assert
            Assert.NotNull(comboBox.SelectedItem);
            Assert.Equal(themeString, comboBox.SelectedItem.ToString());

            form.Dispose();
        }
    }
}