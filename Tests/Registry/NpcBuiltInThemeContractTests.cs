using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;

namespace Tests.Registry
{
    /// <summary>
    /// Contract for the built-in NPC colour themes.
    ///
    /// A theme is authored ONCE as a recipe under Data/BuiltInThemes/NPC/&lt;Character&gt;.json:
    /// a theme name plus the base colour the owner picked for each section they changed.
    /// tools/ThemeBaker turns that recipe into the shipped sprite bin. These tests are the
    /// anti-drift gate: they re-bake the recipe here using the SAME production classes the
    /// theme editor uses (SectionMappingLoader + PaletteModifier + RelativeShadeGenerator)
    /// and require the shipped bytes to match. If the baker, the shade maths, or a section
    /// mapping ever changes without the bin being regenerated, this goes red.
    /// </summary>
    public class NpcBuiltInThemeContractTests
    {
        private static string RepoRoot()
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."),
                Directory.GetCurrentDirectory(),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(Path.Combine(c, "ColorMod", "Data", "StoryCharacters.json")))
                    return c;
            }
            throw new InvalidOperationException("Repo root with ColorMod/Data/StoryCharacters.json not found");
        }

        private static string UnitPath() => Path.Combine(
            RepoRoot(), "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit");

        private static string RecipeDir() => Path.Combine(
            RepoRoot(), "ColorMod", "Data", "BuiltInThemes", "NPC");

        public sealed record Recipe(string Character, string Theme, Dictionary<string, string> Sections);

        /// <summary>Every (character, theme) recipe that ships, read off disk.</summary>
        public static TheoryData<string, string> AllRecipes()
        {
            var data = new TheoryData<string, string>();
            foreach (var r in LoadRecipes())
                data.Add(r.Character, r.Theme);
            return data;
        }

        private static List<Recipe> LoadRecipes()
        {
            var recipes = new List<Recipe>();
            if (!Directory.Exists(RecipeDir()))
                return recipes;

            foreach (var file in Directory.GetFiles(RecipeDir(), "*.json").OrderBy(f => f))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var character = doc.RootElement.GetProperty("character").GetString()!;
                foreach (var theme in doc.RootElement.GetProperty("themes").EnumerateArray())
                {
                    var sections = new Dictionary<string, string>();
                    foreach (var s in theme.GetProperty("sections").EnumerateObject())
                        sections[s.Name] = s.Value.GetString()!;
                    recipes.Add(new Recipe(character, theme.GetProperty("name").GetString()!, sections));
                }
            }
            return recipes;
        }

        private static Recipe Find(string character, string theme) =>
            LoadRecipes().Single(r => r.Character == character && r.Theme == theme);

        private static SectionMapping Mapping(string character) =>
            SectionMappingLoader.LoadFromFile(Path.Combine(
                RepoRoot(), "ColorMod", "Data", "SectionMappings", "NPC", $"{character}.json"));

        private static string OriginalBin(string character, string sprite) =>
            StoryCharacterSpritePathResolver.ResolveSpritePath(UnitPath(), character, sprite);

        private static string ThemedBin(string character, string theme, string sprite) => Path.Combine(
            UnitPath(), $"sprites_{character.ToLower()}_{theme}", sprite);

        /// <summary>
        /// Every sprite the character draws from. Characters like Alma own three bins and
        /// the sprite manager applies all of them, so a theme that bakes only the first
        /// reverts to vanilla in her other appearances with nothing logged.
        /// </summary>
        private static string[] Sprites(string character) => Mapping(character).Sprites;

        /// <summary>
        /// Re-bakes the recipe with the production theme-editor classes. This is deliberately
        /// the same call sequence the editor performs when the owner drags a slider and saves.
        /// </summary>
        private static byte[] Bake(Recipe recipe, string sprite)
        {
            var mapping = Mapping(recipe.Character);
            var modifier = new PaletteModifier();
            modifier.LoadTemplate(OriginalBin(recipe.Character, sprite), recipe.Character);

            foreach (var pair in recipe.Sections)
            {
                var section = mapping.Sections.SingleOrDefault(s => s.Name == pair.Key);
                Assert.True(section != null,
                    $"{recipe.Character} recipe names section '{pair.Key}', " +
                    $"which is not in its section mapping");
                modifier.ApplySectionColor(section!, ColorTranslator.FromHtml(pair.Value));
            }

            var tmp = Path.Combine(Path.GetTempPath(), $"FFTCC_Bake_{Guid.NewGuid()}.bin");
            try
            {
                modifier.SaveToFile(tmp);
                return File.ReadAllBytes(tmp);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        [Theory]
        [MemberData(nameof(AllRecipes))]
        public void BuiltInTheme_Is_Listed_In_The_Character_Registry(string character, string theme)
        {
            var service = new CharacterDefinitionService();
            service.LoadFromJson(Path.Combine(RepoRoot(), "ColorMod", "Data", "StoryCharacters.json"));

            var definition = service.GetCharacterByName(character);
            Assert.NotNull(definition);
            Assert.Contains(theme, definition!.AvailableThemes);
        }

        [Theory]
        [MemberData(nameof(AllRecipes))]
        public void BuiltInTheme_Ships_A_Sprite_Bin(string character, string theme)
        {
            foreach (var sprite in Sprites(character))
            {
                var path = ThemedBin(character, theme, sprite);
                Assert.True(File.Exists(path), $"Built-in theme bin was never baked: {path}");
            }
        }

        [Theory]
        [MemberData(nameof(AllRecipes))]
        public void BuiltInTheme_Bin_Matches_A_Fresh_Bake_Of_Its_Recipe(string character, string theme)
        {
            var recipe = Find(character, theme);
            foreach (var sprite in Sprites(character))
            {
                var shipped = File.ReadAllBytes(ThemedBin(character, theme, sprite));
                var rebaked = Bake(recipe, sprite);

                Assert.True(shipped.SequenceEqual(rebaked),
                    $"{character}/{theme} ({sprite}): the shipped bin no longer matches its recipe. " +
                    $"Re-run tools/ThemeBaker (see docs/ADDING_AN_NPC_THEME.md).");
            }
        }

        /// <summary>
        /// Non-vacuity: a recipe that changed nothing would satisfy every test above while
        /// shipping a byte-identical copy of vanilla. The palette must actually move.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllRecipes))]
        public void BuiltInTheme_Actually_Recolours_Something(string character, string theme)
        {
            foreach (var sprite in Sprites(character))
            {
                var original = File.ReadAllBytes(OriginalBin(character, sprite));
                var shipped = File.ReadAllBytes(ThemedBin(character, theme, sprite));

                Assert.Equal(original.Length, shipped.Length);
                Assert.False(original.SequenceEqual(shipped),
                    $"{character}/{theme} ({sprite}) is byte-identical to the original sprite");
            }
        }

        /// <summary>
        /// Sections the owner did not touch must survive untouched, and nothing outside the
        /// 512-byte palette block may move. This is what makes "if the slider is not in the
        /// screenshot, it did not change" an enforceable statement rather than a hope.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllRecipes))]
        public void BuiltInTheme_Leaves_Untouched_Sections_And_Pixels_Alone(string character, string theme)
        {
            var recipe = Find(character, theme);
            var mapping = Mapping(character);

            var changed = mapping.Sections
                .Where(s => recipe.Sections.ContainsKey(s.Name))
                .SelectMany(s => s.Indices)
                .ToHashSet();

            foreach (var sprite in Sprites(character))
            {
                var original = File.ReadAllBytes(OriginalBin(character, sprite));
                var shipped = File.ReadAllBytes(ThemedBin(character, theme, sprite));

                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == shipped[i]) continue;

                    Assert.True(i < 32,
                        $"{character}/{theme} ({sprite}) changed byte 0x{i:X} outside palette 0");
                    Assert.Contains(i / 2, changed);
                }
            }
        }
    }
}
