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
        public void Enter_SetsSelectedItem()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle", "Items" });
            tracker.OnKeyPressed(VK_RETURN);

            Assert.Equal("Attack", tracker.SelectedItem);
        }

        [Fact]
        public void Enter_OnMettle_SetsSelectedToMettle()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle", "Items" });
            tracker.OnKeyPressed(VK_DOWN); // Mettle
            tracker.OnKeyPressed(VK_RETURN);

            Assert.Equal("Mettle", tracker.SelectedItem);
        }

        [Fact]
        public void SelectedItem_NullBeforeEnter()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });

            Assert.Null(tracker.SelectedItem);
        }

        [Fact]
        public void SelectedItem_ClearedOnEscape()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_RETURN); // select Attack
            tracker.OnKeyPressed(VK_ESCAPE); // back out

            Assert.Null(tracker.SelectedItem);
        }

        [Fact]
        public void SelectedItem_ClearedOnNewTurn()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_RETURN);
            tracker.OnNewTurn();

            Assert.Null(tracker.SelectedItem);
        }

        // === Ability list (third level) tests ===

        [Fact]
        public void EnterSkillset_SetsAbilityList()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // Mettle
            tracker.EnterAbilityList(new[] { "Focus", "Rush", "Shout" });

            Assert.True(tracker.InAbilityList);
            Assert.Equal("Focus", tracker.CurrentAbility);
            Assert.Equal(0, tracker.AbilityCursorIndex);
        }

        [Fact]
        public void AbilityList_DownNavigates()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.EnterAbilityList(new[] { "Focus", "Rush", "Shout" });
            tracker.OnKeyPressed(VK_DOWN);

            Assert.Equal("Rush", tracker.CurrentAbility);
            Assert.Equal(1, tracker.AbilityCursorIndex);
        }

        [Fact]
        public void AbilityList_UpWraps()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.EnterAbilityList(new[] { "Focus", "Rush", "Shout" });
            tracker.OnKeyPressed(VK_UP);

            Assert.Equal("Shout", tracker.CurrentAbility);
        }

        [Fact]
        public void AbilityList_EscapeReturnsToSubmenu()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // Mettle
            tracker.EnterAbilityList(new[] { "Focus", "Rush", "Shout" });
            tracker.OnKeyPressed(VK_ESCAPE);

            Assert.False(tracker.InAbilityList);
            Assert.True(tracker.InSubmenu);
            Assert.Equal("Mettle", tracker.CurrentItem);
            Assert.Null(tracker.CurrentAbility);
        }

        [Fact]
        public void AbilityList_DownUpDoesNotAffectSubmenuCursor()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // Mettle
            tracker.EnterAbilityList(new[] { "Focus", "Rush", "Shout" });
            tracker.OnKeyPressed(VK_DOWN); // Rush
            tracker.OnKeyPressed(VK_DOWN); // Shout

            // Submenu cursor should still be on Mettle
            Assert.Equal(1, tracker.CursorIndex);
        }

        [Fact]
        public void AbilityList_NewTurnClearsAll()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.EnterAbilityList(new[] { "Focus", "Rush", "Shout" });
            tracker.OnNewTurn();

            Assert.False(tracker.InAbilityList);
            Assert.False(tracker.InSubmenu);
            Assert.Null(tracker.CurrentAbility);
        }

        [Fact]
        public void AbilityList_FilteredByLearned()
        {
            // If unit only learned Focus and Shout, list should only contain those
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.EnterAbilityList(new[] { "Focus", "Shout" });

            Assert.Equal("Focus", tracker.CurrentAbility);
            tracker.OnKeyPressed(VK_DOWN);
            Assert.Equal("Shout", tracker.CurrentAbility);
            tracker.OnKeyPressed(VK_DOWN);
            Assert.Equal("Focus", tracker.CurrentAbility); // wraps
        }

        [Fact]
        public void AbilityList_EnterDoesNotExitList()
        {
            // Enter selects the ability (for targeting), but tracker
            // stays in list — screen detection handles the transition
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.EnterAbilityList(new[] { "Focus", "Rush", "Shout" });
            tracker.OnKeyPressed(VK_DOWN); // Rush
            tracker.OnKeyPressed(VK_RETURN);

            Assert.True(tracker.InAbilityList);
            Assert.Equal("Rush", tracker.SelectedAbility);
        }

        [Fact]
        public void SelectedAbility_ClearedOnEscape()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.EnterAbilityList(new[] { "Focus", "Rush" });
            tracker.OnKeyPressed(VK_RETURN); // select Focus
            tracker.OnKeyPressed(VK_ESCAPE); // back to submenu

            Assert.Null(tracker.SelectedAbility);
        }

        [Fact]
        public void ReturnToMyTurn_ResetsSubmenuAndAbilityList()
        {
            // After battle_ability completes, the game returns to Battle_MyTurn.
            // The tracker should fully reset so ui= shows the correct main menu cursor.
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle", "Items" });
            tracker.OnKeyPressed(VK_DOWN); // Mettle
            tracker.OnKeyPressed(VK_RETURN); // select Mettle
            tracker.EnterAbilityList(new[] { "Focus", "Rush", "Shout" });
            tracker.OnKeyPressed(VK_DOWN); // Rush

            tracker.ReturnToMyTurn();

            Assert.False(tracker.InSubmenu);
            Assert.False(tracker.InAbilityList);
            Assert.Null(tracker.CurrentItem);
            Assert.Null(tracker.CurrentAbility);
            Assert.Null(tracker.SelectedItem);
            Assert.Null(tracker.SelectedAbility);
            // Cursor indices reset so next entry starts fresh
            Assert.Equal(0, tracker.CursorIndex);
            Assert.Equal(0, tracker.AbilityCursorIndex);
        }

        // === Existing tests below ===

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

        [Fact]
        public void SyncForScreen_BattleActing_WhileInAbilityList_ShouldFullyReset()
        {
            // After battle_ability executes (e.g. Shout), screen becomes Battle_Acting.
            // SyncForScreen should fully reset the tracker — not just one Escape level.
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle", "Items" });
            tracker.OnKeyPressed(VK_DOWN); // Mettle
            tracker.OnKeyPressed(VK_RETURN); // select Mettle
            tracker.EnterAbilityList(new[] { "Focus", "Rush", "Shout" });
            tracker.OnKeyPressed(VK_DOWN); // Rush
            tracker.OnKeyPressed(VK_RETURN); // select Rush

            // Screen transitions to Battle_Acting after ability use
            tracker.SyncForScreen("Battle_Acting");

            Assert.False(tracker.InSubmenu);
            Assert.False(tracker.InAbilityList);
            Assert.Null(tracker.SelectedItem);
            Assert.Null(tracker.SelectedAbility);
        }

        [Fact]
        public void SyncForScreen_BattleAbilities_DoesNotReset()
        {
            // When screen IS Battle_Abilities, the tracker should stay in submenu state.
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_DOWN); // Mettle

            tracker.SyncForScreen("Battle_Abilities");

            Assert.True(tracker.InSubmenu);
            Assert.Equal("Mettle", tracker.CurrentItem);
        }

        [Fact]
        public void HasActedThisTurn_PreventsReenteringSubmenu()
        {
            // After using an ability, SyncForScreen resets the tracker and sets HasActedThisTurn.
            // If stale memory flags cause Battle_Abilities to be detected again,
            // the tracker should refuse to re-enter the submenu.
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle", "Time Magicks" });
            tracker.OnKeyPressed(VK_DOWN); // Mettle
            tracker.OnKeyPressed(VK_DOWN); // Time Magicks
            tracker.OnKeyPressed(VK_RETURN); // select Time Magicks
            tracker.EnterAbilityList(new[] { "Haste", "Slow" });
            tracker.OnKeyPressed(VK_RETURN); // select Haste

            // Ability executes, screen transitions away from Abilities
            tracker.SyncForScreen("Battle_Attacking");

            // Tracker should be reset AND mark that we acted
            Assert.False(tracker.InSubmenu);
            Assert.True(tracker.HasActedThisTurn);

            // Stale memory flags cause Battle_Abilities to be detected again
            // Tracker should refuse to re-enter
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle", "Time Magicks" });
            Assert.False(tracker.InSubmenu); // refused!
        }

        [Fact]
        public void HasActedThisTurn_ClearedOnNewTurn()
        {
            var tracker = new BattleMenuTracker();
            tracker.EnterAbilitiesSubmenu(new[] { "Attack", "Mettle" });
            tracker.OnKeyPressed(VK_RETURN);
            tracker.SyncForScreen("Battle_Acting");
            Assert.True(tracker.HasActedThisTurn);

            tracker.OnNewTurn();
            Assert.False(tracker.HasActedThisTurn);
        }
    }
}
