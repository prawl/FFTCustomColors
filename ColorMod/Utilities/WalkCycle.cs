using System;
using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Which of the game's animation rigs a sprite sheet was drawn for. FFT ships two SEQ/SHP
    /// pairs for standard humanoids, TYPE1 (the squire/armored skeleton) and TYPE2 (the
    /// robed/skirted skeleton). Both rigs share body cells, arm source tiles and beat timing;
    /// ONLY the per-beat arm destination offsets differ, because each rig's arm art is drawn
    /// at a different position inside its tile. Compositing a sheet with the other rig's
    /// offsets dislocates the arms. Monsters add two more rigs,
    /// MON (most families plus Construct 8, one tile per beat) and CYOKO (Chocobo,
    /// multi-tile composites), whose tables live in <see cref="MonsterWalkCycle"/> and share
    /// this class's beat count and durations. <see cref="WalkCycleTypes"/> says which rig
    /// each roster character uses.
    /// </summary>
    public enum WalkCycleType
    {
        Type1,
        Type2,
        Mon,
        Cyoko
    }

    /// <summary>
    /// One beat of a walk cycle, which 64x80 body cell to draw, and where the two 48x48 arm
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
    /// The authoritative walk-cycle composition tables. The real 8-beat composition is parsed
    /// from the game's TYPE1.SEQ/TYPE1.SHP, and the TYPE2 pair was added after the owner's live pass
    /// found dislocated arms on every robed/skirted character (TYPE1 offsets had been applied
    /// to everyone).
    ///
    /// These numbers were parsed from the game's own animation data (TYPE1 and TYPE2 SEQ/SHP as
    /// shipped in the FFTPatcher repo, via a faithful port of Shishi's parser): walk forward is
    /// animation 6, walk backward is animation 7 in BOTH files. Front sequence = SHP frames
    /// [11,10,9,10,11,12,13,12], back = [16,15,14,15,16,17,18,17], identical for both types, as
    /// are the body tiles and arm source tiles. Per-beat arm placement is the arm tile's
    /// destination offset minus the body tile's destination offset, the ONLY thing that
    /// differs between TYPE1 and TYPE2. All coordinates are doubled for these HD sheets (SPR
    /// coords -> HD coords). Per-beat durations come from SEQ delays of 6/8/10 sixtieths of a
    /// second. Do not re-derive or adjust these values.
    /// </summary>
    public static class WalkCycle
    {
        public const int BeatCount = 8;
        public const int ArmBoxSize = 48;

        /// <summary>Identical for both types (same SEQ delays in TYPE1.SEQ and TYPE2.SEQ).</summary>
        public static readonly int[] BeatDurationsMs = { 100, 133, 167, 133, 100, 133, 167, 133 };

        public static readonly WalkBeat[] Type1Front;
        public static readonly WalkBeat[] Type1Back;
        public static readonly WalkBeat[] Type2Front;
        public static readonly WalkBeat[] Type2Back;

        static WalkCycle()
        {
            Type1Front = new WalkBeat[BeatCount];
            Type1Front[0] = new WalkBeat(new Point(0, 1), new Point(256, 160), new Point(6, 16), new Point(304, 160), new Point(16, 24));
            Type1Front[1] = new WalkBeat(new Point(6, 0), new Point(160, 160), new Point(6, 16), new Point(208, 160), new Point(16, 24));
            Type1Front[2] = new WalkBeat(new Point(5, 0), new Point(64, 160), new Point(8, 16), new Point(112, 160), new Point(16, 24));
            Type1Front[3] = Type1Front[1];
            Type1Front[4] = Type1Front[0];
            Type1Front[5] = new WalkBeat(new Point(1, 1), new Point(352, 160), new Point(0, 16), new Point(400, 160), new Point(16, 24));
            Type1Front[6] = new WalkBeat(new Point(2, 1), new Point(448, 160), new Point(0, 16), new Point(64, 208), new Point(16, 24));
            Type1Front[7] = Type1Front[5];

            Type1Back = new WalkBeat[BeatCount];
            Type1Back[0] = new WalkBeat(new Point(5, 1), new Point(352, 208), new Point(16, 16), new Point(304, 208), new Point(0, 22));
            Type1Back[1] = new WalkBeat(new Point(4, 1), new Point(256, 208), new Point(16, 16), new Point(208, 208), new Point(0, 22));
            Type1Back[2] = new WalkBeat(new Point(3, 1), new Point(160, 208), new Point(16, 16), new Point(112, 208), new Point(0, 22));
            Type1Back[3] = Type1Back[1];
            Type1Back[4] = Type1Back[0];
            Type1Back[5] = new WalkBeat(new Point(6, 1), new Point(448, 208), new Point(16, 16), new Point(400, 208), new Point(0, 22));
            Type1Back[6] = new WalkBeat(new Point(0, 2), new Point(112, 256), new Point(16, 16), new Point(64, 256), new Point(0, 22));
            Type1Back[7] = Type1Back[5];

            // TYPE2, same body cells and arm sources; the arm offsets are the rig's own.
            // Negative X offsets are real (the far arm tucks past the body cell's left edge on
            // front beat 0, and the near arm on back beat 5); the compositor clips them.
            Type2Front = new WalkBeat[BeatCount];
            Type2Front[0] = new WalkBeat(new Point(0, 1), new Point(256, 160), new Point(-2, 18), new Point(304, 160), new Point(22, 24));
            Type2Front[1] = new WalkBeat(new Point(6, 0), new Point(160, 160), new Point(2, 16), new Point(208, 160), new Point(18, 24));
            Type2Front[2] = new WalkBeat(new Point(5, 0), new Point(64, 160), new Point(6, 18), new Point(112, 160), new Point(16, 22));
            Type2Front[3] = Type2Front[1];
            Type2Front[4] = Type2Front[0];
            Type2Front[5] = new WalkBeat(new Point(1, 1), new Point(352, 160), new Point(0, 16), new Point(400, 160), new Point(18, 22));
            Type2Front[6] = new WalkBeat(new Point(2, 1), new Point(448, 160), new Point(0, 18), new Point(64, 208), new Point(20, 22));
            Type2Front[7] = Type2Front[5];

            Type2Back = new WalkBeat[BeatCount];
            Type2Back[0] = new WalkBeat(new Point(5, 1), new Point(352, 208), new Point(16, 16), new Point(304, 208), new Point(0, 24));
            Type2Back[1] = new WalkBeat(new Point(4, 1), new Point(256, 208), new Point(24, 18), new Point(208, 208), new Point(2, 24));
            Type2Back[2] = new WalkBeat(new Point(3, 1), new Point(160, 208), new Point(20, 20), new Point(112, 208), new Point(4, 22));
            Type2Back[3] = Type2Back[1];
            Type2Back[4] = Type2Back[0];
            Type2Back[5] = new WalkBeat(new Point(6, 1), new Point(448, 208), new Point(24, 18), new Point(400, 208), new Point(-2, 22));
            Type2Back[6] = new WalkBeat(new Point(0, 2), new Point(112, 256), new Point(24, 18), new Point(64, 256), new Point(2, 22));
            Type2Back[7] = Type2Back[5];
        }

        /// <summary>Front for SW (the front-facing stride), Back for NW, from the requested
        /// rig's table. Mirrored directions (SE/NE) never reach here; the compositor mirrors
        /// the finished SW/NW frame instead.</summary>
        public static WalkBeat[] For(Direction sourceDirection, WalkCycleType type)
        {
            return (sourceDirection, type) switch
            {
                (Direction.SW, WalkCycleType.Type1) => Type1Front,
                (Direction.NW, WalkCycleType.Type1) => Type1Back,
                (Direction.SW, WalkCycleType.Type2) => Type2Front,
                (Direction.NW, WalkCycleType.Type2) => Type2Back,
                _ => throw new ArgumentException($"WalkCycle.For only accepts SW or NW, got {sourceDirection}", nameof(sourceDirection))
            };
        }
    }
}
