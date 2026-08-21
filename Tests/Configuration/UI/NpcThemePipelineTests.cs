using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    /// <summary>
    /// End-to-end contract for a freshly authored NPC theme: after the theme editor saves it,
    /// the NPC's config row must PREVIEW it (config window) and the sprite manager must APPLY
    /// it (the .bin the game reads). Both halves resolve the NPC's sprite from the character
    /// registry, so a new roster entry needs no code change.
    ///
    /// The regression these lock down: preview used a hardcoded display-name to sprite-name
    /// table that knew nothing about the NPC roster, so it looked for battle_argath_spr.bin
    /// (does not exist) instead of battle_aru_spr.bin, gave up, and silently rendered vanilla.
    /// </summary>
    [Collection("RegistryTests")]
    public class NpcThemePipelineTests : IDisposable
    {
        private const string Npc = "Argath";
        private const string NpcSprite = "aru";
        private const string ThemeName = "test_theme";

        private readonly string _modPath;
        private readonly string _unitPath;

        public NpcThemePipelineTests()
        {
            _modPath = Path.Combine(Path.GetTempPath(), $"FFTCC_NpcTheme_{Guid.NewGuid()}");
            _unitPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(Path.Combine(_unitPath, "sprites_original"));

            // Registry: the mod reads Data/StoryCharacters.json out of the mod folder.
            Directory.CreateDirectory(Path.Combine(_modPath, "Data"));
            File.Copy(
                Path.Combine(RepoRoot(), "ColorMod", "Data", "StoryCharacters.json"),
                Path.Combine(_modPath, "Data", "StoryCharacters.json"));

            // Vanilla sprite + HD preview sheet, straight from the repo.
            File.Copy(
                Path.Combine(RepoRoot(), "ColorMod", "FFTIVC", "data", "enhanced", "fftpack",
                    "unit", "sprites_original", $"battle_{NpcSprite}_spr.bin"),
                Path.Combine(_unitPath, "sprites_original", $"battle_{NpcSprite}_spr.bin"));

            var imagesSource = Path.Combine(RepoRoot(), "ColorMod", "Images", Npc, "original");
            var imagesDest = Path.Combine(_modPath, "Images", Npc, "original");
            Directory.CreateDirectory(imagesDest);
            foreach (var bmp in Directory.GetFiles(imagesSource, "*_hd.bmp"))
                File.Copy(bmp, Path.Combine(imagesDest, Path.GetFileName(bmp)));

            // A saved user theme: registry row + a palette that is loudly different from vanilla.
            new UserThemeService(_modPath).SaveTheme(Npc, ThemeName, InvertedVanillaPalette());

            CharacterServiceSingleton.SetModPath(_modPath);
            UserThemeServiceSingleton.Initialize(_modPath);
        }

        public void Dispose()
        {
            CharacterServiceSingleton.Reset();
            UserThemeServiceSingleton.Reset();
            try
            {
                if (Directory.Exists(_modPath))
                    Directory.Delete(_modPath, true);
            }
            catch
            {
                // temp cleanup is best effort
            }
        }

        [Fact]
        public void Npc_User_Theme_Preview_Differs_From_Vanilla()
        {
            using var vanilla = RenderRow("original");
            using var themed = RenderRow(ThemeName);

            Assert.NotNull(vanilla);
            Assert.NotNull(themed);
            Assert.False(PixelsEqual(vanilla, themed),
                $"{Npc}'s '{ThemeName}' preview rendered identically to vanilla, so the user " +
                "palette was never applied.");
        }

        [Fact]
        public void Npc_Row_Lists_The_Saved_Theme_Under_My_Themes()
        {
            using var panel = new TableLayoutPanel { ColumnCount = 3, RowCount = 1 };
            BuildRow(panel, "original");

            var combo = panel.Controls.OfType<ThemeComboBox>().Single();

            // "original", the "My Themes" separator, then the saved theme
            Assert.Equal(3, combo.Items.Count);

            combo.SelectedThemeValue = ThemeName;
            Assert.Equal(ThemeName, combo.SelectedThemeValue);
        }

        [Fact]
        public void Npc_User_Theme_Is_Applied_To_The_Sprite_The_Game_Reads()
        {
            var configPath = Path.Combine(_modPath, "Config.json");
            var configManager = new ConfigurationManager(configPath);
            var characterService = new CharacterDefinitionService();
            characterService.LoadFromJson(Path.Combine(_modPath, "Data", "StoryCharacters.json"));

            var config = new Config();
            typeof(Config).GetProperty(Npc)!.SetValue(config, ThemeName);
            configManager.SaveConfig(config);

            new ConfigBasedSpriteManager(_modPath, configManager, characterService).ApplyConfiguration();

            var applied = Path.Combine(_unitPath, $"battle_{NpcSprite}_spr.bin");
            Assert.True(File.Exists(applied),
                $"{Npc}'s themed sprite was never written to {applied}");
            Assert.Equal(
                InvertedVanillaPalette(),
                File.ReadAllBytes(applied).Take(512).ToArray());
        }

        [Theory]
        [InlineData("Argath", "aru")]
        [InlineData("Celia", "seria")]
        [InlineData("Elmdore", "eru")]
        [InlineData("Gaffgarion", "baruna")]
        [InlineData("Isilud", "h76")]
        [InlineData("Lettie", "ledy")]
        [InlineData("Orran", "oran")]
        [InlineData("Ovelia", "hime")]
        [InlineData("Valmafra", "baru")]
        [InlineData("Zalmour", "zarumou")]
        [InlineData("Alma", "aruma")]
        [InlineData("Simon", "simon")]
        // Story characters keep resolving the way they always did.
        [InlineData("Agrias", "aguri")]
        [InlineData("Rapha", "h79")]
        [InlineData("Construct8", "tetsu")]
        [InlineData("RamzaChapter23", "ramuza2")]
        public void Internal_Sprite_Name_Comes_From_The_Registry(string character, string expected)
        {
            var service = new CharacterDefinitionService();
            service.LoadFromJson(Path.Combine(_modPath, "Data", "StoryCharacters.json"));

            Assert.Equal(expected, InternalSpriteNameResolver.Resolve(character, service));
        }

        [Fact]
        public void Names_Outside_The_Registry_Still_Fall_Back_To_The_Alias_Table()
        {
            var empty = new CharacterDefinitionService();

            Assert.Equal("ramuza", InternalSpriteNameResolver.Resolve("Ramza", empty));
            Assert.Equal("dily", InternalSpriteNameResolver.Resolve("Delita", empty));
            Assert.Equal("nobody", InternalSpriteNameResolver.Resolve("Nobody", empty));
        }

        /// <summary>
        /// Builds a real config row for the NPC on the given theme and runs its lazy-load
        /// callback, i.e. exactly what the config window does when the row scrolls into view.
        /// Returns a copy of the rendered preview (the carousel owns and disposes the original).
        /// </summary>
        private Bitmap RenderRow(string theme)
        {
            using var panel = new TableLayoutPanel { ColumnCount = 3, RowCount = 1 };
            var carousel = BuildRow(panel, theme).AllCarousels.Single();
            carousel.LoadImagesCallback?.Invoke(carousel);

            return carousel.Image == null ? null : new Bitmap(carousel.Image);
        }

        /// <summary>Adds the NPC's config-window row to <paramref name="panel"/>, on the given theme.</summary>
        private CharacterRowBuilder BuildRow(TableLayoutPanel panel, string theme)
        {
            var storyControls = new List<Control>();
            var builder = new CharacterRowBuilder(
                panel,
                new PreviewImageManager(_modPath),
                () => false,
                new List<Control>(),
                storyControls);

            builder.AddStoryCharacterRow(0, new StoryCharacterRegistry.StoryCharacterConfig
            {
                Name = Npc,
                PreviewName = Npc,
                EnumType = typeof(string),
                GetValue = () => theme,
                SetValue = _ => { },
                AvailableThemes = new[] { "original" },
                Category = "NPC"
            }, storyControls);

            return builder;
        }

        private byte[] InvertedVanillaPalette()
        {
            var vanilla = File.ReadAllBytes(
                Path.Combine(_unitPath, "sprites_original", $"battle_{NpcSprite}_spr.bin"));
            var palette = new byte[512];
            Array.Copy(vanilla, 0, palette, 0, 512);

            // Flip every colour bit but leave index 0 (the transparency index) alone, so the
            // sprite still has a shape and only its colours move.
            for (int i = 2; i < palette.Length; i++)
                palette[i] ^= 0xFF;

            return palette;
        }

        private static bool PixelsEqual(Bitmap a, Bitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height)
                return false;

            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    if (a.GetPixel(x, y) != b.GetPixel(x, y))
                        return false;

            return true;
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ColorMod", "Data", "StoryCharacters.json")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("Repo root with ColorMod/Data/StoryCharacters.json not found");
        }
    }
}
