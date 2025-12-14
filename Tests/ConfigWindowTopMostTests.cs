using System.Windows.Forms;
using FFTColorMod.Configuration;
using Xunit;

namespace FFTColorMod.Tests
{
    public class ConfigWindowTopMostTests
    {
        [Fact]
        public void ConfigurationForm_Should_Be_TopMost()
        {
            // Arrange & Act
            var form = new ConfigurationForm(new Config());

            // Assert
            Assert.True(form.TopMost, "Configuration form should be TopMost to appear above game window");
        }
    }
}