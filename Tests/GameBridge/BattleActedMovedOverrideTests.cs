using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// The raw battleActed (0x14077CA8C) and battleMoved (0x14077CA9C)
    /// memory bytes read 0 transiently right after a confirmed player
    /// action — Phoenix Down and Throw Stone both live-observed with
    /// battleActed: 0 in the response despite the action clearly
    /// resolving. The bridge tracks the action commit via private
    /// `_actedThisTurn` / `_movedThisTurn` flags, but those only fed
    /// the UI cursor disambiguation; the response.screen.battleActed
    /// and battleMoved fields still reflected raw memory.
    ///
    /// This pure helper applies the flags as an override so callers
    /// see a consistent acted/moved signal across both the UI tag and
    /// the raw byte fields. The flags reset on turn boundaries
    /// (BattleLifecycleEvent.StartBattle and other sites), so the
    /// override is naturally bounded to the current turn.
    /// </summary>
    public class BattleActedMovedOverrideTests
    {
        [Fact]
        public void Apply_FlagsFalse_ReturnsRawBytesUnchanged()
        {
            var (a, m) = BattleActedMovedOverride.Apply(0, 0, false, false);
            Assert.Equal(0, a);
            Assert.Equal(0, m);
        }

        [Fact]
        public void Apply_BothFlagsTrue_OverridesZeroBytesToOne()
        {
            // The whole point of the helper: raw byte stale-reads 0
            // right after the action confirmed → flag forces it to 1.
            var (a, m) = BattleActedMovedOverride.Apply(0, 0, true, true);
            Assert.Equal(1, a);
            Assert.Equal(1, m);
        }

        [Fact]
        public void Apply_RawByteAlreadyOne_FlagDoesNotChangeIt()
        {
            // When the byte is correct (1), the flag is redundant — we
            // still return 1, no change.
            var (a, m) = BattleActedMovedOverride.Apply(1, 1, true, true);
            Assert.Equal(1, a);
            Assert.Equal(1, m);
        }

        [Fact]
        public void Apply_OnlyActedFlag_OverridesActedNotMoved()
        {
            // Phoenix Down case: acted but didn't move. movedThisTurn
            // stays false, so battleMoved byte passes through.
            var (a, m) = BattleActedMovedOverride.Apply(0, 0, true, false);
            Assert.Equal(1, a);
            Assert.Equal(0, m);
        }

        [Fact]
        public void Apply_OnlyMovedFlag_OverridesMovedNotActed()
        {
            // Move-then-no-act case: movedThisTurn true, actedThisTurn
            // false. battleActed byte passes through.
            var (a, m) = BattleActedMovedOverride.Apply(0, 0, false, true);
            Assert.Equal(0, a);
            Assert.Equal(1, m);
        }

        [Fact]
        public void Apply_RawByteIsTwo_PreservedUnlessFlagged()
        {
            // FFT only writes 0/1 to these bytes in practice but the
            // helper should not corrupt unexpected values when no
            // flag is set — return as-is. When flagged, force to 1
            // (the canonical "yes, this happened" value).
            var (a1, m1) = BattleActedMovedOverride.Apply(2, 2, false, false);
            Assert.Equal(2, a1);
            Assert.Equal(2, m1);
            var (a2, m2) = BattleActedMovedOverride.Apply(2, 2, true, true);
            Assert.Equal(1, a2);
            Assert.Equal(1, m2);
        }
    }
}
