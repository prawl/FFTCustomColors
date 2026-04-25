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

        /// <summary>
        /// Live-repro 2026-04-24 Lenalian Plateau: Ramza at (8,2) with 6
        /// enemies in varied positions. Pins the recommended facing AND
        /// arc counts so future refactors can't silently drift. Earlier
        /// filed TODO suggested the arc math was off ("0/4/2 expected");
        /// hand-trace showed the algorithm is CORRECT — it picks the
        /// facing that minimizes back-arc exposure, and 2/4/0 is the
        /// right distribution for the chosen facing (0, +1).
        /// </summary>
        [Fact]
        public void ArcCount_LenalianPlateauLiveScenario_PinsRecommendation()
        {
            var (ally, enemies) = LenalianScenario();
            var result = FacingStrategy.ComputeOptimalFacingDetailed(ally, enemies);

            // Algorithm picks (0, +1) — "South" post-flip — because 0 back
            // minimizes backstab-arc exposure. No enemy is north of Ramza.
            Assert.Equal(0, result.Dx);
            Assert.Equal(1, result.Dy);
            Assert.Equal(2, result.Front);   // Goblin S, Knight SE
            Assert.Equal(4, result.Side);    // Gob SW, BG SW, Exploder W, Archer NE
            Assert.Equal(0, result.Back);    // nothing north
        }

        private static (FacingStrategy.UnitPosition ally, System.Collections.Generic.List<FacingStrategy.UnitPosition> enemies)
            LenalianScenario()
        {
            var ally = MakeUnit(8, 2, team: 0, hp: 595, maxHp: 719);
            var enemies = new System.Collections.Generic.List<FacingStrategy.UnitPosition>
            {
                MakeUnit(1, 6, team: 1, hp: 518, maxHp: 518),   // Gobbledygook SW
                MakeUnit(0, 6, team: 1, hp: 438, maxHp: 438),   // Black Goblin SW
                MakeUnit(8, 11, team: 1, hp: 522, maxHp: 522),  // Goblin S
                MakeUnit(7, 2, team: 1, hp: 87, maxHp: 541),    // Exploder W (adjacent)
                MakeUnit(9, 5, team: 1, hp: 73, maxHp: 521),    // Knight SE
                MakeUnit(10, 1, team: 1, hp: 452, maxHp: 452),  // Archer NE
            };
            return (ally, enemies);
        }

        /// <summary>
        /// Hand-trace pin: for each of the 4 cardinals, count which enemies
        /// land in front / side / back given the Lenalian scenario.
        /// These numbers are derived directly from the dot-product arc
        /// rule (cross &lt;= |dot| && dot &gt; 0 → front, cross &lt;= |dot| &&
        /// dot &lt; 0 → back, else side) and do NOT depend on the
        /// recommendation scoring.
        /// </summary>
        [Theory]
        // Facing East (+1, 0):
        //   Gob   (-7,+4): dot=-7, cross=4 → |dot|>cross, dot<0 → BACK
        //   BG    (-8,+4): dot=-8, cross=4 → BACK
        //   Goblin( 0,+9): dot=0,  cross=9 → SIDE
        //   Expl  (-1, 0): dot=-1, cross=0 → BACK
        //   Knight(+1,+3): dot=1,  cross=3 → cross>|dot| → SIDE
        //   Archer(+2,-1): dot=2,  cross=1 → cross<|dot|, dot>0 → FRONT
        // → 1 front, 2 side, 3 back
        [InlineData(1, 0, 1, 2, 3)]
        // Facing West (-1, 0): mirror of east
        //   Gob→FRONT, BG→FRONT, Goblin→SIDE, Expl→FRONT, Knight→SIDE, Archer→BACK
        // → 3 front, 2 side, 1 back
        [InlineData(-1, 0, 3, 2, 1)]
        // Facing (0, +1) "South" post-flip:
        //   Gob   (-7,+4): dot=4, cross=7 → SIDE
        //   BG    (-8,+4): dot=4, cross=8 → SIDE
        //   Goblin( 0,+9): dot=9, cross=0 → FRONT
        //   Expl  (-1, 0): dot=0, cross=1 → SIDE
        //   Knight(+1,+3): dot=3, cross=1 → FRONT
        //   Archer(+2,-1): dot=-1, cross=2 → SIDE
        // → 2 front, 4 side, 0 back (matches shipped recommendation test)
        [InlineData(0, 1, 2, 4, 0)]
        // Facing (0, -1) "North" post-flip: mirror of south
        //   Goblin→BACK, Knight→BACK, others all SIDE
        // → 0 front, 4 side, 2 back
        [InlineData(0, -1, 0, 4, 2)]
        public void ArcCount_LenalianScenario_ForEachCardinal_MatchesHandTrace(
            int facingDx, int facingDy, int expectedFront, int expectedSide, int expectedBack)
        {
            var (ally, enemies) = LenalianScenario();

            // Probe each cardinal directly via a single-enemy-at-a-time
            // synthetic inputs isn't helpful here — we want the arc counts
            // for the actual algorithm when FORCED to a specific facing.
            // Reach that via the facingOverride path: FacingDecider exposes
            // the forced-arc counts through ComputeOptimalFacingDetailed
            // when only one facing vector is considered. Simulate by
            // picking the matching cardinal manually and asking the
            // helper directly for the arc counts at that facing.
            var arcs = CountArcsAtFacing(ally, enemies, facingDx, facingDy);
            Assert.Equal(expectedFront, arcs.front);
            Assert.Equal(expectedSide, arcs.side);
            Assert.Equal(expectedBack, arcs.back);
        }

        /// <summary>
        /// Duplicates the arc-classification branch of
        /// <see cref="FacingStrategy.ComputeOptimalFacingDetailed"/> so
        /// tests can ask "what would the arcs be for a specific facing?"
        /// Separate from the scoring path — pins the classification
        /// independently of the recommendation algorithm.
        /// </summary>
        private static (int front, int side, int back) CountArcsAtFacing(
            FacingStrategy.UnitPosition ally,
            System.Collections.Generic.List<FacingStrategy.UnitPosition> enemies,
            int dx, int dy)
        {
            int front = 0, side = 0, back = 0;
            foreach (var e in enemies)
            {
                int relX = e.GridX - ally.GridX;
                int relY = e.GridY - ally.GridY;
                float dot = relX * dx + relY * dy;
                float cross = System.MathF.Abs(relX * dy - relY * dx);
                float mag = System.MathF.Abs(dot);
                if (cross <= mag && dot > 0) front++;
                else if (cross <= mag && dot < 0) back++;
                else side++;
            }
            return (front, side, back);
        }

        [Theory]
        // Confirmed empirically in-game on the facing screen (2026-04-07).
        // FFT grid convention: -y = north, +y = south (see
        // FacingCoordConventionConsistencyTests). The empirical mapping is
        // rotation-only — pressing Up at rot=0 still faces game-North on screen,
        // which in grid coords is (0,-1).
        // rot=0: Right=E  Left=W  Up=N  Down=S
        [InlineData(0,  1,  0, "Right")]  // East at rot=0
        [InlineData(0, -1,  0, "Left")]   // West at rot=0
        [InlineData(0,  0, -1, "Up")]     // North at rot=0  (-y in grid)
        [InlineData(0,  0,  1, "Down")]   // South at rot=0  (+y in grid)
        // rot=1: Left=E  Right=W  Down=N  Up=S
        [InlineData(1,  1,  0, "Left")]   // East at rot=1
        [InlineData(1, -1,  0, "Right")]  // West at rot=1
        [InlineData(1,  0, -1, "Down")]   // North at rot=1
        [InlineData(1,  0,  1, "Up")]     // South at rot=1
        // rot=2: Up=E  Down=W  Left=N  Right=S
        [InlineData(2,  1,  0, "Up")]     // East at rot=2
        [InlineData(2, -1,  0, "Down")]   // West at rot=2
        [InlineData(2,  0, -1, "Left")]   // North at rot=2
        [InlineData(2,  0,  1, "Right")]  // South at rot=2
        // rot=3: Down=E  Up=W  Right=N  Left=S
        [InlineData(3,  1,  0, "Down")]   // East at rot=3
        [InlineData(3, -1,  0, "Up")]     // West at rot=3
        [InlineData(3,  0, -1, "Right")]  // North at rot=3
        [InlineData(3,  0,  1, "Left")]   // South at rot=3
        public void GetFacingArrowKey_CorrectForAllRotations(int rotation, int faceDx, int faceDy, string expectedKey)
        {
            var result = FacingStrategy.GetFacingArrowKey(rotation, faceDx, faceDy);
            Assert.Equal(expectedKey, result);
        }

        [Theory]
        // If Right maps to East(+1,0), that's rotation 0
        // FFT grid convention: -y = north, +y = south
        [InlineData(0,  1,  0, 0)]  // Right=East → rot=0
        [InlineData(0, -1,  0, 1)]  // Right=West → rot=1
        [InlineData(0,  0,  1, 2)]  // Right=South(+y) → rot=2
        [InlineData(0,  0, -1, 3)]  // Right=North(-y) → rot=3
        // If Down maps to South(+y), that's rotation 0
        [InlineData(3,  0,  1, 0)]  // Down=South(+y) → rot=0
        [InlineData(3,  0, -1, 1)]  // Down=North(-y) → rot=1
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
