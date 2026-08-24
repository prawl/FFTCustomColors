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
        public void WalkCycle_MatchesAuthoritativeSeqData()
        {
            WalkCycle.BeatCount.Should().Be(8);
            WalkCycle.BeatDurationsMs.Should().Equal(100, 133, 167, 133, 100, 133, 167, 133);

            var frontCells = WalkCycle.Type1Front.Select(b => b.BodyCell).ToArray();
            frontCells.Should().Equal(
                new Point(0, 1), new Point(6, 0), new Point(5, 0), new Point(6, 0),
                new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(1, 1));

            var backCells = WalkCycle.Type1Back.Select(b => b.BodyCell).ToArray();
            backCells.Should().Equal(
                new Point(5, 1), new Point(4, 1), new Point(3, 1), new Point(4, 1),
                new Point(5, 1), new Point(6, 1), new Point(0, 2), new Point(6, 1));

            // Palindrome identities: beat3 == beat1, beat4 == beat0, beat7 == beat5.
            WalkCycle.Type1Front[3].Should().Be(WalkCycle.Type1Front[1]);
            WalkCycle.Type1Front[4].Should().Be(WalkCycle.Type1Front[0]);
            WalkCycle.Type1Front[7].Should().Be(WalkCycle.Type1Front[5]);
            WalkCycle.Type1Back[3].Should().Be(WalkCycle.Type1Back[1]);
            WalkCycle.Type1Back[4].Should().Be(WalkCycle.Type1Back[0]);
            WalkCycle.Type1Back[7].Should().Be(WalkCycle.Type1Back[5]);

            // Spot-check two arm entries exactly.
            WalkCycle.Type1Front[2].FarArmSource.Should().Be(new Point(64, 160));
            WalkCycle.Type1Front[2].FarArmOffset.Should().Be(new Point(8, 16));
            WalkCycle.Type1Front[2].NearArmSource.Should().Be(new Point(112, 160));
            WalkCycle.Type1Front[2].NearArmOffset.Should().Be(new Point(16, 24));

            WalkCycle.Type1Back[6].FarArmSource.Should().Be(new Point(112, 256));
            WalkCycle.Type1Back[6].FarArmOffset.Should().Be(new Point(16, 16));
            WalkCycle.Type1Back[6].NearArmSource.Should().Be(new Point(64, 256));
            WalkCycle.Type1Back[6].NearArmOffset.Should().Be(new Point(0, 22));

            WalkCycle.For(Direction.SW, WalkCycleType.Type1).Should().BeSameAs(WalkCycle.Type1Front);
            WalkCycle.For(Direction.NW, WalkCycleType.Type1).Should().BeSameAs(WalkCycle.Type1Back);
        }

        /// <summary>
        /// CC-36 defect fix: robed/skirted rigs use the game's TYPE2.SEQ/TYPE2.SHP walk data,
        /// not TYPE1's. Parsed the same way as TYPE1 (Shishi parser port over the files shipped
        /// in the FFTPatcher repo): same animations 6/7, same frame indices, same body tiles,
        /// same arm source tiles, same delays; ONLY the arm destination offsets differ. Applying
        /// TYPE1 offsets to a TYPE2 sheet is exactly the owner's "dislocated left arm".
        /// </summary>
        [Fact]
        public void WalkCycle_Type2_MatchesAuthoritativeSeqData()
        {
            // Body cells and durations are shared with TYPE1 (verified identical in both SHPs).
            WalkCycle.Type2Front.Select(b => b.BodyCell).Should().Equal(
                WalkCycle.Type1Front.Select(b => b.BodyCell));
            WalkCycle.Type2Back.Select(b => b.BodyCell).Should().Equal(
                WalkCycle.Type1Back.Select(b => b.BodyCell));

            // Arm source tiles are shared too; only the destination offsets move.
            for (int i = 0; i < WalkCycle.BeatCount; i++)
            {
                WalkCycle.Type2Front[i].FarArmSource.Should().Be(WalkCycle.Type1Front[i].FarArmSource);
                WalkCycle.Type2Front[i].NearArmSource.Should().Be(WalkCycle.Type1Front[i].NearArmSource);
                WalkCycle.Type2Back[i].FarArmSource.Should().Be(WalkCycle.Type1Back[i].FarArmSource);
                WalkCycle.Type2Back[i].NearArmSource.Should().Be(WalkCycle.Type1Back[i].NearArmSource);
            }

            // Palindrome identities hold for TYPE2 as well.
            WalkCycle.Type2Front[3].Should().Be(WalkCycle.Type2Front[1]);
            WalkCycle.Type2Front[4].Should().Be(WalkCycle.Type2Front[0]);
            WalkCycle.Type2Front[7].Should().Be(WalkCycle.Type2Front[5]);
            WalkCycle.Type2Back[3].Should().Be(WalkCycle.Type2Back[1]);
            WalkCycle.Type2Back[4].Should().Be(WalkCycle.Type2Back[0]);
            WalkCycle.Type2Back[7].Should().Be(WalkCycle.Type2Back[5]);

            // Exact TYPE2 arm offsets, pinned against the parsed SEQ/SHP data (SPR coords x2).
            WalkCycle.Type2Front[0].FarArmOffset.Should().Be(new Point(-2, 18));
            WalkCycle.Type2Front[0].NearArmOffset.Should().Be(new Point(22, 24));
            WalkCycle.Type2Front[1].FarArmOffset.Should().Be(new Point(2, 16));
            WalkCycle.Type2Front[1].NearArmOffset.Should().Be(new Point(18, 24));
            WalkCycle.Type2Front[2].FarArmOffset.Should().Be(new Point(6, 18));
            WalkCycle.Type2Front[2].NearArmOffset.Should().Be(new Point(16, 22));
            WalkCycle.Type2Front[5].FarArmOffset.Should().Be(new Point(0, 16));
            WalkCycle.Type2Front[5].NearArmOffset.Should().Be(new Point(18, 22));
            WalkCycle.Type2Front[6].FarArmOffset.Should().Be(new Point(0, 18));
            WalkCycle.Type2Front[6].NearArmOffset.Should().Be(new Point(20, 22));

            WalkCycle.Type2Back[0].FarArmOffset.Should().Be(new Point(16, 16));
            WalkCycle.Type2Back[0].NearArmOffset.Should().Be(new Point(0, 24));
            WalkCycle.Type2Back[1].FarArmOffset.Should().Be(new Point(24, 18));
            WalkCycle.Type2Back[1].NearArmOffset.Should().Be(new Point(2, 24));
            WalkCycle.Type2Back[2].FarArmOffset.Should().Be(new Point(20, 20));
            WalkCycle.Type2Back[2].NearArmOffset.Should().Be(new Point(4, 22));
            WalkCycle.Type2Back[5].FarArmOffset.Should().Be(new Point(24, 18));
            WalkCycle.Type2Back[5].NearArmOffset.Should().Be(new Point(-2, 22));
            WalkCycle.Type2Back[6].FarArmOffset.Should().Be(new Point(24, 18));
            WalkCycle.Type2Back[6].NearArmOffset.Should().Be(new Point(2, 22));

            WalkCycle.For(Direction.SW, WalkCycleType.Type2).Should().BeSameAs(WalkCycle.Type2Front);
            WalkCycle.For(Direction.NW, WalkCycleType.Type2).Should().BeSameAs(WalkCycle.Type2Back);
        }

        /// <summary>
        /// Pins the per-character animation-type registry. Classification sources, in order:
        /// (1) the game's own classic BATTLE.BIN sprite-attributes table (fftpack/battle_bin.en.bin
        /// inside data/classic/0002.en.pac, table at 0x2D748, byte 0 = SHP type, read via the
        /// FFTPatcher/Shishi format); (2) a socket-position pixel classifier run over every
        /// shipped HD sheet (arm tile content centroid + candidate offset must land at the
        /// anatomical socket measured from TYPE1 exemplars) - the two agreed on every character
        /// with a classic entry; (3) the owner's live pass. Where the owner's list disagreed
        /// (Bard, Calculator M/F reported good), zoomed per-frame renders show TYPE1 offsets
        /// detach the fist on back f18, so the data verdict (TYPE2) stands.
        /// </summary>
        [Fact]
        public void WalkCycleTypes_ClassifyEveryRosterCharacterFromGameData()
        {
            var type2 = new[]
            {
                "Chemist_Male", "Chemist_Female", "WhiteMage_Male", "WhiteMage_Female",
                "BlackMage_Male", "BlackMage_Female", "TimeMage_Male", "TimeMage_Female",
                "Summoner_Male", "Summoner_Female", "Mystic_Male", "Mystic_Female",
                "Mediator_Male", "Mediator_Female", "Geomancer_Male", "Geomancer_Female",
                "Calculator_Male", "Calculator_Female", "Bard_Male", "Dancer_Female",
                "Mime_Male", "Mime_Female",
                "Alma", "Celia", "Elmdore", "Lettie", "Ovelia", "Rapha", "Reis",
                "Simon", "Valmafra", "Zalmour"
            };
            foreach (var name in type2)
                WalkCycleTypes.For(name).Should().Be(WalkCycleType.Type2, $"{name} is a TYPE2 rig");

            var type1 = new[]
            {
                "RamzaChapter1", "RamzaChapter23", "RamzaChapter4",
                "Squire_Male", "Squire_Female", "Knight_Male", "Knight_Female",
                "Archer_Male", "Archer_Female", "Monk_Male", "Monk_Female",
                "Thief_Male", "Thief_Female", "Dragoon_Male", "Dragoon_Female",
                "Samurai_Male", "Samurai_Female", "Ninja_Male", "Ninja_Female",
                "DarkKnight_Male", "DarkKnight_Female", "OnionKnight_Male", "OnionKnight_Female",
                "Agrias", "Argath", "Beowulf", "Cloud", "Gaffgarion", "Isilud",
                "Marach", "Meliadoul", "Mustadio", "Orlandeau", "Orran"
            };
            foreach (var name in type1)
                WalkCycleTypes.For(name).Should().Be(WalkCycleType.Type1, $"{name} is a TYPE1 rig");

            // Unknown characters default to TYPE1 (the majority rig, and the pre-fix behavior).
            WalkCycleTypes.For("SomeBrandNewCharacter").Should().Be(WalkCycleType.Type1);
        }

        /// <summary>
        /// LOAD-BEARING: proves ExtractWalkFrame composites body + far arm + near arm at the
        /// exact offsets WalkCycle's front beat-0 entry claims, in the right z-order (body over
        /// far arm, near arm over body). A synthetic sheet carries a distinct marker color at
        /// the body cell origin and at each arm source's origin; a flipped offset or a wrong
        /// z-order breaks this.
        /// </summary>
        [Fact]
        public void ExtractWalkFrame_CompositesBodyAndBothArms()
        {
            var sheet = new Bitmap(512, 512, PixelFormat.Format32bppArgb);

            // Front beat 0: body (0,1) -> sheet (0,80); far arm src (256,160); near arm src (304,160).
            sheet.SetPixel(0, 80, Color.Red);       // body marker
            sheet.SetPixel(256, 160, Color.Lime);   // far-arm marker
            sheet.SetPixel(304, 160, Color.Blue);   // near-arm marker

            // (a) far arm draws first, body draws over it: give the far source an opaque pixel
            // at +(2,2) (sheet 258,162) which lands at body-local (8,18); give the body cell an
            // opaque pixel at its own local (8,18) too (sheet 8,98). The BODY color must win.
            sheet.SetPixel(258, 162, Color.Cyan);   // far-arm's +(2,2) pixel
            sheet.SetPixel(8, 98, Color.Yellow);    // body's local (8,18) pixel (body origin 0,80 + 8,18)

            // (b) near arm draws last, over the body: give the body cell an opaque pixel at its
            // local (16,24) (sheet 16,104). The NEAR marker (blue, at body-local (16,24) too,
            // since near offset is (16,24)) must win there.
            sheet.SetPixel(16, 104, Color.Magenta); // body's local (16,24) pixel

            var layout = FrameLayout.Standard;
            var frame = WalkFrameCompositor.ExtractWalkFrame(sheet, Direction.SW, layout, 0, WalkCycleType.Type1);

            AssertPixel(frame, 0, 0, Color.Red);     // body marker at its own origin
            AssertPixel(frame, 6, 16, Color.Lime);   // far arm marker at its offset
            AssertPixel(frame, 16, 24, Color.Blue);  // near arm marker at its offset

            AssertPixel(frame, 8, 18, Color.Yellow); // (a) body wins over far arm
            AssertPixel(frame, 16, 24, Color.Blue);  // (b) near arm wins over body
        }

        /// <summary>
        /// The TYPE2 twin of the composite test above, using front beat 0's TYPE2 offsets:
        /// far arm (-2, 18) (partially clipped off the left edge - the compositor must clip,
        /// not throw), near arm (22, 24). Markers prove the TYPE2 table is actually applied:
        /// under TYPE1 offsets the near marker would land at (16, 24), which must stay empty.
        /// </summary>
        [Fact]
        public void ExtractWalkFrame_Type2_PlacesArmsAtType2Offsets()
        {
            var sheet = new Bitmap(512, 512, PixelFormat.Format32bppArgb);

            sheet.SetPixel(0, 80, Color.Red);       // body cell (0,1) origin marker
            sheet.SetPixel(260, 160, Color.Lime);   // far-arm tile-local (4,0): lands at (4 + -2, 0 + 18) = (2, 18)
            sheet.SetPixel(304, 160, Color.Blue);   // near-arm origin: lands at (22, 24)

            var frame = WalkFrameCompositor.ExtractWalkFrame(sheet, Direction.SW, FrameLayout.Standard, 0, WalkCycleType.Type2);

            AssertPixel(frame, 0, 0, Color.Red);
            AssertPixel(frame, 2, 18, Color.Lime);
            AssertPixel(frame, 22, 24, Color.Blue);

            // The TYPE1 landing spot must be empty: this fails if the compositor silently
            // falls back to the TYPE1 table for a TYPE2 request.
            frame.GetPixel(16, 24).A.Should().Be(0);
        }

        private static void AssertOrigin(Bitmap frame, Color expected)
        {
            AssertPixel(frame, 0, 0, expected);
        }

        private static void AssertPixel(Bitmap frame, int x, int y, Color expected)
        {
            var pixel = frame.GetPixel(x, y);
            pixel.R.Should().Be(expected.R);
            pixel.G.Should().Be(expected.G);
            pixel.B.Should().Be(expected.B);
        }

        [Fact]
        public void ExtractWalkFrame_SE_MirrorsSW_NE_MirrorsNW()
        {
            var sheet = new Bitmap(512, 512, PixelFormat.Format32bppArgb);
            sheet.SetPixel(0, 80, Color.Red);      // front beat-0 body cell (0,1) origin
            sheet.SetPixel(320, 80, Color.Yellow);  // back beat-0 body cell (5,1) origin

            var layout = FrameLayout.Standard;

            var seFrame = WalkFrameCompositor.ExtractWalkFrame(sheet, Direction.SE, layout, 0, WalkCycleType.Type1);
            var sePixel = seFrame.GetPixel(63, 0);
            sePixel.R.Should().Be(255);
            sePixel.G.Should().Be(0);
            sePixel.B.Should().Be(0);

            var neFrame = WalkFrameCompositor.ExtractWalkFrame(sheet, Direction.NE, layout, 0, WalkCycleType.Type1);
            var nePixel = neFrame.GetPixel(63, 0);
            nePixel.R.Should().Be(255);
            nePixel.G.Should().Be(255);
            nePixel.B.Should().Be(0);
        }

        [Fact]
        public void ExtractWalkFrame_ReturnsNull_ForNonWalkLayouts()
        {
            var sheet = new Bitmap(512, 512, PixelFormat.Format32bppArgb);

            var rectsLayout = FrameLayout.Rects(0, 0, 46, 78, 205, 4, 46, 78);
            WalkFrameCompositor.ExtractWalkFrame(sheet, Direction.SW, rectsLayout, 0, WalkCycleType.Type1).Should().BeNull();

            var construct8Layout = FrameLayout.For("Construct8");
            WalkFrameCompositor.ExtractWalkFrame(sheet, Direction.SW, construct8Layout, 0, WalkCycleType.Type2).Should().BeNull();
        }

        /// <summary>
        /// OWNER'S ARMLESS-CATCHER: round 1 shipped a walk cycle that rendered without arms.
        /// Loads the REAL repo sheet (not a synthetic fixture) and asserts every beat's frame
        /// carries meaningfully more opaque pixels than the bare body cell alone, so a
        /// regression that silently drops the arm compositing (e.g. an offset that lands fully
        /// off-sheet, or a z-order bug that lets a later opaque body pixel erase the arm) fails
        /// loudly here even if the synthetic composite test above is somehow satisfied.
        ///
        /// Threshold note (deviation from the spec's literal "+150 for every beat"): measuring
        /// all 16 beat/direction combinations on this real sheet shows two populations, not one.
        /// The 5 "extended stride" beats (0, 4, 5, 6, 7) add 269-439 opaque pixels, comfortably
        /// inside the spec's cited 250-450 range. The 3 "passing/recovery" beats (1, 2, 3) add
        /// only 23-173: at that point in the stride the arms swing close against the torso, so
        /// the arm boxes legitimately overlap a lot of already-opaque body silhouette (verified:
        /// an independent far/body/near union computed pixel-by-pixel outside SpriteSheetExtractor
        /// matches ExtractWalkFrame's output exactly for every one of these beats, so this is a
        /// real property of the sheet, not a compositing bug). +15 sits comfortably below the
        /// measured minimum (23) while staying far enough above zero to still fail hard on a
        /// wholesale "arms not drawn" regression (which yields a diff of exactly 0).
        /// </summary>
        [Fact]
        public void RealSheet_WalkFramesCarryArms()
        {
            const int MinArmContributionPx = 15;

            var repoRoot = FindRepoRoot();
            var bmpPath = Path.Combine(repoRoot, "ColorMod", "Images", "Squire_Male", "original", "924_Squire_Male_hd.bmp");
            if (!File.Exists(bmpPath))
            {
                Assert.True(false, $"Real sheet fixture not found at {bmpPath}; cannot run the armless-catcher check.");
                return;
            }

            var layout = FrameLayout.Standard;
            using var sheet = BmpPaletteSwapper.LoadWithOriginalPalette(bmpPath);

            foreach (var direction in new[] { Direction.SW, Direction.NW })
            {
                var beats = WalkCycle.For(direction, WalkCycleType.Type1);
                for (int beat = 0; beat < WalkCycle.BeatCount; beat++)
                {
                    using var frame = WalkFrameCompositor.ExtractWalkFrame(sheet, direction, layout, beat, WalkCycleType.Type1);
                    frame.Should().NotBeNull();

                    var bodyCell = beats[beat].BodyCell;
                    using var bodyOnly = CropRegion(sheet, bodyCell.X * layout.FrameWidth, bodyCell.Y * layout.FrameHeight, layout.FrameWidth, layout.FrameHeight);

                    int bodyOpaque = CountOpaquePixels(bodyOnly);
                    int frameOpaque = CountOpaquePixels(frame);

                    frameOpaque.Should().BeGreaterThanOrEqualTo(bodyOpaque + MinArmContributionPx,
                        $"{direction} beat {beat} frame ({frameOpaque} opaque px) should carry the arm sub-sprites over the bare body cell ({bodyOpaque} opaque px)");
                }
            }
        }

        /// <summary>
        /// ARM-SOCKET POSITION test (mutation-proven by its own wrong-type half): loads REAL
        /// repo sheets and asserts a pixel that is opaque ONLY when the arm sits at the
        /// sheet's correct rig position. The coordinates are interior pixels of the
        /// TYPE1-vs-TYPE2 composite difference (computed off the shipped sheets), so a
        /// presence-only regression - the blind spot that let a dislocated-but-present arm
        /// pass review - cannot satisfy this: the wrong table leaves the pixel EMPTY.
        /// Squire_Male is the TYPE1 exemplar; WhiteMage_Male is a confirmed-broken TYPE2 rig.
        /// </summary>
        [Theory]
        [InlineData("Squire_Male", "924_Squire_Male_hd.bmp", WalkCycleType.Type1, WalkCycleType.Type2, Direction.SW, 6, 43, 36)]
        [InlineData("Squire_Male", "924_Squire_Male_hd.bmp", WalkCycleType.Type1, WalkCycleType.Type2, Direction.NW, 5, 47, 29)]
        [InlineData("WhiteMage_Male", "1012_Priest_Male_hd.bmp", WalkCycleType.Type2, WalkCycleType.Type1, Direction.SW, 0, 21, 35)]
        [InlineData("WhiteMage_Male", "1012_Priest_Male_hd.bmp", WalkCycleType.Type2, WalkCycleType.Type1, Direction.NW, 6, 54, 34)]
        public void RealSheet_ArmSocketPixels_RequireTheSheetsOwnType(
            string folder, string file, WalkCycleType correctType, WalkCycleType wrongType,
            Direction direction, int beat, int x, int y)
        {
            var repoRoot = FindRepoRoot();
            var bmpPath = Path.Combine(repoRoot, "ColorMod", "Images", folder, "original", file);
            File.Exists(bmpPath).Should().BeTrue($"real sheet fixture {bmpPath} must exist");

            // The registry must agree with the sheet's pixel-verified rig.
            WalkCycleTypes.For(folder).Should().Be(correctType);

            var layout = FrameLayout.Standard;
            using var sheet = BmpPaletteSwapper.LoadWithOriginalPalette(bmpPath);

            using var correctFrame = WalkFrameCompositor.ExtractWalkFrame(sheet, direction, layout, beat, correctType);
            correctFrame.GetPixel(x, y).A.Should().NotBe(0,
                $"{folder} {direction} beat {beat}: ({x},{y}) is arm content under the sheet's own type");

            using var wrongFrame = WalkFrameCompositor.ExtractWalkFrame(sheet, direction, layout, beat, wrongType);
            wrongFrame.GetPixel(x, y).A.Should().Be(0,
                $"{folder} {direction} beat {beat}: ({x},{y}) must be EMPTY under the wrong type (non-vacuity proof)");
        }

        /// <summary>
        /// TYPE2 armless catcher. Per-beat thresholds don't work here: on the WhiteMage robe,
        /// back beat 2's arms sit entirely inside the body silhouette (measured contribution 0),
        /// so the assertion is on each direction's TOTAL arm contribution across the cycle
        /// (measured: front 1563 px, back 1362 px on the shipped sheet; a wholesale
        /// "arms not composited" regression yields exactly 0).
        /// </summary>
        [Fact]
        public void RealSheet_Type2_WalkFramesCarryArms()
        {
            const int MinTotalArmContributionPx = 500;

            var repoRoot = FindRepoRoot();
            var bmpPath = Path.Combine(repoRoot, "ColorMod", "Images", "WhiteMage_Male", "original", "1012_Priest_Male_hd.bmp");
            File.Exists(bmpPath).Should().BeTrue($"real sheet fixture {bmpPath} must exist");

            var layout = FrameLayout.Standard;
            using var sheet = BmpPaletteSwapper.LoadWithOriginalPalette(bmpPath);

            foreach (var direction in new[] { Direction.SW, Direction.NW })
            {
                var beats = WalkCycle.For(direction, WalkCycleType.Type2);
                int total = 0;
                for (int beat = 0; beat < WalkCycle.BeatCount; beat++)
                {
                    using var frame = WalkFrameCompositor.ExtractWalkFrame(sheet, direction, layout, beat, WalkCycleType.Type2);
                    frame.Should().NotBeNull();

                    var bodyCell = beats[beat].BodyCell;
                    using var bodyOnly = CropRegion(sheet, bodyCell.X * layout.FrameWidth, bodyCell.Y * layout.FrameHeight, layout.FrameWidth, layout.FrameHeight);
                    total += CountOpaquePixels(frame) - CountOpaquePixels(bodyOnly);
                }
                total.Should().BeGreaterThanOrEqualTo(MinTotalArmContributionPx,
                    $"{direction} cycle should composite arm sub-sprites over the bare body cells");
            }
        }

        /// <summary>
        /// End-to-end registry wiring: a fixture named "WhiteMage_Male" (a registered TYPE2
        /// character) must render its walk frames with the TYPE2 table. The marker sits at the
        /// near-arm source origin; under TYPE2 it lands at frame (22,24), under TYPE1 at
        /// (16,24) - so this fails if any layer between PaletteModifier and the compositor
        /// drops the character's type on the floor.
        /// </summary>
        [Fact]
        public void GetWalkPreviewFrames_UsesType2TableForRegisteredType2Character()
        {
            var modPath = Path.Combine(Path.GetTempPath(), "WalkAnimType2_" + Guid.NewGuid().ToString("N"));
            var binPath = Path.Combine(Path.GetTempPath(), "walk_anim_type2_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                // Marker at (304,160): front beat-0 NEAR-ARM source origin, palette index 2 -> red.
                WriteWalkHdFixture(modPath, "WhiteMage_Male", binPath, markerX: 304, markerY: 160);

                var modifier = new PaletteModifier();
                modifier.LoadTemplate(binPath, "WhiteMage_Male", modPath);
                modifier.SupportsWalkAnimation.Should().BeTrue();

                var frames = modifier.GetWalkPreviewFrames(5); // direction 5 = SW
                frames.Should().NotBeNull();

                var type2Pixel = frames[0].GetPixel(22, 24);
                type2Pixel.R.Should().Be(255);
                type2Pixel.G.Should().Be(0);
                type2Pixel.B.Should().Be(0);

                frames[0].GetPixel(16, 24).A.Should().Be(0, "the TYPE1 landing spot must stay empty for a TYPE2 character");
            }
            finally
            {
                if (File.Exists(binPath)) File.Delete(binPath);
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);
            }
        }

        /// <summary>Walks up from the test bin dir to the repo root (the dir holding ColorMod/Images),
        /// same approach as TodoContractTests.RepoRoot for docs/.</summary>
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

        private static Bitmap CropRegion(Bitmap source, int x, int y, int width, int height)
        {
            var crop = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            for (int sx = 0; sx < width; sx++)
            {
                for (int sy = 0; sy < height; sy++)
                {
                    if (x + sx < 0 || x + sx >= source.Width || y + sy < 0 || y + sy >= source.Height)
                        continue;
                    crop.SetPixel(sx, sy, source.GetPixel(x + sx, y + sy));
                }
            }
            return crop;
        }

        private static int CountOpaquePixels(Bitmap bmp)
        {
            int count = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    if (bmp.GetPixel(x, y).A != 0)
                        count++;
                }
            }
            return count;
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
        public void GetWalkPreviewFrames_UsesLivePalette_AndReturnsEightFrames()
        {
            var modPath = Path.Combine(Path.GetTempPath(), "WalkAnimFrames_" + Guid.NewGuid().ToString("N"));
            var binPath = Path.Combine(Path.GetTempPath(), "walk_anim_frames_test_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                // Marker at (0, 80): front beat-0 body cell's origin pixel, palette index 2.
                WriteWalkHdFixture(modPath, "TestKnight", binPath, markerX: 0, markerY: 80);

                var modifier = new PaletteModifier();
                modifier.LoadTemplate(binPath, "TestKnight", modPath); // bin palette 0 index 2 -> red

                var frames = modifier.GetWalkPreviewFrames(5); // direction 5 = SW
                frames.Should().NotBeNull();
                frames.Count.Should().Be(WalkCycle.BeatCount);

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
                animator.TimerIntervalMs.Should().Be(100); // WalkCycle.BeatDurationsMs[0]
                var frame0 = animator.ModifiedFrames[0];
                spritePreview.Image.Should().BeSameAs(frame0);

                animator.AdvanceOnce();
                animator.Phase.Should().Be(1);
                animator.TimerIntervalMs.Should().Be(133); // WalkCycle.BeatDurationsMs[1]
                var frame1 = animator.ModifiedFrames[1];
                spritePreview.Image.Should().BeSameAs(frame1);

                animator.AdvanceOnce();
                animator.Phase.Should().Be(2);
                animator.TimerIntervalMs.Should().Be(167); // WalkCycle.BeatDurationsMs[2]

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
