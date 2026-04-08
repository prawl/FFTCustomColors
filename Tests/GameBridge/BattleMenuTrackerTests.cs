using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleMenuTrackerTests
    {
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_RETURN = 0x0D;
        private const int VK_ESCAPE = 0x1B;

        [Fact]
        public void EnterAbilities_SetsSubmenuWithAttackAndMettle()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });

            Assert.True(tracker.InSubmenu);
            Assert.Equal("Attack", tracker.CurrentItem);
            Assert.Equal(0, tracker.CursorIndex);
        }

        [Fact]
        public void DownKey_MovesToNextItem()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN);

            Assert.Equal("Mettle", tracker.CurrentItem);
            Assert.Equal(1, tracker.CursorIndex);
        }

        [Fact]
        public void UpKey_MovesToPreviousItem()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN);
            tracker.OnKeyPressed(VK_UP);

            Assert.Equal("Attack", tracker.CurrentItem);
            Assert.Equal(0, tracker.CursorIndex);
        }

        [Fact]
        public void DownKey_AtBottom_Wraps()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN);
            tracker.OnKeyPressed(VK_DOWN);

            Assert.Equal("Attack", tracker.CurrentItem);
            Assert.Equal(0, tracker.CursorIndex);
        }

        [Fact]
        public void UpKey_AtTop_Wraps()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_UP);

            Assert.Equal("Mettle", tracker.CurrentItem);
            Assert.Equal(1, tracker.CursorIndex);
        }

        [Fact]
        public void Escape_ExitsSubmenu_ButRemembersCursor()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // now on Mettle
            tracker.OnKeyPressed(VK_ESCAPE);

            Assert.False(tracker.InSubmenu);
            // Cursor position is remembered for re-entry
            Assert.Equal(1, tracker.CursorIndex);
        }

        [Fact]
        public void ReenterSubmenu_CursorStaysWhereItWas()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // now on Mettle
            tracker.OnKeyPressed(VK_ESCAPE); // exit
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" }); // re-enter

            Assert.True(tracker.InSubmenu);
            Assert.Equal("Mettle", tracker.CurrentItem);
            Assert.Equal(1, tracker.CursorIndex);
        }

        [Fact]
        public void NotInSubmenu_KeysAreIgnored()
        {
            var tracker = new BattleMenuTracker();
            tracker.OnKeyPressed(VK_DOWN);

            Assert.False(tracker.InSubmenu);
            Assert.Null(tracker.CurrentItem);
        }

        [Fact]
        public void NewTurn_ResetsCursorToZero()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // now on Mettle
            tracker.OnNewTurn();

            Assert.False(tracker.InSubmenu);
            Assert.Equal(0, tracker.CursorIndex);
        }

        [Fact]
        public void AfterNewTurn_ReenterStartsAtAttack()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // now on Mettle
            tracker.OnNewTurn();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });

            Assert.Equal("Attack", tracker.CurrentItem);
            Assert.Equal(0, tracker.CursorIndex);
        }

        [Fact]
        public void ThreeItems_NavigatesCorrectly()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle", "Items" });

            Assert.Equal("Attack", tracker.CurrentItem);
            tracker.OnKeyPressed(VK_DOWN);
            Assert.Equal("Mettle", tracker.CurrentItem);
            tracker.OnKeyPressed(VK_DOWN);
            Assert.Equal("Items", tracker.CurrentItem);
            tracker.OnKeyPressed(VK_DOWN);
            Assert.Equal("Attack", tracker.CurrentItem);
        }

        [Fact]
        public void Enter_DoesNotExitSubmenu()
        {
            // Enter selects within the submenu (e.g. opens Attack targeting)
            // but the tracker shouldn't exit — the screen detection will
            // transition to a different state via memory
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_RETURN);

            Assert.True(tracker.InSubmenu);
            Assert.Equal("Attack", tracker.CurrentItem);
        }

        [Fact]
        public void DifferentItemsOnNewTurn_CursorResets()
        {
            // Different unit may have different abilities (e.g. Attack + Items)
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // on Mettle
            tracker.OnNewTurn();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Items" });

            Assert.Equal("Attack", tracker.CurrentItem);
            Assert.Equal(0, tracker.CursorIndex);
        }

        [Fact]
        public void UI_ShouldReflectCurrentItem_WhenInSubmenu()
        {
            // This tests what the screen output should show:
            // When in Battle_Abilities, UI should be the current submenu item
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });

            Assert.Equal("Attack", tracker.CurrentItem);

            tracker.OnKeyPressed(VK_DOWN);
            Assert.Equal("Mettle", tracker.CurrentItem);
        }

        [Fact]
        public void DetectScreenFlicker_ShouldNotLoseCursorPosition()
        {
            // Simulates DetectScreenSettled flickering through Battle_MyTurn
            // during settling after entering Abilities submenu.
            // The tracker should not lose cursor position.
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // on Mettle

            // Simulate flicker: DetectScreen sees Battle_MyTurn briefly → exits submenu
            tracker.OnKeyPressed(VK_ESCAPE);
            // Then DetectScreen sees Battle_Abilities again → re-enters
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });

            Assert.True(tracker.InSubmenu);
            Assert.Equal("Mettle", tracker.CurrentItem);
            Assert.Equal(1, tracker.CursorIndex);
        }

        [Fact]
        public void KeyPress_WhileNotInSubmenu_ThenReenter_ShouldTrack()
        {
            // Bug scenario: Down key sent while InSubmenu=false (due to flicker exit),
            // then screen settles back to Battle_Abilities.
            // The Down should be lost (tracker wasn't in submenu), but the cursor
            // position from before the flicker should be preserved.
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });

            // Flicker exit
            tracker.OnKeyPressed(VK_ESCAPE);
            Assert.False(tracker.InSubmenu);

            // Down pressed while not in submenu — should be ignored
            tracker.OnKeyPressed(VK_DOWN);
            Assert.Equal(0, tracker.CursorIndex); // stayed at Attack (0)

            // Re-enter
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            Assert.Equal("Attack", tracker.CurrentItem); // still Attack, Down was lost
        }
    }
}
