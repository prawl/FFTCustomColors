using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the fresh-entry-into-BattleMyTurn classification. When this
    /// returns true, the caller should reset the action-menu cursor byte
    /// at 0x1407FC620 to 0 — the game itself does this on turn-start, so
    /// writing 0 just makes the byte reflect the state unambiguously.
    ///
    /// "Fresh entry" = transition into BattleMyTurn from a state that
    /// implies a turn-boundary (enemy turn ended, pause escape, battle
    /// start, etc.). Submenu escapes (BattleMoving, BattleAbilities)
    /// are NOT fresh — the game preserves the cursor when returning from
    /// a submenu.
    /// </summary>
    public class FreshBattleMyTurnEntryClassifierTests
    {
        // Fresh-entry transitions: cursor should reset to 0.

        [Theory]
        [InlineData("BattleEnemiesTurn")]
        [InlineData("BattleAlliesTurn")]
        [InlineData("BattlePaused")]
        [InlineData("BattleFormation")]
        [InlineData("BattleSequence")]
        [InlineData("BattleDialogue")]
        [InlineData("BattleChoice")]
        public void FreshEntry_FromTurnBoundaryState_ReturnsTrue(string prev)
        {
            Assert.True(FreshBattleMyTurnEntryClassifier.IsFresh(prev, "BattleMyTurn"));
        }

        // Submenu-escape transitions: preserve cursor (NOT fresh).

        [Theory]
        [InlineData("BattleMoving")]
        [InlineData("BattleWaiting")]
        [InlineData("BattleAttacking")]
        [InlineData("BattleCasting")]
        [InlineData("BattleAbilities")]
        [InlineData("BattleActing")]
        public void FreshEntry_FromSubmenuState_ReturnsFalse(string prev)
        {
            Assert.False(FreshBattleMyTurnEntryClassifier.IsFresh(prev, "BattleMyTurn"));
        }

        [Fact]
        public void FreshEntry_SameScreen_ReturnsFalse()
        {
            // BattleMyTurn → BattleMyTurn is a no-op, not a fresh entry.
            Assert.False(FreshBattleMyTurnEntryClassifier.IsFresh("BattleMyTurn", "BattleMyTurn"));
        }

        [Fact]
        public void FreshEntry_ToNonBattleMyTurn_ReturnsFalse()
        {
            // Rule only applies when the destination is BattleMyTurn.
            Assert.False(FreshBattleMyTurnEntryClassifier.IsFresh("BattleEnemiesTurn", "BattleAbilities"));
            Assert.False(FreshBattleMyTurnEntryClassifier.IsFresh("BattlePaused", "BattleWaiting"));
        }

        [Fact]
        public void FreshEntry_NullPrev_TreatedAsFresh()
        {
            // First-frame-of-battle or uninitialized tracker: treat as
            // fresh and reset. If the game's cursor happens to be non-
            // zero at that moment (impossible per FFT turn-start rules),
            // the reset is still correct.
            Assert.True(FreshBattleMyTurnEntryClassifier.IsFresh(null, "BattleMyTurn"));
        }

        [Fact]
        public void FreshEntry_EmptyPrev_TreatedAsFresh()
        {
            Assert.True(FreshBattleMyTurnEntryClassifier.IsFresh("", "BattleMyTurn"));
        }

        [Fact]
        public void FreshEntry_UnknownPrev_TreatedAsFresh()
        {
            // An unknown prior state name is a safe reset — the cursor
            // byte might be anything. Writing 0 doesn't hurt because
            // the game re-reads on user input.
            Assert.True(FreshBattleMyTurnEntryClassifier.IsFresh("SomeUnknownScreen", "BattleMyTurn"));
        }

        [Fact]
        public void FreshEntry_WorldMap_TreatedAsFresh()
        {
            // WorldMap → BattleMyTurn is a post-load/post-formation
            // edge case. Treat as fresh.
            Assert.True(FreshBattleMyTurnEntryClassifier.IsFresh("WorldMap", "BattleMyTurn"));
        }
    }
}
