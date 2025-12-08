using Xunit;
using FluentAssertions;

namespace FFTColorMod.Tests
{
    public class ColorSchemeCyclerTests
    {
        [Fact]
        public void GetNextScheme_CyclesFromOriginalToCorpseBrigade()
        {
            // TLDR: Test cycling from original to corpse_brigade
            var cycler = new ColorSchemeCycler();
            cycler.SetCurrentScheme("original");

            string next = cycler.GetNextScheme();

            next.Should().Be("corpse_brigade");
        }

        [Fact]
        public void GetNextScheme_CyclesFromCorpseBrigadeToLucavi()
        {
            // TLDR: Test cycling from corpse_brigade to next color scheme
            var cycler = new ColorSchemeCycler();
            cycler.SetCurrentScheme("corpse_brigade");

            string next = cycler.GetNextScheme();

            next.Should().Be("lucavi");
        }

        [Fact]
        public void GetNextScheme_WrapsAroundToOriginal()
        {
            // TLDR: Test cycling wraps back to beginning
            var cycler = new ColorSchemeCycler();
            cycler.SetCurrentScheme("southern_sky");

            string next = cycler.GetNextScheme();

            next.Should().Be("original");
        }

        [Fact]
        public void GetAvailableSchemes_ReturnsAllSchemes()
        {
            // TLDR: Test that we can get list of all available schemes
            var cycler = new ColorSchemeCycler();

            var schemes = cycler.GetAvailableSchemes();

            schemes.Should().Contain("original");
            schemes.Should().Contain("corpse_brigade");
            schemes.Should().HaveCountGreaterThan(2);
        }

        [Fact]
        public void GetCurrentScheme_ReturnsCurrentScheme()
        {
            // TLDR: Test that we can get the current scheme
            var cycler = new ColorSchemeCycler();
            cycler.SetCurrentScheme("lucavi");

            string current = cycler.GetCurrentScheme();

            current.Should().Be("lucavi");
        }
    }
}