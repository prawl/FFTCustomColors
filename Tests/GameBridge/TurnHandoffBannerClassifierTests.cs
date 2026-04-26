using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Multi-unit party play: after `execute_turn` / `battle_wait` returns
    /// control to a DIFFERENT player unit, the response must announce the
    /// hand-off loudly enough that an LLM agent doesn't keep issuing
    /// commands meant for the prior unit. The banner is prepended to
    /// `response.Info` so it shows up in the compact one-line trailer.
    ///
    /// Pre/post UnitIdentity is sourced from the active-unit identity
    /// cache (snapshotted before the turn-boundary clear, then re-read
    /// after the auto-scan repopulates it).
    /// </summary>
    public class TurnHandoffBannerClassifierTests
    {
        private static TurnHandoffBannerClassifier.UnitIdentity Id(
            string? name, string? job = "Thief", int x = 9, int y = 9, int hp = 467, int maxHp = 467)
            => new(name, job, x, y, hp, maxHp);

        [Fact]
        public void SameUnit_ReturnsNull()
        {
            var before = Id("Kenrick", "Thief", 9, 9, 467, 467);
            var after  = Id("Kenrick", "Thief", 9, 9, 467, 467);
            Assert.Null(TurnHandoffBannerClassifier.BuildBanner(before, after));
        }

        [Fact]
        public void SameUnitDifferentPosition_ReturnsNull()
        {
            // Same Kenrick, just moved or HP ticked — not a hand-off.
            var before = Id("Kenrick", "Thief", 9, 9, 467, 467);
            var after  = Id("Kenrick", "Thief", 6, 5, 437, 467);
            Assert.Null(TurnHandoffBannerClassifier.BuildBanner(before, after));
        }

        [Fact]
        public void DifferentUnit_EmitsBannerWithJobAndPosition()
        {
            var before = Id("Kenrick", "Thief", 9, 9, 467, 467);
            var after  = Id("Lloyd", "Orator", 10, 9, 432, 432);
            var banner = TurnHandoffBannerClassifier.BuildBanner(before, after);
            Assert.Equal(
                "=== TURN HANDOFF: Kenrick(Thief) → Lloyd(Orator) (10,9) HP=432/432 ===",
                banner);
        }

        [Fact]
        public void BeforeNull_ReturnsNull()
        {
            // No prior identity to compare — happens on first turn after
            // battle start when the cache hasn't been seeded yet.
            var after = Id("Lloyd", "Orator", 10, 9, 432, 432);
            Assert.Null(TurnHandoffBannerClassifier.BuildBanner(null, after));
        }

        [Fact]
        public void AfterNull_ReturnsNull()
        {
            // Auto-scan failed / transient empty active-unit read. Don't
            // emit a misleading "→ null" banner — wait for the next read.
            var before = Id("Kenrick", "Thief", 9, 9, 467, 467);
            Assert.Null(TurnHandoffBannerClassifier.BuildBanner(before, null));
        }

        [Fact]
        public void BeforeNameNull_ReturnsNull()
        {
            // Cache was cleared / never populated for the prior turn — no
            // hand-off comparison possible.
            var before = Id(null, "Thief", 9, 9, 467, 467);
            var after  = Id("Lloyd", "Orator", 10, 9, 432, 432);
            Assert.Null(TurnHandoffBannerClassifier.BuildBanner(before, after));
        }

        [Fact]
        public void AfterNameNull_ReturnsNull()
        {
            var before = Id("Kenrick", "Thief", 9, 9, 467, 467);
            var after  = Id(null, "Orator", 10, 9, 432, 432);
            Assert.Null(TurnHandoffBannerClassifier.BuildBanner(before, after));
        }

        [Fact]
        public void DifferentUnit_MissingAfterJob_StillEmitsWithUnknownJob()
        {
            // Job lookup occasionally lags one frame behind name. Banner
            // still fires — name swap is the load-bearing signal.
            var before = Id("Kenrick", "Thief", 9, 9, 467, 467);
            var after  = Id("Lloyd", null, 10, 9, 432, 432);
            var banner = TurnHandoffBannerClassifier.BuildBanner(before, after);
            Assert.Equal(
                "=== TURN HANDOFF: Kenrick(Thief) → Lloyd(?) (10,9) HP=432/432 ===",
                banner);
        }

        [Fact]
        public void DifferentUnit_MissingBeforeJob_EmitsWithUnknownBeforeJob()
        {
            var before = Id("Kenrick", null, 9, 9, 467, 467);
            var after  = Id("Lloyd", "Orator", 10, 9, 432, 432);
            var banner = TurnHandoffBannerClassifier.BuildBanner(before, after);
            Assert.Equal(
                "=== TURN HANDOFF: Kenrick(?) → Lloyd(Orator) (10,9) HP=432/432 ===",
                banner);
        }
    }
}
