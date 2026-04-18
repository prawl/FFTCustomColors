using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure function: given (attacker x/y, attacker elevation, target x/y, target
    /// elevation) plus a height lookup callback <c>heightAt(x, y)</c>, decide
    /// whether any intermediate tile's terrain height obstructs a straight-line
    /// projectile path.
    ///
    /// Algorithm:
    ///   1. Walk the straight-line path from attacker tile to target tile using
    ///      a Bresenham-style DDA — at least one tile per cell crossed.
    ///   2. For each INTERMEDIATE tile (excluding attacker and target), compute
    ///      the line-of-sight altitude at that tile's midpoint by linear
    ///      interpolation between attacker elevation and target elevation.
    ///   3. If the tile's terrain height exceeds the LoS altitude, the projectile
    ///      is blocked.
    ///
    /// This is LoS Option B from TODO §Tier 3. The memory-based "damage preview"
    /// path was conclusively ruled out in session 30 (see
    /// memory/project_damage_preview_hunt_s30.md).
    /// </summary>
    public class LineOfSightCalculatorTests
    {
        // heightAt(x, y) returns the blocking terrain height at that tile. We
        // model tiles as flat — the line has to clear each tile's "top surface".

        [Fact]
        public void SameTile_Unblocked()
        {
            // An attacker at its own tile has trivial LoS to itself.
            var result = LineOfSightCalculator.HasLineOfSight(
                5, 5, attackerElevation: 2, 5, 5, targetElevation: 2,
                heightAt: (x, y) => 0);
            Assert.True(result);
        }

        [Fact]
        public void AdjacentTile_Unblocked()
        {
            // One tile apart, no intermediates to check → always unblocked.
            var result = LineOfSightCalculator.HasLineOfSight(
                5, 5, 2, 6, 5, 2, heightAt: (x, y) => 99);
            Assert.True(result);
        }

        [Fact]
        public void FlatLand_StraightShot_Unblocked()
        {
            // Attacker (0,0) height 2, target (5,0) height 2, all terrain at 0.
            // The projectile flies over flat ground; no blocking.
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 2, 5, 0, 2, heightAt: (x, y) => 0);
            Assert.True(result);
        }

        [Fact]
        public void WallAtMidpoint_Blocked()
        {
            // Attacker (0,0) at h=2, target (4,0) at h=2. Tile (2,0) has height 5
            // (a wall). Line of sight = 2 at all x, wall is at 5. Blocked.
            int HeightAt(int x, int y) => (x == 2 && y == 0) ? 5 : 0;
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 2, 4, 0, 2, HeightAt);
            Assert.False(result);
        }

        [Fact]
        public void LowObstacle_BelowLine_NotBlocked()
        {
            // Attacker (0,0) h=5, target (4,0) h=5. Tile (2,0) height 3 — below
            // the LoS altitude of 5. Projectile clears it.
            int HeightAt(int x, int y) => (x == 2 && y == 0) ? 3 : 0;
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 5, 4, 0, 5, HeightAt);
            Assert.True(result);
        }

        [Fact]
        public void LowElevationAttacker_HighTarget_OvershootsWall()
        {
            // Attacker (0,0) h=0, target (4,0) h=8. Midpoint LoS altitude ~= 4.
            // Wall at midpoint height 3 → doesn't block (line is at ~4 there).
            int HeightAt(int x, int y) => (x == 2 && y == 0) ? 3 : 0;
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 0, 4, 0, 8, HeightAt);
            Assert.True(result);
        }

        [Fact]
        public void LowElevationAttacker_HighTarget_WallTooHigh_Blocks()
        {
            // Same attacker (0,0) h=0, target (4,0) h=8. Midpoint LoS = 4.
            // Wall at midpoint height 6 → blocks (6 > 4).
            int HeightAt(int x, int y) => (x == 2 && y == 0) ? 6 : 0;
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 0, 4, 0, 8, HeightAt);
            Assert.False(result);
        }

        [Fact]
        public void DiagonalPath_WallOnDiagonal_Blocks()
        {
            // Attacker (0,0) h=2, target (4,4) h=2. Tile (2,2) is on the diagonal.
            int HeightAt(int x, int y) => (x == 2 && y == 2) ? 8 : 0;
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 2, 4, 4, 2, HeightAt);
            Assert.False(result);
        }

        [Fact]
        public void DiagonalPath_WallOffDiagonal_DoesNotBlock()
        {
            // Wall at (3, 0) is NOT on the (0,0)→(4,4) diagonal. LoS is clear.
            int HeightAt(int x, int y) => (x == 3 && y == 0) ? 99 : 0;
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 2, 4, 4, 2, HeightAt);
            Assert.True(result);
        }

        [Fact]
        public void TargetTile_Height_DoesNotBlockSelf()
        {
            // A wall AT the target tile shouldn't block — the target IS on that
            // tile. Only intermediate tiles can obstruct.
            int HeightAt(int x, int y) => (x == 4 && y == 0) ? 99 : 0;
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 2, 4, 0, 2, HeightAt);
            Assert.True(result);
        }

        [Fact]
        public void AttackerTile_Height_DoesNotBlockSelf()
        {
            // Same — wall at the attacker's own tile doesn't block.
            int HeightAt(int x, int y) => (x == 0 && y == 0) ? 99 : 0;
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 2, 4, 0, 2, HeightAt);
            Assert.True(result);
        }

        [Fact]
        public void ShortRangeVertical_Unblocked()
        {
            // (3,1) → (3,3), flat ground. Line walks through (3,2); no obstacle.
            var result = LineOfSightCalculator.HasLineOfSight(
                3, 1, 2, 3, 3, 2, heightAt: (x, y) => 0);
            Assert.True(result);
        }

        [Fact]
        public void NullHeightCallback_Treats0AsBaseline()
        {
            // Defensive: if heightAt is null, default to unblocked (no data → no
            // obstruction).
            var result = LineOfSightCalculator.HasLineOfSight(
                0, 0, 2, 4, 0, 2, heightAt: null);
            Assert.True(result);
        }
    }
}
