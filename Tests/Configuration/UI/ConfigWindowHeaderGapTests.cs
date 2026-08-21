using System.Linq;
using System.Windows.Forms;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Tests.Helpers;

namespace Tests.Configuration.UI
{
    // --- Owner cosmetic pass: too much dead space between the title bar and "Themes" ---
    //
    // Measured from a live screenshot: the 30px title bar ends at y47, but the "Themes"
    // heading did not start rendering until roughly y62 -- about 15px of unexplained gap,
    // made up of _mainPanel's own top padding (10) plus the header label's own top padding
    // (5). Shrinking just the panel's top inset (leaving left/right/bottom alone, since every
    // other row still wants that breathing room) visibly tightens the header without touching
    // anything else's spacing.
    public class ConfigWindowHeaderGapTests
    {
        [Fact]
        public void MainPanel_TopPadding_IsReducedButOtherSidesAreUnchanged()
        {
            using var form = new TestConfigurationForm(new Config());
            var handle = form.Handle;

            var mainPanel = GetPrivateField<TableLayoutPanel>(form, "_mainPanel");

            Assert.True(mainPanel.Padding.Top < 10,
                $"expected the panel's top padding to be reduced below its old 10px, got {mainPanel.Padding.Top}");
            Assert.Equal(10, mainPanel.Padding.Left);
            Assert.Equal(10, mainPanel.Padding.Right);
            Assert.Equal(10, mainPanel.Padding.Bottom);
        }

        [Fact]
        public void ThemesHeading_RendersCloserToTheTopOfMainPanel()
        {
            using var form = new TestConfigurationForm(new Config());
            var handle = form.Handle;
            form.PerformLayout();

            var mainPanel = GetPrivateField<TableLayoutPanel>(form, "_mainPanel");
            var headerLabel = mainPanel.Controls.OfType<Label>().Single(l => l.Text == "Themes");

            // Old layout put the header's own top around 10px into the panel (the panel's old
            // uniform 10px padding). It must now sit measurably closer to the panel's top edge.
            Assert.True(headerLabel.Top < 10,
                $"expected \"Themes\" to render closer to the panel's top edge, got Top={headerLabel.Top}");
        }

        private static T GetPrivateField<T>(object target, string name) where T : class
        {
            var field = typeof(ConfigurationForm).GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            return field!.GetValue(target) as T;
        }
    }
}
