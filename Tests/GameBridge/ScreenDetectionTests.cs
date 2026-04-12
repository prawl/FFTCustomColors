using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ScreenDetectionTests
    {
        // Step 1: TravelList should never be reported during battle
        [Fact]
        public void DetectScreen_InBattle_WithUiFlag_ShouldNotReturnTravelList()
        {
            // During attack animations, uiFlag flickers to 1 and battleMode flickers to 0.
            // This looks like TravelList (party=0, ui=1) but we're still in battle.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.NotEqual("TravelList", result);
            Assert.StartsWith("Battle", result);
        }

        [Fact]
        public void DetectScreen_InBattle_NormalMyTurn_ShouldReturnBattleMyTurn()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_MyTurn", result);
        }

        [Fact]
        public void DetectScreen_InBattle_AfterActing_ShouldReturnBattleActing()
        {
            // Actual acting/animation: submenuFlag=0 (not in a submenu, actually performing action)
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 0);

            Assert.Equal("Battle_Acting", result);
        }

        [Fact]
        public void DetectScreen_InBattle_AbilitiesSubmenu_ShouldReturnBattleAbilities()
        {
            // Entering Abilities submenu sets acted=1, moved=1, submenuFlag=1, menuCursor=1 (Abilities)
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1,
                menuCursor: 1);

            Assert.Equal("Battle_Abilities", result);
        }

        [Fact]
        public void DetectScreen_InBattle_AbilitiesSubmenu_ActedOnly_ShouldReturnBattleAbilities()
        {
            // Sometimes only acted=1 when entering submenu (moved may vary)
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1,
                menuCursor: 1);

            Assert.Equal("Battle_Abilities", result);
        }

        [Fact]
        public void DetectScreen_InBattle_EnemyTurn_ShouldReturnBattleEnemiesTurn()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_EnemiesTurn", result);
        }

        [Fact]
        public void DetectScreen_WorldMap_ShouldReturnWorldMap()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 26, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false);

            Assert.Equal("WorldMap", result);
        }

        [Fact]
        public void DetectScreen_TravelList_NotInBattle_ShouldReturnTravelList()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 26, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false);

            Assert.Equal("TravelList", result);
        }

        [Fact]
        public void DetectScreen_EncounterDialog_ShouldReturnEncounterDialog()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 26, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 7, isPartySubScreen: false);

            Assert.Equal("EncounterDialog", result);
        }

        [Fact]
        public void DetectScreen_GameOver_ShouldReturnGameOver()
        {
            // Game over: submenuFlag=1 (repurposed), paused=1, battleMode=0
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 1,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1);

            Assert.Equal("GameOver", result);
        }

        [Fact]
        public void DetectScreen_MoveMode_SubmenuFlag1_ShouldReturnBattleMoving()
        {
            // Move mode also sets submenuFlag=1, should still detect as Battle_Moving
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1);

            Assert.Equal("Battle_Moving", result);
        }

        [Fact]
        public void DetectScreen_InBattle_AbilityList_Slot0NotFF_ShouldStayInBattle()
        {
            // When browsing ability lists (e.g. Mettle submenu), slot0 changes from 255
            // to a non-FF value (e.g. 146). But we're still in battle — slot9=0xFFFFFFFF
            // and battleMode=3 confirm this. Should NOT detect as Cutscene.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 146, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 401,
                submenuFlag: 1);

            Assert.StartsWith("Battle", result);
        }

        [Fact]
        public void DetectScreen_InBattle_AbilityListBrowsing_BattleMode3_ShouldReturnBattleAbilities()
        {
            // Browsing ability list (e.g. Mettle abilities): battleMode=3, submenuFlag=1, menuCursor=1
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1,
                menuCursor: 1);

            Assert.Equal("Battle_Abilities", result);
        }

        [Fact]
        public void DetectScreen_InBattle_AttackTargeting_BattleMode4_ShouldReturnBattleTargeting()
        {
            // Real targeting mode (after selecting Attack): battleMode=4, submenuFlag=1, moveMode=255
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 4, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1);

            Assert.Equal("Battle_Attacking", result);
        }

        [Fact]
        public void DetectScreen_InBattle_SkillsetTargeting_BattleMode1_ShouldReturnBattleCasting()
        {
            // Empirical capture during Ramza casting Tailwind: battleMode=1, slot0=150 (not 255),
            // slot9=0xFFFFFFFF, submenuFlag=1, menuCursor=1, battleActed=1, battleMoved=1,
            // eventId=401. Previously mis-detected as Cutscene because slot0 != 255 and
            // battleMode=1 wasn't in battleModeActive.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 150, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 401,
                submenuFlag: 1, menuCursor: 1);

            Assert.Equal("Battle_Casting", result);
        }

        [Fact]
        public void DetectScreen_InBattle_MoveMode_BattleMode2_ShouldReturnBattleMoving()
        {
            // Move tile selection: battleMode=2, moveMode=255
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1);

            Assert.Equal("Battle_Moving", result);
        }

        [Theory]
        [InlineData("Attack", "Battle_Attack")]
        [InlineData("Mettle", "Battle_Mettle")]
        [InlineData("Items", "Battle_Items")]
        [InlineData("Arts of War", "Battle_ArtsOfWar")]
        [InlineData("White Magicks", "Battle_WhiteMagicks")]
        [InlineData("Black Magicks", "Battle_BlackMagicks")]
        [InlineData("Time Magicks", "Battle_TimeMagicks")]
        [InlineData("Steal", "Battle_Steal")]
        [InlineData("Fundaments", "Battle_Fundaments")]
        [InlineData("Throw", "Battle_Throw")]
        public void DetectScreen_AbilitySubmenuSelected_ShouldReturnSpecificScreen(
            string selectedAbility, string expectedScreen)
        {
            // When a specific ability is selected from the Abilities submenu,
            // the screen name should include the selected skillset.
            // This is handled by ScreenDetectionLogic.GetAbilityScreenName.
            var result = ScreenDetectionLogic.GetAbilityScreenName(selectedAbility);

            Assert.Equal(expectedScreen, result);
        }

        [Fact]
        public void DetectScreen_BattleMode4_ShouldReturnBattleAttacking()
        {
            // Attack targeting mode should be called Battle_Attacking (not Battle_Attacking)
            // to match Battle_Moving naming convention
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 4, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1);

            Assert.Equal("Battle_Attacking", result);
        }

        [Fact]
        public void DetectScreen_BattleMoving_ShouldReturnBattleMoving()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("Battle_Moving", result);
        }

        [Fact]
        public void DetectScreen_BattleTargeting_ShouldReturnBattleTargeting()
        {
            // Targeting mode is battleMode=4 (not 2, which is Move mode)
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 4, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1);

            Assert.Equal("Battle_Attacking", result);
        }

        [Fact]
        public void DetectScreen_WorldMap_WithStaleUnitSlots_ShouldReturnWorldMap()
        {
            // After leaving battle, unit slots stay populated (0xFF).
            // If rawLocation is valid and battleMode=0, we're on the world map.
            // The save logic depends on this to write last_location.txt.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 26, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false);

            Assert.Equal("WorldMap", result);
        }

        // The critical regression test: battle with flickering flags
        [Theory]
        [InlineData(0, 0)]  // battleMode=0, ui=0 → could look like WorldMap
        [InlineData(0, 1)]  // battleMode=0, ui=1 → could look like TravelList
        public void DetectScreen_InBattle_WithFlickeringFlags_ShouldStayInBattle(int battleMode, int ui)
        {
            // slot0=255, slot9=0xFFFFFFFF → units exist = we're in battle
            // Even if battleMode/ui flicker, we should stay in battle
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: ui, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: battleMode, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.StartsWith("Battle", result);
        }
        [Fact]
        public void DetectScreen_FacingSelection_ShouldReturnBattleWaiting()
        {
            // After Move+Attack, the game enters facing selection.
            // battleMode=2 (same as Move), but menuCursor=2 (Wait) distinguishes it.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1,
                menuCursor: 2);

            Assert.Equal("Battle_Waiting", result);
        }

        [Fact]
        public void DetectScreen_NormalMoveMode_MenuCursor0_ShouldReturnBattleMoving()
        {
            // Normal Move mode: battleMode=2, menuCursor=0 (Move)
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1,
                menuCursor: 0);

            Assert.Equal("Battle_Moving", result);
        }

        [Fact]
        public void DetectScreen_AfterAbilityUse_SubmenuFlagStale_ShouldReturnBattleActing()
        {
            // After using an ability, the game returns to the action menu.
            // submenuFlag=1 stays stale, battleActed=1, battleMode=3, menuCursor=0 (Move).
            // This should NOT be detected as Battle_Abilities — it's Battle_Acting.
            // The key differentiator: menuCursor=0 (Move), not 1 (Abilities).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1,
                menuCursor: 0);

            Assert.Equal("Battle_Acting", result);
        }

        [Fact]
        public void DetectScreen_Formation_BattleMode1_NoUnits_ShouldReturnFormation()
        {
            // Formation screen: battleMode=1, slot9=0xFFFFFFFF (battle-like),
            // but slot0=0xFFFFFFFF (no units populated yet, not 0x000000FF).
            // Location is valid (28), encA==encB (encounter accepted).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 28, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 3, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.Equal("Battle_Formation", result);
        }

        [Fact]
        public void DetectScreen_Formation_ShouldNotReturnBattleCasting()
        {
            // Previously Formation was misdetected as Battle_Casting because
            // battleMode=1 + slot9=0xFFFFFFFF triggered battleModeActive.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 28, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 3, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.NotEqual("Battle_Casting", result);
        }

        [Fact]
        public void DetectScreen_RealBattleCasting_UnitsPopulated_StillWorks()
        {
            // Real Battle_Casting: slot0=255 (0x000000FF, units exist), battleMode=1.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.Equal("Battle_Casting", result);
        }

        [Fact]
        public void DetectScreen_Victory_EncANotEqualEncB_ShouldReturnBattleVictory()
        {
            // Victory screen: stale battle slots, battleMode=0, acted+moved=1,
            // encA != encB (encounter values diverge as battle ends).
            // Previously misdetected as EncounterDialog.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 28, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 6, encB: 5, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 1);

            Assert.Equal("Battle_Victory", result);
        }

        [Fact]
        public void DetectScreen_Victory_ShouldNotReturnEncounterDialog()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 28, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 6, encB: 5, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 1);

            Assert.NotEqual("EncounterDialog", result);
        }

        [Fact]
        public void DetectScreen_Desertion_EncAEqualsEncB_ShouldReturnBattleDesertion()
        {
            // Desertion warning: same post-battle flags as Victory but encA == encB.
            // Needs Enter to dismiss. Previously misdetected as TravelList.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 28, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 10, encB: 10, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 1);

            Assert.Equal("Battle_Desertion", result);
        }

        [Fact]
        public void DetectScreen_RealEncounterDialog_NotInBattle_StillWorks()
        {
            // Real encounter dialog: encA != encB but NOT post-battle (acted=0, moved=0).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 28, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.Equal("EncounterDialog", result);
        }

        [Fact]
        public void DetectScreen_Desertion_WithPaused_ShouldReturnBattleDesertion()
        {
            // Second observed Desertion variant: paused=1, location=255, gameOverFlag=1.
            // Previously misdetected as GameOver. Distinguished by acted=1/moved=1.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 1,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 9, encB: 9, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.Equal("Battle_Desertion", result);
        }

        [Fact]
        public void DetectScreen_Status_Paused_MenuCursor3_ShouldReturnBattleStatus()
        {
            // Status screen: paused=1 + menuCursor=3. Previously misdetected as Battle_Paused.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 3);

            Assert.Equal("Battle_Status", result);
        }

        [Fact]
        public void DetectScreen_Status_ShouldNotReturnBattlePaused()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 3);

            Assert.NotEqual("Battle_Paused", result);
        }

        [Fact]
        public void DetectScreen_AutoBattle_MenuCursor4_ShouldReturnBattleAutoBattle()
        {
            // Auto-Battle submenu: menuCursor=4 + submenuFlag=1 + battleMode=3.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 4);

            Assert.Equal("Battle_AutoBattle", result);
        }

        [Fact]
        public void DetectScreen_RealPause_MenuCursorNot3_StillReturnsBattlePaused()
        {
            // Real pause (Tab key): paused=1, menuCursor != 3.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.Equal("Battle_Paused", result);
        }

        [Fact]
        public void DetectScreen_PostBattle_StaleFlags_ShouldNotReturnBattle()
        {
            // After battle ends and Desertion is dismissed, stale flags persist:
            // slot0=255, slot9=0xFFFFFFFF, acted=1, moved=1, submenuFlag=1,
            // battleMode=0, location=255. Should NOT detect as any Battle_ state.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 8, encB: 8, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.DoesNotContain("Battle", result);
        }

        [Fact]
        public void DetectScreen_PostBattle_StaleFlags_ShouldReturnTitleScreen()
        {
            // With location=255 and no battle, should fall through to TitleScreen.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 8, encB: 8, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.Equal("TitleScreen", result);
        }

        [Fact]
        public void DetectScreen_InBattle_WithRealEventId_BattleMode0_ShouldReturnBattleDialogue()
        {
            // Mid-battle dialogue: inBattle=true (units populated), real eventId < 200,
            // battleMode=0 (no action menu — dialogue is showing).
            // Real event script IDs are small (event004.en.mes), not nameIds (401+).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 4);

            Assert.Equal("Battle_Dialogue", result);
        }

        [Fact]
        public void DetectScreen_InBattle_WithEventId_BattleMode3_ShouldNotReturnBattleDialogue()
        {
            // eventId=401 also fires during normal battle (ability browsing).
            // If battleMode=3 (action menu active), it's NOT dialogue.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 401);

            Assert.NotEqual("Battle_Dialogue", result);
        }
    }
}
