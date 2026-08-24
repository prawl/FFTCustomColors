using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    public enum Direction
    {
        SW,
        NW,
        NE,
        SE
    }

    /// <summary>
    /// Per-character HD-sheet frame layout. Standard humanoids use 64x80 cells with SW at
    /// cell (1, 0) and NW at cell (3, 0). Non-standard characters (e.g. Construct 8 / tetsu,
    /// whose native frames are 48x48 instead of 32x40) need a different cell size.
    /// </summary>
    public readonly struct FrameLayout
    {
        public int FrameWidth { get; }
        public int FrameHeight { get; }
        public int SwCol { get; }   // SW source column (frame index, not pixel)
        public int NwCol { get; }   // NW source column
        public int Row { get; }     // Row index
        public int OffsetX { get; } // pixel offset of the grid origin (for sheets whose frames aren't grid-aligned)
        public int OffsetY { get; }

        // Explicit-rect mode: irregular monster sheets often place the two chosen poses at
        // positions a single uniform cell size can't isolate (different widths, non-uniform
        // pitch — a wide cell that centers one pose swallows the other's neighbor). In that case
        // the SW and NW source boxes are given outright; NE = mirror(NW), SE = mirror(SW).
        public bool UsesExplicitRects { get; }
        public Rectangle SwRect { get; }
        public Rectangle NwRect { get; }

        public FrameLayout(int frameWidth, int frameHeight, int swCol, int nwCol, int row, int offsetX = 0, int offsetY = 0)
        {
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            SwCol = swCol;
            NwCol = nwCol;
            Row = row;
            OffsetX = offsetX;
            OffsetY = offsetY;
            UsesExplicitRects = false;
            SwRect = Rectangle.Empty;
            NwRect = Rectangle.Empty;
        }

        private FrameLayout(Rectangle swRect, Rectangle nwRect)
        {
            // Frame size = SW box size (SW and NW boxes should match for a uniform carousel).
            FrameWidth = swRect.Width;
            FrameHeight = swRect.Height;
            SwCol = 0; NwCol = 0; Row = 0; OffsetX = 0; OffsetY = 0;
            UsesExplicitRects = true;
            SwRect = swRect;
            NwRect = nwRect;
        }

        /// <summary>Explicit per-pose source boxes (SW and NW); NE/SE are their mirrors.</summary>
        public static FrameLayout Rects(int swX, int swY, int swW, int swH, int nwX, int nwY, int nwW, int nwH)
            => new FrameLayout(new Rectangle(swX, swY, swW, swH), new Rectangle(nwX, nwY, nwW, nwH));

        public static readonly FrameLayout Standard = new FrameLayout(64, 80, 1, 3, 0);

        public static FrameLayout For(string characterName)
        {
            return characterName switch
            {
                "Construct8" => new FrameLayout(96, 96, 1, 3, 0),
                // Chocobo's HD sheet is irregular (poses aren't grid-aligned). The two clean
                // front-facing standing poses (grid cells 6,5 and 7,5) sit at pixel (368,400),
                // 64x64 each — SW = col 0, NW = col 1 from that origin.
                "Chocobo" => new FrameLayout(64, 64, 0, 1, 0, 368, 400),

                // Monster families — explicit per-pose boxes (irregular sheets, see
                // docs/ADDING_A_MONSTER.md). SW = the front-facing standing pose, NW = the
                // away-facing one (editor mirrors SW→SE, NW→NE). Boxes were human-picked off the
                // numbered-pose images and auto-centered by scripts/monster/pick_poses.py.
                "Goblin" => FrameLayout.Rects(76, 1, 46, 78, 205, 4, 46, 78),
                "Bomb" => FrameLayout.Rects(109, 19, 62, 70, 303, 19, 62, 70),
                "Panther" => FrameLayout.Rects(109, 19, 74, 72, 300, 21, 74, 72),
                "Mindflayer" => FrameLayout.Rects(72, 3, 52, 74, 199, 3, 52, 74),
                "Skeleton" => FrameLayout.Rects(75, 1, 50, 80, 202, 2, 50, 80),
                "Ghost" => FrameLayout.Rects(115, 21, 56, 70, 308, 23, 56, 70),
                "Ahriman" => FrameLayout.Rects(123, 13, 50, 76, 315, 16, 50, 76),
                // Explicit per-pose boxes (irregular sheet): SW = 2nd standing bird, NW = 4th.
                "Aevis" => FrameLayout.Rects(126, 110, 44, 76, 318, 109, 44, 76),
                "Pig" => FrameLayout.Rects(127, 17, 36, 66, 319, 17, 36, 66),
                "Treant" => FrameLayout.Rects(105, 1, 82, 92, 296, 2, 82, 92),
                "Minotaur" => FrameLayout.Rects(107, 1, 64, 88, 293, 1, 64, 88),
                "Malboro" => FrameLayout.Rects(105, 8, 74, 86, 299, 7, 74, 86),
                "Behemoth" => FrameLayout.Rects(99, 1, 80, 90, 290, 1, 80, 90),
                "Dragon" => FrameLayout.Rects(98, 0, 88, 92, 291, 1, 88, 92),
                // Hydra/Tiamat (battle_dora2_spr.bin, HD 1098_Tiamat). Same dragon rig as Dragon:
                // SW = pose col1 (front), NW = pose col3 (away), row 0. Detected on 1098_Tiamat_hd.bmp.
                "Hydra" => FrameLayout.Rects(100, 8, 86, 82, 292, 8, 86, 82),

                _ => Standard
            };
        }

        // --- CC-36: walk-cycle support --------------------------------------------------
        //
        // Derived by direct inspection of shipped HD BMPs (Knight_Male, Monk_Male,
        // Squire_Female, RamzaChapter1, Agrias, DarkKnight_Male, Lettie) this session. Every
        // standard humanoid sheet is 512x512, 4bpp indexed, 64x80 cells, 8 columns per row:
        //
        //   Row 0 = the five standing poses (W, SW, S, NW, N, ...).
        //   Row 1 = the walk stride: cols 0-2 are the front-facing step (A, passing, B), cols
        //           3-6 are the back-facing step (A, passing, B, passing).
        //
        // Non-standard sheets (Construct8's 96x96 grid, Chocobo's offset grid, every monster's
        // explicit-rect layout) don't carry this row-1 stride in a derivable place, so they
        // never support the walk cycle.

        /// <summary>Number of distinct poses in one full walk cycle (one full stride, looping).</summary>
        public const int WalkPhaseCount = 4;

        // Row 1, cols 0/1/2, replaying the passing pose (1) for the loop's closing beat.
        private static readonly int[] FrontWalkColumns = { 0, 1, 2, 1 };
        // Row 1, cols 3/4/5/6: the back-facing stride already has four distinct cells.
        private static readonly int[] BackWalkColumns = { 3, 4, 5, 6 };

        /// <summary>
        /// True only for the standard humanoid layout (see the derivation above). Construct 8's
        /// 96x96 columns, Chocobo's offset grid and every explicit-rect monster layout all
        /// return false, since their sheets have no row-1 stride to sample.
        /// </summary>
        public bool SupportsWalkCycle =>
            !UsesExplicitRects && FrameWidth == 64 && FrameHeight == 80 && SwCol == 1
            && NwCol == 3 && Row == 0 && OffsetX == 0 && OffsetY == 0;

        /// <summary>
        /// The row-1 cell (col, row) for one beat of the walk cycle. <paramref name="sourceDirection"/>
        /// is SW or NW only; mirroring to the SE/NE facing is the extractor's job, exactly as
        /// for the standing poses. <paramref name="phase"/> is taken modulo <see cref="WalkPhaseCount"/>
        /// defensively.
        /// </summary>
        public Point GetWalkCell(Direction sourceDirection, int phase)
        {
            int p = ((phase % WalkPhaseCount) + WalkPhaseCount) % WalkPhaseCount;
            var columns = sourceDirection == Direction.NW ? BackWalkColumns : FrontWalkColumns;
            return new Point(columns[p], 1);
        }
    }
}
