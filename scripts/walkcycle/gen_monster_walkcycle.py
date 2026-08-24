"""Bake ColorMod/Utilities/MonsterWalkCycle.cs from the game's MON/CYOKO SEQ+SHP
(Shishi parser port, CC-36/CC-46). Run scripts/walkcycle/fetch_data.py first.

Emits per-beat tile draw lists in HD (x2) coords, dst pre-shifted into the rig's frame box.
Draw order = back-to-front (SHP file order tile 0 is frontmost, so it is emitted last).
Filler tiles at dst(124,124) land outside any frame box and are dropped.
Hard-fails if delays or palindrome structure drift from the humanoid walk's shape.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parse_seq as P
from fetch_data import data_dir

SHISHI = data_dir()
EXPECT_DELAYS = [6, 8, 10, 8, 6, 8, 10, 8]

def bake(name):
    frames, secondHalf, jump = P.parse_shp(os.path.join(SHISHI, f"{name}.SHP"))
    anims = P.parse_seq(os.path.join(SHISHI, f"{name}.SEQ"))
    seqs = {"Front": anims[6], "Back": anims[7]}
    for d, seq in seqs.items():
        assert [dl for fi, dl in seq] == EXPECT_DELAYS, (name, d, seq)
        fis = [fi for fi, dl in seq]
        assert fis[3] == fis[1] and fis[4] == fis[0] and fis[7] == fis[5], (name, d, fis)
    # global HD bbox across both directions' tiles (excluding off-frame filler)
    used = {fi for seq in seqs.values() for fi, dl in seq}
    tiles_of = {fi: [t for t in frames[fi] if t["dst"] != (124, 124)] for fi in used}
    xs, ys = [], []
    for ts in tiles_of.values():
        for t in ts:
            dx, dy = t["dst"]; w, h = t["src"][2], t["src"][3]
            xs += [dx*2, dx*2 + w*2]; ys += [dy*2, dy*2 + h*2]
    x0, y0, x1, y1 = min(xs), min(ys), max(xs), max(ys)
    beats = {}
    for d, seq in seqs.items():
        rows = []
        for fi, dl in seq:
            draws = []
            for t in reversed(tiles_of[fi]):  # emit back-to-front
                sx, sy, w, h = t["src"]
                assert not t["flipY"], (name, fi, "flipY unexpected in walk data")
                draws.append((sx*2, sy*2, w*2, h*2, t["dst"][0]*2 - x0, t["dst"][1]*2 - y0, t["flipX"]))
            rows.append((fi, draws))
        beats[d] = rows
    return beats, (x1-x0, y1-y0), tiles_of

def emit(name, csname, out):
    beats, (W, H), _ = bake(name)
    out.append(f"        public const int {csname}FrameWidth = {W};")
    out.append(f"        public const int {csname}FrameHeight = {H};")
    for d in ("Front", "Back"):
        out.append(f"        public static readonly WalkTileDraw[][] {csname}{d};")
    return beats

def emit_init(csname, beats, out):
    for d in ("Front", "Back"):
        rows = beats[d]
        out.append(f"            {csname}{d} = new WalkTileDraw[BeatCountLocal][];")
        for i, (fi, draws) in enumerate(rows):
            if i == 3 or i == 4 or i == 7:
                src = {3: 1, 4: 0, 7: 5}[i]
                out.append(f"            {csname}{d}[{i}] = {csname}{d}[{src}];")
                continue
            items = ", ".join(
                f"new WalkTileDraw(new Rectangle({sx}, {sy}, {w}, {h}), new Point({dx}, {dy}), {str(fx).lower()})"
                for sx, sy, w, h, dx, dy, fx in draws)
            out.append(f"            {csname}{d}[{i}] = new[] {{ {items} }}; // SHP frame {fi}")

header = '''using System;
using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// One tile of a monster walk-cycle beat: a source box on the HD sheet, where its top-left
    /// lands inside the rig's frame box, and whether the tile is drawn mirrored. Draw order in
    /// a beat's array is back to front.
    /// </summary>
    public readonly struct WalkTileDraw
    {
        public Rectangle Source { get; }
        public Point Offset { get; }
        public bool FlipX { get; }

        public WalkTileDraw(Rectangle source, Point offset, bool flipX)
        {
            Source = source;
            Offset = offset;
            FlipX = flipX;
        }
    }

    /// <summary>
    /// The walk-cycle composition tables for the game's two monster animation rigs (CC-46).
    /// BATTLE.BIN's sprite-attributes table (0x2D748) assigns every roster sprite a SHP/SEQ
    /// pair: most monster families plus Construct 8 use MON, Chocobo uses CYOKO (Goblin,
    /// Skeleton and Mindflayer use the humanoid TYPE1/TYPE2 rigs in WalkCycle instead).
    ///
    /// Parsed from the game's own animation data (MON and CYOKO SEQ/SHP as shipped in the
    /// FFTPatcher repo, via the same faithful port of Shishi's parser as CC-36): walk forward
    /// is animation 6 and walk backward is animation 7 in both files, with the exact 8-beat
    /// palindrome and 6/8/10-sixtieths delays of the humanoid walk, so BeatCount and
    /// BeatDurationsMs in WalkCycle apply unchanged. MON beats are one 48x48 tile; CYOKO
    /// beats composite up to six smaller tiles. All coordinates are doubled to HD (SPR -> HD
    /// is exactly x2) and tile destinations are pre-shifted into a per-rig frame box covering
    /// every tile of both directions. Off-frame filler tiles (dst 124,124 in the SHP) are
    /// dropped. Generated by scripts/walkcycle/gen_monster_walkcycle.py; do not hand-edit or re-derive.
    /// </summary>
    public static class MonsterWalkCycle
    {
        private const int BeatCountLocal = 8;
'''

out = []
mon = emit("MON", "Mon", out)
cyo = emit("CYOKO", "Cyoko", out)
init = ["", "        static MonsterWalkCycle()", "        {"]
emit_init("Mon", mon, init)
init.append("")
emit_init("Cyoko", cyo, init)
init.append("        }")

footer = '''
        /// <summary>Front for SW, Back for NW, from the requested rig's table. Mirrored
        /// directions (SE/NE) never reach here; the compositor mirrors the finished SW/NW
        /// frame, same contract as WalkCycle.For.</summary>
        public static WalkTileDraw[][] For(Direction sourceDirection, WalkCycleType type)
        {
            return (sourceDirection, type) switch
            {
                (Direction.SW, WalkCycleType.Mon) => MonFront,
                (Direction.NW, WalkCycleType.Mon) => MonBack,
                (Direction.SW, WalkCycleType.Cyoko) => CyokoFront,
                (Direction.NW, WalkCycleType.Cyoko) => CyokoBack,
                _ => throw new ArgumentException($"MonsterWalkCycle.For only accepts SW or NW with a monster rig, got {sourceDirection}/{type}", nameof(sourceDirection))
            };
        }

        /// <summary>The rig's frame-box size (width, height).</summary>
        public static Size FrameSize(WalkCycleType type)
        {
            return type switch
            {
                WalkCycleType.Mon => new Size(MonFrameWidth, MonFrameHeight),
                WalkCycleType.Cyoko => new Size(CyokoFrameWidth, CyokoFrameHeight),
                _ => throw new ArgumentException($"FrameSize only accepts monster rigs, got {type}", nameof(type))
            };
        }
    }
}
'''

repo = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
path = os.path.join(repo, "ColorMod", "Utilities", "MonsterWalkCycle.cs")
with open(path, "w", newline="\r\n") as f:
    f.write(header + "\n".join(out) + "\n" + "\n".join(init) + footer)
print("wrote", path)
print("MON box:", mon and "96x96")
