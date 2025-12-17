using System;
using System.Windows.Forms;
using System.Linq;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    /// <summary>
    /// Tests to verify that ThemeComboBox properly displays formatted theme names
    /// </summary>
    [Collection("WinForms")]
    public class ThemeComboBoxFormattingTests : IDisposable
    {
        private readonly Form _form;
        private readonly ThemeComboBox _comboBox;

        public ThemeComboBoxFormattingTests()
        {
            _form = new Form();
            _comboBox = new ThemeComboBox();
            _form.Controls.Add(_comboBox);

            // Force handle creation
            var handle = _form.Handle;
        }

        public void Dispose()
        {
            _comboBox?.Dispose();
            _form?.Dispose();
        }

        [Fact]
        public void ThemeComboBox_Should_Display_All_Theme_Names_Capitalized()
        {
            // Arrange
            var themes = new[]
            {
                "original",
                "corpse_brigade",
                "lucavi",
                "northern_sky",
                "southern_sky",
                "crimson_red",
                "royal_purple",
                "phoenix_flame",
                "frost_knight",
                "silver_knight",
                "emerald_dragon",
                "rose_gold",
                "ocean_depths",
                "golden_templar",
                "blood_moon",
                "celestial",
                "volcanic",
                "amethyst"
            };

            var expectedDisplayNames = new[]
            {
                "Original",
                "Corpse Brigade",
                "Lucavi",
                "Northern Sky",
                "Southern Sky",
                "Crimson Red",
                "Royal Purple",
                "Phoenix Flame",
                "Frost Knight",
                "Silver Knight",
                "Emerald Dragon",
                "Rose Gold",
                "Ocean Depths",
                "Golden Templar",
                "Blood Moon",
                "Celestial",
                "Volcanic",
                "Amethyst"
            };

            // Act
            _comboBox.SetThemes(themes);

            // Assert
            _comboBox.Items.Count.Should().Be(themes.Length);

            for (int i = 0; i < _comboBox.Items.Count; i++)
            {
                var displayedText = _comboBox.Items[i].ToString();
                displayedText.Should().Be(expectedDisplayNames[i],
                    $"Theme '{themes[i]}' should be displayed as '{expectedDisplayNames[i]}'");
            }
        }

        [Fact]
        public void ThemeComboBox_Should_Show_Formatted_Name_In_Text_Property()
        {
            // Arrange
            var themes = new[] { "corpse_brigade", "northern_sky", "blood_moon" };
            _comboBox.SetThemes(themes);

            // Act
            _comboBox.SelectedThemeValue = "northern_sky";

            // Assert
            _comboBox.Text.Should().Be("Northern Sky",
                "Selected theme should display formatted name in Text property");
        }

        [Fact]
        public void ThemeComboBox_Should_Return_Internal_Value_Despite_Display_Formatting()
        {
            // Arrange
            var themes = new[] { "corpse_brigade", "northern_sky", "blood_moon" };
            _comboBox.SetThemes(themes);

            // Act - Select by internal value
            _comboBox.SelectedThemeValue = "corpse_brigade";

            // Assert
            _comboBox.Text.Should().Be("Corpse Brigade", "Display should be formatted");
            _comboBox.SelectedThemeValue.Should().Be("corpse_brigade", "Internal value should be preserved");
        }

        [Fact]
        public void ThemeComboBox_Should_Handle_Story_Character_Theme_Names()
        {
            // Arrange - Story character specific themes
            var storyThemes = new[]
            {
                "original",
                "ash_dark",
                "knights_round",
                "sephiroth_black"
            };

            var expectedDisplayNames = new[]
            {
                "Original",
                "Ash Dark",
                "Knights Round",
                "Sephiroth Black"
            };

            // Act
            _comboBox.SetThemes(storyThemes);

            // Assert
            for (int i = 0; i < _comboBox.Items.Count; i++)
            {
                var displayedText = _comboBox.Items[i].ToString();
                displayedText.Should().Be(expectedDisplayNames[i],
                    $"Story theme '{storyThemes[i]}' should be displayed as '{expectedDisplayNames[i]}'");
            }
        }

        [Fact]
        public void ThemeComboBox_Dropdown_Should_Show_All_Formatted_Names()
        {
            // This test verifies that when the dropdown is opened,
            // all items are displayed with proper formatting

            // Arrange
            var themes = new[]
            {
                "corpse_brigade",
                "lucavi",
                "northern_sky",
                "crimson_red",
                "frost_knight"
            };

            _comboBox.SetThemes(themes);

            // Act - Simulate dropdown opening
            _comboBox.DroppedDown = true;

            // Assert - Check all items in the dropdown list
            var items = _comboBox.Items.Cast<object>().Select(item => item.ToString()).ToList();

            items.Should().NotContain(name => name.Contains("_"),
                "No dropdown items should contain underscores");

            items.Should().Contain("Corpse Brigade");
            items.Should().Contain("Lucavi");
            items.Should().Contain("Northern Sky");
            items.Should().Contain("Crimson Red");
            items.Should().Contain("Frost Knight");

            // Close dropdown
            _comboBox.DroppedDown = false;
        }

        [Theory]
        [InlineData("smoke", "Smoke")]
        [InlineData("aaron", "Aaron")]
        [InlineData("celestial", "Celestial")]
        [InlineData("volcanic", "Volcanic")]
        [InlineData("amethyst", "Amethyst")]
        public void ThemeComboBox_Should_Format_Single_Word_Themes(string internalName, string expectedDisplay)
        {
            // Arrange
            _comboBox.SetThemes(new[] { internalName });

            // Act
            var displayedText = _comboBox.Items[0].ToString();

            // Assert
            displayedText.Should().Be(expectedDisplay,
                $"Single word theme '{internalName}' should be displayed as '{expectedDisplay}'");
        }
    }
}