using System;
using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// One beat of a walk cycle: which 64x80 body cell to draw, and where the two 48x48 arm
    /// sub-sprites (drawn behind and in front of the body) come from and land. See
    /// <see cref="WalkCycle"/> for the full provenance of these numbers.
    /// </summary>
    public readonly struct WalkBeat
    {
        public Point BodyCell { get; }
        public Point FarArmSource { get; }
        public Point FarArmOffset { get; }
        public Point NearArmSource { get; }
        public Point NearArmOffset { get; }

        public WalkBeat(Point bodyCell, Point farArmSource, Point farArmOffset, Point nearArmSource, Point nearArmOffset)
        {
            BodyCell = bodyCell;
            FarArmSource = farArmSource;
            FarArmOffset = farArmOffset;
            NearArmSource = nearArmSource;
            NearArmOffset = nearArmOffset;
        }
    }

    /// <summary>
    /// The authoritative walk-cycle composition table (CC-36 rework round 2). The round-1 walk
    /// cycle sampled a bare row-1 "stride" cell per beat, which the owner's live pass found
    /// ARMLESS (the sheet's stride bodies carry no arms; the game composites arm sub-sprites
    /// per beat) and only HALF a stride (the right-foot-forward frames were never shown).
    ///
    /// These numbers were parsed from the game's own animation data (TYPE1.SHP / TYPE1.SEQ as
    /// shipped in the FFTPatcher repo, via a faithful port of Shishi's parser): walk forward is
    /// animation 6, walk backward is animation 7. Front sequence = SHP frames
    /// [11,10,9,10,11,12,13,12], back = [16,15,14,15,16,17,18,17]. Per-beat arm placement is the
    /// arm tile's destination offset minus the body tile's destination offset; all coordinates
    /// are doubled for these HD sheets (SPR coords -> HD coords). Per-beat durations come from
    /// SEQ delays of 6/8/10 sixtieths of a second. Do not re-derive or adjust these values.
    /// </summary>
    public static class WalkCycle
    {
        public const int BeatCount = 8;
        public const int ArmBoxSize = 48;

        public static readonly int[] BeatDurationsMs = { 100, 133, 167, 133, 100, 133, 167, 133 };

        public static readonly WalkBeat[] Front;
        public static readonly WalkBeat[] Back;

        static WalkCycle()
        {
            Front = new WalkBeat[BeatCount];
            Front[0] = new WalkBeat(new Point(0, 1), new Point(256, 160), new Point(6, 16), new Point(304, 160), new Point(16, 24));
            Front[1] = new WalkBeat(new Point(6, 0), new Point(160, 160), new Point(6, 16), new Point(208, 160), new Point(16, 24));
            Front[2] = new WalkBeat(new Point(5, 0), new Point(64, 160), new Point(8, 16), new Point(112, 160), new Point(16, 24));
            Front[3] = Front[1];
            Front[4] = Front[0];
            Front[5] = new WalkBeat(new Point(1, 1), new Point(352, 160), new Point(0, 16), new Point(400, 160), new Point(16, 24));
            Front[6] = new WalkBeat(new Point(2, 1), new Point(448, 160), new Point(0, 16), new Point(64, 208), new Point(16, 24));
            Front[7] = Front[5];

            Back = new WalkBeat[BeatCount];
            Back[0] = new WalkBeat(new Point(5, 1), new Point(352, 208), new Point(16, 16), new Point(304, 208), new Point(0, 22));
            Back[1] = new WalkBeat(new Point(4, 1), new Point(256, 208), new Point(16, 16), new Point(208, 208), new Point(0, 22));
            Back[2] = new WalkBeat(new Point(3, 1), new Point(160, 208), new Point(16, 16), new Point(112, 208), new Point(0, 22));
            Back[3] = Back[1];
            Back[4] = Back[0];
            Back[5] = new WalkBeat(new Point(6, 1), new Point(448, 208), new Point(16, 16), new Point(400, 208), new Point(0, 22));
            Back[6] = new WalkBeat(new Point(0, 2), new Point(112, 256), new Point(16, 16), new Point(64, 256), new Point(0, 22));
            Back[7] = Back[5];
        }

        /// <summary>Front for SW (the front-facing stride), Back for NW. Mirrored directions
        /// (SE/NE) never reach here; the extractor mirrors the finished SW/NW frame instead.</summary>
        public static WalkBeat[] For(Direction sourceDirection)
        {
            return sourceDirection switch
            {
                Direction.SW => Front,
                Direction.NW => Back,
                _ => throw new ArgumentException($"WalkCycle.For only accepts SW or NW, got {sourceDirection}", nameof(sourceDirection))
            };
        }
    }
}
