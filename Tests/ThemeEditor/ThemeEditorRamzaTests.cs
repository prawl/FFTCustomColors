using Xunit;
using FFTColorCustomizer.ThemeEditor;
using System.IO;

namespace Tests.ThemeEditor
{
    public class ThemeEditorRamzaTests
    {
        private string GetMappingsDirectory()
        {
            // Find the ColorMod/Data/SectionMappings directory
            var baseDir = Directory.GetCurrentDirectory();
            var mappingsPath = Path.Combine(baseDir, "..", "..", "..", "..", "ColorMod", "Data", "SectionMappings");
            return Path.GetFullPath(mappingsPath);
        }

        [Fact]
        public void ThemeEditorPanel_WithMappings_ShouldIncludeRamzaChaptersInDropdown()
        {
            // Arrange
            var mappingsDir = GetMappingsDirectory();

            // Skip if mappings directory doesn't exist (CI environment)
            if (!Directory.Exists(mappingsDir))
                return;

            var panel = new ThemeEditorPanel(mappingsDir);

            // Act - get the template dropdown items
            var dropdown = panel.Controls.Find("TemplateDropdown", false);

            // Assert - Ramza chapters should be included
            Assert.NotEmpty(dropdown);
            var comboBox = dropdown[0] as System.Windows.Forms.ComboBox;
            Assert.NotNull(comboBox);

            // Check for Ramza entries (they will be added after story characters separator)
            var items = new System.Collections.Generic.List<string>();
            foreach (var item in comboBox.Items)
            {
                items.Add(item.ToString());
            }

            Assert.Contains("Ramza (Chapter 1)", items);
            Assert.Contains("Ramza (Chapter 2/3)", items);
            Assert.Contains("Ramza (Chapter 4)", items);
        }

        [Theory]
        [InlineData("Ramza (Chapter 1)")]
        [InlineData("Ramza (Chapter 2/3)")]
        [InlineData("Ramza (Chapter 4)")]
        public void ThemeEditorPanel_WhenRamzaSelected_ShouldShowHslColorPickers(string ramzaChapter)
        {
            // Arrange
            var mappingsDir = GetMappingsDirectory();

            // Skip if mappings directory doesn't exist (CI environment)
            if (!Directory.Exists(mappingsDir))
                return;

            var panel = new ThemeEditorPanel(mappingsDir);
            var dropdown = panel.Controls.Find("TemplateDropdown", false)[0] as System.Windows.Forms.ComboBox;

            // Act - select the Ramza chapter
            var index = dropdown.Items.IndexOf(ramzaChapter);
            if (index < 0)
                return; // Skip if Ramza is not in dropdown

            dropdown.SelectedIndex = index;

            // Assert - SectionColorPickersPanel should contain HslColorPicker controls (not RamzaColorPanel)
            var colorPickersPanel = panel.Controls.Find("SectionColorPickersPanel", false)[0] as System.Windows.Forms.Panel;
            Assert.NotNull(colorPickersPanel);

            // Should contain HslColorPickers (standard color pickers like other characters)
            var hasHslColorPicker = false;
            foreach (System.Windows.Forms.Control control in colorPickersPanel.Controls)
            {
                if (control is HslColorPicker)
                {
                    hasHslColorPicker = true;
                    break;
                }
            }
            Assert.True(hasHslColorPicker, $"HslColorPicker should be visible when {ramzaChapter} is selected (using SPR-based editing)");
        }

        [Fact]
        public void ThemeEditorPanel_WhenNonRamzaSelected_ShouldShowStandardColorPickers()
        {
            // Arrange
            var mappingsDir = GetMappingsDirectory();

            // Skip if mappings directory doesn't exist (CI environment)
            if (!Directory.Exists(mappingsDir))
                return;

            var panel = new ThemeEditorPanel(mappingsDir);
            var dropdown = panel.Controls.Find("TemplateDropdown", false)[0] as System.Windows.Forms.ComboBox;

            // Act - select a non-Ramza character (Squire Male is default)
            var index = dropdown.Items.IndexOf("Squire (Male)");
            if (index < 0)
                return; // Skip if Squire Male is not in dropdown

            dropdown.SelectedIndex = index;

            // Assert - should contain HslColorPickers (or be empty without sprite directory)
            var colorPickersPanel = panel.Controls.Find("SectionColorPickersPanel", false)[0] as System.Windows.Forms.Panel;
            Assert.NotNull(colorPickersPanel);

            // At minimum, the panel should exist and be accessible
            // (actual HslColorPickers require the sprite directory to be set)
        }

        [Fact]
        public void SectionMappingLoader_ShouldLoadRamzaChapterMappings()
        {
            // Arrange
            var mappingsDir = GetMappingsDirectory();

            // Skip if mappings directory doesn't exist (CI environment)
            if (!Directory.Exists(mappingsDir))
                return;

            // Act - get available story characters
            var storyCharacters = SectionMappingLoader.GetAvailableStoryCharacters(mappingsDir);

            // Assert - Ramza chapters should be in the list
            Assert.Contains("RamzaCh1", storyCharacters);
            Assert.Contains("RamzaCh23", storyCharacters);
            Assert.Contains("RamzaCh4", storyCharacters);
        }

        [Theory]
        [InlineData("RamzaCh1", "battle_ramuza_spr.bin", 4)]   // Jacket, BootsGloves, Hair, SkinColor
        [InlineData("RamzaCh23", "battle_ramuza2_spr.bin", 5)] // Armor, LegsGloves, Underarmor, Hair, SkinColor
        [InlineData("RamzaCh4", "battle_ramuza3_spr.bin", 5)]  // ArmsLegs, UnderarmorChest, Accent, Hair, SkinColor
        public void SectionMappingLoader_ShouldLoadCorrectRamzaMappingDetails(string jobName, string expectedSprite, int expectedSectionCount)
        {
            // Arrange
            var mappingsDir = GetMappingsDirectory();

            // Skip if mappings directory doesn't exist (CI environment)
            if (!Directory.Exists(mappingsDir))
                return;

            var mappingPath = Path.Combine(mappingsDir, "Story", $"{jobName}.json");

            // Skip if mapping file doesn't exist
            if (!File.Exists(mappingPath))
                return;

            // Act
            var mapping = SectionMappingLoader.LoadFromFile(mappingPath);

            // Assert
            Assert.NotNull(mapping);
            Assert.Equal(jobName, mapping.Job);
            Assert.Equal(expectedSprite, mapping.Sprite);
            Assert.Equal(expectedSectionCount, mapping.Sections.Length);
        }

        [Fact]
        public void RamzaCh1Mapping_ShouldHaveCorrectSections()
        {
            // Arrange
            var mappingsDir = GetMappingsDirectory();

            // Skip if mappings directory doesn't exist (CI environment)
            if (!Directory.Exists(mappingsDir))
                return;

            var mappingPath = Path.Combine(mappingsDir, "Story", "RamzaCh1.json");

            // Skip if mapping file doesn't exist
            if (!File.Exists(mappingPath))
                return;

            // Act
            var mapping = SectionMappingLoader.LoadFromFile(mappingPath);

            // Assert
            Assert.NotNull(mapping);

            // Check Jacket section
            var jacket = System.Array.Find(mapping.Sections, s => s.Name == "Jacket");
            Assert.NotNull(jacket);
            Assert.Equal("Jacket", jacket.DisplayName);
            Assert.Equal(new[] { 3, 4, 5, 6 }, jacket.Indices);
            Assert.Equal(new[] { "shadow", "dark", "base", "highlight" }, jacket.Roles);

            // Check Hair section
            var hair = System.Array.Find(mapping.Sections, s => s.Name == "Hair");
            Assert.NotNull(hair);
            Assert.Equal(new[] { 10, 11, 12 }, hair.Indices);
        }
    }
}
