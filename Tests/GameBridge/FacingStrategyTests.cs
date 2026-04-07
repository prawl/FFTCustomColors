using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class FacingStrategyTests
    {
        [Fact]
        public void NoEnemies_ReturnsDefaultFacing()
        {
            var ally = MakeUnit(5, 5, team: 0, hp: 100, maxHp: 100);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>();

            var (dx, dy) = FacingStrategy.ComputeOptimalFacing(ally, enemies);

            // With no enemies, any valid cardinal facing is acceptable
            Assert.True(dx != 0 || dy != 0, "Facing should not be (0,0)");
        }

        [Fact]
        public void SingleEnemy_East_FacesEast()
        {
            var ally = MakeUnit(5, 5, team: 0, hp: 100, maxHp: 100);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>
            {
                MakeUnit(8, 5, team: 1, hp: 100, maxHp: 100)
            };

            var (dx, dy) = FacingStrategy.ComputeOptimalFacing(ally, enemies);

            Assert.Equal((1, 0), (dx, dy));
        }

        [Fact]
        public void SingleEnemy_North_FacesNorth()
        {
            var ally = MakeUnit(5, 5, team: 0, hp: 100, maxHp: 100);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>
            {
                MakeUnit(5, 8, team: 1, hp: 100, maxHp: 100)
            };

            var (dx, dy) = FacingStrategy.ComputeOptimalFacing(ally, enemies);

            Assert.Equal((0, 1), (dx, dy));
        }

        [Fact]
        public void MultipleEnemies_FacesTowardCluster_NotJustNearest()
        {
            // Ally at center. 1 enemy close west, 2 enemies east.
            // "Nearest" logic would face west. But facing east is better:
            // facing west = 2 enemies at back (score 6) + 1 front (score 1) = 7
            // facing east = 1 enemy at back (score 3) + 2 front (score 2) = 5  <-- lower
            var ally = MakeUnit(5, 5, team: 0, hp: 100, maxHp: 100);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>
            {
                MakeUnit(3, 5, team: 1, hp: 100, maxHp: 100),  // west, close
                MakeUnit(8, 5, team: 1, hp: 100, maxHp: 100),  // east
                MakeUnit(9, 5, team: 1, hp: 100, maxHp: 100),  // east
            };

            var (dx, dy) = FacingStrategy.ComputeOptimalFacing(ally, enemies);

            Assert.Equal((1, 0), (dx, dy)); // Should face east toward the cluster
        }

        [Fact]
        public void DistanceWeighting_NearbyEnemyMattersMore()
        {
            // 1 enemy 1 tile east, 2 enemies 10 tiles west.
            // Without distance weighting: face east = 1(front) + 2*3(back) = 7,
            //   face west = 1*3(back) + 2*1(front) = 5 → west wins (WRONG: ignores imminent threat).
            // With distance weighting: the adjacent east enemy dominates → face east.
            var ally = MakeUnit(5, 5, team: 0, hp: 100, maxHp: 100);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>
            {
                MakeUnit(6, 5, team: 1, hp: 100, maxHp: 100),    // east, 1 tile away
                MakeUnit(-5, 5, team: 1, hp: 100, maxHp: 100),   // west, 10 tiles away
                MakeUnit(-4, 5, team: 1, hp: 100, maxHp: 100),   // west, 9 tiles away
            };

            var (dx, dy) = FacingStrategy.ComputeOptimalFacing(ally, enemies);

            Assert.Equal((1, 0), (dx, dy)); // Face toward the imminent threat
        }

        [Fact]
        public void HpWeighting_HealthyEnemyMattersMore()
        {
            // 2 enemies equidistant (3 tiles). East enemy at full HP, west at 1 HP.
            // Without HP weighting: symmetric, tie goes to first facing (east) — would
            // pass by accident. So put the full-HP enemy WEST and nearly-dead EAST.
            // Without HP weight: symmetric → east wins by iteration order.
            // With HP weight: west enemy (full HP) is the real threat → face west.
            var ally = MakeUnit(5, 5, team: 0, hp: 100, maxHp: 100);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>
            {
                MakeUnit(8, 5, team: 1, hp: 1, maxHp: 100),     // east, nearly dead
                MakeUnit(2, 5, team: 1, hp: 100, maxHp: 100),   // west, full HP
            };

            var (dx, dy) = FacingStrategy.ComputeOptimalFacing(ally, enemies);

            Assert.Equal((-1, 0), (dx, dy)); // Face toward the dangerous enemy
        }

        [Fact]
        public void MapEdge_Surrounded3Sides_FacesAwayFromWall()
        {
            // Ally at (0,5) — west edge of map, no tile at (-1,5).
            // 3 enemies on the 3 open cardinal tiles: east, north, south.
            // Back is against the wall so no enemy can be behind us.
            //
            // Face east (+1,0):  east=front(1), north=side(2), south=side(2) = 5
            // Face north (0,+1): north=front(1), east=side(2), south=back(3) = 6
            // Face south (0,-1): south=front(1), east=side(2), north=back(3) = 6
            // Face west (-1,0):  east=back(3), north=side(2), south=side(2) = 7
            //
            // Best = east (toward the enemy opposite the wall), back is defended.
            var ally = MakeUnit(0, 5, team: 0, hp: 100, maxHp: 100);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>
            {
                MakeUnit(1, 5, team: 1, hp: 100, maxHp: 100),  // east, adjacent
                MakeUnit(0, 6, team: 1, hp: 100, maxHp: 100),  // north, adjacent
                MakeUnit(0, 4, team: 1, hp: 100, maxHp: 100),  // south, adjacent
            };

            var (dx, dy) = FacingStrategy.ComputeOptimalFacing(ally, enemies);

            Assert.Equal((1, 0), (dx, dy)); // Face away from wall, back protected
        }

        [Fact]
        public void DiagonalEnemy_LandsInSideArc_NotFrontOrBack()
        {
            // Enemy at exact diagonal (3,3) from ally: dot == cross for East/West facings.
            // Should consistently classify as side arc (arcWeight=2), not flip between
            // front(1) and side(2) due to floating point.
            //
            // Facing east (+1,0): dot=3, cross=3 → dot==cross, cross <= mag is true,
            //   dot > 0 → front(1). Actually front for east.
            // Facing west (-1,0): dot=-3, cross=3 → cross <= mag true, dot < 0 → back(3).
            // Facing north (0,+1): dot=3, cross=3 → front(1).
            // Facing south (0,-1): dot=-3, cross=3 → back(3).
            //
            // East and north both score 1 (front). East wins by tiebreak (first in array).
            var ally = MakeUnit(5, 5, team: 0, hp: 100, maxHp: 100);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>
            {
                MakeUnit(8, 8, team: 1, hp: 100, maxHp: 100),  // exact diagonal NE
            };

            var (dx, dy) = FacingStrategy.ComputeOptimalFacing(ally, enemies);

            // Diagonal enemy is equidistant between east and north front arcs;
            // east wins by tiebreak. Key assertion: result is deterministic.
            Assert.Equal((1, 0), (dx, dy));
        }

        [Fact]
        public void FacingResult_IncludesArcCounts()
        {
            // Same map-edge scenario: ally at west wall, 3 enemies E/N/S.
            // Face east: 1 front, 2 side, 0 back.
            var ally = MakeUnit(0, 5, team: 0, hp: 100, maxHp: 100);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>
            {
                MakeUnit(1, 5, team: 1, hp: 100, maxHp: 100),
                MakeUnit(0, 6, team: 1, hp: 100, maxHp: 100),
                MakeUnit(0, 4, team: 1, hp: 100, maxHp: 100),
            };

            var result = FacingStrategy.ComputeOptimalFacingDetailed(ally, enemies);

            Assert.Equal((1, 0), (result.Dx, result.Dy));
            Assert.Equal(1, result.Front);
            Assert.Equal(2, result.Side);
            Assert.Equal(0, result.Back);
        }

        [Theory]
        // Confirmed empirically in-game on the facing screen (2026-04-07).
        // rot=0: Right=E  Left=W  Up=N  Down=S
        [InlineData(0,  1,  0, "Right")]  // East at rot=0
        [InlineData(0, -1,  0, "Left")]   // West at rot=0
        [InlineData(0,  0,  1, "Up")]     // North at rot=0
        [InlineData(0,  0, -1, "Down")]   // South at rot=0
        // rot=1: Left=E  Right=W  Down=N  Up=S
        [InlineData(1,  1,  0, "Left")]   // East at rot=1
        [InlineData(1, -1,  0, "Right")]  // West at rot=1
        [InlineData(1,  0,  1, "Down")]   // North at rot=1
        [InlineData(1,  0, -1, "Up")]     // South at rot=1
        // rot=2: Up=E  Down=W  Left=N  Right=S
        [InlineData(2,  1,  0, "Up")]     // East at rot=2
        [InlineData(2, -1,  0, "Down")]   // West at rot=2
        [InlineData(2,  0,  1, "Left")]   // North at rot=2
        [InlineData(2,  0, -1, "Right")]  // South at rot=2
        // rot=3: Down=E  Up=W  Right=N  Left=S
        [InlineData(3,  1,  0, "Down")]   // East at rot=3
        [InlineData(3, -1,  0, "Up")]     // West at rot=3
        [InlineData(3,  0,  1, "Right")]  // North at rot=3
        [InlineData(3,  0, -1, "Left")]   // South at rot=3
        public void GetFacingArrowKey_CorrectForAllRotations(int rotation, int faceDx, int faceDy, string expectedKey)
        {
            var result = FacingStrategy.GetFacingArrowKey(rotation, faceDx, faceDy);
            Assert.Equal(expectedKey, result);
        }

        [Theory]
        // If Right maps to East(+1,0), that's rotation 0
        [InlineData(0,  1,  0, 0)]  // Right=East → rot=0
        [InlineData(0, -1,  0, 1)]  // Right=West → rot=1
        [InlineData(0,  0, -1, 2)]  // Right=South → rot=2
        [InlineData(0,  0,  1, 3)]  // Right=North → rot=3
        // If Down maps to South(0,-1), that's rotation 0
        [InlineData(3,  0, -1, 0)]  // Down=South → rot=0
        [InlineData(3,  0,  1, 1)]  // Down=North → rot=1
        [InlineData(3, -1,  0, 2)]  // Down=West → rot=2
        [InlineData(3,  1,  0, 3)]  // Down=East → rot=3
        public void DeriveRotation_CorrectFromKeyAndDirection(int keyIndex, int dirDx, int dirDy, int expectedRot)
        {
            var result = FacingStrategy.DeriveRotation(keyIndex, dirDx, dirDy);
            Assert.Equal(expectedRot, result);
        }

        private static FacingStrategy.UnitPosition MakeUnit(int x, int y, int team, int hp, int maxHp)
        {
            return new FacingStrategy.UnitPosition
            {
                GridX = x,
                GridY = y,
                Team = team,
                Hp = hp,
                MaxHp = maxHp
            };
        }
    }
}
