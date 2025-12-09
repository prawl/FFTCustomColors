using System;
using System.IO;
using Xunit;
using FluentAssertions;

namespace FFTColorMod.Tests
{
    public class ColorSchemeCyclerTests : IDisposable
    {
        private readonly string _testPath;

        public ColorSchemeCyclerTests()
        {
            // Create test directory structure with expected schemes
            _testPath = Path.Combine(Path.GetTempPath(), $"test_cycler_{Path.GetRandomFileName()}");
            Directory.CreateDirectory(_testPath);
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_original"));
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_amethyst"));
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_corpse_brigade"));
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_lucavi"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
                Directory.Delete(_testPath, true);
        }

        [Fact]
        public void GetNextScheme_CyclesFromOriginalToAmethyst()
        {
            // TLDR: Test cycling from original to amethyst (first alphabetically after original)
            var cycler = new ColorSchemeCycler(_testPath);
            cycler.SetCurrentScheme("original");

            string next = cycler.GetNextScheme();

            next.Should().Be("amethyst");
        }

        [Fact]
        public void GetNextScheme_CyclesFromCorpseBrigadeToLucavi()
        {
            // TLDR: Test cycling from corpse_brigade to lucavi
            var cycler = new ColorSchemeCycler(_testPath);
            cycler.SetCurrentScheme("corpse_brigade");

            string next = cycler.GetNextScheme();

            next.Should().Be("lucavi");
        }

        [Fact]
        public void GetNextScheme_WrapsAroundToOriginal()
        {
            // TLDR: Test cycling wraps back to beginning
            var cycler = new ColorSchemeCycler(_testPath);
            cycler.SetCurrentScheme("lucavi"); // Last scheme alphabetically

            string next = cycler.GetNextScheme();

            next.Should().Be("original"); // First scheme
        }

        [Fact]
        public void GetAvailableSchemes_ReturnsAllSchemes()
        {
            // TLDR: Test that we can get list of all available schemes
            var cycler = new ColorSchemeCycler(_testPath);

            var schemes = cycler.GetAvailableSchemes();

            schemes.Should().Contain("original");
            schemes.Should().Contain("corpse_brigade");
            schemes.Should().HaveCount(4); // We created 4 test directories
        }

        [Fact]
        public void GetCurrentScheme_ReturnsCurrentScheme()
        {
            // TLDR: Test that we can get the current scheme
            var cycler = new ColorSchemeCycler(_testPath);
            cycler.SetCurrentScheme("lucavi");

            string current = cycler.GetCurrentScheme();

            current.Should().Be("lucavi");
        }
    }
}