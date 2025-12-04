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

        [Fact]
        public void GetRedirectedPath_Should_Return_Color_Variant_Path()
        {
            // TLDR: GetRedirectedPath returns path with color suffix for active scheme
            var redirector = new FileRedirector();
            redirector.SetActiveColorScheme(ColorScheme.Blue);

            var result = redirector.GetRedirectedPath("sprites/ramza.pac");

            Assert.Equal("sprites/ramza_blue.pac", result);
        }

        [Fact]
        public void GetRedirectedPath_Should_Return_Original_Path_For_Original_Scheme()
        {
            // TLDR: GetRedirectedPath returns original path when scheme is Original
            var redirector = new FileRedirector();
            // Default is Original

            var result = redirector.GetRedirectedPath("sprites/ramza.pac");

            Assert.Equal("sprites/ramza.pac", result);
        }
    }
}