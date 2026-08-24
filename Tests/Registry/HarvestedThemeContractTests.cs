using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorCustomizer.ThemeEditor;
using Newtonsoft.Json.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.Registry
{
    /// <summary>
    /// CC-31 contract. A harvested theme promotes one of the game's own unused palette slots into
    /// slot 0 verbatim. That makes it cheap to pin exactly: re-derive every shipped harvested bin
    /// from the vanilla original with the production <see cref="PaletteHarvester"/> and compare
    /// bytes. If a shipped bin is ever hand-edited, or a recipe's sourceSlot is changed without a
    /// re-bake, these go red by name.
    ///
    /// The non-vacuity check matters most here: the census that produced this set found three
    /// candidate slots that were byte-identical or near-identical to slot 0, which would have
    /// shipped a dropdown entry that changes nothing a player can see.
    /// </summary>
    public class HarvestedThemeContractTests
    {
        private const int Block = PaletteHarvester.PaletteBlockSize;
        private const int Stride = PaletteHarvester.PaletteStride;

        private static string RepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "FFTColorCustomizer.Tests.csproj")))
                dir = Path.GetDirectoryName(dir);
            Assert.False(dir == null, "Could not locate the repo root from the test output directory.");
            return dir;
        }

        private static string UnitPath() => Path.Combine(
            RepoRoot(), "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit");

        private static string RecipeDir() => Path.Combine(
            RepoRoot(), "ColorMod", "Data", "BuiltInThemes", "Harvested");

        /// <summary>character, themeName, sourceSlot, spriteStem</summary>
        public static IEnumerable<object[]> EveryHarvestedSprite()
        {
            var registry = JObject.Parse(File.ReadAllText(
                Path.Combine(RepoRoot(), "ColorMod", "Data", "StoryCharacters.json")));

            foreach (var file in Directory.GetFiles(RecipeDir(), "*.json").OrderBy(f => f))
            {
                var recipe = JObject.Parse(File.ReadAllText(file));
                var character = (string)recipe["character"];

                var entry = registry["characters"]
                    .OfType<JObject>()
                    .SingleOrDefault(c => (string)c["name"] == character);
                Assert.True(entry != null, $"{character} has a harvested recipe but is not in StoryCharacters.json");

                foreach (var theme in recipe["themes"].OfType<JObject>())
                    foreach (var stem in entry["spriteNames"].Select(s => (string)s))
                        yield return new object[]
                        {
                            character, (string)theme["name"], (int)theme["sourceSlot"], stem
                        };
            }
        }

        private static (byte[] original, byte[] shipped) Load(
            string character, string themeName, string stem)
        {
            var bin = $"battle_{stem}_spr.bin";
            var original = Path.Combine(UnitPath(), "sprites_original", bin);
            var shipped = Path.Combine(
                UnitPath(), $"sprites_{character.ToLowerInvariant()}_{themeName}", bin);

            Assert.True(File.Exists(original), $"Vanilla original missing: {original}");
            Assert.True(File.Exists(shipped), $"Shipped harvested bin missing: {shipped}");
            return (File.ReadAllBytes(original), File.ReadAllBytes(shipped));
        }

        [Theory]
        [MemberData(nameof(EveryHarvestedSprite))]
        public void Shipped_bin_is_exactly_what_the_production_harvester_produces(
            string character, string themeName, int sourceSlot, string stem)
        {
            var (original, shipped) = Load(character, themeName, stem);

            var rebaked = (byte[])original.Clone();
            PaletteHarvester.PromoteSlotToZero(rebaked, sourceSlot);

            Assert.Equal(rebaked, shipped);
        }

        [Theory]
        [MemberData(nameof(EveryHarvestedSprite))]
        public void Shipped_slot_zero_is_the_vanilla_source_slot_verbatim(
            string character, string themeName, int sourceSlot, string stem)
        {
            var (original, shipped) = Load(character, themeName, stem);

            Assert.Equal(
                original.Skip(sourceSlot * Stride).Take(Stride).ToArray(),
                shipped.Take(Stride).ToArray());
        }

        [Theory]
        [MemberData(nameof(EveryHarvestedSprite))]
        public void Harvest_is_not_vacuous_it_actually_changes_slot_zero(
            string character, string themeName, int sourceSlot, string stem)
        {
            var (original, shipped) = Load(character, themeName, stem);

            Assert.NotEqual(original.Take(Stride).ToArray(), shipped.Take(Stride).ToArray());
        }

        [Theory]
        [MemberData(nameof(EveryHarvestedSprite))]
        public void Everything_outside_slot_zero_is_untouched(
            string character, string themeName, int sourceSlot, string stem)
        {
            var (original, shipped) = Load(character, themeName, stem);

            Assert.Equal(original.Length, shipped.Length);
            Assert.Equal(original.Skip(Stride).ToArray(), shipped.Skip(Stride).ToArray());
        }

        [Theory]
        [MemberData(nameof(EveryHarvestedSprite))]
        public void Theme_is_registered_in_the_character_registry(
            string character, string themeName, int sourceSlot, string stem)
        {
            var registry = JObject.Parse(File.ReadAllText(
                Path.Combine(RepoRoot(), "ColorMod", "Data", "StoryCharacters.json")));

            var entry = registry["characters"].OfType<JObject>()
                .Single(c => (string)c["name"] == character);
            var themes = entry["availableThemes"].Select(t => (string)t).ToList();

            Assert.Contains(themeName, themes);
            Assert.Equal("original", themes[0]);
        }
    }
}
