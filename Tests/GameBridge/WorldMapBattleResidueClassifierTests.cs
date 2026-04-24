using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the suppression rule that rejects a WorldMap detection when the
    /// caller was observably in a battle moments ago. battleMode flickers to
    /// 0 during some enemy-turn animations, tripping the post-battle
    /// stale-rule that converts rawLocation=255 + submenuFlag=1 into
    /// WorldMap. Real post-battle transitions are not time-bounded — they
    /// just require the last battle state to have been "long enough" ago.
    /// </summary>
    public class WorldMapBattleResidueClassifierTests
    {
        [Fact]
        public void WorldMap_RecentBattle_Suppresses()
        {
            Assert.True(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: "WorldMap",
                msSinceLastBattleState: 100));
        }

        [Fact]
        public void WorldMap_OldBattle_DoesNotSuppress()
        {
            Assert.False(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: "WorldMap",
                msSinceLastBattleState: 5000));
        }

        [Fact]
        public void WorldMap_NoBattleEver_DoesNotSuppress()
        {
            Assert.False(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: "WorldMap",
                msSinceLastBattleState: -1));
        }

        [Fact]
        public void NonWorldMapDetection_Untouched()
        {
            Assert.False(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: "BattleMyTurn",
                msSinceLastBattleState: 100));
            Assert.False(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: "TravelList",
                msSinceLastBattleState: 100));
        }

        [Fact]
        public void NullDetection_DoesNotSuppress()
        {
            Assert.False(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: null,
                msSinceLastBattleState: 100));
        }

        [Fact]
        public void CustomWindow_Respected()
        {
            // 500ms since battle, 400ms window → outside, no suppress
            Assert.False(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: "WorldMap",
                msSinceLastBattleState: 500,
                suppressWindowMs: 400));
            // 500ms since battle, 2000ms window → inside, suppress
            Assert.True(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: "WorldMap",
                msSinceLastBattleState: 500,
                suppressWindowMs: 2000));
        }

        [Fact]
        public void DefaultWindow_IsThreeSeconds()
        {
            // 3000ms is the observed live-transient; real post-battle
            // transitions settle within <1s. Default needs headroom for
            // animation stretches (long-spell casts can stall battleMode
            // readings for 2s+).
            Assert.Equal(3000, WorldMapBattleResidueClassifier.DefaultSuppressWindowMs);
        }

        [Fact]
        public void ZeroMsSinceBattle_IsWithinWindow()
        {
            Assert.True(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: "WorldMap",
                msSinceLastBattleState: 0));
        }

        [Fact]
        public void BoundaryWindowEdge_IsOutside()
        {
            Assert.False(WorldMapBattleResidueClassifier.ShouldSuppress(
                detectedName: "WorldMap",
                msSinceLastBattleState: 3000,
                suppressWindowMs: 3000));
        }
    }
}
