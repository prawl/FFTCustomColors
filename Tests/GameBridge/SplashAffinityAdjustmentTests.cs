using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure function: given per-hit affinity markers (positionally aligned with
    /// enemy/ally name lists), return a weighted score adjustment for ranking
    /// splash centers. Adds to the base ComputeSplashScore.
    ///
    /// Weights (enemy-target ability):
    ///   weak       +2  (target takes 2x damage — prefer this splash)
    ///   half       -1  (target takes 0.5x — less effective placement)
    ///   null       -3  (zero damage — wasted)
    ///   absorb     -5  (HEALS the enemy — actively bad)
    ///   strengthen  0  (strengthens target's OUTGOING element, not incoming)
    ///
    /// For ally-target abilities, the same weights apply but negated — we WANT
    /// allies to absorb the element if possible (free heal from a Cure aimed at
    /// a Holy-absorbing ally is still positive). But in practice most ally
    /// abilities (Cure) hit Holy-resistant / Holy-neutral allies, so the
    /// adjustment is usually zero. Keep symmetric for simplicity.
    /// </summary>
    public class SplashAffinityAdjustmentTests
    {
        [Fact]
        public void NoAffinities_ReturnsZero()
        {
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                enemyAffinities: null, allyAffinities: null, wantsAlly: false);
            Assert.Equal(0, result);
        }

        [Fact]
        public void EmptyLists_ReturnZero()
        {
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                new List<string?>(), new List<string?>(), wantsAlly: false);
            Assert.Equal(0, result);
        }

        [Fact]
        public void AllNullMarkers_ReturnZero()
        {
            // Hit units with no affinity contribute 0.
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                new List<string?> { null, null, null }, null, wantsAlly: false);
            Assert.Equal(0, result);
        }

        [Fact]
        public void WeakEnemy_AddsBonus()
        {
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                new List<string?> { "weak" }, null, wantsAlly: false);
            Assert.Equal(2, result);
        }

        [Fact]
        public void AbsorbEnemy_SubtractsHeavyPenalty()
        {
            // Hitting an absorbing enemy HEALS them — strongly negative.
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                new List<string?> { "absorb" }, null, wantsAlly: false);
            Assert.Equal(-5, result);
        }

        [Fact]
        public void NullEnemy_SubtractsModeratePenalty()
        {
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                new List<string?> { "null" }, null, wantsAlly: false);
            Assert.Equal(-3, result);
        }

        [Fact]
        public void HalfEnemy_SubtractsMildPenalty()
        {
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                new List<string?> { "half" }, null, wantsAlly: false);
            Assert.Equal(-1, result);
        }

        [Fact]
        public void StrengthenEnemy_Zero()
        {
            // Strengthen is outgoing boost, doesn't affect our hit.
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                new List<string?> { "strengthen" }, null, wantsAlly: false);
            Assert.Equal(0, result);
        }

        [Fact]
        public void MixedEnemies_Sum()
        {
            // 2 weak + 1 absorb + 1 null + 1 half
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                new List<string?> { "weak", "weak", "absorb", "null", "half" },
                null, wantsAlly: false);
            Assert.Equal(2 + 2 + (-5) + (-3) + (-1), result);
        }

        [Fact]
        public void AllyTarget_AbsorbingAllies_Bonus()
        {
            // When firing an ally-buffing ability at allies who absorb that
            // element (rare but possible), it's a WIN (free heal bonus).
            // Symmetric weighting — use enemy weights on allies.
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                null, new List<string?> { "absorb" }, wantsAlly: true);
            // For ally-target we flip sign on the weights: absorbing allies = good.
            Assert.Equal(5, result);
        }

        [Fact]
        public void AllyTarget_WeakAllies_Penalty()
        {
            // Healing an ally weak to Holy... hurts them (Cure deals damage
            // to undead). Avoid.
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                null, new List<string?> { "weak" }, wantsAlly: true);
            Assert.Equal(-2, result);
        }

        [Fact]
        public void UnknownMarker_Ignored()
        {
            // Defensive: unknown marker string contributes 0.
            var result = AbilityTargetCalculator.SplashAffinityAdjustment(
                new List<string?> { "weak", "unknownMarker", "absorb" },
                null, wantsAlly: false);
            Assert.Equal(2 + 0 + (-5), result);
        }
    }
}
