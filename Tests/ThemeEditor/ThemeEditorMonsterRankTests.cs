using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Xunit;
using FFTColorCustomizer.ThemeEditor;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    // --- CC-27: the editor must be able to reach ranks II and III, not just rank I ---
    //
    // A monster family's three ranks share one sprite bin as palettes 0/1/2. The dropdown used
    // to list one entry per family and always edit palette 0, so a player designing a Black
    // Chocobo theme was silently shown and given the Yellow Chocobo's colours to edit. These
    // tests drive the real ThemeEditorPanel end to end (mapping load, sprite load, dropdown
    // selection) the way the existing panel tests do, using the real "Chocobo" family from
    // MonsterThemeRegistry so the dropdown labels match production. Owner follow-up: each rank
    // now carries a "(rank N)" suffix ("Black Chocobo (rank 2)"). A non-selectable per-family
    // divider was tried too, then dropped again for cluttering the dropdown; the existing
    // "── Monsters ──" group header above the whole section already marks it as a group.
    public class ThemeEditorMonsterRankTests : IDisposable
    {
        private readonly string _mappingsDir;
        private readonly string _spritesDir;

        public ThemeEditorMonsterRankTests()
        {
            _mappingsDir = Path.Combine(Path.GetTempPath(), "ThemeEditorMonsterRank_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_mappingsDir, "Monster"));

            var chocoboMapping = @"{
  ""job"": ""Chocobo"",
  ""sprite"": ""battle_cyoko_spr.bin"",
  ""sections"": [
    { ""name"": ""Primary"", ""displayName"": ""Primary Color"", ""indices"": [1], ""roles"": [""base""], ""primaryIndex"": 1 }
  ]
}";
            File.WriteAllText(Path.Combine(_mappingsDir, "Monster", "Chocobo.json"), chocoboMapping);

            _spritesDir = Path.Combine(Path.GetTempPath(), "ThemeEditorMonsterRankSprites_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_spritesDir, "sprites_original"));
            File.WriteAllBytes(Path.Combine(_spritesDir, "sprites_original", "battle_cyoko_spr.bin"), WriteTwoRankBin());
        }

        public void Dispose()
        {
            try { Directory.Delete(_mappingsDir, true); } catch { }
            try { Directory.Delete(_spritesDir, true); } catch { }
        }

        /// <summary>Bin whose palette 0 index 1 is RED (rank I) and palette 1 index 1 is GREEN (rank II).</summary>
        private static byte[] WriteTwoRankBin()
        {
            var data = new byte[512 + 1024];
            data[2] = 0x1F; data[3] = 0x00;      // palette 0 index 1 -> red
            data[34] = 0xE0; data[35] = 0x03;    // palette 1 index 1 -> green (1*32 + 1*2)
            return data;
        }

        [Fact]
        [STAThread]
        public void Selecting_a_rank_two_monster_edits_that_ranks_palette_not_rank_one()
        {
            using var panel = new ThemeEditorPanel(_mappingsDir, _spritesDir, null);
            var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");

            dropdown.SelectedItem = "Black Chocobo (rank 2)"; // rank II display name from MonsterThemeRegistry

            Assert.NotNull(panel.PaletteModifier);
            Assert.Equal(1, panel.PaletteModifier!.PaletteIndex);

            var color = panel.PaletteModifier.GetPaletteColor(1);
            Assert.True(color.G > color.R,
                $"expected rank II's green, got R={color.R} G={color.G} B={color.B} — wiring reverted to always-rank-I");

            // The Compare pane's baseline must show the SAME rank the user is editing —
            // otherwise editing a Black Chocobo shows the Yellow Chocobo alongside it.
            Assert.NotNull(panel.OriginalPaletteModifier);
            Assert.Equal(1, panel.OriginalPaletteModifier!.PaletteIndex);

            var originalColor = panel.OriginalPaletteModifier.GetPaletteColor(1);
            Assert.True(originalColor.G > originalColor.R,
                $"expected the Compare pane to show rank II's green too, got R={originalColor.R} G={originalColor.G} B={originalColor.B}");
        }

        [Fact]
        [STAThread]
        public void Saving_a_rank_two_theme_writes_rank_twos_colours_into_the_first_32_bytes()
        {
            using var panel = new ThemeEditorPanel(_mappingsDir, _spritesDir, null);
            var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
            dropdown.SelectedItem = "Black Chocobo (rank 2)";

            ThemeSavedEventArgs? saved = null;
            panel.ThemeSaved += (s, e) => saved = (ThemeSavedEventArgs)e;

            var themeNameInput = panel.Controls.Find("ThemeNameInput", true).OfType<TextBox>().Single();
            themeNameInput.Text = "Test Theme";
            var saveButton = panel.Controls.Find("SaveButton", true).OfType<Button>().Single();
            saveButton.PerformClick();

            Assert.NotNull(saved);
            // Saved theme key is family-scoped (tier-agnostic) so all three ranks share it.
            Assert.Equal("Chocobo", saved!.JobName);

            var lo = saved.PaletteData[1 * 2];
            var hi = saved.PaletteData[1 * 2 + 1];
            var bgr555 = (ushort)(lo | (hi << 8));
            int g5 = (bgr555 >> 5) & 0x1F;
            int r5 = bgr555 & 0x1F;
            Assert.True(g5 > r5, "saved palette bytes 0-31 should carry rank II's (green) colour, not rank I's (red)");
        }

        [Fact]
        [STAThread]
        public void Loading_a_non_monster_template_still_defaults_to_palette_index_zero()
        {
            var genericMappingsDir = Path.Combine(Path.GetTempPath(), "ThemeEditorGenericJob_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(genericMappingsDir);
            var genericSpritesDir = Path.Combine(Path.GetTempPath(), "ThemeEditorGenericJobSprites_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(genericSpritesDir, "sprites_original"));
            try
            {
                var mapping = @"{
  ""job"": ""Squire_Male"",
  ""sprite"": ""battle_mina_m_spr.bin"",
  ""sections"": [
    { ""name"": ""Primary"", ""displayName"": ""Primary Color"", ""indices"": [1], ""roles"": [""base""], ""primaryIndex"": 1 }
  ]
}";
                File.WriteAllText(Path.Combine(genericMappingsDir, "Squire_Male.json"), mapping);
                File.WriteAllBytes(Path.Combine(genericSpritesDir, "sprites_original", "battle_mina_m_spr.bin"), WriteTwoRankBin());

                using var panel = new ThemeEditorPanel(genericMappingsDir, genericSpritesDir, null);
                var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");

                dropdown.SelectedItem = "Squire (Male)";

                Assert.NotNull(panel.PaletteModifier);
                Assert.Equal(0, panel.PaletteModifier!.PaletteIndex);

                // Rank I colours (red) unchanged for a generic job.
                var color = panel.PaletteModifier.GetPaletteColor(1);
                Assert.True(color.R > color.G, $"expected untouched rank I red, got R={color.R} G={color.G} B={color.B}");
            }
            finally
            {
                try { Directory.Delete(genericMappingsDir, true); } catch { }
                try { Directory.Delete(genericSpritesDir, true); } catch { }
            }
        }

        /// <summary>Recursively enumerates a control tree, including nested containers.</summary>
        private static IEnumerable<Control> AllDescendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var grandchild in AllDescendants(child))
                    yield return grandchild;
            }
        }

        [Fact]
        [STAThread]
        public void Editing_a_colour_on_a_rank_two_monster_writes_rank_twos_bytes_not_rank_ones()
        {
            using var panel = new ThemeEditorPanel(_mappingsDir, _spritesDir, null);
            var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
            dropdown.SelectedItem = "Black Chocobo (rank 2)"; // rank II
            Assert.Equal(1, panel.PaletteModifier!.PaletteIndex);

            var picker = AllDescendants(panel).OfType<HslColorPicker>().Single(p => p.SectionName == "Primary Color");

            var workingDataField = typeof(PaletteModifier).GetField("_workingData", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(workingDataField);
            var before = (byte[])((byte[])workingDataField!.GetValue(panel.PaletteModifier)!).Clone();

            // Edit the colour, WITHOUT resetting afterwards — proves the write itself, not a
            // write that a later (offset-correct) reset happens to paper over.
            picker.SetColor(Color.Blue);

            var after = (byte[])workingDataField.GetValue(panel.PaletteModifier)!;

            // Section "Primary" is index 1. Rank II lives at palette 1 (offset 32 + 1*2 = 34-35);
            // rank I lives at palette 0 (offset 1*2 = 2-3).
            var rankTwoBefore = (ushort)(before[34] | (before[35] << 8));
            var rankTwoAfter = (ushort)(after[34] | (after[35] << 8));
            var rankOneBefore = (ushort)(before[2] | (before[3] << 8));
            var rankOneAfter = (ushort)(after[2] | (after[3] << 8));

            Assert.NotEqual(rankTwoBefore, rankTwoAfter); // the edit landed in the SELECTED rank...
            Assert.Equal(rankOneBefore, rankOneAfter);    // ...and did NOT leak into the untouched rank
        }

        [Fact]
        [STAThread]
        public void ClickingResetAll_OnARankTwoMonster_ReloadsThatRanksModifiers()
        {
            using var panel = new ThemeEditorPanel(_mappingsDir, _spritesDir, null);
            var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
            dropdown.SelectedItem = "Black Chocobo (rank 2)"; // rank II

            // Skip the real modal confirmation dialog, which would block the test.
            panel.ConfirmResetAll = () => true;

            // No slider ever moves in this test, so HslColorPicker.OnResetClick's own guard
            // ("nothing changed, nothing to reset") means the per-section reset path never runs
            // here at all. The reason a colour check alone still cannot distinguish "reloaded"
            // from "never touched" is simpler: the INITIAL load already left rank II's green in
            // place, so the colour matches rank II whether or not the click does anything. Capture
            // the instance so a missing reload (the object never gets replaced) is provably caught.
            var modifierBeforeReset = panel.PaletteModifier;

            var resetButton = panel.Controls.OfType<Button>().Single(b => b.Name == "ResetButton");
            resetButton.PerformClick();

            Assert.NotSame(modifierBeforeReset, panel.PaletteModifier); // proves a reload actually ran
            Assert.NotNull(panel.PaletteModifier);
            Assert.Equal(1, panel.PaletteModifier!.PaletteIndex);
            var color = panel.PaletteModifier.GetPaletteColor(1);
            Assert.True(color.G > color.R,
                $"expected rank II's green after clicking Reset All, got R={color.R} G={color.G} B={color.B}");
        }

        [Fact]
        [STAThread]
        public void ClickingResetAll_WhenUserDeclines_LeavesEverythingUnchanged()
        {
            using var panel = new ThemeEditorPanel(_mappingsDir, _spritesDir, null);
            var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
            dropdown.SelectedItem = "Black Chocobo (rank 2)"; // rank II

            panel.ConfirmResetAll = () => false; // user clicks No

            var themeNameInput = panel.Controls.Find("ThemeNameInput", true).OfType<TextBox>().Single();
            themeNameInput.Text = "My Theme";

            var modifierBeforeClick = panel.PaletteModifier;

            var resetButton = panel.Controls.OfType<Button>().Single(b => b.Name == "ResetButton");
            resetButton.PerformClick();

            // Declining must be a true no-op: nothing reloaded, nothing cleared. Without the
            // theme-name assertion this is vacuous the same way the Yes-path test nearly was —
            // the reference check alone would still pass if the handler only skipped the
            // reload but still wiped the theme name and colours in between.
            Assert.Same(modifierBeforeClick, panel.PaletteModifier);
            Assert.Equal("My Theme", themeNameInput.Text);
        }

        [Fact]
        [STAThread]
        public void Resetting_a_section_on_a_rank_two_monster_restores_that_ranks_colour_not_rank_one()
        {
            using var panel = new ThemeEditorPanel(_mappingsDir, _spritesDir, null);
            var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
            dropdown.SelectedItem = "Black Chocobo (rank 2)"; // rank II

            var picker = AllDescendants(panel).OfType<HslColorPicker>().Single(p => p.SectionName == "Primary Color");

            // Simulate the user dragging the sliders away from the loaded (rank II / green) colour...
            picker.SetColor(Color.Blue);
            Assert.Equal(1, panel.PaletteModifier!.PaletteIndex); // selection itself must not have moved

            // ...then clicking that section's reset button.
            picker.ResetToOriginal();

            Assert.Equal(1, panel.PaletteModifier!.PaletteIndex);
            var color = panel.PaletteModifier.GetPaletteColor(1);
            Assert.True(color.G > color.R,
                $"expected rank II's green restored, got R={color.R} G={color.G} B={color.B} (still blue = reset no-op, red = fell back to rank I)");
        }

        [Fact]
        [STAThread]
        public void ReloadModifiersFromCurrentSprite_AppliesTheSelectedMonstersRankToBothModifiers()
        {
            // Drives the reload seam directly, independent of ClickingResetAll_... above (which
            // proves OnResetAllClick reaches this method at all). This is the sole test asserting
            // rank II on BOTH PaletteModifier and OriginalPaletteModifier for the two loads inside.
            using var panel = new ThemeEditorPanel(_mappingsDir, _spritesDir, null);
            var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");
            dropdown.SelectedItem = "Black Chocobo (rank 2)"; // rank II

            var method = typeof(ThemeEditorPanel).GetMethod("ReloadModifiersFromCurrentSprite", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method); // the shared reload seam must exist

            method!.Invoke(panel, null);

            Assert.NotNull(panel.PaletteModifier);
            Assert.Equal(1, panel.PaletteModifier!.PaletteIndex);
            var color = panel.PaletteModifier.GetPaletteColor(1);
            Assert.True(color.G > color.R,
                $"expected rank II's green on PaletteModifier, got R={color.R} G={color.G} B={color.B}");

            Assert.NotNull(panel.OriginalPaletteModifier);
            Assert.Equal(1, panel.OriginalPaletteModifier!.PaletteIndex);
            var originalColor = panel.OriginalPaletteModifier.GetPaletteColor(1);
            Assert.True(originalColor.G > originalColor.R,
                $"expected rank II's green on OriginalPaletteModifier, got R={originalColor.R} G={originalColor.G} B={originalColor.B}");
        }

        [Fact]
        [STAThread]
        public void All_dropdown_display_names_are_unique_across_every_group()
        {
            // Real production mapping content (42 generic jobs, 13 story characters, 12 NPCs,
            // 4 WotL entries, 16 monster families x 3 ranks = 48 monster rows), copied to the
            // test output alongside the assembly. Only SELECTABLE names need to be unique,
            // since a display name is what _displayNameToJobName is keyed by — the group
            // separators are excluded on purpose (the "==" check is a harmless no-op now that
            // per-family dividers were tried and dropped again; left in as a cheap safety net).
            var mappingsDir = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data", "SectionMappings");

            using var panel = new ThemeEditorPanel(mappingsDir);
            var dropdown = panel.Controls.OfType<ComboBox>().First(c => c.Name == "TemplateDropdown");

            var items = dropdown.Items.Cast<object>().Select(i => i.ToString()!).ToList();
            var selectable = items.Where(s => !s.StartsWith("──") && !s.StartsWith("=="));

            var duplicates = selectable.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(duplicates.Count == 0, "Duplicate template dropdown display names: " + string.Join(", ", duplicates));
        }
    }
}
