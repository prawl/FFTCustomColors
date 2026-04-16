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

            Assert.Equal("BattleMyTurn", result);
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

            Assert.Equal("BattleActing", result);
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

            Assert.Equal("BattleAbilities", result);
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

            Assert.Equal("BattleAbilities", result);
        }

        [Fact]
        public void DetectScreen_InBattle_EnemyTurn_ShouldReturnBattleEnemiesTurn()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("BattleEnemiesTurn", result);
        }

        [Fact]
        public void DetectScreen_WorldMap_ShouldReturnWorldMap()
        {
            // WorldMap empirical signature (audit #28, post-restart + save load):
            // rawLocation=255, party=0, ui=1, slot0=0xFFFFFFFF, battleMode=255 (uninit),
            // moveMode=255, eventId=0xFFFF. See detection_audit.md §28.
            //
            // Note: a "pristine WorldMap with ui=0" state has not been directly observed in
            // the audit — all post-load world maps read ui=1 until the user interacts. This
            // test uses ui=1 to match reality.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 255, slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 2, battleActed: 0, battleMoved: 0,
                encA: 8, encB: 8, isPartySubScreen: false, eventId: 0xFFFF);

            // With current inputs, post-load WorldMap and TravelList are byte-identical.
            // Best-effort split routes party=0+ui=1 to TravelList. Either result indicates
            // we're on the world-map-side of the game, which is what callers need.
            Assert.True(result == "TravelList" || result == "WorldMap",
                $"Expected TravelList or WorldMap, got {result}");
        }

        [Fact]
        public void DetectScreen_TravelList_NotInBattle_ShouldReturnTravelList()
        {
            // TravelList: rawLocation=255, ui=1 (menu overlay on the world map).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 255, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, eventId: 0xFFFF);

            Assert.Equal("TravelList", result);
        }

        [Fact]
        public void DetectScreen_LocationMenu_AtNamedLocation_ShouldReturnLocationMenu()
        {
            // LocationMenu: rawLocation=0-42 + hover=255 + locationMenuFlag=1 (from
            // 0x140D43481 — the real menu-inside-location signal found via memory diff
            // 2026-04-14). Shop types (Outfitter/Tavern/Warrior Guild/etc.) still
            // indistinguishable, all collapse to generic LocationMenu.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 9, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, hover: 255,
                locationMenuFlag: 1);

            Assert.Equal("LocationMenu", result);
        }

        [Fact]
        public void DetectScreen_ShopInterior_AtShopSelector_ShouldReturnShopTypeName()
        {
            // Inside an Outfitter, cursor on Buy/Sell/Fitting at the shop menu
            // (not yet Entered into a sub-action). shopSubMenuIndex=0 until Enter.
            // shopTypeIndex=0 → Outfitter. Verified 2026-04-14 at Dorter.
            // Post-rename (session 19) the shop interior screen is named after
            // the shop itself (Outfitter/Tavern/WarriorsGuild/PoachersDen/SaveGame).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 9, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, hover: 255,
                locationMenuFlag: 1, insideShopFlag: 1, shopSubMenuIndex: 0,
                shopTypeIndex: 0);

            Assert.Equal("Outfitter", result);
        }

        [Fact]
        public void DetectScreen_ShopInterior_Tavern_ReturnsTavern()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 9, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, hover: 255,
                locationMenuFlag: 1, insideShopFlag: 1, shopSubMenuIndex: 0,
                shopTypeIndex: 1);

            Assert.Equal("Tavern", result);
        }

        [Fact]
        public void DetectScreen_ShopInterior_PoachersDen_ReturnsPoachersDen()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 9, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, hover: 255,
                locationMenuFlag: 1, insideShopFlag: 1, shopSubMenuIndex: 0,
                shopTypeIndex: 3);

            Assert.Equal("PoachersDen", result);
        }

        [Fact]
        public void DetectScreen_ShopBuy_ShouldReturnShopBuy()
        {
            // shopSubMenuIndex=1 after Enter on Buy inside Outfitter.
            // Verified 2026-04-14 at Dorter: 0x14184276C reads 1 when inside Buy.
            // Note: insideShopFlag/locationMenuFlag both read 0 here in live testing —
            // shopSubMenuIndex alone discriminates (255 on worldmap, 0 at shop menu,
            // 1/4/6 inside submenus).
            // Live state at Dorter Outfitter Buy (verified via dump_detection_inputs
            // 2026-04-14): slot0=0xFFFFFFFF, slot9=0xFFFFFFFF, battleMode=255, hover=254.
            // Out-of-battle branch (slot0 not 255, battleMode not in 0-5).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 9, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, hover: 254,
                locationMenuFlag: 0, insideShopFlag: 0, shopSubMenuIndex: 1);

            Assert.Equal("OutfitterBuy", result);
        }

        [Fact]
        public void DetectScreen_ShopSell_ShouldReturnShopSell()
        {
            // shopSubMenuIndex=4 after Enter on Sell inside Outfitter.
            // Verified 2026-04-14 at Dorter: 0x14184276C reads 4 when inside Sell.
            // Live state matches Shop_Buy — out-of-battle branch (slot0=0xFFFFFFFF).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 9, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, hover: 254,
                locationMenuFlag: 0, insideShopFlag: 0, shopSubMenuIndex: 4);

            Assert.Equal("OutfitterSell", result);
        }

        [Fact]
        public void DetectScreen_ShopFitting_ShouldReturnShopFitting()
        {
            // shopSubMenuIndex=6 after Enter on Fitting inside Outfitter.
            // Verified 2026-04-14 at Dorter: 0x14184276C reads 6 when inside Fitting.
            // Live state matches Shop_Buy — out-of-battle branch (slot0=0xFFFFFFFF).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 9, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, hover: 254,
                locationMenuFlag: 0, insideShopFlag: 0, shopSubMenuIndex: 6);

            Assert.Equal("OutfitterFitting", result);
        }

        [Fact]
        public void ResolveShopSubAction_Outfitter_MapsAllThreeValues()
        {
            Assert.Equal("OutfitterBuy", ScreenDetectionLogic.ResolveShopSubAction(0, 1));
            Assert.Equal("OutfitterSell", ScreenDetectionLogic.ResolveShopSubAction(0, 4));
            Assert.Equal("OutfitterFitting", ScreenDetectionLogic.ResolveShopSubAction(0, 6));
        }

        [Fact]
        public void ResolveShopSubAction_Outfitter_UnmappedValueReturnsNull()
        {
            // shopSubMenuIndex=0 means player is at the sub-action selector,
            // not inside any of Buy/Sell/Fitting. Return null so the caller
            // falls through to SettlementMenu.
            Assert.Null(ScreenDetectionLogic.ResolveShopSubAction(0, 0));
        }

        [Fact]
        public void ResolveShopSubAction_UnmappedShopTypes_ReturnNull()
        {
            // Tavern, Warriors' Guild, Poachers' Den sub-action values haven't
            // been scanned yet — ResolveShopSubAction returns null for them
            // regardless of shopSubMenuIndex, so the caller falls through to
            // SettlementMenu until those values are mapped.
            Assert.Null(ScreenDetectionLogic.ResolveShopSubAction(1, 1)); // Tavern
            Assert.Null(ScreenDetectionLogic.ResolveShopSubAction(2, 1)); // WarriorsGuild
            Assert.Null(ScreenDetectionLogic.ResolveShopSubAction(3, 1)); // PoachersDen
        }

        [Fact]
        public void DetectScreen_ShopSubMenuIndex_OnWorldMap_ShouldNotTriggerShopScreen()
        {
            // Sentinel check: on WorldMap, shopSubMenuIndex reads 255.
            // Outfitter_* rules must not fire on 255 (they only fire on 1/4/6).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 26, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, hover: 26,
                locationMenuFlag: 0, insideShopFlag: 0, shopSubMenuIndex: 255);

            Assert.DoesNotContain("Outfitter", result);
            Assert.DoesNotContain("Shop_", result);
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

            Assert.Equal("BattleMoving", result);
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

            Assert.Equal("BattleAbilities", result);
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

            Assert.Equal("BattleAttacking", result);
        }

        [Fact]
        public void DetectScreen_InBattle_SkillsetTargeting_BattleMode1_ShouldReturnBattleAttacking()
        {
            // Empirical capture during Ramza casting Tailwind: battleMode=1, slot0=150 (not 255),
            // slot9=0xFFFFFFFF, submenuFlag=1, menuCursor=1, battleActed=1, battleMoved=1,
            // eventId=401.
            // Audit 2026-04-14: cast-time and instant targeting are byte-identical in memory.
            // Battle_Casting collapsed into Battle_Attacking. Callers track cast-time via
            // the ability that was selected (client-side state).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 150, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false, eventId: 401,
                submenuFlag: 1, menuCursor: 1);

            Assert.Equal("BattleAttacking", result);
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

            Assert.Equal("BattleMoving", result);
        }

        [Theory]
        [InlineData("Attack", "BattleAttack")]
        [InlineData("Mettle", "BattleMettle")]
        [InlineData("Items", "BattleItems")]
        [InlineData("Arts of War", "BattleArtsOfWar")]
        [InlineData("White Magicks", "BattleWhiteMagicks")]
        [InlineData("Black Magicks", "BattleBlackMagicks")]
        [InlineData("Time Magicks", "BattleTimeMagicks")]
        [InlineData("Steal", "BattleSteal")]
        [InlineData("Fundaments", "BattleFundaments")]
        [InlineData("Throw", "BattleThrow")]
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

            Assert.Equal("BattleAttacking", result);
        }

        [Fact]
        public void DetectScreen_BattleMoving_ShouldReturnBattleMoving()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false);

            Assert.Equal("BattleMoving", result);
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

            Assert.Equal("BattleAttacking", result);
        }

        [Fact]
        public void DetectScreen_LocationMenu_WithStaleUnitSlots_ShouldReturnLocationMenu()
        {
            // Active in-session LocationMenu: rawLocation=0-42 + hover=255 +
            // locationMenuFlag=1 (the 0x140D43481 discriminator). Stale battle sentinels
            // (slot0=0xFF) present but don't interfere because atNamedLocation overrides.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 26, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false, hover: 255,
                locationMenuFlag: 1);

            Assert.Equal("LocationMenu", result);
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

            Assert.Equal("BattleWaiting", result);
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

            Assert.Equal("BattleMoving", result);
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

            Assert.Equal("BattleActing", result);
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

            Assert.Equal("BattleFormation", result);
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

            Assert.NotEqual("BattleCasting", result);
        }

        [Fact]
        public void DetectScreen_RealBattleCasting_UnitsPopulated_ReturnsAttacking()
        {
            // battleMode=1 with units populated maps to Battle_Attacking after the
            // Battle_Casting collapse (audit 2026-04-14).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.Equal("BattleAttacking", result);
        }

        [Fact]
        public void DetectScreen_Victory_ShouldReturnBattleVictory()
        {
            // Victory screen: post-battle at named battle location, acted+moved=1,
            // locationMenuFlag=1 (we're at the battle location's post-battle subscreen).
            // Synthetic test — Battle_Victory hasn't been captured live in the audit yet
            // because desertion has always triggered. Assumes locationMenuFlag=1 pattern
            // consistent with other post-battle named-location states.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 28, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 6, encB: 5, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 1, locationMenuFlag: 1);

            Assert.Equal("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_Victory_ShouldNotReturnEncounterDialog()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 28, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 6, encB: 5, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 1, locationMenuFlag: 1);

            Assert.NotEqual("EncounterDialog", result);
        }

        [Fact]
        public void DetectScreen_Desertion_Paused_ShouldReturnBattleDesertion()
        {
            // Desertion warning: paused=1 + submenuFlag=1 at a named battle location with
            // post-battle sticky flags (acted=1, moved=1). Empirical from audit samples #44,
            // #45 — both observed Desertion readings had paused=1. Distinguished from Victory
            // by paused (Victory doesn't pause the game).
            // locationMenuFlag=1 assumed since we're at a named battle location subscreen.
            //
            // Audit 2026-04-14 disproved the old encA==encB discriminator — those counters
            // drift independently and are not a reliable screen signal.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 28, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 10, encB: 10, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 1, locationMenuFlag: 1);

            Assert.Equal("BattleDesertion", result);
        }

        [Fact]
        public void DetectScreen_RealEncounterDialog_NotInBattle_StillWorks()
        {
            // EncounterDialog rules disabled — encA/encB are sticky noise.
            // With rules disabled, this falls through to something else.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 28, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.NotEqual("EncounterDialog", result);
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

            Assert.Equal("BattleDesertion", result);
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

            Assert.Equal("BattleStatus", result);
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

            Assert.NotEqual("BattlePaused", result);
        }

        [Fact]
        public void DetectScreen_AutoBattle_MenuCursor4_ShouldNotReturnBattleAutoBattle()
        {
            // Audit 2026-04-14: Battle_AutoBattle rule deleted. The real top-level AutoBattle
            // hover has submenuFlag=0 (this test's submenuFlag=1 is a submenu state), so the
            // old rule never fired on genuine AutoBattle hovers. It fired SPURIOUSLY inside
            // the Abilities submenu when cursor landed at skillset index 4, root-causing the
            // "Auto-Battle instead of Wait" bug. The UI label (already set in DetectScreen
            // from menuCursor) handles the user-facing "hovering AutoBattle" case correctly.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 4);

            Assert.NotEqual("BattleAutoBattle", result);
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

            Assert.Equal("BattlePaused", result);
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
        public void DetectScreen_PostBattle_StaleFlags_ShouldReturnWorldMap()
        {
            // After battle ends or dialogue is dismissed, stale flags persist briefly while
            // transitioning back to world map. Verified live 2026-04-14: user confirmed this
            // state lands on WorldMap, not TitleScreen. TitleScreen requires full uninit
            // sentinels (slot0=0xFFFFFFFF, battleMode=255) handled by the strict rule earlier.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 8, encB: 8, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.Equal("WorldMap", result);
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

            Assert.Equal("BattleDialogue", result);
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

            Assert.NotEqual("BattleDialogue", result);
        }

        // === Tab-flag override tests (session 20, 2026-04-16) ===
        // 0x140D3A41E (unitsTabFlag) and 0x140D3A38E (inventoryTabFlag) are
        // cross-session-stable binary flags that override the stale party byte.
        // When either is 1, the player is in the PartyMenu tree regardless of
        // what party/ui/rawLocation say.

        [Fact]
        public void DetectScreen_UnitsTabFlag_OverridesStalePartyByte()
        {
            // Real scenario: on EqA (party sub-screen), party byte cleared to 0,
            // ui=1, rawLocation=6. Without tab flags this falls through to
            // TravelList. With unitsTabFlag=1, should return PartyMenu.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 6, slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 2, battleActed: 255, battleMoved: 255,
                encA: 9, encB: 9, isPartySubScreen: true, eventId: 0xFFFF,
                unitsTabFlag: 1, inventoryTabFlag: 0);

            Assert.Equal("PartyMenu", result);
        }

        [Fact]
        public void DetectScreen_InventoryTabFlag_OverridesStalePartyByte()
        {
            // On Inventory tab, party byte cleared to 0. inventoryTabFlag=1.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 6, slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 2, battleActed: 255, battleMoved: 255,
                encA: 9, encB: 9, isPartySubScreen: true, eventId: 0xFFFF,
                unitsTabFlag: 0, inventoryTabFlag: 1);

            Assert.Equal("PartyMenu", result);
        }

        [Fact]
        public void DetectScreen_NoTabFlags_FallsThroughToTravelList()
        {
            // Same inputs as above but with both tab flags off — should NOT
            // return PartyMenu. This is the pre-fix behavior.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 6, slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 2, battleActed: 255, battleMoved: 255,
                encA: 9, encB: 9, isPartySubScreen: true, eventId: 0xFFFF,
                unitsTabFlag: 0, inventoryTabFlag: 0);

            Assert.NotEqual("PartyMenu", result);
        }

        [Fact]
        public void DetectScreen_UnitsTabFlag_OverridesBattleSentinels()
        {
            // Edge case: stale battle sentinels (slot0=0xFF, slot9=0xFFFFFFFF)
            // that would normally put us in the battle branch. Tab flag should
            // still win because PartyMenu is checked before inBattle.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                unitsTabFlag: 1, inventoryTabFlag: 0);

            Assert.Equal("PartyMenu", result);
        }

        [Fact]
        public void DetectScreen_TabFlag_WithPartyOne_DoesNotPreempt()
        {
            // When party==1, the normal `party==1 → PartyMenu` rule handles
            // detection. Tab flags should NOT fire early — doing so would
            // bypass the SM-based sub-screen resolution (CharacterStatus, EqA).
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 6, slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 2, battleActed: 255, battleMoved: 255,
                encA: 9, encB: 9, isPartySubScreen: true, eventId: 0xFFFF,
                unitsTabFlag: 1, inventoryTabFlag: 0);

            // Should still return PartyMenu (via the party==1 rule, not the tab flag rule)
            Assert.Equal("PartyMenu", result);
        }

        [Fact]
        public void DetectScreen_TabFlags_DontFireWhenBothZero()
        {
            // Normal WorldMap: both tab flags off, party=0. Should detect normally.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, hover: 5,
                unitsTabFlag: 0, inventoryTabFlag: 0);

            Assert.Equal("WorldMap", result);
        }
    }
}
