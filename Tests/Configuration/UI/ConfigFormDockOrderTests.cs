using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Configuration.UI;
using Xunit;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    public class ConfigFormDockOrderTests
    {
        [Fact]
        [STAThread]
        public void ContentPanel_IsNotCoveredByTitleBar()
        {
            // Arrange
            using var form = new ConfigurationForm(new Config());

            // Act - force layout so Dock positions are resolved
            var _ = form.Handle;
            form.PerformLayout();

            var titleBar = form.Controls.OfType<CustomTitleBar>().Single();
            var contentPanel = form.Controls.OfType<Panel>()
                .Single(p => p is not CustomTitleBar && p.Dock == DockStyle.Fill);

            // Assert - the Fill-docked content panel must start below the title bar,
            // not underneath it (dock layout processes back-to-front; the Fill panel
            // must be in front of the Top-docked title bar to sit below it).
            Assert.True(contentPanel.Top >= titleBar.Bottom,
                $"Content panel Top ({contentPanel.Top}) should be >= title bar Bottom ({titleBar.Bottom})");
        }
    }
}
