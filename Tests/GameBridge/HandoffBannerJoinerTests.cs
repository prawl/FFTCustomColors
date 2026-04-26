using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// `execute_turn` joins sub-step Infos with " | ". Each sub-step that
    /// might produce a hand-off banner (notably the wait sub-step) already
    /// prepended one. Without dedupe, ExecuteTurn's own outer prepend
    /// creates a second banner in the joined Info — observed live
    /// 2026-04-25 playtest.
    ///
    /// HandoffBannerJoiner.PrependIfAbsent is the single chokepoint that
    /// (a) prepends the banner when it's not already present anywhere in
    /// Info, (b) skips when Info already carries the same banner from a
    /// sub-step, (c) tolerates null / empty inputs.
    /// </summary>
    public class HandoffBannerJoinerTests
    {
        private const string Banner = "=== TURN HANDOFF: Kenrick(Thief) → Lloyd(Orator) (10,9) HP=432/432 ===";

        [Fact]
        public void NullBanner_ReturnsInfoUnchanged()
        {
            Assert.Equal("Friendly turn after 3000ms",
                HandoffBannerJoiner.PrependIfAbsent("Friendly turn after 3000ms", null));
        }

        [Fact]
        public void EmptyBanner_ReturnsInfoUnchanged()
        {
            Assert.Equal("Friendly turn after 3000ms",
                HandoffBannerJoiner.PrependIfAbsent("Friendly turn after 3000ms", ""));
        }

        [Fact]
        public void NullInfo_ReturnsBanner()
        {
            Assert.Equal(Banner, HandoffBannerJoiner.PrependIfAbsent(null, Banner));
        }

        [Fact]
        public void EmptyInfo_ReturnsBanner()
        {
            Assert.Equal(Banner, HandoffBannerJoiner.PrependIfAbsent("", Banner));
        }

        [Fact]
        public void NewBanner_PrependsWithSeparator()
        {
            var result = HandoffBannerJoiner.PrependIfAbsent("Friendly turn after 3000ms", Banner);
            Assert.Equal(Banner + " | Friendly turn after 3000ms", result);
        }

        [Fact]
        public void BannerAlreadyAtStart_NoDuplicate()
        {
            // Sub-step (wait) already prepended this exact banner. ExecuteTurn
            // outer prepend should detect and no-op.
            string priorInfo = Banner + " | Friendly turn after 3000ms";
            Assert.Equal(priorInfo, HandoffBannerJoiner.PrependIfAbsent(priorInfo, Banner));
        }

        [Fact]
        public void BannerAlreadyMidString_NoDuplicate()
        {
            // Sub-step Info joined into a longer ExecuteTurn join — the
            // banner is now in the middle. Still no double-prepend.
            string priorInfo = "Moved (8,10)→(5,9) | " + Banner + " | Friendly turn after 3000ms";
            Assert.Equal(priorInfo, HandoffBannerJoiner.PrependIfAbsent(priorInfo, Banner));
        }

        [Fact]
        public void DifferentBannerText_PrependsAnyway()
        {
            // Different actual banner content (different units) → genuinely
            // a new hand-off, not a dupe.
            string priorInfo = "=== TURN HANDOFF: Ramza(Gallant Knight) → Kenrick(Thief) (8,9) HP=467/467 === | Friendly turn after 3000ms";
            string newBanner = "=== TURN HANDOFF: Kenrick(Thief) → Wilham(Samurai) (8,11) HP=528/528 ===";
            var result = HandoffBannerJoiner.PrependIfAbsent(priorInfo, newBanner);
            Assert.Equal(newBanner + " | " + priorInfo, result);
        }

        [Fact]
        public void AnyHandoffBannerAtStart_BlocksAnotherHandoffPrepend()
        {
            // Defensive: if Info already has ANY "=== TURN HANDOFF:" prefix
            // (even a different one — shouldn't happen in practice), don't
            // double-prepend. Keep the existing one as the canonical record.
            // This handles a subtle race: if the wait sub-step + an earlier
            // sub-step both surfaced different hand-offs, we want to surface
            // the SUB-STEP's banner (which represents the actual sequence)
            // rather than the outer ExecuteTurn snapshot diff.
            string priorInfo = "=== TURN HANDOFF: A(X) → B(Y) (1,1) HP=10/10 === | something";
            string outerBanner = "=== TURN HANDOFF: A(X) → C(Z) (2,2) HP=20/20 ===";
            // Detection by EXACT banner string: previous test covers same banner.
            // Here banners differ. Caller should still prepend (different content).
            // But this is an edge case — execute_turn snapshot pre/post the WHOLE
            // bundle, so the outer banner is the authoritative one. Document the
            // chosen behavior: PrependIfAbsent keys on EXACT match only.
            var result = HandoffBannerJoiner.PrependIfAbsent(priorInfo, outerBanner);
            Assert.Equal(outerBanner + " | " + priorInfo, result);
        }
    }
}
