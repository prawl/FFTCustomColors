using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    // --- CC-46: walk-cycle preview for monsters, Chocobo and Construct 8 ---
    //
    // BATTLE.BIN's sprite-attributes table (0x2D748) assigns every sprite one of the game's
    // animation rigs. Goblin and Skeleton are TYPE1 and Mindflayer is TYPE2 (their sheets are
    // standard humanoid layouts, so the existing WalkCycle compositor covers them); Chocobo is
    // CYOKO and the other twelve families plus Construct 8 are MON, whose walk tables live in
    // MonsterWalkCycle (parsed from MON/CYOKO SEQ+SHP exactly like CC-36 parsed TYPE1/TYPE2).
    // Position asserts, not presence asserts: a tile that exists but lands in the wrong place
    // passed CC-36's round-3 blind spot, so every check below pins coordinates.
    public class MonsterWalkAnimationTests
    {
        // ------------------------------------------------------------------
        // Baked tables match the parsed SEQ/SHP data
        // ------------------------------------------------------------------

        [Fact]
        public void MonsterWalkCycle_MonMatchesAuthoritativeSeqData()
        {
            // MON walk beats are one 48x48 SPR tile each (96x96 HD), all sharing the same
            // anchor, so every draw lands at the frame origin. Animation 6 (front): SHP frames
            // 2,8,1,8,2,9,5,9. Animation 7 (back): 4,10,7,10,4,11,24,11.
            MonsterWalkCycle.FrameSize(WalkCycleType.Mon).Should().Be(new Size(96, 96));

            var frontSources = MonsterWalkCycle.MonFront.Select(b => b.Single().Source).ToArray();
            frontSources.Should().Equal(
                new Rectangle(96, 0, 96, 96),    // f2
                new Rectangle(0, 96, 96, 96),    // f8
                new Rectangle(0, 0, 96, 96),     // f1
                new Rectangle(0, 96, 96, 96),    // f8
                new Rectangle(96, 0, 96, 96),    // f2
                new Rectangle(96, 96, 96, 96),   // f9
                new Rectangle(192, 0, 96, 96),   // f5
                new Rectangle(96, 96, 96, 96));  // f9

            var backSources = MonsterWalkCycle.MonBack.Select(b => b.Single().Source).ToArray();
            backSources.Should().Equal(
                new Rectangle(288, 0, 96, 96),   // f4
                new Rectangle(192, 96, 96, 96),  // f10
                new Rectangle(384, 0, 96, 96),   // f7
                new Rectangle(192, 96, 96, 96),  // f10
                new Rectangle(288, 0, 96, 96),   // f4
                new Rectangle(288, 96, 96, 96),  // f11
                new Rectangle(384, 288, 96, 96), // f24
                new Rectangle(288, 96, 96, 96)); // f11

            foreach (var beat in MonsterWalkCycle.MonFront.Concat(MonsterWalkCycle.MonBack))
            {
                beat.Single().Offset.Should().Be(new Point(0, 0));
                beat.Single().FlipX.Should().BeFalse();
            }

            // Palindrome identities shared with the humanoid walk: beat3 == beat1,
            // beat4 == beat0, beat7 == beat5.
            MonsterWalkCycle.MonFront[3].Should().BeSameAs(MonsterWalkCycle.MonFront[1]);
            MonsterWalkCycle.MonFront[4].Should().BeSameAs(MonsterWalkCycle.MonFront[0]);
            MonsterWalkCycle.MonFront[7].Should().BeSameAs(MonsterWalkCycle.MonFront[5]);
            MonsterWalkCycle.MonBack[3].Should().BeSameAs(MonsterWalkCycle.MonBack[1]);
            MonsterWalkCycle.MonBack[4].Should().BeSameAs(MonsterWalkCycle.MonBack[0]);
            MonsterWalkCycle.MonBack[7].Should().BeSameAs(MonsterWalkCycle.MonBack[5]);

            MonsterWalkCycle.For(Direction.SW, WalkCycleType.Mon).Should().BeSameAs(MonsterWalkCycle.MonFront);
            MonsterWalkCycle.For(Direction.NW, WalkCycleType.Mon).Should().BeSameAs(MonsterWalkCycle.MonBack);
        }

        [Fact]
        public void MonsterWalkCycle_CyokoMatchesAuthoritativeSeqData()
        {
            // CYOKO walk beats composite several small tiles inside a 102x88 HD frame box
            // (the union of every walk tile in both directions). Draw order is back to front:
            // the SHP lists tiles front-first, so the file's first tile is the array's last.
            MonsterWalkCycle.FrameSize(WalkCycleType.Cyoko).Should().Be(new Size(102, 88));

            // Front beat 0 = SHP frame 9 (6 tiles). Backmost drawn first: the tail tile at
            // SPR (200,88) -> HD (400,176); frontmost drawn last: the body tile at SPR
            // (64,64) -> HD (128,128), landing at dst (-5,-22) -> box (28,26).
            var front0 = MonsterWalkCycle.CyokoFront[0];
            front0.Should().HaveCount(6);
            front0.First().Source.Should().Be(new Rectangle(400, 176, 48, 48));
            front0.First().Offset.Should().Be(new Point(24, 0));
            front0.Last().Source.Should().Be(new Rectangle(128, 128, 48, 32));
            front0.Last().Offset.Should().Be(new Point(28, 26));

            // Back beat 2 = SHP frame 16: its off-frame filler tile (dst 124,124) is dropped,
            // leaving 5 draws; frontmost is the head tile SPR (48,144) -> HD (96,288) landing
            // at dst (-15,-20) -> box (8,30).
            var back2 = MonsterWalkCycle.CyokoBack[2];
            back2.Should().HaveCount(5);
            back2.First().Source.Should().Be(new Rectangle(176, 336, 64, 64));
            back2.First().Offset.Should().Be(new Point(2, 20));
            back2.Last().Source.Should().Be(new Rectangle(96, 288, 48, 48));
            back2.Last().Offset.Should().Be(new Point(8, 30));

            // No walk tile in either monster rig is mirrored (flips only appear in the
            // standing poses); the palindrome shape still holds.
            foreach (var beat in MonsterWalkCycle.CyokoFront.Concat(MonsterWalkCycle.CyokoBack))
                beat.Should().OnlyContain(d => !d.FlipX);
            MonsterWalkCycle.CyokoFront[3].Should().BeSameAs(MonsterWalkCycle.CyokoFront[1]);
            MonsterWalkCycle.CyokoFront[4].Should().BeSameAs(MonsterWalkCycle.CyokoFront[0]);
            MonsterWalkCycle.CyokoFront[7].Should().BeSameAs(MonsterWalkCycle.CyokoFront[5]);
            MonsterWalkCycle.CyokoBack[3].Should().BeSameAs(MonsterWalkCycle.CyokoBack[1]);
            MonsterWalkCycle.CyokoBack[4].Should().BeSameAs(MonsterWalkCycle.CyokoBack[0]);
            MonsterWalkCycle.CyokoBack[7].Should().BeSameAs(MonsterWalkCycle.CyokoBack[5]);
        }

        // ------------------------------------------------------------------
        // Rig classification (BATTLE.BIN 0x2D748 provenance)
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("Goblin", WalkCycleType.Type1)]
        [InlineData("Skeleton", WalkCycleType.Type1)]
        [InlineData("Mindflayer", WalkCycleType.Type2)]
        [InlineData("Chocobo", WalkCycleType.Cyoko)]
        [InlineData("Bomb", WalkCycleType.Mon)]
        [InlineData("Panther", WalkCycleType.Mon)]
        [InlineData("Ghost", WalkCycleType.Mon)]
        [InlineData("Ahriman", WalkCycleType.Mon)]
        [InlineData("Aevis", WalkCycleType.Mon)]
        [InlineData("Pig", WalkCycleType.Mon)]
        [InlineData("Treant", WalkCycleType.Mon)]
        [InlineData("Minotaur", WalkCycleType.Mon)]
        [InlineData("Malboro", WalkCycleType.Mon)]
        [InlineData("Behemoth", WalkCycleType.Mon)]
        [InlineData("Dragon", WalkCycleType.Mon)]
        [InlineData("Hydra", WalkCycleType.Mon)]
        [InlineData("Construct8", WalkCycleType.Mon)]
        public void WalkCycleTypes_ClassifiesMonsterRoster(string characterName, WalkCycleType expected)
        {
            WalkCycleTypes.For(characterName).Should().Be(expected);
        }

        // ------------------------------------------------------------------
        // Tile compositor: position asserts on a synthetic sheet
        // ------------------------------------------------------------------

        [Fact]
        public void TileCompositor_MonBeat_PlacesTilePixelsAtExactFramePositions()
        {
            using var sheet = MakeSheet(512, 512, (96, 0, Color.Red), (96 + 95, 95, Color.Lime), (384, 288, Color.Blue));

            // SW beat 0 samples HD tile (96,0,96,96): its corners land at frame (0,0)/(95,95).
            using (var frame = TileWalkFrameCompositor.ExtractWalkFrame(sheet, Direction.SW, 0, WalkCycleType.Mon))
            {
                frame.Width.Should().Be(96);
                frame.Height.Should().Be(96);
                frame.GetPixel(0, 0).ToArgb().Should().Be(Color.Red.ToArgb());
                frame.GetPixel(95, 95).ToArgb().Should().Be(Color.Lime.ToArgb());
            }

            // NW beat 6 samples HD tile (384,288,96,96).
            using (var frame = TileWalkFrameCompositor.ExtractWalkFrame(sheet, Direction.NW, 6, WalkCycleType.Mon))
            {
                frame.GetPixel(0, 0).ToArgb().Should().Be(Color.Blue.ToArgb());
            }

            // SE mirrors the finished SW frame: x -> width-1-x.
            using (var frame = TileWalkFrameCompositor.ExtractWalkFrame(sheet, Direction.SE, 0, WalkCycleType.Mon))
            {
                frame.GetPixel(95, 0).ToArgb().Should().Be(Color.Red.ToArgb());
                frame.GetPixel(0, 95).ToArgb().Should().Be(Color.Lime.ToArgb());
            }
        }

        [Fact]
        public void TileCompositor_CyokoBeat_CompositesTilesBackToFront()
        {
            // Front beat 0's frontmost (last-drawn) tile is HD (128,128,48,32) at box (28,26);
            // its backmost is HD (400,176,48,48) at box (24,0). Give both source origins
            // different colors and check who wins where they do not overlap, and that the
            // frontmost tile's pixels sit at its exact offset.
            using var sheet = MakeSheet(512, 512, (128, 128, Color.Red), (400, 176, Color.Lime));

            using var frame = TileWalkFrameCompositor.ExtractWalkFrame(sheet, Direction.SW, 0, WalkCycleType.Cyoko);
            frame.Width.Should().Be(102);
            frame.Height.Should().Be(88);
            frame.GetPixel(28, 26).ToArgb().Should().Be(Color.Red.ToArgb());
            frame.GetPixel(24, 0).ToArgb().Should().Be(Color.Lime.ToArgb());
        }

        [Fact]
        public void TileCompositor_FlippedTile_MirrorsSourcePixelsWithinTheTileBox()
        {
            // A tile drawn with FlipX lands mirrored inside its own box: the source's left
            // column appears at the box's right edge. Uses the internal Compose seam directly,
            // since no real walk beat carries a flip.
            using var sheet = MakeSheet(64, 64, (0, 0, Color.Red));

            var draws = new[] { new WalkTileDraw(new Rectangle(0, 0, 16, 16), new Point(4, 6), flipX: true) };
            using var frame = TileWalkFrameCompositor.Compose(sheet, draws, new Size(32, 32));
            frame.GetPixel(4 + 15, 6).ToArgb().Should().Be(Color.Red.ToArgb());
            frame.GetPixel(4, 6).A.Should().Be(0);
        }

        // ------------------------------------------------------------------
        // Shared-content crop
        // ------------------------------------------------------------------

        [Fact]
        public void FrameLayout_CropsWalkFramesToContent_OnlyForIrregularStaticLayouts()
        {
            // Explicit-rect monsters and Chocobo's offset grid show tight static poses, so
            // their walk frames crop to content to keep the Zoom-mode preview the same size.
            // Standard humanoids and Construct 8 (whose static pose is already the full cell)
            // must not crop, or the sprite would pop when the animation starts.
            FrameLayout.For("Goblin").CropsWalkFramesToContent.Should().BeTrue();
            FrameLayout.For("Bomb").CropsWalkFramesToContent.Should().BeTrue();
            FrameLayout.For("Chocobo").CropsWalkFramesToContent.Should().BeTrue();
            FrameLayout.For("Construct8").CropsWalkFramesToContent.Should().BeFalse();
            FrameLayout.Standard.CropsWalkFramesToContent.Should().BeFalse();
        }

        [Fact]
        public void WalkFrameCrop_CropsAllFramesToTheUnionContentBox()
        {
            // Content at (10,20) in frame 0 and (30,40) in frame 1: the union box is
            // (10,20)-(30,40) inclusive, and every frame is cropped by the same box so the
            // animation cannot wobble.
            var frames = new List<Bitmap> { MakeArgbFrame(64, 64, (10, 20, Color.Red)), MakeArgbFrame(64, 64, (30, 40, Color.Lime)) };
            try
            {
                var cropped = WalkFrameCrop.ToSharedContentBox(frames);
                cropped.Should().HaveCount(2);
                cropped[0].Width.Should().Be(21);
                cropped[0].Height.Should().Be(21);
                cropped[1].Width.Should().Be(21);
                cropped[1].Height.Should().Be(21);
                cropped[0].GetPixel(0, 0).ToArgb().Should().Be(Color.Red.ToArgb());
                cropped[1].GetPixel(20, 20).ToArgb().Should().Be(Color.Lime.ToArgb());
                foreach (var f in cropped) f.Dispose();
            }
            finally
            {
                foreach (var f in frames) f.Dispose();
            }
        }

        [Fact]
        public void WalkFrameCrop_LeavesFullyTransparentFramesUncropped()
        {
            var frames = new List<Bitmap> { MakeArgbFrame(48, 40), MakeArgbFrame(48, 40) };
            try
            {
                var result = WalkFrameCrop.ToSharedContentBox(frames);
                result[0].Width.Should().Be(48);
                result[0].Height.Should().Be(40);
                if (!ReferenceEquals(result[0], frames[0]))
                    foreach (var f in result) f.Dispose();
            }
            finally
            {
                foreach (var f in frames) f.Dispose();
            }
        }

        // ------------------------------------------------------------------
        // End-to-end through PaletteModifier (fixture sheets on disk)
        // ------------------------------------------------------------------

        [Fact]
        public void GetWalkPreviewFrames_MonRig_SupportsAndRendersCroppedFrames()
        {
            var modPath = Path.Combine(Path.GetTempPath(), "MonWalk_" + Guid.NewGuid().ToString("N"));
            var binPath = Path.Combine(Path.GetTempPath(), "mon_walk_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                // Marker at HD (0,0): the origin pixel of front beat 2's tile (SHP frame 1).
                // With only that pixel opaque, the shared content box is 1x1, which pins both
                // the tile extraction position and the crop in one assert.
                WriteMonsterHdFixture(modPath, "Bomb", binPath, markerX: 0, markerY: 0);

                var modifier = new PaletteModifier();
                modifier.LoadTemplate(binPath, "Bomb", modPath); // bin palette 0 index 2 -> red
                modifier.SupportsWalkAnimation.Should().BeTrue();

                var frames = modifier.GetWalkPreviewFrames(5); // direction 5 = SW (front)
                frames.Should().NotBeNull();
                frames.Count.Should().Be(WalkCycle.BeatCount);
                frames[2].Width.Should().Be(1);
                frames[2].Height.Should().Be(1);
                frames[2].GetPixel(0, 0).ToArgb().Should().Be(Color.FromArgb(255, 255, 0, 0).ToArgb());
                frames[0].GetPixel(0, 0).A.Should().Be(0); // beat 0 samples tile (96,0): empty
                foreach (var f in frames) f.Dispose();
            }
            finally
            {
                if (File.Exists(binPath)) File.Delete(binPath);
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);
            }
        }

        [Fact]
        public void GetWalkPreviewFrames_Construct8_KeepsTheFullTileBox()
        {
            var modPath = Path.Combine(Path.GetTempPath(), "C8Walk_" + Guid.NewGuid().ToString("N"));
            var binPath = Path.Combine(Path.GetTempPath(), "c8_walk_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                // Construct 8's static pose is the full 96x96 cell, so its walk frames stay
                // uncropped and toggling Animate cannot change the sprite's size.
                WriteMonsterHdFixture(modPath, "Construct8", binPath, markerX: 96, markerY: 0);

                var modifier = new PaletteModifier();
                modifier.LoadTemplate(binPath, "Construct8", modPath);
                modifier.SupportsWalkAnimation.Should().BeTrue();

                var frames = modifier.GetWalkPreviewFrames(5);
                frames.Should().NotBeNull();
                frames[0].Width.Should().Be(96);
                frames[0].Height.Should().Be(96);
                frames[0].GetPixel(0, 0).ToArgb().Should().Be(Color.FromArgb(255, 255, 0, 0).ToArgb());
                foreach (var f in frames) f.Dispose();
            }
            finally
            {
                if (File.Exists(binPath)) File.Delete(binPath);
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);
            }
        }

        [Fact]
        public void GetWalkPreviewFrames_Chocobo_RendersTheCyokoComposite()
        {
            var modPath = Path.Combine(Path.GetTempPath(), "ChocoWalk_" + Guid.NewGuid().ToString("N"));
            var binPath = Path.Combine(Path.GetTempPath(), "choco_walk_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                // Marker at HD (128,128): source origin of front beat 0's frontmost tile,
                // which lands at box (28,26). The 1x1 shared content box pins the position.
                WriteMonsterHdFixture(modPath, "Chocobo", binPath, markerX: 128, markerY: 128);

                var modifier = new PaletteModifier();
                modifier.LoadTemplate(binPath, "Chocobo", modPath);
                modifier.SupportsWalkAnimation.Should().BeTrue();

                var frames = modifier.GetWalkPreviewFrames(5);
                frames.Should().NotBeNull();
                frames[0].Width.Should().Be(1);
                frames[0].GetPixel(0, 0).ToArgb().Should().Be(Color.FromArgb(255, 255, 0, 0).ToArgb());
                frames[1].GetPixel(0, 0).A.Should().Be(0); // beat 1 (SHP frame 10) never samples (128,128)
                foreach (var f in frames) f.Dispose();
            }
            finally
            {
                if (File.Exists(binPath)) File.Delete(binPath);
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);
            }
        }

        [Fact]
        public void GetWalkPreviewFrames_HumanoidRigMonster_UsesTheStandardWalkComposite()
        {
            var modPath = Path.Combine(Path.GetTempPath(), "GobWalk_" + Guid.NewGuid().ToString("N"));
            var binPath = Path.Combine(Path.GetTempPath(), "gob_walk_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                // Goblin is a TYPE1 humanoid rig on an explicit-rect static layout: the walk
                // must come from the standard humanoid compositor (body cell row 1 col 0 for
                // front beat 0 = sheet (0,80)), then crop to content like the other monsters.
                WriteMonsterHdFixture(modPath, "Goblin", binPath, markerX: 0, markerY: 80);

                var modifier = new PaletteModifier();
                modifier.LoadTemplate(binPath, "Goblin", modPath);
                modifier.SupportsWalkAnimation.Should().BeTrue();

                var frames = modifier.GetWalkPreviewFrames(5);
                frames.Should().NotBeNull();
                frames.Count.Should().Be(WalkCycle.BeatCount);
                frames[0].Width.Should().Be(1); // 1x1 shared content box around the marker
                frames[0].GetPixel(0, 0).ToArgb().Should().Be(Color.FromArgb(255, 255, 0, 0).ToArgb());
                frames[2].GetPixel(0, 0).A.Should().Be(0); // beat 2's body cell is (5,0): empty
                foreach (var f in frames) f.Dispose();
            }
            finally
            {
                if (File.Exists(binPath)) File.Delete(binPath);
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);
            }
        }

        // ------------------------------------------------------------------
        // Real repo sheets: every newly animated character renders a live cycle
        // ------------------------------------------------------------------

        /// <summary>
        /// Sweeps the full CC-46 roster against the SHIPPED HD sheets through the real
        /// pipeline (HdSheetPreview.RenderWalkFrames: rig branch, layout choice and crop),
        /// so a family whose Images/ folder, rig entry and sheet content drift apart fails
        /// here and not in front of the owner. Substance checks per direction: 8 uniform
        /// frames, each carrying real content, and a stride that actually moves (beat 2 is
        /// a different pose than beat 0 in every rig's table).
        /// </summary>
        [Theory]
        [InlineData("Goblin")]
        [InlineData("Skeleton")]
        [InlineData("Mindflayer")]
        [InlineData("Chocobo")]
        [InlineData("Bomb")]
        [InlineData("Panther")]
        [InlineData("Ghost")]
        [InlineData("Ahriman")]
        [InlineData("Aevis")]
        [InlineData("Pig")]
        [InlineData("Treant")]
        [InlineData("Minotaur")]
        [InlineData("Malboro")]
        [InlineData("Behemoth")]
        [InlineData("Dragon")]
        [InlineData("Hydra")]
        [InlineData("Construct8")]
        public void RealSheet_MonsterRoster_RendersAMovingWalkCycle(string characterName)
        {
            const int MinContentPx = 200;

            var repoRoot = FindRepoRoot();
            var modPath = Path.Combine(repoRoot, "ColorMod");
            var bmpPath = HdSheetPreview.ResolveSheetPath(modPath, characterName);
            bmpPath.Should().NotBeNull($"{characterName} must have an Images/{characterName}/original HD sheet");

            // Any all-opaque 16-color palette works: content is decided by palette index 0,
            // which the loaders key to alpha 0 no matter what colors are supplied.
            var palette = new byte[32];
            for (int i = 1; i < 16; i++) { palette[i * 2] = 0xFF; palette[i * 2 + 1] = 0x7F; }

            foreach (var directionIndex in new[] { 5, 0 }) // 5 = SW (front), 0 = NW (back)
            {
                var frames = HdSheetPreview.RenderWalkFrames(bmpPath, characterName, palette, directionIndex);
                frames.Should().NotBeNull($"{characterName} direction {directionIndex} must render");
                frames.Count.Should().Be(WalkCycle.BeatCount);

                foreach (var frame in frames)
                {
                    frame.Width.Should().Be(frames[0].Width, $"{characterName}: a wobbling frame box would resize the preview mid-cycle");
                    frame.Height.Should().Be(frames[0].Height);
                    CountOpaque(frame).Should().BeGreaterThanOrEqualTo(MinContentPx,
                        $"{characterName} direction {directionIndex}: every beat must carry real sprite content");
                }

                CountDifferingPixels(frames[0], frames[2]).Should().BeGreaterThan(0,
                    $"{characterName} direction {directionIndex}: beat 2 is a different stride pose than beat 0 in every rig's table");

                foreach (var frame in frames) frame.Dispose();
            }
        }

        private static int CountOpaque(Bitmap bmp)
        {
            int count = 0;
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                    if (bmp.GetPixel(x, y).A != 0)
                        count++;
            return count;
        }

        private static int CountDifferingPixels(Bitmap a, Bitmap b)
        {
            int count = 0;
            for (int x = 0; x < a.Width; x++)
                for (int y = 0; y < a.Height; y++)
                    if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb())
                        count++;
            return count;
        }

        /// <summary>Walks up from the test bin dir to the repo root (the dir holding
        /// ColorMod/Images), same approach as WalkAnimationTests.FindRepoRoot.</summary>
        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "ColorMod", "Images")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("repo root (ColorMod/Images) not found above the test bin dir");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>ARGB sheet (the compositors' input contract: loaders key palette index 0
        /// to alpha 0) that is transparent except for the given marker pixels.</summary>
        private static Bitmap MakeSheet(int width, int height, params (int x, int y, Color color)[] markers)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            foreach (var (x, y, color) in markers)
                bmp.SetPixel(x, y, color);
            return bmp;
        }

        private static Bitmap MakeArgbFrame(int width, int height, params (int x, int y, Color color)[] pixels)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            foreach (var (x, y, color) in pixels)
                bmp.SetPixel(x, y, color);
            return bmp;
        }

        /// <summary>
        /// Builds a modPath/Images/&lt;jobName&gt;/original 512x512 4bpp fixture (monster walk
        /// tiles reach y 384..480, deeper than the humanoid fixture's 192 rows) with one
        /// palette-index-2 marker, plus a bin whose palette 0 paints index 2 red. Mirrors
        /// WalkAnimationTests.WriteWalkHdFixture.
        /// </summary>
        private static string WriteMonsterHdFixture(string modPath, string jobName, string binPath, int markerX, int markerY)
        {
            var bmpDir = Path.Combine(modPath, "Images", jobName, "original");
            Directory.CreateDirectory(bmpDir);

            const int sheetSize = 512;
            using (var bmp = new Bitmap(sheetSize, sheetSize, PixelFormat.Format4bppIndexed))
            {
                var rect = new Rectangle(0, 0, sheetSize, sheetSize);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format4bppIndexed);
                try
                {
                    var buffer = new byte[data.Stride * sheetSize];
                    int byteIndex = markerY * data.Stride + (markerX >> 1);
                    // Palette index 2: even x -> high nibble, odd x -> low nibble.
                    buffer[byteIndex] = (markerX % 2 == 0) ? (byte)0x20 : (byte)0x02;
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
    }
}
