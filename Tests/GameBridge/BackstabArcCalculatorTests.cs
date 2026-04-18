using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure function: given attacker pos, target pos, and the target's cardinal
    /// facing name ("North"/"South"/"East"/"West"), return one of "front" /
    /// "side" / "back" describing which arc the attack is coming from relative
    /// to the target.
    ///
    /// FFT canonical rule: a 4-quadrant split along the target's facing axis.
    /// Front = attacker is in the half-plane the target faces; Back = opposite;
    /// Side = attacker is perpendicular on the facing axis (tie).
    ///
    /// Concrete behavior on screen-coord grid (y grows down, i.e. South = y+1):
    ///   Target faces North: front = attacker has smaller y; back = larger y; side = same y.
    ///   Target faces South: front = attacker has larger y;  back = smaller y; side = same y.
    ///   Target faces East:  front = attacker has larger x;  back = smaller x; side = same x.
    ///   Target faces West:  front = attacker has smaller x; back = larger x;  side = same x.
    /// </summary>
    public class BackstabArcCalculatorTests
    {
        [Fact]
        public void FacingNorth_AttackerAbove_IsFront()
        {
            // attacker at (5, 2), target at (5, 5), target faces North.
            // Attacker is directly north of target → front arc.
            Assert.Equal("front", BackstabArcCalculator.ComputeArc(5, 2, 5, 5, "North"));
        }

        [Fact]
        public void FacingNorth_AttackerBelow_IsBack()
        {
            Assert.Equal("back", BackstabArcCalculator.ComputeArc(5, 8, 5, 5, "North"));
        }

        [Fact]
        public void FacingNorth_AttackerEast_IsSide()
        {
            // Same y, east of target → side.
            Assert.Equal("side", BackstabArcCalculator.ComputeArc(8, 5, 5, 5, "North"));
        }

        [Fact]
        public void FacingNorth_AttackerWest_IsSide()
        {
            Assert.Equal("side", BackstabArcCalculator.ComputeArc(2, 5, 5, 5, "North"));
        }

        [Fact]
        public void FacingSouth_AttackerBelow_IsFront()
        {
            Assert.Equal("front", BackstabArcCalculator.ComputeArc(5, 8, 5, 5, "South"));
        }

        [Fact]
        public void FacingSouth_AttackerAbove_IsBack()
        {
            Assert.Equal("back", BackstabArcCalculator.ComputeArc(5, 2, 5, 5, "South"));
        }

        [Fact]
        public void FacingSouth_SameRow_IsSide()
        {
            Assert.Equal("side", BackstabArcCalculator.ComputeArc(8, 5, 5, 5, "South"));
        }

        [Fact]
        public void FacingEast_AttackerEast_IsFront()
        {
            Assert.Equal("front", BackstabArcCalculator.ComputeArc(8, 5, 5, 5, "East"));
        }

        [Fact]
        public void FacingEast_AttackerWest_IsBack()
        {
            Assert.Equal("back", BackstabArcCalculator.ComputeArc(2, 5, 5, 5, "East"));
        }

        [Fact]
        public void FacingEast_AttackerNorth_IsSide()
        {
            Assert.Equal("side", BackstabArcCalculator.ComputeArc(5, 2, 5, 5, "East"));
        }

        [Fact]
        public void FacingWest_AttackerWest_IsFront()
        {
            Assert.Equal("front", BackstabArcCalculator.ComputeArc(2, 5, 5, 5, "West"));
        }

        [Fact]
        public void FacingWest_AttackerEast_IsBack()
        {
            Assert.Equal("back", BackstabArcCalculator.ComputeArc(8, 5, 5, 5, "West"));
        }

        // ---- Diagonal cases: dot-product on the facing axis decides ----

        [Fact]
        public void FacingNorth_DiagonalAheadLeft_IsFront()
        {
            // attacker at (3, 2), target at (5, 5), facing North.
            // Attacker is north AND west; north dominates → front.
            Assert.Equal("front", BackstabArcCalculator.ComputeArc(3, 2, 5, 5, "North"));
        }

        [Fact]
        public void FacingNorth_DiagonalBehindRight_IsBack()
        {
            // attacker south-east of target facing North → back (south dominates).
            Assert.Equal("back", BackstabArcCalculator.ComputeArc(8, 8, 5, 5, "North"));
        }

        [Fact]
        public void FacingEast_DiagonalAheadDown_IsFront()
        {
            // attacker east AND south of target facing East → front.
            Assert.Equal("front", BackstabArcCalculator.ComputeArc(8, 8, 5, 5, "East"));
        }

        // ---- Degenerate cases ----

        [Fact]
        public void SameTile_ReturnsNull()
        {
            // Can't compute an arc from zero distance.
            Assert.Null(BackstabArcCalculator.ComputeArc(5, 5, 5, 5, "North"));
        }

        [Fact]
        public void UnknownFacing_ReturnsNull()
        {
            // Defensive: unit's facing byte may be null/unresolvable.
            Assert.Null(BackstabArcCalculator.ComputeArc(5, 2, 5, 5, null));
            Assert.Null(BackstabArcCalculator.ComputeArc(5, 2, 5, 5, ""));
            Assert.Null(BackstabArcCalculator.ComputeArc(5, 2, 5, 5, "Diagonal"));
        }

        [Fact]
        public void CaseInsensitive_Facing()
        {
            Assert.Equal("front", BackstabArcCalculator.ComputeArc(5, 2, 5, 5, "north"));
            Assert.Equal("back", BackstabArcCalculator.ComputeArc(5, 2, 5, 5, "SOUTH"));
        }

        // ---- Hit/damage bonus lookup (canonical FFT values) ----
        // Back: +50% hit, crit damage bonus (per FFT canon; damage multiplier not
        // used here — scoring weight captures the desirability).
        // Side: modest bonus.
        // Front: no bonus.

        [Fact]
        public void ArcBonusScore_OrdersBackOverSideOverFront()
        {
            int front = BackstabArcCalculator.ArcBonusScore("front");
            int side  = BackstabArcCalculator.ArcBonusScore("side");
            int back  = BackstabArcCalculator.ArcBonusScore("back");
            Assert.True(back > side, $"back={back}, side={side}");
            Assert.True(side > front, $"side={side}, front={front}");
        }

        [Fact]
        public void ArcBonusScore_FrontIsZero()
        {
            Assert.Equal(0, BackstabArcCalculator.ArcBonusScore("front"));
        }

        [Fact]
        public void ArcBonusScore_NullReturnsZero()
        {
            Assert.Equal(0, BackstabArcCalculator.ArcBonusScore(null));
        }
    }
}
