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
        /// Ally-occupied tiles can be walked through but cost +1 move point
        /// and cannot be a final destination. Verified in-game 2026-04-13:
        /// Kenrick at (10,9) Move=4 Jump=4 with allies at (9,9), (10,10), (10,6).
        /// Without ally penalty: BFS returns 13 tiles (3 extras past allies).
        /// With ally penalty: BFS returns exactly the 10 game-valid tiles.
        /// </summary>
        [Fact]
        public void BFS_AllyTiles_CostExtraAndCannotBeDestination()
        {
            // Flat 3x3 map, all height 0, no obstacles
            var map = CreateFlatMap(5, 5, tileHeight: 0);

            // Unit at (2,2), Move=2, Jump=4
            // Ally at (2,3) — directly adjacent
            var allyPositions = new HashSet<(int, int)> { (2, 3) };

            var tiles = MovementBfs.ComputeValidTiles(
                map, unitX: 2, unitY: 2, moveStat: 2, jumpStat: 4,
                enemyPositions: null, allyPositions: allyPositions);

            var tileSet = tiles.Select(t => (t.X, t.Y)).ToHashSet();

            // Ally tile (2,3) should NOT be in the result (can't stop on ally)
            Assert.DoesNotContain((2, 3), tileSet);

            // (2,4) is 2 tiles away normally, but through ally at (2,3) costs 1+2=3 > Move=2
            // So (2,4) should NOT be reachable
            Assert.DoesNotContain((2, 4), tileSet);

            // (1,2), (3,2), (2,1) should all be reachable (no ally in path, cost=1)
            Assert.Contains((1, 2), tileSet);
            Assert.Contains((3, 2), tileSet);
            Assert.Contains((2, 1), tileSet);

            // (0,2), (4,2), (2,0) should be reachable (cost=2, no ally in path)
            Assert.Contains((0, 2), tileSet);
            Assert.Contains((4, 2), tileSet);
            Assert.Contains((2, 0), tileSet);
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
