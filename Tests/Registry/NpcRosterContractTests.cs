using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;

namespace Tests.Registry
{
    /// <summary>
    /// Contract for the NPC roster: every NPC character ships fully wired - registry entry
    /// (category NPC, the themes listed in ExpectedThemes), Config property, a per-index section mapping
    /// under NPC/, and an HD preview image. Sprite bin names were verified against the
    /// vanilla game files by exact palette match (2026-08-13).
    /// </summary>
    public class NpcRosterContractTests
    {
        public static TheoryData<string, string[]> Roster => new()
        {
            { "Alma", new[] { "aruma", "gyumu", "h82" } },
            { "Argath", new[] { "aru" } },
            { "Celia", new[] { "seria", "h83" } },
            { "DelitaChapter1", new[] { "dily" } },
            { "DelitaChapter23", new[] { "dily2" } },
            { "DelitaChapter4", new[] { "dily3" } },
            { "Elmdore", new[] { "eru" } },
            { "Gaffgarion", new[] { "baruna", "h61" } },
            { "Isilud", new[] { "h76" } },
            { "Lettie", new[] { "ledy", "arufu" } },
            { "Orran", new[] { "oran" } },
            { "Ovelia", new[] { "hime" } },
            { "Simon", new[] { "simon" } },
            { "Valmafra", new[] { "baru" } },
            { "Zalmour", new[] { "zarumou" } },
        };

        /// <summary>
        /// NPCs shipped with authored colour themes. Everyone not listed here is still
        /// original-only. Adding a theme means adding it here AND to the character registry;
        /// the bytes themselves are pinned by NpcBuiltInThemeContractTests.
        /// </summary>
        private static readonly Dictionary<string, string[]> ExpectedThemes = new()
        {
            { "Valmafra", new[] { "original", "dark_witch" } },
            { "Ovelia", new[] { "original", "fantasia", "scorned_princess" } },
            { "Isilud", new[] { "original", "dusk_dragoon", "night_blade", "gladiator" } },
            { "Alma", new[] { "original", "ballet", "nocturnal_rose", "flower_maid", "wedding", "rain_dancer" } },
            { "Argath", new[] { "original", "red_snake", "water_rat" } },
            { "Elmdore", new[] { "original", "silver_noble" } },
            { "Zalmour", new[] { "original", "pure_saint", "druid" } },
            { "Gaffgarion", new[] { "original", "dark_knight", "white_knight" } },
            { "Orran", new[] { "original", "scholar", "sea_scientist" } },
            { "Simon", new[] { "original", "blood_priest", "bishop" } },
        };

        private static string[] ThemesFor(string name) =>
            ExpectedThemes.TryGetValue(name, out var themes) ? themes : new[] { "original" };

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

        private static CharacterDefinitionService LoadService()
        {
            var service = new CharacterDefinitionService();
            service.LoadFromJson(Path.Combine(RepoRoot(), "ColorMod", "Data", "StoryCharacters.json"));
            return service;
        }

        [Theory]
        [MemberData(nameof(Roster))]
        public void NpcCharacter_Is_Registered_With_Npc_Category_And_Expected_Themes(string name, string[] sprites)
        {
            var character = LoadService().GetCharacterByName(name);

            Assert.NotNull(character);
            Assert.Equal("NPC", character.Category);
            Assert.Equal(ThemesFor(name), character.AvailableThemes);
            Assert.Equal(sprites, character.SpriteNames);
        }

        [Theory]
        [MemberData(nameof(Roster))]
        public void NpcCharacter_Has_Config_Property_Defaulting_Original(string name, string[] _)
        {
            var prop = typeof(Config).GetProperty(name);

            Assert.NotNull(prop);
            Assert.Equal(typeof(string), prop.PropertyType);
            Assert.Equal("original", prop.GetValue(new Config()));
        }

        [Theory]
        [MemberData(nameof(Roster))]
        public void NpcCharacter_Has_PerIndex_Section_Mapping(string name, string[] sprites)
        {
            var mappingPath = Path.Combine(RepoRoot(), "ColorMod", "Data", "SectionMappings", "NPC", $"{name}.json");
            Assert.True(File.Exists(mappingPath), $"Missing NPC section mapping: {mappingPath}");

            var mapping = SectionMappingLoader.LoadFromFile(mappingPath);
            Assert.Equal($"battle_{sprites[0]}_spr.bin", mapping.Sprite);

            // Per-index calibration mappings expose every paintable index (1-15) until the
            // owner hands back groupings; grouped mappings replace them per character later,
            // so only require full coverage while the mapping is still per-index style
            var allIndices = mapping.Sections.SelectMany(s => s.Indices).OrderBy(i => i).ToArray();
            if (mapping.Sections.Length == 15)
            {
                Assert.Equal(Enumerable.Range(1, 15).ToArray(), allIndices);
            }
            Assert.NotEmpty(mapping.Sections);
        }

        [Theory]
        [MemberData(nameof(Roster))]
        public void NpcCharacter_Has_Hd_Preview_Image(string name, string[] _)
        {
            var imagesDir = Path.Combine(RepoRoot(), "ColorMod", "Images", name, "original");
            Assert.True(Directory.Exists(imagesDir), $"Missing preview image folder: {imagesDir}");
            Assert.True(Directory.GetFiles(imagesDir, "*_hd.bmp").Length > 0,
                $"No *_hd.bmp preview in {imagesDir}");
        }
    }
}
