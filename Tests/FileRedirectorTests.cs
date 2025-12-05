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

            redirector.SetActiveColorScheme(ColorScheme.OceanBlue);
            Assert.Equal(ColorScheme.OceanBlue, redirector.ActiveScheme);

            redirector.SetActiveColorScheme(ColorScheme.WhiteSilver);
            Assert.Equal(ColorScheme.WhiteSilver, redirector.ActiveScheme);
        }

        [Fact]
        public void GetRedirectedPath_Should_Return_Color_Variant_Path()
        {
            // TLDR: GetRedirectedPath returns path with color suffix for active scheme
            var redirector = new FileRedirector();
            redirector.SetActiveColorScheme(ColorScheme.OceanBlue);

            var result = redirector.GetRedirectedPath("sprites/ramza.pac");

            Assert.Equal("sprites/ramza_ocean_blue.pac", result);
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