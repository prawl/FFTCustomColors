using System.Windows.Forms;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Tests.Helpers;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    public class ConfigWindowTopMostTests
    {
        [Fact]
        public void ConfigurationForm_Should_Be_TopMost()
        {
            // Arrange & Act
            // Create form without showing it
            TestConfigurationForm? form = null;
            try
            {
                form = new TestConfigurationForm(new Config());

                // Assert
                Assert.True(form.TopMost, "Configuration form should be TopMost to appear above game window");
            }
            finally
            {
                // Clean up without showing
                form?.Dispose();
            }
        }
    }
}
