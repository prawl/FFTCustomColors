using System;
using System.IO;
using Xunit;

namespace FFTColorMod.Tests
{
    public class FileRedirectorTests
    {
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // TLDR: FileRedirector can be instantiated
            var redirector = new FileRedirector();
            Assert.NotNull(redirector);
        }

        [Fact]
        public void SetActiveColorScheme_ChangesScheme()
        {
            // TLDR: Can switch between color schemes
            var redirector = new FileRedirector();

            redirector.SetActiveColorScheme(ColorScheme.Blue);
            Assert.Equal(ColorScheme.Blue, redirector.ActiveScheme);

            redirector.SetActiveColorScheme(ColorScheme.Red);
            Assert.Equal(ColorScheme.Red, redirector.ActiveScheme);
        }
    }
}