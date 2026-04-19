using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Audit: cost==0 in CostByName is a sentinel meaning "this ability
    /// exists but cannot be learned via JP" (obtained by other means —
    /// enemy crystal drop, story, etc.). Only one ability intentionally
    /// carries this sentinel: Zodiark (Summoner; drops from certain
    /// enemies). Any other cost==0 entry would silently suppress the
    /// "Next: N" header for the affected skillset.
    ///
    /// This test pins the sentinel count to catch accidental zeroes
    /// added during bulk edits (e.g. forgetting to fill in a new ability
    /// before committing). If a legitimate unlearnable-via-JP ability
    /// is discovered later, add it to ExpectedUnlearnableViaJp AND
    /// document the source.
    /// </summary>
    public class UnlearnableAbilitySentinelTests
    {
        private static readonly HashSet<string> ExpectedUnlearnableViaJp = new()
        {
            // Zodiark — Summoner skillset capstone. Canonically NOT purchasable
            // via JP; obtained by a Chemist landing a Phoenix Down / Elixir
            // throw on a certain enemy (or equivalent drop mechanism). The
            // ComputeNextJpForSkillset path explicitly filters cost <= 0, so
            // Summoner's "Next: N" reports the next non-Zodiark ability.
            "Zodiark",
        };

        [Fact]
        public void OnlyExpectedAbilities_HaveCostZero()
        {
            var zeros = AbilityJpCosts.CostByName
                .Where(kv => kv.Value == 0)
                .Select(kv => kv.Key)
                .ToHashSet();

            // Check every zero is expected.
            foreach (var name in zeros)
            {
                Assert.True(
                    ExpectedUnlearnableViaJp.Contains(name),
                    $"Ability '{name}' has cost=0 but isn't in the expected " +
                    "unlearnable-via-JP set. If legitimate, add to " +
                    "ExpectedUnlearnableViaJp with a source comment. " +
                    "Otherwise fill in the real JP cost.");
            }

            // Check every expected sentinel is actually present with cost=0
            // (catches rename/deletion that would silently flip the semantic).
            foreach (var name in ExpectedUnlearnableViaJp)
            {
                Assert.True(
                    zeros.Contains(name),
                    $"Expected '{name}' to be a cost=0 sentinel but it's " +
                    "either missing from CostByName or has a non-zero cost.");
            }
        }

        [Fact]
        public void NegativeCosts_DoNotExist()
        {
            // ComputeNextJp treats cost <= 0 as unlearnable. A negative cost
            // would also get filtered, which is probably NOT the author's
            // intent — more likely a typo. Catch them separately.
            var negatives = AbilityJpCosts.CostByName
                .Where(kv => kv.Value < 0)
                .ToList();
            Assert.Empty(negatives);
        }

        [Fact]
        public void ComputeNextJp_Summon_SkipsZodiark_ReturnsCheapestAlternative()
        {
            // With nothing learned, Summon's cheapest purchasable is Moogle
            // (110 JP). Zodiark (cost 0) must be skipped — if included, it
            // would return 0 which is nonsensical as "Next: N".
            int? next = AbilityJpCosts.ComputeNextJpForSkillset(
                "Summon", new HashSet<int>());
            Assert.Equal(110, next);
        }

        [Fact]
        public void GetCost_Zodiark_ReturnsZero_NotNull()
        {
            // The sentinel is stored as 0 (not null / not missing). Null
            // would mean "unknown cost" which has a different semantic.
            Assert.Equal(0, AbilityJpCosts.GetCost("Zodiark"));
        }
    }
}
