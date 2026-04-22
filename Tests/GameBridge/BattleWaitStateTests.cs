using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleWaitStateTests
    {
        /// <summary>
        /// After Move+Act, the game skips the action menu and goes directly to the facing screen.
        /// BattleWait needs to detect this and skip menu navigation.
        /// </summary>
        [Theory]
        [InlineData("BattleMyTurn", false)]     // Normal: at action menu, needs menu nav
        [InlineData("BattleActing", false)]      // Acted but not moved: still has menu
        [InlineData("BattleAttacking", true)]    // After Move+Act: already on facing screen
        [InlineData("BattleMoving", true)]       // Could also be facing screen
        public void ShouldSkipMenuNavigation_DetectsCorrectly(string screenName, bool expectedSkip)
        {
            bool skip = BattleWaitLogic.ShouldSkipMenuNavigation(screenName);
            Assert.Equal(expectedSkip, skip);
        }

        /// <summary>
        /// BattleWait should accept these screen states as valid starting points.
        /// </summary>
        [Theory]
        [InlineData("BattleMyTurn", true)]
        [InlineData("BattleActing", true)]
        [InlineData("BattleAttacking", true)]   // After Move+Act facing screen
        [InlineData("BattleMoving", true)]      // Could be facing screen variant
        [InlineData("WorldMap", false)]
        [InlineData("TitleScreen", false)]
        [InlineData(null, false)]
        public void CanStartBattleWait_ValidatesScreen(string? screenName, bool expected)
        {
            bool valid = BattleWaitLogic.CanStartBattleWait(screenName);
            Assert.Equal(expected, valid);
        }

        [Fact]
        public void NeedsConfirmation_NoMoveNoAct_ReturnsTrue()
        {
            Assert.True(BattleWaitLogic.NeedsConfirmation(acted: false, moved: false, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_Moved_ReturnsFalse()
        {
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: false, moved: true, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_Acted_ReturnsFalse()
        {
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: true, moved: false, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_BothMovedAndActed_ReturnsFalse()
        {
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: true, moved: true, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_NoMoveNoAct_ButConfirmed_ReturnsFalse()
        {
            // Second battle_wait call should go through
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: false, moved: false, confirmed: true));
        }

        // --- ShouldRetryVerifyAfterNav ---
        // After NavigateMenuCursor(initialCursor, target) presses the expected number of Downs/Ups,
        // we re-read the cursor memory byte to verify arrival. The memory byte at 0x1407FC620 is
        // KNOWN to be stale in the "moved && !acted" state (see EffectiveMenuCursor). If the
        // verified byte reads back the same suspicious value as the initial raw read, the nav
        // probably succeeded but the memory hasn't updated. Retrying fires extra Downs that
        // overshoot (observed S56: 1-Down expected nav → byte still reads 0 → retry fires 2 more
        // Downs → cursor lands on Auto-battle(4) instead of Wait(2)).
        //
        // Rule: retry ONLY if the verified byte has MOVED from the initial raw (proving the byte
        // is tracking reality), AND is still not at the target. If it's stuck at the same stale
        // value, trust the initial nav and stop.

        [Fact]
        public void ShouldRetryVerifyAfterNav_ByteStaleAtSameValue_DoesNotRetry()
        {
            // Post-move case: initial read=0, corrected to 1 (Abilities), navigated 1 Down.
            // Verify re-reads 0 again — same stale value, memory didn't update. Don't retry.
            Assert.False(BattleWaitLogic.ShouldRetryVerifyAfterNav(
                initialRaw: 0, correctedCursor: 1, verifiedRaw: 0, target: 2));
        }

        [Fact]
        public void ShouldRetryVerifyAfterNav_ByteArrivedAtTarget_DoesNotRetry()
        {
            // Happy path: nav moved cursor to target. No retry needed.
            Assert.False(BattleWaitLogic.ShouldRetryVerifyAfterNav(
                initialRaw: 0, correctedCursor: 0, verifiedRaw: 2, target: 2));
        }

        [Fact]
        public void ShouldRetryVerifyAfterNav_ByteMovedButNotAtTarget_Retries()
        {
            // Byte moved (0 → 1), proving memory IS tracking reality, but we're not at
            // target yet. Retry to close the gap.
            Assert.True(BattleWaitLogic.ShouldRetryVerifyAfterNav(
                initialRaw: 0, correctedCursor: 0, verifiedRaw: 1, target: 2));
        }

        [Fact]
        public void ShouldRetryVerifyAfterNav_NoCorrectionApplied_AndByteStale_Retries()
        {
            // No correction applied (raw == corrected), byte reports same value as before —
            // could be legit "nav failed outright" (e.g. dropped key). Retry is safe.
            Assert.True(BattleWaitLogic.ShouldRetryVerifyAfterNav(
                initialRaw: 0, correctedCursor: 0, verifiedRaw: 0, target: 2));
        }

        [Fact]
        public void ShouldRetryVerifyAfterNav_CorrectionAppliedAndByteMoved_Retries()
        {
            // Correction applied AND byte moved — memory is now tracking reality but
            // the first nav undershot. Retry from the verified position.
            Assert.True(BattleWaitLogic.ShouldRetryVerifyAfterNav(
                initialRaw: 0, correctedCursor: 1, verifiedRaw: 3, target: 2));
        }

        [Fact]
        public void ShouldRetryVerifyAfterNav_VerifyReadFailed_DoesNotRetry()
        {
            // Memory read failed (verifiedRaw = -1). Don't retry — we'd amplify on noise.
            // Trust the initial nav; the Enter press that follows will commit it.
            Assert.False(BattleWaitLogic.ShouldRetryVerifyAfterNav(
                initialRaw: 0, correctedCursor: 1, verifiedRaw: -1, target: 2));
        }
    }
}
