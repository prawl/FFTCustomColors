using System;
using System.Windows.Forms;
using Xunit;
using FFTColorMod.Configuration;

namespace Tests.Configuration
{
    public class ComboBoxHandleTests
    {
        [Fact]
        public void ComboBox_With_Handle_Should_Allow_Selection()
        {
            // Arrange
            var form = new Form();
            var comboBox = new ComboBox();
            form.Controls.Add(comboBox);

            // Create the handle - this is key!
            var handle = comboBox.Handle;

            // Now set the DataSource
            var values = Enum.GetValues(typeof(AgriasColorScheme));
            comboBox.DataSource = values;

            // Act - Try setting by index
            comboBox.SelectedIndex = 1; // ash_dark

            // Assert
            Assert.Equal(1, comboBox.SelectedIndex);
            Assert.Equal(AgriasColorScheme.ash_dark, comboBox.SelectedItem);

            form.Dispose();
        }

        [Fact]
        public void ComboBox_Without_Handle_Has_No_Items()
        {
            // Arrange
            var comboBox = new ComboBox();
            var values = Enum.GetValues(typeof(AgriasColorScheme));
            comboBox.DataSource = values;

            // Act & Assert - Without handle, items aren't populated
            Assert.Equal(0, comboBox.Items.Count); // This is the problem!
        }

        [Fact]
        public void ComboBox_After_Handle_Creation_Has_Items()
        {
            // Arrange
            var form = new Form();
            var comboBox = new ComboBox();
            form.Controls.Add(comboBox);

            var values = Enum.GetValues(typeof(AgriasColorScheme));
            comboBox.DataSource = values;

            // Act - Create handle
            var handle = comboBox.Handle;

            // Assert - Now items are populated
            Assert.True(comboBox.Items.Count > 0);
            Assert.Equal(2, comboBox.Items.Count); // original and ash_dark
        }

        [Fact]
        public void Solution_Set_Selection_After_Adding_To_Parent()
        {
            // This simulates what should happen in ConfigurationForm

            // Arrange
            var form = new Form();
            var panel = new TableLayoutPanel();
            form.Controls.Add(panel);

            var comboBox = new ComboBox();
            object currentTheme = AgriasColorScheme.ash_dark;

            // Set DataSource
            var values = Enum.GetValues(typeof(AgriasColorScheme));
            comboBox.DataSource = values;

            // Add to parent first (this is important!)
            panel.Controls.Add(comboBox);

            // Force handle creation
            var handle = comboBox.Handle;

            // Now find and set the selection
            for (int i = 0; i < values.Length; i++)
            {
                if (values.GetValue(i).Equals(currentTheme))
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }

            // Assert
            Assert.Equal(AgriasColorScheme.ash_dark, comboBox.SelectedItem);

            form.Dispose();
        }
    }
}