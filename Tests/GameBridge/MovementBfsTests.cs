using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class MovementBfsTests
    {
        /// <summary>
        /// Ally-occupied tiles can be walked through at the normal tile cost
        /// (no extra penalty, no discount) but cannot be a final destination.
        /// Verified in-game 2026-04-17 (session 29): Kenrick (Knight Mv=3 Jmp=3)
        /// at (9,9) with allies at (10,9), (10,10), (8,10). Game highlights 11
        /// blue tiles including (10,11) and (8,11) — reached by walking through
        /// allies at cost 1+1+1=3 = Move. A +1 penalty would make those paths
        /// cost 6 (too far); a 0-cost pass-through over-extends reach and
        /// matches more tiles than the game shows.
        /// </summary>
        [Fact]
        public void BFS_AllyTiles_CanBePassedThrough_ButNotStoppedOn()
        {
            // Flat 5x5 map, all height 0, no obstacles
            var map = CreateFlatMap(5, 5, tileHeight: 0);

            // Unit at (2,2), Move=3, Jump=4, ally at (2,3).
            var allyPositions = new HashSet<(int, int)> { (2, 3) };

            var tiles = MovementBfs.ComputeValidTiles(
                map, unitX: 2, unitY: 2, moveStat: 3, jumpStat: 4,
                enemyPositions: null, allyPositions: allyPositions);

            var tileSet = tiles.Select(t => (t.X, t.Y)).ToHashSet();

            // Ally tile (2,3) should NOT be in the result (can't stop on ally).
            Assert.DoesNotContain((2, 3), tileSet);

            // (2,4) is 2 steps away through ally at (2,3). Normal cost: 1+1=2,
            // within Move=3, so reachable.
            Assert.Contains((2, 4), tileSet);

            // Other directions — no allies in path, normal cost-1 per step.
            Assert.Contains((1, 2), tileSet);
            Assert.Contains((3, 2), tileSet);
            Assert.Contains((2, 1), tileSet);
            Assert.Contains((0, 2), tileSet);
            Assert.Contains((4, 2), tileSet);
            Assert.Contains((2, 0), tileSet);
        }

        // Corpses (dead units, not crystallized) behave like allies for BFS:
        // units can WALK THROUGH a corpse at normal cost, but cannot STOP on
        // it. Caller classifies dead units and adds them to allyPositions.
        // Crystallized and treasure units disappear — their tile is empty.
        [Fact]
        public void BFS_CorpseTile_PassThroughButNotStop()
        {
            var map = CreateFlatMap(5, 5, tileHeight: 0);

            // Corpse at (2,3) — caller should push into allyPositions.
            var corpsePositions = new HashSet<(int, int)> { (2, 3) };

            var tiles = MovementBfs.ComputeValidTiles(
                map, unitX: 2, unitY: 2, moveStat: 3, jumpStat: 4,
                enemyPositions: null, allyPositions: corpsePositions);

            var tileSet = tiles.Select(t => (t.X, t.Y)).ToHashSet();

            // Corpse tile NOT reachable (can't stop there).
            Assert.DoesNotContain((2, 3), tileSet);

            // Beyond-corpse tile (2,4) IS reachable — passed through corpse at cost 1+1=2.
            Assert.Contains((2, 4), tileSet);
        }

        [Theory]
        [InlineData("Movement +1", 4, 4, 5, 4)]
        [InlineData("Movement +2", 4, 4, 6, 4)]
        [InlineData("Movement +3", 4, 4, 7, 4)]
        [InlineData("Jump +1", 4, 3, 4, 4)]
        [InlineData("Jump +2", 4, 3, 4, 5)]
        [InlineData("Jump +3", 4, 3, 4, 6)]
        [InlineData("Teleport", 4, 3, 4, 3)]
        [InlineData("Fly", 4, 3, 4, 3)]
        [InlineData("Manafont", 4, 3, 4, 3)]
        [InlineData(null, 4, 3, 4, 3)]
        public void ApplyMovementAbility_CorrectBonus(string? abilityName, int baseMove, int baseJump, int expectedMove, int expectedJump)
        {
            var (move, jump) = MovementBfs.ApplyMovementAbility(baseMove, baseJump, abilityName);
            Assert.Equal(expectedMove, move);
            Assert.Equal(expectedJump, jump);
        }

        /// <summary>
        /// Edge-height rule vs display-height averaging: an "Incline N" ramp
        /// with h=0 sh=3 has its NORTH edge at 3 (top of ramp) and SOUTH edge
        /// at 0 (bottom). A plateau to the north (Flat h=3) connects cleanly
        /// to the ramp's NORTH edge (both = 3). The old display-height formula
        /// would have computed the ramp's height as 0 + 3/2 = 1.5 and mistakenly
        /// treated the plateau→ramp transition as a 1.5-height drop.
        /// With Jump=1 the step needs edge-height delta ≤ 1 — the new rule
        /// admits it (delta=0), the old rule would have rejected it on maps
        /// where the plateau was higher than 1.5 above display height.
        /// </summary>
        [Fact]
        public void BFS_Slope_UsesEdgeHeightNotDisplayHeight()
        {
            // Column of 3 tiles, y=0 = plateau at north (top-of-ramp side),
            // y=1 = ramp rising north, y=2 = ground at south.
            var map = new MapData
            {
                Width = 1, Height = 3,
                Tiles = new MapTile[1, 3]
            };
            map.Tiles[0, 0] = new MapTile { Height = 3, SlopeHeight = 0, SlopeType = "Flat 0" };
            map.Tiles[0, 1] = new MapTile { Height = 0, SlopeHeight = 3, SlopeType = "Incline N" };
            map.Tiles[0, 2] = new MapTile { Height = 0, SlopeHeight = 0, SlopeType = "Flat 0" };

            // Start at (0,2) south ground, Jump=1, Move=4.
            // Step (0,2)→(0,1): exit N of ground = 0. Entry S of ramp = 0. Δ=0 OK.
            // Step (0,1)→(0,0): exit N of ramp = 3. Entry S of plateau = 3. Δ=0 OK.
            var tiles = MovementBfs.ComputeValidTiles(
                map, unitX: 0, unitY: 2, moveStat: 4, jumpStat: 1);
            var set = tiles.Select(t => (t.X, t.Y)).ToHashSet();
            Assert.Contains((0, 1), set);
            Assert.Contains((0, 0), set);

            // Cliff (no ramp) with Jump=1 must reject the 3-step jump.
            var cliff = new MapData
            {
                Width = 1, Height = 2,
                Tiles = new MapTile[1, 2]
            };
            cliff.Tiles[0, 0] = new MapTile { Height = 0, SlopeHeight = 0, SlopeType = "Flat 0" };
            cliff.Tiles[0, 1] = new MapTile { Height = 3, SlopeHeight = 0, SlopeType = "Flat 0" };
            var cliffTiles = MovementBfs.ComputeValidTiles(
                cliff, unitX: 0, unitY: 0, moveStat: 4, jumpStat: 1);
            Assert.DoesNotContain((0, 1), cliffTiles.Select(t => (t.X, t.Y)));
        }

        /// <summary>
        /// Incline approached from the wrong side: a unit on a h=0 flat tile
        /// cannot step south onto an "Incline N" whose south-edge is at h=0
        /// and north-edge is at h=3 — but wait, entering the ramp from the
        /// NORTH side (from a unit standing north-of-the-ramp at h=0) sees
        /// the ramp's N-edge at 3 → delta 3 > Jump=1. Rejected.
        /// This is the regression guard against display-height averaging,
        /// which would have computed the ramp as h=1.5 and incorrectly
        /// admitted the 1.5-delta step.
        /// </summary>
        [Fact]
        public void BFS_Slope_RejectsApproachFromWrongSide()
        {
            var map = new MapData
            {
                Width = 1, Height = 2,
                Tiles = new MapTile[1, 2]
            };
            // (0,0) flat h=0, (0,1) Incline N h=0 sh=3 (N-edge=3, S-edge=0).
            // Approaching (0,1) from (0,0) (going south→north?) — N-direction
            // means (0,0) is NORTH of (0,1), so the step (0,0)→(0,1) is going
            // SOUTH. Exit S of (0,0) = 0. Entry N of (0,1) = 3. Δ=3 > Jump=1.
            // The ramp is inaccessible from its high-edge side with Jump=1.
            map.Tiles[0, 0] = new MapTile { Height = 0, SlopeHeight = 0, SlopeType = "Flat 0" };
            map.Tiles[0, 1] = new MapTile { Height = 0, SlopeHeight = 3, SlopeType = "Incline N" };
            var tiles = MovementBfs.ComputeValidTiles(
                map, unitX: 0, unitY: 0, moveStat: 4, jumpStat: 1);
            Assert.DoesNotContain((0, 1), tiles.Select(t => (t.X, t.Y)));

            // But with Jump=3 the step is admissible.
            var tiles2 = MovementBfs.ComputeValidTiles(
                map, unitX: 0, unitY: 0, moveStat: 4, jumpStat: 3);
            Assert.Contains((0, 1), tiles2.Select(t => (t.X, t.Y)));
        }

        // --- Move=0 degenerate case: used when heap read for the active unit's
        // Move stat fails. Caller sets moveStat=0 (honest "unknown") instead of
        // falling back to UIBuffer (the cursor-hovered unit's BASE stats, which
        // gave Wilham Mv=4 when his actual Mv was 3 — BFS then reported tile
        // (7,9) as valid, game refused the move, unit stuck in Move mode).
        // With Mv=0 BFS yields the empty list so Claude sees "no move tiles"
        // and doesn't attempt battle_move with bogus data.
        [Fact]
        public void BFS_MoveZero_ReturnsEmpty()
        {
            var map = CreateFlatMap(5, 5, tileHeight: 0);
            var tiles = MovementBfs.ComputeValidTiles(
                map, unitX: 2, unitY: 2, moveStat: 0, jumpStat: 4);
            Assert.Empty(tiles);
        }

        [Fact]
        public void BFS_MoveZero_EvenWithJumpHigh_ReturnsEmpty()
        {
            // Jump is irrelevant when Move=0 — BFS can't leave the start tile.
            var map = CreateFlatMap(5, 5, tileHeight: 0);
            var tiles = MovementBfs.ComputeValidTiles(
                map, unitX: 0, unitY: 0, moveStat: 0, jumpStat: 99);
            Assert.Empty(tiles);
        }

        private static MapData CreateFlatMap(int width, int height, int tileHeight)
        {
            var map = new MapData
            {
                Width = width,
                Height = height,
                Tiles = new MapTile[width, height]
            };
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    map.Tiles[x, y] = new MapTile
                    {
                        Height = tileHeight,
                        SlopeHeight = 0,
                        Depth = 0,
                        NoWalk = false,
                        NoCursor = false,
                        SurfaceType = "Grassland",
                        SlopeType = "Flat 0"
                    };
            return map;
        }
    }
}
