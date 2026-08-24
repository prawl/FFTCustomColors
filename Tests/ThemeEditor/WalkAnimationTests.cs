using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    // --- CC-36: "Animate" checkbox, walk-cycle preview in the Theme Editor ---
    //
    // Standard humanoid HD sheets are 512x512, 4bpp indexed, 64x80 cells, 8 columns per row.
    // Row 1 holds the walk stride: cols 0-2 are the front-facing step (A, passing, B), cols
    // 3-6 are the back-facing step (A, passing, B, passing). Non-standard sheets (Construct8,
    // Chocobo, every explicit-rect monster) have no derivable walk frames, so they never
    // support animation. See docs comment on FrameLayout.SupportsWalkCycle for the full
    // derivation.
    public class WalkAnimationTests
    {
        [Fact]
        public void FrameLayout_SupportsWalkCycle_TrueOnlyForStandardHumanoidLayout()
        {
            FrameLayout.Standard.SupportsWalkCycle.Should().BeTrue();

            FrameLayout.For("Goblin").SupportsWalkCycle.Should().BeFalse();
            FrameLayout.For("Chocobo").SupportsWalkCycle.Should().BeFalse();
            FrameLayout.For("Construct8").SupportsWalkCycle.Should().BeFalse();
        }

        [Fact]
        public void FrameLayout_WalkCells_FrontCycle_0_1_2_1_and_BackCycle_3_4_5_6_OnRow1()
        {
            var layout = FrameLayout.Standard;

            layout.GetWalkCell(Direction.SW, 0).Should().Be(new Point(0, 1));
            layout.GetWalkCell(Direction.SW, 1).Should().Be(new Point(1, 1));
            layout.GetWalkCell(Direction.SW, 2).Should().Be(new Point(2, 1));
            layout.GetWalkCell(Direction.SW, 3).Should().Be(new Point(1, 1));

            layout.GetWalkCell(Direction.NW, 0).Should().Be(new Point(3, 1));
            layout.GetWalkCell(Direction.NW, 1).Should().Be(new Point(4, 1));
            layout.GetWalkCell(Direction.NW, 2).Should().Be(new Point(5, 1));
            layout.GetWalkCell(Direction.NW, 3).Should().Be(new Point(6, 1));
        }

        /// <summary>
        /// LOAD-BEARING: proves ExtractWalkFrame actually samples the row-1 stride cells the
        /// derivation above claims, not just a compile-time contract. A synthetic sheet carries
        /// a distinct marker color at the origin pixel of every row-1 cell; asserting the exact
        /// walk-cycle order catches a flipped column or a wrong row.
        /// </summary>
        [Fact]
        public void ExtractWalkFrame_SamplesTheRow1StrideCells()
        {
            var extractor = new SpriteSheetExtractor();
            var sheet = new Bitmap(512, 512, PixelFormat.Format32bppArgb);
            sheet.SetPixel(0, 80, Color.Red);
            sheet.SetPixel(64, 80, Color.Lime);
            sheet.SetPixel(128, 80, Color.Blue);
            sheet.SetPixel(192, 80, Color.Yellow);
            sheet.SetPixel(256, 80, Color.Cyan);
            sheet.SetPixel(320, 80, Color.Magenta);
            sheet.SetPixel(384, 80, Color.Orange);

            var layout = FrameLayout.Standard;

            AssertOrigin(extractor.ExtractWalkFrame(sheet, Direction.SW, layout, 0), Color.Red);
            AssertOrigin(extractor.ExtractWalkFrame(sheet, Direction.SW, layout, 1), Color.Lime);
            AssertOrigin(extractor.ExtractWalkFrame(sheet, Direction.SW, layout, 2), Color.Blue);
            AssertOrigin(extractor.ExtractWalkFrame(sheet, Direction.SW, layout, 3), Color.Lime);

            AssertOrigin(extractor.ExtractWalkFrame(sheet, Direction.NW, layout, 0), Color.Yellow);
            AssertOrigin(extractor.ExtractWalkFrame(sheet, Direction.NW, layout, 1), Color.Cyan);
            AssertOrigin(extractor.ExtractWalkFrame(sheet, Direction.NW, layout, 2), Color.Magenta);
            AssertOrigin(extractor.ExtractWalkFrame(sheet, Direction.NW, layout, 3), Color.Orange);
        }

        private static void AssertOrigin(Bitmap frame, Color expected)
        {
            var pixel = frame.GetPixel(0, 0);
            pixel.R.Should().Be(expected.R);
            pixel.G.Should().Be(expected.G);
            pixel.B.Should().Be(expected.B);
        }

        [Fact]
        public void ExtractWalkFrame_SE_MirrorsSW_NE_MirrorsNW()
        {
            var extractor = new SpriteSheetExtractor();
            var sheet = new Bitmap(512, 512, PixelFormat.Format32bppArgb);
            sheet.SetPixel(0, 80, Color.Red);     // SW walk step A origin
            sheet.SetPixel(192, 80, Color.Yellow); // NW walk step A origin

            var layout = FrameLayout.Standard;

            var seFrame = extractor.ExtractWalkFrame(sheet, Direction.SE, layout, 0);
            var sePixel = seFrame.GetPixel(63, 0);
            sePixel.R.Should().Be(255);
            sePixel.G.Should().Be(0);
            sePixel.B.Should().Be(0);

            var neFrame = extractor.ExtractWalkFrame(sheet, Direction.NE, layout, 0);
            var nePixel = neFrame.GetPixel(63, 0);
            nePixel.R.Should().Be(255);
            nePixel.G.Should().Be(255);
            nePixel.B.Should().Be(0);
        }

        [Fact]
        public void ExtractWalkFrame_ReturnsNull_ForNonWalkLayouts()
        {
            var extractor = new SpriteSheetExtractor();
            var sheet = new Bitmap(512, 512, PixelFormat.Format32bppArgb);

            var rectsLayout = FrameLayout.Rects(0, 0, 46, 78, 205, 4, 46, 78);
            extractor.ExtractWalkFrame(sheet, Direction.SW, rectsLayout, 0).Should().BeNull();

            var construct8Layout = FrameLayout.For("Construct8");
            extractor.ExtractWalkFrame(sheet, Direction.SW, construct8Layout, 0).Should().BeNull();
        }

        /// <summary>
        /// Builds a modPath/Images/&lt;jobName&gt;/original/*.bmp fixture tall enough to carry
        /// both the standing row (row 0) and the walk-stride row (row 1), mirroring
        /// PaletteModifierTests.WriteHdPreviewFixture. Optionally paints one marker pixel at
        /// palette index 2 (even x -> high nibble, matching the existing fixture's convention).
        /// </summary>
        private static string WriteWalkHdFixture(string modPath, string jobName, string binPath, int? markerX = null, int? markerY = null)
        {
            var bmpDir = Path.Combine(modPath, "Images", jobName, "original");
            Directory.CreateDirectory(bmpDir);

            const int sheetWidth = 512;
            const int sheetHeight = 192; // covers row 0 (y 0-79) and row 1 (y 80-159)
            using (var bmp = new Bitmap(sheetWidth, sheetHeight, PixelFormat.Format4bppIndexed))
            {
                var rect = new Rectangle(0, 0, sheetWidth, sheetHeight);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format4bppIndexed);
                try
                {
                    var buffer = new byte[data.Stride * sheetHeight];
                    if (markerX.HasValue && markerY.HasValue)
                    {
                        int byteIndex = markerY.Value * data.Stride + (markerX.Value >> 1);
                        // Palette index 2: even x -> high nibble, odd x -> low nibble.
                        buffer[byteIndex] = (markerX.Value % 2 == 0) ? (byte)0x20 : (byte)0x02;
                    }
                    Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
                }
                finally { bmp.UnlockBits(data); }

                bmp.Save(Path.Combine(bmpDir, "0_test_hd.bmp"), ImageFormat.Bmp);
            }

            var binData = new byte[512];
            binData[4] = 0x1F; binData[5] = 0x00; // palette 0 index 2 -> red
            File.WriteAllBytes(binPath, binData);

            return bmpDir;
        }

        [Fact]
        public void SupportsWalkAnimation_RequiresHdSheetAndStandardLayout()
        {
            var modPath = Path.Combine(Path.GetTempPath(), "WalkAnimSupports_" + Guid.NewGuid().ToString("N"));
            var binPathNoHd = Path.Combine(Path.GetTempPath(), "walk_anim_test_" + Guid.NewGuid().ToString("N") + ".bin");
            var binPathKnight = Path.Combine(Path.GetTempPath(), "walk_anim_test_" + Guid.NewGuid().ToString("N") + ".bin");
            var binPathConstruct8 = Path.Combine(Path.GetTempPath(), "walk_anim_test_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                // (a) no jobName/modPath -> no HD path at all -> not supported.
                File.WriteAllBytes(binPathNoHd, new byte[512]);
                var noHdModifier = new PaletteModifier();
                noHdModifier.LoadTemplate(binPathNoHd);
                noHdModifier.SupportsWalkAnimation.Should().BeFalse();

                // (b) HD sheet + standard layout -> supported.
                WriteWalkHdFixture(modPath, "TestKnight", binPathKnight);
                var knightModifier = new PaletteModifier();
                knightModifier.LoadTemplate(binPathKnight, "TestKnight", modPath);
                knightModifier.SupportsWalkAnimation.Should().BeTrue();

                // (c) HD sheet present but the job's layout is non-standard -> not supported.
                WriteWalkHdFixture(modPath, "Construct8", binPathConstruct8);
                var construct8Modifier = new PaletteModifier();
                construct8Modifier.LoadTemplate(binPathConstruct8, "Construct8", modPath);
                construct8Modifier.SupportsWalkAnimation.Should().BeFalse();
            }
            finally
            {
                if (File.Exists(binPathNoHd)) File.Delete(binPathNoHd);
                if (File.Exists(binPathKnight)) File.Delete(binPathKnight);
                if (File.Exists(binPathConstruct8)) File.Delete(binPathConstruct8);
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);
            }
        }

        [Fact]
        public void GetWalkPreviewFrames_UsesLivePalette_AndReturnsFourFrames()
        {
            var modPath = Path.Combine(Path.GetTempPath(), "WalkAnimFrames_" + Guid.NewGuid().ToString("N"));
            var binPath = Path.Combine(Path.GetTempPath(), "walk_anim_frames_test_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                // Marker at (0, 80): front step A's origin pixel, palette index 2.
                WriteWalkHdFixture(modPath, "TestKnight", binPath, markerX: 0, markerY: 80);

                var modifier = new PaletteModifier();
                modifier.LoadTemplate(binPath, "TestKnight", modPath); // bin palette 0 index 2 -> red

                var frames = modifier.GetWalkPreviewFrames(5); // direction 5 = SW
                frames.Should().NotBeNull();
                frames.Count.Should().Be(FrameLayout.WalkPhaseCount);

                var pixel = frames[0].GetPixel(0, 0);
                pixel.R.Should().Be(255);
                pixel.G.Should().Be(0);
                pixel.B.Should().Be(0);

                // Live-edit the color and re-fetch: the animation must follow the edit.
                modifier.SetPaletteColor(2, Color.Blue);
                var updatedFrames = modifier.GetWalkPreviewFrames(5);
                var updatedPixel = updatedFrames[0].GetPixel(0, 0);
                updatedPixel.R.Should().Be(0);
                updatedPixel.G.Should().Be(0);
                updatedPixel.B.Should().Be(255);
            }
            finally
            {
                if (File.Exists(binPath)) File.Delete(binPath);
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);
            }
        }

        /// <summary>
        /// Full panel fixture: a generic job ("Knight_Male") with a mapping, a sprite bin under
        /// FFTIVC/data/enhanced/fftpack/unit/sprites_original/ (the exact layout ThemeEditorPanel's
        /// GetModPath() walks back up from) and an Images/Knight_Male/original HD sheet, so the
        /// walk-cycle preview actually has something to render. Sections/palette content don't
        /// matter for these tests, which assert animation plumbing (references, phases, running
        /// state), not rendered pixels.
        /// </summary>
        private static (string mappingsDir, string spritesDir, string tempRoot) CreateKnightFixture()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "WalkAnimPanel_" + Guid.NewGuid().ToString("N"));
            var mappingsDir = Path.Combine(tempRoot, "mappings");
            var spritesDir = Path.Combine(tempRoot, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var spritesOriginalDir = Path.Combine(spritesDir, "sprites_original");
            Directory.CreateDirectory(mappingsDir);
            Directory.CreateDirectory(spritesOriginalDir);

            var knightMapping = @"{
  ""job"": ""Knight_Male"",
  ""sprite"": ""battle_knight_m_spr.bin"",
  ""sections"": [
    { ""name"": ""Cape"", ""displayName"": ""Cape"", ""indices"": [3, 4, 5], ""roles"": [""shadow"", ""base"", ""highlight""] }
  ]
}";
            File.WriteAllText(Path.Combine(mappingsDir, "Knight_Male.json"), knightMapping);
            // 512-byte palette + zero sprite data suffices: the HD path never touches bin sprite bytes.
            File.WriteAllBytes(Path.Combine(spritesOriginalDir, "battle_knight_m_spr.bin"), new byte[512]);

            var imagesDir = Path.Combine(tempRoot, "Images", "Knight_Male", "original");
            Directory.CreateDirectory(imagesDir);
            using (var bmp = new Bitmap(512, 192, PixelFormat.Format4bppIndexed))
            {
                var rect = new Rectangle(0, 0, 512, 192);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format4bppIndexed);
                try
                {
                    var buffer = new byte[data.Stride * 192];
                    Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
                }
                finally { bmp.UnlockBits(data); }
                bmp.Save(Path.Combine(imagesDir, "0_test_hd.bmp"), ImageFormat.Bmp);
            }

            return (mappingsDir, spritesDir, tempRoot);
        }

        [Fact]
        [STAThread]
        public void AnimateCheckbox_SitsBesideCompare_DefaultOffAndDisabledWithoutSheets()
        {
            // NpcThemeEditorTests pattern: mappings dir only, no root-level generic job, so the
            // ctor's default "Squire (Male)" auto-select finds nothing and never touches the
            // checkbox. No sprites/Images means no walk support even if a job were selected.
            var tempMappingsDir = Path.Combine(Path.GetTempPath(), "WalkAnimNoSheets_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(tempMappingsDir, "NPC"));
            var almaMapping = @"{
  ""job"": ""Alma"",
  ""sprite"": ""battle_aruma_spr.bin"",
  ""sections"": [
    { ""name"": ""Hair"", ""displayName"": ""Hair"", ""indices"": [1, 2], ""roles"": [""shadow"", ""base""] }
  ]
}";
            File.WriteAllText(Path.Combine(tempMappingsDir, "NPC", "Alma.json"), almaMapping);

            try
            {
                using var panel = new ThemeEditorPanel(tempMappingsDir);

                var animateCheckbox = panel.Controls.Find("AnimateCheckbox", true).OfType<CheckBox>().Single();
                var compareCheckbox = panel.Controls.Find("ShowOriginalCheckbox", true).OfType<CheckBox>().Single();

                animateCheckbox.Checked.Should().BeFalse();
                animateCheckbox.Enabled.Should().BeFalse();
                animateCheckbox.Top.Should().Be(compareCheckbox.Top);
                animateCheckbox.Left.Should().BeGreaterThan(compareCheckbox.Left);
            }
            finally
            {
                Directory.Delete(tempMappingsDir, true);
            }
        }

        [Fact]
        [STAThread]
        public void CheckingAnimate_AnimatesPreview_AndUncheckingRestoresStatic()
        {
            var (mappingsDir, spritesDir, tempRoot) = CreateKnightFixture();
            try
            {
                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir, null);
                var dropdown = panel.Controls.Find("TemplateDropdown", true).OfType<ComboBox>().Single();
                dropdown.SelectedItem = "Knight (Male)";

                var animateCheckbox = panel.Controls.Find("AnimateCheckbox", true).OfType<CheckBox>().Single();
                animateCheckbox.Enabled.Should().BeTrue();

                var spritePreview = panel.Controls.Find("SpritePreview", true).OfType<PictureBox>().Single();
                var animator = panel.WalkAnimatorForTests;

                animateCheckbox.Checked = true;

                animator.IsRunning.Should().BeTrue();
                animator.Phase.Should().Be(0);
                var frame0 = animator.ModifiedFrames[0];
                spritePreview.Image.Should().BeSameAs(frame0);

                animator.AdvanceOnce();
                animator.Phase.Should().Be(1);
                var frame1 = animator.ModifiedFrames[1];
                spritePreview.Image.Should().BeSameAs(frame1);

                animateCheckbox.Checked = false;

                animator.IsRunning.Should().BeFalse();
                animator.Phase.Should().Be(0);
                spritePreview.Image.Should().NotBeNull();
                spritePreview.Image.Should().NotBeSameAs(frame1);
                animator.ModifiedFrames.Should().BeNull();
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        [STAThread]
        public void CompareOn_BothPanesAnimateInStep()
        {
            var (mappingsDir, spritesDir, tempRoot) = CreateKnightFixture();
            try
            {
                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir, null);
                var dropdown = panel.Controls.Find("TemplateDropdown", true).OfType<ComboBox>().Single();
                dropdown.SelectedItem = "Knight (Male)";

                var compareCheckbox = panel.Controls.Find("ShowOriginalCheckbox", true).OfType<CheckBox>().Single();
                var animateCheckbox = panel.Controls.Find("AnimateCheckbox", true).OfType<CheckBox>().Single();
                compareCheckbox.Checked = true;
                animateCheckbox.Checked = true;

                var animator = panel.WalkAnimatorForTests;
                animator.AdvanceOnce();

                var spritePreview = panel.Controls.Find("SpritePreview", true).OfType<PictureBox>().Single();
                var originalSpritePreview = panel.Controls.Find("OriginalSpritePreview", true).OfType<PictureBox>().Single();

                spritePreview.Image.Should().BeSameAs(animator.ModifiedFrames[animator.Phase]);
                originalSpritePreview.Image.Should().BeSameAs(animator.OriginalFrames[animator.Phase]);
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        [STAThread]
        public void SwitchingToTemplateWithoutSheet_UnchecksAndDisablesAnimate()
        {
            var (mappingsDir, spritesDir, tempRoot) = CreateKnightFixture();
            try
            {
                // Archer: mapping + sprite exist, but no Images/Archer_Male folder -> no HD sheet.
                var archerMapping = @"{
  ""job"": ""Archer_Male"",
  ""sprite"": ""battle_archer_m_spr.bin"",
  ""sections"": []
}";
                File.WriteAllText(Path.Combine(mappingsDir, "Archer_Male.json"), archerMapping);
                File.WriteAllBytes(Path.Combine(spritesDir, "sprites_original", "battle_archer_m_spr.bin"), new byte[512]);

                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir, null);
                var dropdown = panel.Controls.Find("TemplateDropdown", true).OfType<ComboBox>().Single();
                var animateCheckbox = panel.Controls.Find("AnimateCheckbox", true).OfType<CheckBox>().Single();

                dropdown.SelectedItem = "Knight (Male)";
                animateCheckbox.Checked = true;
                panel.WalkAnimatorForTests.IsRunning.Should().BeTrue();

                dropdown.SelectedItem = "Archer (Male)";

                panel.WalkAnimatorForTests.IsRunning.Should().BeFalse();
                animateCheckbox.Checked.Should().BeFalse();
                animateCheckbox.Enabled.Should().BeFalse();

                dropdown.SelectedItem = "Knight (Male)";

                animateCheckbox.Enabled.Should().BeTrue();
                animateCheckbox.Checked.Should().BeFalse(); // owner re-checks manually
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        [STAThread]
        public void PanelDispose_StopsAndDisposesTimer()
        {
            var (mappingsDir, spritesDir, tempRoot) = CreateKnightFixture();
            try
            {
                var panel = new ThemeEditorPanel(mappingsDir, spritesDir, null);
                var dropdown = panel.Controls.Find("TemplateDropdown", true).OfType<ComboBox>().Single();
                dropdown.SelectedItem = "Knight (Male)";

                var animateCheckbox = panel.Controls.Find("AnimateCheckbox", true).OfType<CheckBox>().Single();
                animateCheckbox.Checked = true;

                var animator = panel.WalkAnimatorForTests;
                animator.IsRunning.Should().BeTrue();

                panel.Dispose();

                animator.IsRunning.Should().BeFalse();

                var ex = Record.Exception(() => panel.Dispose());
                ex.Should().BeNull();
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Fact]
        [STAThread]
        public void RotatingWhileAnimating_RebuildsFramesForNewFacing()
        {
            var (mappingsDir, spritesDir, tempRoot) = CreateKnightFixture();
            try
            {
                using var panel = new ThemeEditorPanel(mappingsDir, spritesDir, null);
                var dropdown = panel.Controls.Find("TemplateDropdown", true).OfType<ComboBox>().Single();
                dropdown.SelectedItem = "Knight (Male)";

                var animateCheckbox = panel.Controls.Find("AnimateCheckbox", true).OfType<CheckBox>().Single();
                animateCheckbox.Checked = true;

                var animator = panel.WalkAnimatorForTests;
                var framesBefore = animator.ModifiedFrames;

                var spritePreview = (RotatableSpritePictureBox)panel.Controls.Find("SpritePreview", true).OfType<PictureBox>().Single();
                spritePreview.SimulateClickAt(spritePreview.Width - 10, spritePreview.Height / 2); // right third -> rotate right

                animator.IsRunning.Should().BeTrue();
                animator.ModifiedFrames.Should().NotBeSameAs(framesBefore);
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
