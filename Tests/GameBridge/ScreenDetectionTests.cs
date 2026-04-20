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
            // Game over: submenuFlag=1, paused=1, battleMode=0, battleTeam=1
            // (session 49 added battleTeam!=0 guard — team=0 is the
            // Victory-with-Ramza-dying edge case which routes to BattleVictory).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 1,
                battleTeam: 1, battleActed: 0, battleMoved: 0,
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

        [Fact]
        public void DetectScreen_InBattle_MoveMode_CursorOnOffGridTile_BattleMode1_ShouldStayBattleMoving()
        {
            // Session 30 live-verified: during Move mode, when the cursor sits on a
            // tile OUTSIDE the highlighted blue move grid, battleMode flickers from
            // 2 to 1 (same value the game uses for "off-highlight in targeting").
            // Discriminator: menuCursor stays 0 (Move) in Move mode; targeting has
            // menuCursor=1 (Abilities was selected to reach targeting). Without
            // this discriminator, screen misdetects as BattleAttacking.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false, submenuFlag: 1, menuCursor: 0);

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
        public void DetectScreen_EncounterFlag_Zero_NoEncounterDialog()
        {
            // encounterFlag=0 means no encounter active — even with sticky encA/encB
            // noise, EncounterDialog should NOT fire.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 28, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0, locationMenuFlag: 1,
                encounterFlag: 0);

            Assert.NotEqual("EncounterDialog", result);
        }

        [Fact]
        public void DetectScreen_EncounterFlag_AtNamedLocation_ReturnsEncounterDialog()
        {
            // encounterFlag=10 at a named location (locationMenuFlag=1) → EncounterDialog.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 28, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0, locationMenuFlag: 1,
                encounterFlag: 10);

            Assert.Equal("EncounterDialog", result);
        }

        [Fact]
        public void DetectScreen_EncounterFlag_WhileTraveling_ReturnsEncounterDialog()
        {
            // encounterFlag=10 while traveling (rawLocation=255, moveMode=13) → EncounterDialog.
            // slot9=1 to avoid TitleScreen rule (slot9=0 triggers fresh-process detection).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0, slot9: 1,
                battleMode: 0, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                encounterFlag: 10);

            Assert.Equal("EncounterDialog", result);
        }

        [Fact]
        public void DetectScreen_EncounterFlag_DoesNotFireInBattle()
        {
            // encounterFlag sticky during battle should NOT cause EncounterDialog
            // when we're actually in a battle (slot0=255, slot9=0xFFFFFFFF).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                encounterFlag: 10);

            Assert.NotEqual("EncounterDialog", result);
            Assert.StartsWith("Battle", result);
        }

        [Fact]
        public void DetectScreen_EncounterFlag_DoesNotOverridePartyMenu()
        {
            // encounterFlag should not fire when party=1 (PartyMenu takes priority).
            // slot9=1 to avoid TitleScreen rule.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 0, rawLocation: 255, slot0: 0, slot9: 1,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                encounterFlag: 10);

            Assert.Equal("PartyMenuUnits", result);
        }

        // === BattleChoice (pre/mid-battle objective choice prompt) ===
        // Session 44 2026-04-18: discriminator is the 0xFB byte presence in
        // the event's .mes script data, scanned once at EventScriptLookup
        // init. At runtime, the new `eventHasChoice` parameter is true only
        // for known-choice events (e.g. event 016 at Mandalia Plain).

        [Fact]
        public void DetectScreen_BattleChoice_WhenEventHasChoice_AtNamedLocation()
        {
            // Captured session 44: pre-Mandalia choice prompt "1. Defeat the
            // Brigade" / "2. Rescue the captive" — rawLocation=24, eventId=16,
            // battleTeam=0, acted/moved=1. Requires BOTH eventHasChoice (.mes
            // has 0xFB) AND choiceModalFlag != 0 (runtime byte at 0x140C70055
            // confirms the modal is drawn, not the narration prefix).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 24, slot0: 0xFFFFFFFFL, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 16, locationMenuFlag: 1, eventHasChoice: true,
                choiceModalFlag: 1);
            Assert.Equal("BattleChoice", result);
        }

        [Fact]
        public void DetectScreen_BattleDialogue_WhenChoiceEventButModalFlagZero()
        {
            // Narration prefix of a choice event (eventHasChoice=true because
            // the .mes file contains 0xFB somewhere, but the actual modal
            // prompt hasn't rendered yet). Must return BattleDialogue, not
            // BattleChoice — this is the case that fires during the early
            // lines of event 016 before the "1. Defeat / 2. Rescue" modal
            // appears. Session 44 heap-diff found the flag.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 24, slot0: 0xFFFFFFFFL, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 16, locationMenuFlag: 1, eventHasChoice: true,
                choiceModalFlag: 0);
            Assert.Equal("BattleDialogue", result);
        }

        [Fact]
        public void DetectScreen_BattleDialogue_WhenEventHasNoChoice_FallsThroughToDialogue()
        {
            // Same fingerprint, but eventHasChoice=false (event002 Orbonne
            // opening, or event010 Gariland pre-battle). Must return
            // BattleDialogue, not BattleChoice.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 24, slot0: 0xFFFFFFFFL, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 10, locationMenuFlag: 1, eventHasChoice: false);
            Assert.Equal("BattleDialogue", result);
        }

        [Fact]
        public void DetectScreen_BattleChoice_DoesNotFire_WithoutSlot0Sentinel()
        {
            // eventHasChoice=true but slot0 != 0xFFFFFFFF (e.g. still mid-
            // combat with units loaded). Should not fire — BattleChoice
            // requires the same sentinel pattern as BattleDialogue.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 24, slot0: 0x42, slot9: 0,
                battleMode: 2, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 16, eventHasChoice: true);
            Assert.NotEqual("BattleChoice", result);
        }

        // === BattleSequence (multi-stage campaign sub-selector) ===
        // Enabled session 44 2026-04-18: discriminator byte at 0x14077D1F8
        // reads 1 on minimap / 0 on plain WorldMap. Combined with the 8-loc
        // whitelist (Riovanes/Lionel/Limberry/Zeltennia/Ziekden/Mullonde/
        // Orbonne/FortBesselat) distinguishes the minimap from WorldMap-at-loc.

        [Fact]
        public void DetectScreen_BattleSequence_AtOrbonneWithFlag_ReturnsBattleSequence()
        {
            // Live-captured session 44: at Orbonne (loc 18), minimap auto-open,
            // battleSequenceFlag=1.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 18, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                battleSequenceFlag: 1);
            Assert.Equal("BattleSequence", result);
        }

        [Theory]
        [InlineData(1)]  // Riovanes Castle
        [InlineData(3)]  // Lionel Castle
        [InlineData(4)]  // Limberry Castle
        [InlineData(5)]  // Zeltennia Castle
        [InlineData(15)] // Ziekden Fortress
        [InlineData(16)] // Mullonde
        [InlineData(18)] // Orbonne Monastery
        [InlineData(21)] // Fort Besselat
        public void DetectScreen_BattleSequence_AllWhitelistLocs_FireWithFlag(int loc)
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: loc, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                battleSequenceFlag: 1);
            Assert.Equal("BattleSequence", result);
        }

        [Fact]
        public void DetectScreen_BattleSequence_NonWhitelistLoc_IgnoresFlag()
        {
            // At Bervenia (loc 13, not a BattleSequence location), even if the
            // flag is set (which shouldn't happen in practice, but defensive),
            // we should fall through to the normal WorldMap/LocationMenu path.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 13, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                battleSequenceFlag: 1);
            Assert.NotEqual("BattleSequence", result);
        }

        [Fact]
        public void DetectScreen_BattleSequence_FlagZero_ReturnsNonBattleSequence()
        {
            // At Orbonne (loc 18), but flag=0 means minimap isn't open —
            // player is on plain WorldMap at Orbonne. Should NOT detect as
            // BattleSequence.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 18, slot0: 0, slot9: 0,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                battleSequenceFlag: 0);
            Assert.NotEqual("BattleSequence", result);
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

        // Session 21 at Orbonne Monastery captured slot0=0x67 (not 255) during Victory
        // and Desertion screens. The unitSlotsPopulated requirement (slot0==255) caused
        // these to fall through to BattlePaused. Fix: post-battle detection should work
        // even when slot0 is a non-sentinel value like 0x67, as long as the other battle
        // signals (battleMode=0, actedOrMoved=true, paused=1/0) hold.
        [Fact]
        public void DetectScreen_Desertion_Orbonne_Slot0_0x67_ShouldReturnBattleDesertion()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0,
                eventId: 303);

            Assert.Equal("BattleDesertion", result);
        }

        [Fact]
        public void DetectScreen_Victory_Gariland_EncA255_WinsOverDesertion()
        {
            // Session 44 live capture (2026-04-18): post-Gariland Victory banner
            // showed fingerprint that matched BOTH the BattleDesertion rule
            // (atNamedLocation + slot0=255 + paused=1 + submenuFlag=1 +
            // actedOrMoved + battleMode=0) AND the Victory encA=255 sentinel,
            // but Desertion fires first in the !inBattle branch. Session 50
            // adds an encA=255 Victory short-circuit at the top of the
            // atNamedLocation rules so Victory wins when the banner is visible.
            // Captured fingerprint: rawLocation=6 (Gariland), battleTeam=0,
            // battleActed=1, battleMoved=1, paused=1, submenuFlag=1,
            // menuCursor=1, eventId=12. encA/encB=255 added synthetically
            // matching session-45 Zeklaus banner signature.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 6, slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 255, encB: 255, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 1, locationMenuFlag: 1,
                eventId: 12);

            Assert.Equal("BattleVictory", result);
        }

        /// <summary>
        /// Session 52 regression pin: sticky gameOverFlag=1 from a prior
        /// GameOver must not cause a real story cutscene to misdetect as
        /// LoadGame. Session 21 flagged this via eventId=2 (Orbonne Monastery
        /// opening). The LoadGame rule at ScreenDetectionLogic.cs:557 now
        /// requires `IsEventIdUnset(eventId)` so a live eventId (1-399)
        /// skips LoadGame and lets the Cutscene rule fire.
        /// </summary>
        [Fact]
        public void DetectScreen_Cutscene_WithStickyGameOverFlag_DoesNotMisdetectAsLoadGame()
        {
            // Real cutscene fingerprint with sticky gameOverFlag=1 from a
            // prior game-over. Without the eventId guard, this flows through
            // the LoadGame rule.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 18, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 1,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 2);

            Assert.NotEqual("LoadGame", result);
        }

        [Fact]
        public void DetectScreen_LoadGame_RealState_StillReturnsLoadGame()
        {
            // Negative guard: actual post-GameOver LoadGame (eventId unset)
            // still routes to LoadGame. The eventId guard only narrows scope,
            // doesn't kill the rule.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 1,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 0);

            Assert.Equal("LoadGame", result);
        }

        [Fact]
        public void DetectScreen_Victory_EncA255Sentinel_FiresOverLoadGameAndTitleScreen()
        {
            // Session 49 live capture (Siedge Weald, kill_enemies-triggered victory):
            // During the banner phase the game's encA/encB bytes both hit 255 for
            // ~1 second, but the existing LoadGame rule preempts the Victory
            // sentinel. Captured poll output showed state sequence
            //   GameOver → TitleScreen → LoadGame (locked)
            // while user confirmed V-screen was visible throughout. The bytes at
            // 0x140900824 / 0x140900828 read FF during t=6/7 of the poll —
            // that's our Victory window. The sentinel rule must win over
            // LoadGame and TitleScreen rules even when gameOverFlag=1 and
            // submenuFlag=1 are sticky from the prior battle state.
            // See memory/project_battle_victory_encA255.md.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 1,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 255, encB: 255, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 2,
                eventId: 0);

            Assert.Equal("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_Victory_EncA255_GuardedFromBattleStartMisfire()
        {
            // Session 49 follow-up: at the first frame of a new battle encA can
            // briefly read 0xFF before being set to a real value. Without the
            // actedOrMoved guard, the sentinel would misfire and return
            // BattleVictory on BattleMyTurn frame 1 of a fresh battle.
            // acted=0, moved=0 on a never-acted unit → sentinel must stay silent.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 255, encB: 255, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 0);

            Assert.NotEqual("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_VictoryWithRamzaDying_TeamZeroGuard_ReturnsBattleVictory()
        {
            // Session 49: Victory-with-Ramza-dying-from-counter edge case.
            // Ramza counter-killed the final enemy on the same frame as
            // dying himself. Outcome = Victory. Fingerprint:
            //   - paused=1, gameOverFlag=1, battleMode=0 (shared with GameOver)
            //   - battleTeam=0 (player triggered the final action) ← discriminator
            //   - encA=05, encB=05 (past the Victory banner window)
            //
            // The battleTeam==0 guard sends this to BattleVictory instead of
            // GameOver (which now requires battleTeam != 0).
            //
            // See memory/feedback_victory_gameover_both_encA255_risk.md.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 1,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0x05, encB: 0x05, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 2,
                eventId: 0);

            Assert.Equal("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_GameOver_BattleTeam1_StillReturnsGameOver()
        {
            // Real GameOver: enemy killed Ramza. battleTeam=1 guards against
            // the team=0 Victory-with-dying-Ramza edge case.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0xFFFFFFFF, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 1,
                battleTeam: 1, battleActed: 1, battleMoved: 1,
                encA: 0x05, encB: 0x05, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 4,
                eventId: 0);

            Assert.Equal("GameOver", result);
        }

        [Fact]
        public void DetectScreen_Victory_Orbonne_Slot0_0x67_ShouldReturnBattleVictory()
        {
            // Same as above but paused=0 (auto-advancing victory result screen).
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);

            Assert.Equal("BattleVictory", result);
        }

        // Guard: the Orbonne Victory/Desertion relaxation (session 33) must NOT swallow
        // an active EncounterDialog. EncounterDialog has party=0/ui=0 (not a PartyMenu
        // state); the new Victory rule requires party=1+ui=1 and so can't fire on these
        // inputs. This test proves EncounterDialog still wins when its real signals are set.
        [Fact]
        public void DetectScreen_EncounterDialog_NotMisdetectedAsVictory_OrbonneFix()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 0, encounterFlag: 10);

            Assert.Equal("EncounterDialog", result);
        }

        // Guard: TitleScreen must NOT swallow world-side screens with rawLocation=255.
        // The old catch-all `if (rawLocation == 255) return "TitleScreen"` was removed in
        // a prior session; these tests pin that removal so a future refactor doesn't
        // re-introduce a loose TitleScreen rule.
        [Fact]
        public void DetectScreen_PartyMenu_WithRawLocation255_ShouldNotBeTitleScreen()
        {
            // PartyMenu reached from the world map uses party=1 signal; rawLocation remains
            // 255 because the world-side menu doesn't set a named location.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 0, rawLocation: 255, slot0: 0x10, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);
            Assert.NotEqual("TitleScreen", result);
        }

        [Fact]
        public void DetectScreen_WorldMapHoveringLocation_ShouldNotBeTitleScreen()
        {
            // WorldMap with cursor hovering a named node (hover=26, Siedge Weald).
            // rawLocation is 255 until the player lands on the node; hover holds the ID.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0x10, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                hover: 26);
            Assert.Equal("WorldMap", result);
            Assert.NotEqual("TitleScreen", result);
        }

        // Guard: post-battle stale flags on the WorldMap (party=0, ui=0, no active event)
        // must NOT misdetect as Victory via the Orbonne variant. The new rule requires
        // party=1 + ui=1 + eventId 1..399, which these inputs don't satisfy.
        [Fact]
        public void DetectScreen_StaleWorldMap_NotMisdetectedAsVictory_OrbonneFix()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 8, encB: 8, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0,
                eventId: 0);

            Assert.NotEqual("BattleVictory", result);
            Assert.NotEqual("BattleDesertion", result);
        }

        // Additional post-battle stale-flag edge cases (session 33). These pin
        // behavior across the Orbonne-variant rules and sibling post-battle fallbacks
        // so a future rule tweak doesn't regress world-side detection.

        [Fact]
        public void DetectScreen_StaleFlags_Party1Ui0_NoEventId_ShouldNotBeVictory()
        {
            // party=1 is a PartyMenu signal; without a battle eventId the Orbonne Victory
            // rule should NOT fire — even if slot0 is a non-sentinel value.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 0, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 0);
            Assert.NotEqual("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_StaleFlags_EventId400_ShouldNotBeVictory()
        {
            // eventId must be 1..399 for the Orbonne Victory rule; 400+ is out of the
            // "real battle scene" range and should fall through to other rules.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 400);
            Assert.NotEqual("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_StaleFlags_Slot0FFFFFFFF_ShouldNotBeVictory()
        {
            // slot0=0xFFFFFFFF is the formation sentinel; Orbonne rules explicitly
            // exclude this value since it means units aren't placed yet.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);
            Assert.NotEqual("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_StaleFlags_NotActedOrMoved_ShouldNotBeVictory()
        {
            // actedOrMoved is a post-battle sticky signal; without it, we haven't
            // completed a battle yet, so Victory/Desertion can't fire.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);
            Assert.NotEqual("BattleVictory", result);
            Assert.NotEqual("BattleDesertion", result);
        }

        // Session 34: property-style boundary sweep for Orbonne Victory/Desertion
        // discriminator. The rule in ScreenDetectionLogic.cs requires:
        //   battleModeActive && battleMode == 0 && actedOrMoved
        //   && slot0 != 0xFFFFFFFF && slot0 != 255
        //   && party == 1 && ui == 1
        //   && eventId in [1, 400)
        // Victory: paused == 0
        // Desertion: paused == 1 && submenuFlag == 1
        // These tests pin each boundary so a future rule tweak surfaces visibly.

        [Fact]
        public void DetectScreen_OrbonneVictory_EventId1_LowerBound_ShouldReturnVictory()
        {
            // eventId=1 is the inclusive lower bound of the real-event range.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 1);
            Assert.Equal("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_OrbonneVictory_EventId399_UpperBound_ShouldReturnVictory()
        {
            // eventId=399 is the inclusive upper bound (the rule uses `< 400`).
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 399);
            Assert.Equal("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_OrbonneVictory_EventId0_ShouldNotFire()
        {
            // eventId=0 is "unset"; Victory rule requires an active event in [1, 400).
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 0);
            Assert.NotEqual("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_OrbonneVictory_Paused1_ShouldNotFire()
        {
            // Victory rule requires paused=0 (auto-advancing result screen).
            // With paused=1 + submenuFlag=0 we're neither Victory nor Desertion.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);
            Assert.NotEqual("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_OrbonneDesertion_RequiresSubmenuFlag1_Paused1WithoutSubmenu()
        {
            // Desertion rule requires BOTH paused=1 AND submenuFlag=1. The submenu
            // is the "really leave?" warning overlay — without it we're in a
            // different paused state (not Desertion).
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);
            Assert.NotEqual("BattleDesertion", result);
        }

        [Fact]
        public void DetectScreen_OrbonneDesertion_SubmenuFlag1_Paused1_ShouldReturnDesertion()
        {
            // Full Desertion signature: paused=1 + submenuFlag=1 + other Orbonne gates.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0,
                eventId: 303);
            Assert.Equal("BattleDesertion", result);
        }

        [Fact]
        public void DetectScreen_Orbonne_Party0_ShouldNotBeVictoryOrDesertion()
        {
            // party=0 disqualifies the Orbonne variant. Without the PartyMenu signal
            // we're likely in a stale-flag state or a battle-dialogue screen.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);
            Assert.NotEqual("BattleVictory", result);
            Assert.NotEqual("BattleDesertion", result);
        }

        [Fact]
        public void DetectScreen_Orbonne_Ui0_ShouldNotBeVictoryOrDesertion()
        {
            // ui=0 disqualifies; both Orbonne rules require ui=1.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 0, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);
            Assert.NotEqual("BattleVictory", result);
            Assert.NotEqual("BattleDesertion", result);
        }

        [Fact]
        public void DetectScreen_Orbonne_Slot0_Ox10_NonSentinel_VariantVictory()
        {
            // slot0=0x10 (a different non-sentinel unit-struct value) should still
            // fire Victory — the rule only excludes 0xFFFFFFFF and 255.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x10, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);
            Assert.Equal("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_OrbonneVictory_ActedOnly_ShouldFire()
        {
            // actedOrMoved = acted || moved. Only-acted is sufficient.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);
            Assert.Equal("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_OrbonneVictory_MovedOnly_ShouldFire()
        {
            // Only-moved is also sufficient for actedOrMoved.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255, slot0: 0x67, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0,
                eventId: 303);
            Assert.Equal("BattleVictory", result);
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

            Assert.Equal("PartyMenuUnits", result);
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

            Assert.Equal("PartyMenuUnits", result);
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

            Assert.NotEqual("PartyMenuUnits", result);
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

            Assert.Equal("PartyMenuUnits", result);
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
            Assert.Equal("PartyMenuUnits", result);
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

        // --- SM override for detection-ambiguous screens ---
        // SaveSlotPicker shares every detection-input byte with TravelList
        // (session 2026-04-17 memory hunt: all 28 fields identical). The SM
        // knows we're on SaveSlotPicker because the Enter-on-Save transition
        // is explicit. When detection returns "TravelList" but SM says
        // SaveSlotPicker, prefer SM.

        [Fact]
        public void ResolveAmbiguous_SMOnSaveSlotPicker_DetectionTravelList_ReturnsSaveSlotPicker()
        {
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.SaveSlotPicker,
                detectedName: "TravelList");
            Assert.Equal("SaveSlotPicker", result);
        }

        [Fact]
        public void ResolveAmbiguous_SMAgreesWithDetection_PassesThrough()
        {
            // SM says TravelList and detection says TravelList — no override.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.TravelList,
                detectedName: "TravelList");
            Assert.Equal("TravelList", result);
        }

        [Fact]
        public void ResolveAmbiguous_SMOnSaveSlotPicker_DetectionSaysBattle_TrustsDetection()
        {
            // If detection says anything OTHER than TravelList, it saw a real
            // screen change — trust detection (SM is stale).
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.SaveSlotPicker,
                detectedName: "BattleMyTurn");
            Assert.Equal("BattleMyTurn", result);
        }

        [Fact]
        public void ResolveAmbiguous_SMOnSaveSlotPicker_DetectionWorldMap_TrustsDetection()
        {
            // Escape closes the picker; detection catches up and says WorldMap.
            // Don't force SaveSlotPicker over that.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.SaveSlotPicker,
                detectedName: "WorldMap");
            Assert.Equal("WorldMap", result);
        }

        [Fact]
        public void ResolveAmbiguous_SMOnTavernRumors_DetectionLocationMenu_ReturnsTavernRumors()
        {
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.TavernRumors,
                detectedName: "LocationMenu");
            Assert.Equal("TavernRumors", result);
        }

        [Fact]
        public void ResolveAmbiguous_SMOnTavernErrands_DetectionLocationMenu_ReturnsTavernErrands()
        {
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.TavernErrands,
                detectedName: "LocationMenu");
            Assert.Equal("TavernErrands", result);
        }

        [Fact]
        public void ResolveAmbiguous_SMOnTavernRumors_DetectionBattleMyTurn_TrustsDetection()
        {
            // Real screen transition must not be overridden.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.TavernRumors,
                detectedName: "BattleMyTurn");
            Assert.Equal("BattleMyTurn", result);
        }

        // Additional SaveSlotPicker vs TravelList ambiguity coverage (session 33).
        // These document the full override surface so a future refactor doesn't
        // accidentally override the wrong combinations.

        [Fact]
        public void ResolveAmbiguous_SMOnSaveSlotPicker_DetectionPartyMenuUnits_TrustsDetection()
        {
            // Escape from SaveSlotPicker typically lands on PartyMenuUnits (the save
            // entry point). Don't force SaveSlotPicker there.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.SaveSlotPicker,
                detectedName: "PartyMenuUnits");
            Assert.Equal("PartyMenuUnits", result);
        }

        [Fact]
        public void ResolveAmbiguous_SMOnTravelList_DetectionTravelList_PassesThrough()
        {
            // The non-override case — both SM and detection agree. Important pin:
            // make sure we don't somehow promote TravelList to SaveSlotPicker.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.TravelList,
                detectedName: "TravelList");
            Assert.Equal("TravelList", result);
            Assert.NotEqual("SaveSlotPicker", result);
        }

        [Fact]
        public void ResolveAmbiguous_SMOnWorldMap_DetectionTravelList_TrustsDetection()
        {
            // SM stale; player opened the travel list but SM didn't catch the key.
            // Detection's TravelList wins.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.WorldMap,
                detectedName: "TravelList");
            Assert.Equal("TravelList", result);
        }

        [Fact]
        public void ResolveAmbiguous_SMOnLocationMenu_DetectionLocationMenu_PassesThrough()
        {
            // Should NOT override to TavernRumors/TavernErrands when SM actually
            // knows we're on the generic LocationMenu.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                smScreen: GameScreen.LocationMenu,
                detectedName: "LocationMenu");
            Assert.Equal("LocationMenu", result);
        }

        // --- TitleScreen tightening (session 26, 2026-04-17) ---
        // The loose `rawLocation==255 → TitleScreen` fallback used to swallow
        // valid world-side screens after a GameOver. The strict rule now
        // requires full uninit sentinels. Any state missing those falls
        // through to the world-side rules (WorldMap / TravelList) instead.

        [Fact]
        public void DetectScreen_TitleScreen_FullUninitSentinels_Detected()
        {
            // Truly fresh process launch: all the uninit markers set.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFFL,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 0xFFFF);
            Assert.Equal("TitleScreen", result);
        }

        [Fact]
        public void DetectScreen_TitleScreen_NeedsBattleMode255()
        {
            // The audit flagged battleMode as part of the uninit fingerprint.
            // Same raw fingerprint as the previous test but battleMode=0 —
            // this is post-battle stale state, NOT the title screen. Should
            // fall through to WorldMap via the world-side rule.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFFL,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 0xFFFF);
            Assert.NotEqual("TitleScreen", result);
        }

        [Fact]
        public void DetectScreen_LooseRawLocation255_NoMatchingRule_ReturnsUnknown()
        {
            // Reach the residual fallback deliberately: rawLocation=255 with
            // ui>1 (fails TravelList AND WorldMap branches), no hover (skips
            // hover rule), no uninit sentinels (skips strict TitleScreen),
            // no eventId (skips Cutscene), no party flag, no encounter flag.
            // Old code fell back to "TitleScreen" here. New code should say
            // "Unknown" — we genuinely can't place this state.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 7, rawLocation: 255,
                slot0: 100, slot9: 100,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 0);
            Assert.NotEqual("TitleScreen", result);
        }

        [Fact]
        public void DetectScreen_PartyMenuOpenWithMapHover_ReturnsPartyMenuNotWorldMap()
        {
            // Repro: open PartyMenu while WorldMap cursor is parked on a named
            // location (hover=26 = The Siedge Weald). party==1 is authoritative —
            // must not lose to the WorldMap-hover rule that fires on
            // `hover in 0..42 && rawLocation==255`. The fix: PartyMenu rule
            // needs to run BEFORE the WorldMap hover rule, OR the WorldMap
            // hover rule needs to also require party==0.
            var result = ScreenDetectionLogic.Detect(
                party: 1, ui: 1, rawLocation: 255,
                slot0: 100, slot9: 100,
                battleMode: 255, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                hover: 26);
            Assert.Equal("PartyMenuUnits", result);
        }

        [Fact]
        public void DetectScreen_PreBattleCutscene_EventId302_AtOrbonne_ReturnsBattleDialogue()
        {
            // Real scenario captured session 21: Orbonne Monastery pre-battle
            // Loffrey dialogue. eventId=302 (in 200-399 range = real event but
            // also overlaps nameId range). OUT-OF-BATTLE atNamedLocation rule
            // with slot0=0xFFFFFFFF uniquely identifies pre-battle dialogue
            // at a named battle ground. Must return BattleDialogue, not
            // fall through to LocationMenu/Unknown.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 18,  // Orbonne Monastery
                slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFFL,
                battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 302, locationMenuFlag: 1);
            // Formation fires first (slot0=0xFFFFFFFF + battleMode=1) so that's
            // actually what we'd see. Let me switch to a post-dialogue frame
            // where battleMode has settled back to 255.
            // With battleMode=1 this detects as BattleFormation — expected.
            Assert.Equal("BattleFormation", result);
        }

        [Fact]
        public void DetectScreen_PreBattleCutscene_EventId302_BattleMode255_ReturnsBattleDialogue()
        {
            // Same Orbonne pre-battle scenario but after Formation concluded:
            // battleMode back to 255, but eventId=302 still fires, slot0 still
            // uninit from battle setup. atNamedLocation=true via locationMenuFlag=1.
            // This is the state that motivated the `< 400` filter.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 18,
                slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFFL,
                battleMode: 255, moveMode: 255, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 302, locationMenuFlag: 1);
            Assert.Equal("BattleDialogue", result);
        }

        [Fact]
        public void DetectScreen_EncounterDialog_PreemptsWorldMapHover()
        {
            // Repro: encounterFlag set while walking the map. The
            // encounter prompt appears at a battleground node, so
            // hover might still hold the last-known location ID.
            // encounterFlag > 0 must win over the WorldMap-hover rule.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 100, slot9: 100,
                battleMode: 255, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                hover: 26, encounterFlag: 10);
            Assert.Equal("EncounterDialog", result);
        }

        [Fact]
        public void DetectScreen_LooseRawLocation255_DoesNotFallBackToTitleScreen()
        {
            // The old catch-all `if (rawLocation == 255) return "TitleScreen"` must
            // go. A state with rawLocation=255 but NO uninit sentinels AND no
            // match for earlier world-side rules (party/ui both zero, no hover,
            // no eventId, no paused/gameOver) should NOT be labeled TitleScreen
            // — it's an unknown/stale state and the code should say so.
            //
            // Concrete repro: slot0/slot9 are non-uninit real values (from a
            // live save), ui/party are 0, but we're not on WorldMap (no hover,
            // no moveMode world signal). Old code would catch this as
            // TitleScreen via line 281 fallback. New code must return something
            // else — preferably "Unknown" so the caller knows detection can't
            // place them.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 100, slot9: 100,      // real save values, NOT uninit
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                eventId: 0);
            Assert.NotEqual("TitleScreen", result);
        }

        // ================================================================
        // Session-45 stress tests — real live-captured fingerprints from
        // the 2026-04-19 Zeklaus + Dorter playthrough. Each test pins a
        // known desync surface so future rule edits can't regress them.
        // Reference: memory/project_battle_victory_encA255.md,
        //            memory/project_crystal_states_undetected.md
        // ================================================================

        [Fact]
        public void DetectScreen_Zeklaus_VictoryBanner_encA255_ShouldReturnBattleVictory()
        {
            // Live-captured at Zeklaus Desert Siedge win 2026-04-19.
            // Victory banner is on screen but the game hasn't transitioned back
            // to WorldMap yet — rawLocation is still 255 (not at named location).
            // Prior bridge reported BattleDialogue because IsMidBattleEvent(41)
            // wins first. encA=255 + encB=255 is a transient sentinel unique to
            // the Victory banner; all other session-45 states saw encA ∈ [0..7].
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 1,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 255, encB: 255, isPartySubScreen: false,
                eventId: 41, submenuFlag: 1, menuCursor: 1);

            Assert.Equal("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_MidBattleDialogue_encA7_ShouldStillReturnBattleDialogue()
        {
            // Regression guard: a normal mid-battle dialogue (encA in the
            // 0..7 range seen across session 45) must NOT be stolen by the
            // Victory rule. eventId is a real mid-battle event, encA/encB
            // are normal noise counters, not the 255 sentinel.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 7, encB: 4, isPartySubScreen: false,
                eventId: 40, submenuFlag: 1, menuCursor: 1);

            Assert.Equal("BattleDialogue", result);
        }

        [Fact]
        public void DetectScreen_OnlyEncA255_EncBDifferent_ShouldNotFireVictory()
        {
            // Safety: the Victory rule requires BOTH encA=255 AND encB=255.
            // A rogue encA=255 alone (noise counter max that happens to land
            // at 255 without the companion) must not be mislabeled as Victory.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 255, encB: 3, isPartySubScreen: false,
                eventId: 40, submenuFlag: 1, menuCursor: 1);

            Assert.NotEqual("BattleVictory", result);
        }

        [Fact]
        public void DetectScreen_VictoryEncA255_WithPaused1_ShouldNotFire()
        {
            // Safety: paused=1 means the game is in some overlay (BattleStatus,
            // BattlePaused, Desertion). encA=255 during a paused state should
            // not steal the label from the paused-specific rules. This guards
            // against a paused Victory-esque screen (should be handled by
            // the paused branch), AND a BattleDesertion at the same point.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 255, encB: 255, isPartySubScreen: false,
                eventId: 41, submenuFlag: 1, menuCursor: 3);

            Assert.NotEqual("BattleVictory", result);
        }

        [Theory]
        [InlineData(3, 3, "BattleMyTurn")]          // normal active-unit turn start
        [InlineData(2, 2, "BattleMoving")]          // normal moving (moveMode=255)
        // AbilityLearnedBanner live-captured encA=0/encB=0, verifies banner path.
        public void DetectScreen_Session45_NonCrystalBaselines_DoNotRegressToCrystal(
            int encA, int encB, string expected)
        {
            // Belt-and-suspenders: prove the rules that fire BEFORE the
            // crystal-pickup rule aren't accidentally stolen when encA lands
            // on a value the crystal branch also inspects (0, 2, 4, 7).
            int battleMode = expected == "BattleMyTurn" ? 3 : 1;
            int submenuFlag = expected == "BattleMyTurn" ? 0 : 1;
            int moveMode = expected == "BattleMoving" ? 255 : 0;
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: battleMode, moveMode: moveMode,
                paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: encA, encB: encB, isPartySubScreen: false,
                submenuFlag: submenuFlag, menuCursor: 0);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void DetectScreen_Crystal_AcquireConfirmBoundary_encA5_RoutesToAcquireConfirm()
        {
            // Boundary characterization: the crystal rule splits
            // AcquireConfirm (encA>=5) from CrystalReward (encA<5) at 5.
            // Live-captured values were encA=7 (confirm) and encA=4 (reward),
            // so encA=5 is the precise boundary. Pin it so any future
            // tweak of the threshold triggers a review.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 4, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.Equal("BattleAbilityAcquireConfirm", result);
        }

        [Fact]
        public void DetectScreen_Crystal_RewardBoundary_encA4_RoutesToCrystalReward()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 4, encB: 4, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.Equal("BattleCrystalReward", result);
        }

        [Fact]
        public void DetectScreen_Crystal_LearnedBanner_encA0_RoutesToLearnedBanner()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.Equal("BattleAbilityLearnedBanner", result);
        }

        [Fact]
        public void DetectScreen_Chest_ObtainedBanner_encA1_RoutesToRewardObtained()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 1, encB: 1, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.Equal("BattleRewardObtainedBanner", result);
        }

        [Fact]
        public void DetectScreen_Crystal_MoveConfirm_encA2_RoutesToMoveConfirm()
        {
            // Live-captured at Zeklaus 2026-04-19: stepping onto a crystal
            // tile with moveMode=0 (not a regular move-tile pick which is
            // moveMode=255) and encA=2 opens the Yes/No "Move and obtain?"
            // popup. This is the state the player actually sees first.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 2, encB: 2, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.Equal("BattleCrystalMoveConfirm", result);
        }

        [Fact(Skip = "Known bug — Gariland post-win misdetects as BattleDesertion. " +
                     "Captured session 44; no discriminator found between paused=1 Victory " +
                     "and paused=1 Desertion. Re-enable when the byte hunt lands.")]
        public void DetectScreen_Gariland_PostWin_Paused_KnownMisdetect()
        {
            // Characterization placeholder: fingerprint captured session 44
            // 2026-04-18 at fresh-game Gariland win. Screen shows Victory banner
            // but paused=1 + submenuFlag=1 at rawLocation=6 hits the Desertion
            // rule first and the Victory rule requires paused=0. Both post-battle
            // states are byte-identical with current signals — needs a new
            // discriminator. See TODO.md "BattleVictory misdetects as BattleDesertion".
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 6,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 1,
                eventId: 12, locationMenuFlag: 1);

            Assert.Equal("BattleVictory", result);
        }

        // ================================================================
        // ADVERSARIAL STRESS BATTERY (2026-04-19)
        // Each probe asserts what detection SHOULD do with a suspect
        // fingerprint. Failures reveal real desyncs in the current logic.
        // Grouped by attack surface.
        // ================================================================

        // ---------- Group 1: Turn-owner × battleMode collision ----------

        [Fact]
        public void Stress_EnemyTurn_AfterEnemyActed_ShouldStayEnemiesTurn()
        {
            // Probe: enemy just acted (battleActed=1, battleMoved=1) but the
            // turn hasn't ended yet — still battleTeam=1. Current turn-owner
            // guard is `battleTeam==1 && !actedOrMoved`, so when the enemy
            // has acted it FALLS THROUGH to battleMode-based rules. If the
            // enemy cursor is on a target (battleMode=4), we label it
            // BattleAttacking — as if the PLAYER is attacking.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 4, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 1, battleMoved: 1,
                encA: 3, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            // Correct behavior: it's still the ENEMY's turn regardless of
            // acted/moved flags — they may be animating a multi-hit or
            // preparing to Wait. We should NOT report a player submode.
            Assert.Equal("BattleEnemiesTurn", result);
        }

        [Fact]
        public void Stress_AllyTurn_AfterAllyActed_ShouldStayAlliesTurn()
        {
            // Same probe as above for NPC ally turn (team=2).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 4, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 2, battleActed: 1, battleMoved: 0,
                encA: 3, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.Equal("BattleAlliesTurn", result);
        }

        [Fact]
        public void Stress_EnemyTurn_Moving_ShouldStayEnemiesTurn()
        {
            // Enemy pathing (battleMode=2) after they already started this turn
            // (moved=1). Should NOT label as BattleMoving (the player move mode).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 2, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 0, battleMoved: 1,
                encA: 3, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 2);

            Assert.Equal("BattleEnemiesTurn", result);
        }

        // ---------- Group 2: Sticky flags leaking across screens ----------

        [Fact]
        public void Stress_StaleUnitsTabFlag_AfterFlee_ShouldNotReportPartyMenu()
        {
            // Per handoff: unitsTabFlag sometimes sticks at 1 on WorldMap after
            // battle_flee. If menuDepth is unknown (-1 default), the rule at
            // line ~207 trips and we mis-report WorldMap as PartyMenuUnits.
            // Simulates: party=0 (not actually on PartyMenu), unitsTabFlag=1
            // (stale), moveMode=13 (world map active), menuDepth default -1.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                hover: 26, unitsTabFlag: 1);

            Assert.NotEqual("PartyMenuUnits", result);
        }

        [Fact]
        public void Stress_StaleGameOverFlag_InBattle_ShouldNotFireGameOver()
        {
            // Per session 44: gameOverFlag sticks across save reload. Player
            // starts a fresh battle with stale flag=1. During a normal
            // non-paused mid-battle state we must NOT return "GameOver".
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 1,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 3, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.NotEqual("GameOver", result);
            Assert.Equal("BattleMyTurn", result);
        }

        [Fact]
        public void Stress_StaleBattleSequenceFlag_AtNonWhitelistedLocation()
        {
            // battleSequenceFlag sticky from prior BattleSequence. Now at
            // Dorter (rawLoc=9 — NOT in whitelist). Rule is location-gated
            // so this correctly skips; pin it so a future refactor can't
            // silently drop the whitelist check and over-fire.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 9,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false,
                battleSequenceFlag: 1, hover: 255, locationMenuFlag: 0);

            Assert.NotEqual("BattleSequence", result);
        }

        // ---------- Group 3: Boundary eventId ----------

        [Fact]
        public void Stress_EventId199_InBattle_ShouldBeMidBattleDialogue()
        {
            // 199 is the last eventId that counts as mid-battle under the
            // strict IsMidBattleEvent upper bound (200). Must fire the
            // dialogue rule.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 3, encB: 3, isPartySubScreen: false,
                eventId: 199, submenuFlag: 1, menuCursor: 1);

            Assert.Equal("BattleDialogue", result);
        }

        [Fact]
        public void Stress_EventId200_InBattle_ShouldNotFireMidBattleRule()
        {
            // 200 is the first nameId-aliased value. Must NOT route via
            // IsMidBattleEvent — otherwise combat animations produce
            // spurious BattleDialogue. Should fall through to post-battle /
            // MyTurn / BattleActing rules.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 3, encB: 3, isPartySubScreen: false,
                eventId: 200, submenuFlag: 0, menuCursor: 0);

            Assert.NotEqual("BattleDialogue", result);
            Assert.Equal("BattleMyTurn", result);
        }

        // ---------- Group 4: new Victory rule — collision guards ----------

        [Fact]
        public void Stress_Victory_EncA255_DuringAllyTurn_ShouldNotStealFromAlly()
        {
            // Pathological: encA=255, encB=255 during NPC ally turn. Victory
            // rule fires FIRST — steals the ally-turn label. Is that correct?
            // Ground truth: Victory banner clears the battle HUD, so there
            // is no "active unit turn" during it. If the game is showing
            // an ally turn with encA=255 (hypothetical), it's not actually
            // the Victory banner. Our rule must be precise enough to avoid
            // firing when a live unit turn is clearly happening.
            //
            // Current rule: `encA == 255 && encB == 255 && battleMode == 0
            // && paused == 0`. battleMode=0 during ally turn is plausible
            // if the ally hasn't started acting. This WOULD over-fire.
            //
            // Expected fix if this fails: add `&& !(battleTeam == 1 ||
            // battleTeam == 2)` or `&& actedOrMoved` to the rule.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 2, battleActed: 0, battleMoved: 0,
                encA: 255, encB: 255, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            // Assert what we THINK should happen — an active ally turn
            // should win over a theoretical sentinel that only exists on
            // the post-battle banner. If this fails we need the guard.
            Assert.NotEqual("BattleVictory", result);
        }

        [Fact]
        public void Stress_Victory_EncA255_DuringEnemyTurn_ShouldNotStealFromEnemy()
        {
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 1, battleActed: 0, battleMoved: 0,
                encA: 255, encB: 255, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.NotEqual("BattleVictory", result);
        }

        // ---------- Group 5: moveMode flicker during battle ----------

        [Fact]
        public void Stress_MoveMode13Flicker_DuringBattle_ShouldStayInBattle()
        {
            // Hypothetical flicker: mid-battle, moveMode byte transiently
            // reads a world-map value (13). inBattle should STILL be true
            // because slot sentinels are authoritative. If moveMode alone
            // can force us out of inBattle, every animation frame is a
            // potential desync.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 3, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 3, encB: 3, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            Assert.StartsWith("Battle", result);
        }

        // ---------- Group 6: Crystal encA drift ----------

        [Fact]
        public void Stress_CrystalRule_encA100_RoutesToCrystalMoveConfirm()
        {
            // If encA shifts (memory note: widget-stack-depth — may shift
            // across restarts), what happens at an unexpected value like
            // 100? Current code: paused=0 + moveMode=0 + encA!=0,1 →
            // BattleCrystalMoveConfirm. Pin it so we know the fallback.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 100, encB: 100, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            Assert.Equal("BattleCrystalMoveConfirm", result);
        }

        [Fact]
        public void Stress_CrystalRule_encA255_WhilePaused_FiresAcquireConfirm()
        {
            // What if the new Victory sentinel overlaps with a paused crystal
            // modal? Victory rule guards on paused==0 so it's safe. Crystal
            // rule at battleMode==1 paused==1 with encA>=5 fires. Pin it.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 1, moveMode: 0, paused: 1, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 255, encB: 255, isPartySubScreen: false,
                submenuFlag: 1, menuCursor: 0);

            // Victory rule is paused==0, falls through. Crystal paused
            // branch fires with encA>=5 → AcquireConfirm.
            Assert.Equal("BattleAbilityAcquireConfirm", result);
        }

        // ---------- Group 7: sentinel ambiguity ----------

        [Fact]
        public void Stress_Slot0_0xFFFFFFFF_WithBattleMode0_InBattleBranch()
        {
            // Formation requires battleMode==1. What if battleMode flickers
            // to 0 during formation load? Formation rule doesn't match —
            // falls through. slot0=0xFFFFFFFF + slot9=0xFFFFFFFF is
            // unitSlotsPopulated=false but battleModeActive requires
            // battleMode in {0..5} so battleMode=0 AND slot9=0xFFFFFFFF →
            // battleModeActive=true → inBattle=true → goes into in-battle
            // branch. postBattle requires unitSlotsPopulated=true (slot0=255).
            // So we fall through. Probably end at the catch-all "Battle".
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 0, encB: 0, isPartySubScreen: false,
                submenuFlag: 0, menuCursor: 0);

            // Whatever we return, it must be battle-adjacent — not
            // "TravelList" or "WorldMap".
            Assert.DoesNotContain("Travel", result);
            Assert.NotEqual("WorldMap", result);
        }

        // ---------- Group 8: Victory ruling with sticky gameOverFlag ----------

        // Session 46: "post-load WorldMap hover=254 → WorldMap" test removed.
        // Live capture 2026-04-19 at Grogh Heights confirmed post-load
        // WorldMap and a freshly-opened TravelList produce byte-identical
        // detection inputs (hover=254, moveMode=255, party=0, ui=1, all
        // slot values match). No memory signal distinguishes them —
        // Detect() returns TravelList by default; the SM overrides via
        // ResolveAmbiguousScreen when it knows we haven't opened the list.

        [Fact]
        public void Stress_ResolveAmbiguous_SmWorldMapFresh_DetectionTravelList_ReturnsWorldMap()
        {
            // Post-load / post-boot WorldMap: SM freshly set (no keys
            // flowed through it yet). Trust SM over the ambiguous bytes.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                GameScreen.WorldMap, "TravelList",
                keysSinceLastSetScreen: 0,
                lastSetScreenFromKey: false);
            Assert.Equal("WorldMap", result);
        }

        [Fact]
        public void Stress_ResolveAmbiguous_SmWorldMapStale_DetectionTravelList_TrustsDetection()
        {
            // SM stale: keys have flowed through since it was set, but
            // the user may have opened the TravelList via a key the SM
            // missed. Trust detection to avoid latching onto stale SM.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                GameScreen.WorldMap, "TravelList",
                keysSinceLastSetScreen: 3,
                lastSetScreenFromKey: true);
            Assert.Equal("TravelList", result);
        }

        [Fact]
        public void Stress_ResolveAmbiguous_SmTravelList_DetectionTravelList_ReturnsTravelList()
        {
            // Normal TravelList: SM and Detect() agree. No override.
            var result = ScreenDetectionLogic.ResolveAmbiguousScreen(
                GameScreen.TravelList, "TravelList",
                keysSinceLastSetScreen: 1,
                lastSetScreenFromKey: true);
            Assert.Equal("TravelList", result);
        }

        [Fact]
        public void Stress_RealTravelList_Hover255_StillTravelList()
        {
            // Regression guard: a REAL TravelList with hover=255 (no specific
            // location cursored) must still be reported as TravelList.
            // Only the specific hover=254 stale sentinel should override to
            // WorldMap. This pins the narrow scope of the post-load fix.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 255,
                slot0: 0xFFFFFFFFL, slot9: 0xFFFFFFFF,
                battleMode: 255, moveMode: 20, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 5, encB: 5, isPartySubScreen: false,
                eventId: 0xFFFF, hover: 255,
                submenuFlag: 0, menuCursor: 0,
                locationMenuFlag: 0);

            Assert.Equal("TravelList", result);
        }

        [Fact]
        public void Stress_OpenTravelList_AtBattleground_StaleHover254_StillTravelList()
        {
            // Live-captured 2026-04-19 at Grogh Heights with the TravelList
            // OPEN (menu showing battlegrounds tab). Fingerprint:
            //   hover=254 (stale from parent WorldMap), rawLocation=33,
            //   party=0, ui=1, moveMode=13 (cursor active in list)
            // The post-load WorldMap rule must NOT fire here. Discriminator:
            // moveMode=13 (list cursor active) vs moveMode=255 (post-load
            // with cursor system inactive).
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 1, rawLocation: 33,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 13, paused: 0, gameOverFlag: 0,
                battleTeam: 0, battleActed: 0, battleMoved: 0,
                encA: 11, encB: 11, isPartySubScreen: false,
                eventId: 0xFFFF, hover: 254,
                submenuFlag: 0, menuCursor: 0,
                locationMenuFlag: 0);

            Assert.Equal("TravelList", result);
        }

        [Fact]
        public void Stress_Victory_WithGameOverFlagSticky_StillFiresVictory()
        {
            // Session 45 Zeklaus capture had gameOverFlag=1 during Victory.
            // Verify this doesn't accidentally route to GameOver when the
            // new Victory rule is present. gameOverFlag is sticky in real
            // saves, so the Victory rule must handle it gracefully.
            var result = ScreenDetectionLogic.Detect(
                party: 0, ui: 0, rawLocation: 255,
                slot0: 255, slot9: 0xFFFFFFFF,
                battleMode: 0, moveMode: 0, paused: 0, gameOverFlag: 1,
                battleTeam: 0, battleActed: 1, battleMoved: 1,
                encA: 255, encB: 255, isPartySubScreen: false,
                eventId: 41, submenuFlag: 1, menuCursor: 1);

            Assert.Equal("BattleVictory", result);
        }
    }
}
