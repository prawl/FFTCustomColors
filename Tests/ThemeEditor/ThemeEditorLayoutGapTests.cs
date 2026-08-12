using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    public class ThemeEditorLayoutGapTests
    {
        [Fact]
        [STAThread]
        public void SectionPickers_HaveRenderedGapsBetweenThem()
        {
            // Arrange - mirrors ThemeEditorSectionTests.ThemeEditorPanel_SectionColorPickers_DisplayInJsonOrder:
            // a mapping-only ThemeEditorPanel (no sprite needed) with multiple sections.
            var tempDir = Path.Combine(Path.GetTempPath(), "ThemeEditorLayoutGapTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                var mappingJson = @"{
                    ""job"": ""Test_Job"",
                    ""sprite"": ""test.bin"",
                    ""sections"": [
                        { ""name"": ""First"", ""displayName"": ""First Section"", ""indices"": [1], ""roles"": [""base""] },
                        { ""name"": ""Second"", ""displayName"": ""Second Section"", ""indices"": [2], ""roles"": [""base""] },
                        { ""name"": ""Third"", ""displayName"": ""Third Section"", ""indices"": [3], ""roles"": [""base""] }
                    ]
                }";
                File.WriteAllText(Path.Combine(tempDir, "Test_Job.json"), mappingJson);

                using var panel = new ThemeEditorPanel(tempDir);

                var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
                dropdown.SelectedIndex = dropdown.Items.IndexOf("Test (Job)");

                var colorPickersPanel = panel.Controls.OfType<Panel>().First(c => c.Name == "SectionColorPickersPanel");

                // Guard - the gap assertion is meaningless with fewer than two sections.
                var pickers = colorPickersPanel.Controls.OfType<HslColorPicker>().ToList();
                Assert.True(pickers.Count >= 2, "need at least two sections for a gap assertion");

                // Act - force layout so Top/Bottom positions are resolved
                var _ = panel.Handle;
                panel.PerformLayout();

                var orderedPickers = pickers.OrderBy(p => p.Top).ToList();

                // Assert - every adjacent pair must have a rendered gap of at least 15px
                for (int i = 1; i < orderedPickers.Count; i++)
                {
                    var previous = orderedPickers[i - 1];
                    var next = orderedPickers[i];
                    var gap = next.Top - previous.Bottom;
                    Assert.True(gap >= 15,
                        $"Expected a rendered gap of at least 15px between sections {i - 1} and {i}, was {gap}px");
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void SectionHeader_TallEnoughForItsFont()
        {
            // Arrange & Act
            using var picker = new HslColorPicker();
            var header = (Label)picker.Controls.Find("SectionHeaderLabel", true).Single();

            // Assert - a 13pt bold font (~22px line) plus vertical padding needs more than
            // the old fixed Height = 25, or the label clips even at 100% display scale.
            Assert.True(header.Height >= header.Font.Height + header.Padding.Vertical,
                $"Section header Height ({header.Height}) should be >= Font.Height + Padding.Vertical ({header.Font.Height + header.Padding.Vertical})");
        }
    }
}
