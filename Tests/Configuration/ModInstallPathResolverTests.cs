using System;
using System.IO;
using System.Text.RegularExpressions;
using FFTColorCustomizer.Configuration;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    /// <summary>
    /// CC-12: the Reloaded-II Configure Mod button flow used to hard-code the sprite target
    /// as &lt;reloadedRoot&gt;/Mods/FFTColorCustomizer, which is wrong for every release install
    /// (paxtrick.fft.colorcustomizer, or Vortex's FFTColorCustomizer-56-x-y-z-* names): the
    /// config saved, the UI reported success, and zero sprites were copied. The resolver must
    /// trust where the mod's own DLL actually runs from, never a guessed folder name.
    /// </summary>
    public class ModInstallPathResolverTests
    {
        [Fact]
        public void Prefers_the_executing_assembly_directory()
        {
            var result = ModInstallPathResolver.Resolve(
                @"C:\Reloaded\Mods\paxtrick.fft.colorcustomizer",
                @"C:\SomeOther\ModFolder");
            Assert.Equal(@"C:\Reloaded\Mods\paxtrick.fft.colorcustomizer", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Falls_back_to_ModFolder_when_assembly_dir_is_unusable(string? assemblyDir)
        {
            var result = ModInstallPathResolver.Resolve(assemblyDir, @"C:\Reloaded\Mods\FFTColorCustomizer-56-3-1-0-123");
            Assert.Equal(@"C:\Reloaded\Mods\FFTColorCustomizer-56-3-1-0-123", result);
        }

        [Fact]
        public void Returns_empty_when_neither_source_is_usable()
        {
            Assert.Equal(string.Empty, ModInstallPathResolver.Resolve(null, null));
            Assert.Equal(string.Empty, ModInstallPathResolver.Resolve(" ", ""));
        }

        [Fact]
        public void Resolver_never_invents_a_folder_name()
        {
            // A release install named paxtrick.fft.colorcustomizer must come back verbatim;
            // nothing may rewrite it toward the legacy dev name.
            var result = ModInstallPathResolver.Resolve(
                @"D:\Games\Reloaded\Mods\paxtrick.fft.colorcustomizer", null);
            Assert.DoesNotContain(@"Mods\FFTColorCustomizer" + Path.DirectorySeparatorChar, result + Path.DirectorySeparatorChar);
            Assert.EndsWith("paxtrick.fft.colorcustomizer", result);
        }

        /// <summary>
        /// Source-scan regression pin (the TodoContractTests/LogContractTests idiom): the
        /// hard-coded Path.Combine(reloadedRoot, "Mods", "FFTColorCustomizer") compose must
        /// never return to Configurator.cs.
        /// </summary>
        [Fact]
        public void Configurator_source_no_longer_hardcodes_the_dev_folder_name()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            string? repoRoot = null;
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "ColorMod")) &&
                    File.Exists(Path.Combine(dir.FullName, "ColorMod", "Configuration", "Configurator.cs")))
                {
                    repoRoot = dir.FullName;
                    break;
                }
                dir = dir.Parent;
            }
            Assert.NotNull(repoRoot);

            string source = File.ReadAllText(Path.Combine(repoRoot!, "ColorMod", "Configuration", "Configurator.cs"));
            Assert.False(
                Regex.IsMatch(source, @"Combine\([^)]*""Mods""\s*,\s*""FFTColorCustomizer"""),
                "Configurator.cs composes a hard-coded Mods/FFTColorCustomizer path again (CC-12 regression)");
        }
    }
}
