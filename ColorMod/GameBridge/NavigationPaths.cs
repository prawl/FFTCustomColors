using System.Collections.Generic;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Returns valid navigation paths for each detected screen.
    /// Each path is a command fragment that Claude can send verbatim to the bridge.
    /// Keys use VK codes, waitForScreen ensures the response reflects settled state.
    /// </summary>
    public static class NavigationPaths
    {
        // VK codes
        private const int VK_ENTER = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_LEFT = 0x25;
        private const int VK_RIGHT = 0x27;
        private const int VK_SPACE = 0x20;
        private const int VK_TAB = 0x09;
        private const int VK_C = 0x43;
        private const int VK_F = 0x46;
        private const int VK_Q = 0x51;
        private const int VK_E = 0x45;
        private const int VK_T = 0x54;
        private const int VK_A = 0x41;
        private const int VK_D = 0x44;
        private const int VK_R = 0x52;
        private const int VK_1 = 0x31;

        public static Dictionary<string, PathEntry>? GetPaths(DetectedScreen screen)
        {
            if (screen == null) return null;

            return screen.Name switch
            {
                "Cutscene" => GetCutscenePaths(),
                "TitleScreen" => GetTitleScreenPaths(),
                "WorldMap" => GetWorldMapPaths(),
                "PartyMenu" => GetPartyMenuPaths(),
                "PartyMenuInventory" => GetPartyMenuInventoryPaths(),
                "PartyMenuChronicle" => GetPartyMenuChroniclePaths(),
                "PartyMenuOptions" => GetPartyMenuOptionsPaths(),
                "CharacterStatus" => GetCharacterStatusPaths(screen),
                "CharacterDialog" => GetCharacterDialogPaths(),
                "DismissUnit" => GetDismissUnitPaths(),
                "CombatSets" => GetCombatSetsPaths(),
                "EquipmentAndAbilities" => GetEquipmentAndAbilitiesPaths(),
                "EquipmentItemList" => GetEquippableItemPaths("item"),  // fallback for unknown equipment slots
                "EquippableWeapons" => GetEquippableItemPaths("weapon"),
                "EquippableShields" => GetEquippableItemPaths("shield"),
                "EquippableHeadware" => GetEquippableItemPaths("helm"),
                "EquippableCombatGarb" => GetEquippableItemPaths("armor"),
                "EquippableAccessories" => GetEquippableItemPaths("accessory"),
                "ActionAbilities" => GetAbilityPickerPaths("action ability (skillset)"),
                "SecondaryAbilities" => GetAbilityPickerPaths("secondary action skillset (Items / Arts of War / Aim / etc.)"),
                "ReactionAbilities" => GetAbilityPickerPaths("reaction ability"),
                "SupportAbilities" => GetAbilityPickerPaths("support ability"),
                "MovementAbilities" => GetAbilityPickerPaths("movement ability"),
                "JobSelection" => GetJobSelectionPaths(),
                "JobActionMenu" => GetJobActionMenuPaths(),
                "JobChangeConfirmation" => GetJobChangeConfirmationPaths(),
                "TravelList" => GetTravelListPaths(),
                "LocationMenu" => GetLocationMenuPaths(),
                "Outfitter" => GetSettlementMenuPaths(),
                "Tavern" => GetSettlementMenuPaths(),
                "WarriorsGuild" => GetSettlementMenuPaths(),
                "PoachersDen" => GetSettlementMenuPaths(),
                "SaveGame" => GetSettlementMenuPaths(),
                "OutfitterBuy" => GetShopItemListPaths("Buy highlighted item (opens quantity/confirm dialog)"),
                "OutfitterSell" => GetShopItemListPaths("Sell highlighted item (opens quantity/confirm dialog)"),
                "OutfitterFitting" => GetShopItemListPaths("Select highlighted slot/item (advances picker)"),
                "ShopConfirmDialog" => GetShopConfirmDialogPaths(),
                "SaveGame_Menu" => GetSaveGameMenuPaths(),
                "BattleSequence" => GetBattleSequencePaths(),
                "EncounterDialog" => GetEncounterDialogPaths(),
                "GameOver" => GetGameOverPaths(),
                "BattleMyTurn" => GetBattleMyTurnPaths(screen),
                "BattleMoving" => GetBattleMovingPaths(),
                "BattleAttacking" => GetBattleTargetingPaths(),
                "BattleCasting" => GetBattleTargetingPaths(),
                "BattleActing" => GetBattleActingPaths(),
                "BattlePaused" => GetBattlePausedPaths(),
                "BattleStatus" => GetBackOutPaths("You're in the Status screen! Press Escape to get back to battle."),
                "BattleAutoBattle" => GetBackOutPaths("You're in the Auto-Battle menu! Press Escape to get back to battle before the AI takes over."),
                "BattleDialogue" => GetBattleDialoguePaths(),
                "BattleVictory" => null, // auto-advances, no action needed
                "BattleDesertion" => GetDesertionPaths(),
                "BattleFormation" => GetBattleFormationPaths(),
                "BattleAbilities" => GetBattleAbilitiesSubPaths(),
                "BattleAlliesTurn" => GetBattleWaitingPaths(),
                "BattleEnemiesTurn" => GetBattleWaitingPaths(),
                "Battle" => GetBattleWaitingPaths(),
                // Chronicle sub-screens — not yet implemented, Back only
                "ChronicleEncyclopedia" => GetChronicleSubPaths(),
                "ChronicleStateOfRealm" => GetChronicleSubPaths(),
                "ChronicleEvents" => GetChronicleSubPaths(),
                "ChronicleAuracite" => GetChronicleSubPaths(),
                "ChronicleReadingMaterials" => GetChronicleSubPaths(),
                "ChronicleCollection" => GetChronicleSubPaths(),
                "ChronicleErrands" => GetChronicleSubPaths(),
                "ChronicleStratagems" => GetChronicleSubPaths(),
                "ChronicleLessons" => GetChronicleSubPaths(),
                "ChronicleAkademicReport" => GetChronicleSubPaths(),
                // Options sub-screen — not yet implemented, Back only
                "OptionsSettings" => GetOptionsSubPaths(),
                _ when screen.Name.StartsWith("Battle_") => GetBattleAbilityListPaths(),
                _ => null
            };
        }

        private static Dictionary<string, PathEntry> GetCutscenePaths()
        {
            return new()
            {
                ["Advance"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Advance dialogue (press Enter)"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetTitleScreenPaths()
        {
            return new()
            {
                ["Advance"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Press Enter once (advance one screen)"
                },
                ["Continue"] = new PathEntry
                {
                    Keys = new[]
                    {
                        Key(VK_ENTER, "Enter"), Key(VK_ENTER, "Enter"),
                        Key(VK_ENTER, "Enter"), Key(VK_ENTER, "Enter"),
                        Key(VK_ENTER, "Enter"), Key(VK_ENTER, "Enter"),
                    },
                    DelayBetweenMs = 2000,
                    WaitUntilScreenNot = "TitleScreen",
                    WaitTimeoutMs = 20000,
                    Desc = "Continue saved game (press Enter through title, load, and continue screens)"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetWorldMapPaths()
        {
            return new()
            {
                ["PartyMenu"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "PartyMenu",
                    Desc = "Open party menu"
                },
                ["TravelList"] = new PathEntry
                {
                    Keys = new[] { Key(VK_T, "T") },
                    WaitForScreen = "TravelList",
                    Desc = "Open travel list to select a destination"
                },
                ["EnterLocation"] = new PathEntry
                {
                    // Pre-press C to recenter the WorldMap cursor on the
                    // current node before pressing Enter. The game's `C`
                    // key (also middle-mouse) snaps the cursor to wherever
                    // the player is standing — without this, a drifted
                    // cursor causes Enter to silently no-op (the
                    // pre-existing "Enter does nothing" failure mode
                    // logged 2026-04-14). Single deterministic key, no
                    // memory read needed.
                    Keys = new[] { Key(VK_C, "C"), Key(VK_ENTER, "Enter") },
                    DelayBetweenMs = 200,
                    WaitUntilScreenNot = "WorldMap",
                    WaitTimeoutMs = 5000,
                    Desc = "Recenter cursor (C) then enter current location (may trigger encounter, settlement, or story event)"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetPartyMenuPaths()
        {
            // Tab aliases from Units: E (Inventory), EE (Chronicle), EEE (Options).
            // Q reaches Options in one press (E wraps backward).
            return new()
            {
                ["WorldMap"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "WorldMap",
                    Desc = "Return to world map"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(1),
                ["PrevTab"] = new PathEntry { Keys = new[] { Key(VK_Q, "Q") }, Desc = "Switch to previous tab" },
                ["NextTab"] = new PathEntry { Keys = new[] { Key(VK_E, "E") }, Desc = "Switch to next tab" },
                ["OpenUnits"] = new PathEntry { Keys = System.Array.Empty<KeyInfo>(), Desc = "(already on Units tab — no-op)" },
                ["OpenInventory"] = new PathEntry
                {
                    Keys = new[] { Key(VK_E, "E") },
                    WaitForScreen = "PartyMenuInventory",
                    Desc = "Jump to Inventory tab (E once)"
                },
                ["OpenChronicle"] = new PathEntry
                {
                    Keys = new[] { Key(VK_E, "E"), Key(VK_E, "E") },
                    DelayBetweenMs = 500,
                    WaitForScreen = "PartyMenuChronicle",
                    Desc = "Jump to Chronicle tab (E twice)"
                },
                ["OpenOptions"] = new PathEntry
                {
                    Keys = new[] { Key(VK_Q, "Q") },
                    WaitForScreen = "PartyMenuOptions",
                    Desc = "Jump to Options tab (Q wraps backward)"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Move cursor left" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Move cursor right" },
                ["SelectUnit"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Open selected unit's status screen (Units tab only)"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetCharacterStatusPaths(DetectedScreen screen)
        {
            // Sidebar: 0=Equipment & Abilities, 1=Job, 2=Combat Sets
            // We don't know sidebar index from memory, so offer all options
            return new()
            {
                ["SidebarUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move sidebar up (wraps)" },
                ["SidebarDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move sidebar down (wraps)" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Open selected sidebar item (EquipmentAndAbilities / JobSelection / CombatSets)"
                },
                ["Back"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Back to party unit grid"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(2),
                ["PrevUnit"] = new PathEntry { Keys = new[] { Key(VK_Q, "Q") }, Desc = "View previous unit (wraps)" },
                ["NextUnit"] = new PathEntry { Keys = new[] { Key(VK_E, "E") }, Desc = "View next unit (wraps)" },
                ["OpenDialog"] = new PathEntry
                {
                    Keys = new[] { Key(VK_SPACE, "Space") },
                    WaitForScreen = "CharacterDialog",
                    WaitTimeoutMs = 2000,
                    Desc = "Open unit's flavor-text dialog (press Space)"
                },
                ["ToggleStatsPanel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_1, "1") },
                    Desc = "Toggle full stat grid expansion ([1] More / [1] Less hint)"
                },
                // Dismiss Unit is opened by HOLDING B for 3+ seconds.
                // Our existing `keys` infrastructure sends single presses;
                // no hold-key helper exists yet, so we don't emit a
                // ValidPath for DismissUnit here. Once a hold-key action
                // lands, add: ["DismissUnit"] = { action: "hold_key",
                // vk: VK_B, durationMs: 3500, WaitForScreen: "DismissUnit" }.
            };
        }

        private static Dictionary<string, PathEntry> GetJobActionMenuPaths()
        {
            // Modal with Learn Abilities (left, default) / Change Job (right).
            // Cursor defaults to Learn Abilities. Combined sequences below
            // re-assert the cursor position before Enter so they work from
            // either starting cursor.
            return new()
            {
                ["LearnAbilities"] = new PathEntry
                {
                    Keys = new[] { Key(VK_LEFT, "Left"), Key(VK_ENTER, "Enter") },
                    Desc = "Select Learn Abilities (asserts cursor Left then Enter)"
                },
                ["ChangeJob"] = new PathEntry
                {
                    Keys = new[] { Key(VK_RIGHT, "Right"), Key(VK_ENTER, "Enter") },
                    Desc = "Change to this job (asserts cursor Right then Enter)"
                },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Move cursor to Learn Abilities (wraps)" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Move cursor to Change Job (wraps)" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Activate highlighted option at current cursor position"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Cancel, back to job grid"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(4),
            };
        }

        private static Dictionary<string, PathEntry> GetJobChangeConfirmationPaths()
        {
            return new()
            {
                ["Confirm"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Dismiss confirmation"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(5),
            };
        }

        private static Dictionary<string, PathEntry> GetTravelListPaths()
        {
            return new()
            {
                ["PrevTab"] = new PathEntry { Keys = new[] { Key(VK_Q, "Q") }, Desc = "Previous tab (Settlements/Battlegrounds/Misc)" },
                ["NextTab"] = new PathEntry { Keys = new[] { Key(VK_E, "E") }, Desc = "Next tab" },
                ["ScrollUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Scroll up in location list" },
                ["ScrollDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Scroll down in location list" },
                ["SelectLocation"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    WaitForScreen = "WorldMap",
                    WaitTimeoutMs = 2000,
                    Desc = "Select highlighted location and close list (sets world map cursor, does NOT travel)"
                },
                ["Close"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "WorldMap",
                    Desc = "Close travel list"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetLocationMenuPaths()
        {
            // At a named location (Dorter/Warjilis/etc.) with the shop list open.
            // Cursor highlights a shop (Outfitter/Tavern/WarriorsGuild/PoachersDen).
            // Pressing Enter enters the highlighted shop's interior.
            return new()
            {
                ["EnterShop"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    // Screen name after Enter depends on which shop was
                    // highlighted (Outfitter/Tavern/WarriorsGuild/PoachersDen/
                    // SaveGame). Wait until we're no longer on LocationMenu
                    // rather than fixing a single target screen.
                    WaitUntilScreenNot = "LocationMenu",
                    WaitTimeoutMs = 3000,
                    Desc = "Enter the highlighted shop/service"
                },
                ["Leave"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "WorldMap",
                    WaitTimeoutMs = 3000,
                    Desc = "Leave location, return to world map"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up in shop list" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down in shop list" },
            };
        }

        private static Dictionary<string, PathEntry> GetSettlementMenuPaths()
        {
            // Inside a shop with the Buy/Sell/Fitting (or equivalent) menu up.
            // Pressing Enter on the highlighted sub-action advances to
            // OutfitterBuy / OutfitterSell / OutfitterFitting (or the
            // corresponding sub-state for Tavern / Warriors' Guild / Poachers' Den
            // once those are mapped).
            return new()
            {
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    // Screen name after select depends on which shop + which
                    // sub-action (Outfitter → OutfitterBuy/Sell/Fitting;
                    // Tavern → Rumors/Errands; etc). Rely on the general
                    // wait rather than a single named target screen.
                    WaitTimeoutMs = 1500,
                    Desc = "Enter the highlighted sub-action (Buy/Sell/Fitting/etc.)"
                },
                ["Leave"] = new PathEntry
                {
                    // Escape from a shop opens a "Come back anytime" farewell
                    // dialog. A second Enter dismisses it and returns to
                    // LocationMenu. Sending just Escape leaves the player
                    // stuck on the dialog.
                    Keys = new[] { Key(VK_ESCAPE, "Escape"), Key(VK_ENTER, "Enter") },
                    DelayBetweenMs = 300,
                    WaitForScreen = "LocationMenu",
                    WaitTimeoutMs = 3000,
                    Desc = "Leave shop (dismisses farewell dialog automatically)"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up in sub-action menu" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down in sub-action menu" },
            };
        }

        private static Dictionary<string, PathEntry> GetShopItemListPaths(string selectDesc)
        {
            // Inside an Outfitter sub-action (Buy/Sell/Fitting). The highlighted
            // row is an item / slot / character depending on the sub-action.
            // Enter advances into the next step (quantity dialog for Buy/Sell,
            // slot→item picker for Fitting). Escape goes back to SettlementMenu.
            return new()
            {
                ["ScrollUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Scroll up in list" },
                ["ScrollDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Scroll down in list" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = selectDesc
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    // Escape from a sub-action list returns to the parent
                    // shop (Outfitter/Tavern/WarriorsGuild/PoachersDen/SaveGame).
                    // We can't statically know which, so rely on timeout + poll.
                    WaitTimeoutMs = 1500,
                    Desc = "Cancel, back to shop menu"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetSaveGameMenuPaths()
        {
            // Save slot picker inside a SettlementMenu Save Game option.
            // Scaffold for when detection lands — NOT YET LIVE-VERIFIED. The
            // Save screen likely shows a list of slots (slot 1, slot 2, ...);
            // Select saves to the highlighted slot, Cancel backs out.
            // TODO: verify slot-picker flow at Warjilis once shopTypeIndex=4
            // is confirmed as SaveGame.
            return new()
            {
                ["ScrollUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Previous save slot" },
                ["ScrollDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Next save slot" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Save to highlighted slot (may open overwrite confirmation)"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    // Back to the parent shop (SaveGame / Outfitter / etc).
                    WaitTimeoutMs = 1500,
                    Desc = "Cancel, back to settlement menu"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetShopConfirmDialogPaths()
        {
            // "Buy 3 Potions for 60 gil?" yes/no modal that appears after
            // confirming a quantity inside OutfitterBuy/Sell. Default cursor
            // on Yes (Enter confirms; Escape cancels the purchase). A memory
            // scan is needed to actually DETECT this modal — see TODO §10
            // "Confirm dialog detection". Once detected, this ValidPaths
            // entry ensures Claude doesn't press Enter blindly and buy by
            // mistake.
            return new()
            {
                ["Confirm"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Confirm purchase/sale — completes the transaction"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Cancel purchase/sale — returns to item list"
                },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Toggle Yes/No cursor" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Toggle Yes/No cursor" },
            };
        }

        private static Dictionary<string, PathEntry> GetBattleSequencePaths()
        {
            return new()
            {
                ["CommenceBattle"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    WaitUntilScreenNot = "BattleSequence",
                    WaitTimeoutMs = 5000,
                    Desc = "Start the next sub-battle in the sequence"
                },
                ["PartyMenu"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "PartyMenu",
                    Desc = "Open party menu (change formation/equipment between battles)"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetEncounterDialogPaths()
        {
            return new()
            {
                ["Fight"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    WaitUntilScreenNot = "EncounterDialog",
                    WaitTimeoutMs = 5000,
                    Desc = "Accept fight (cursor defaults to Fight)"
                },
                ["Flee"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "WorldMap",
                    WaitTimeoutMs = 3000,
                    Desc = "Flee from encounter"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetGameOverPaths()
        {
            // Game Over menu (cursor defaults to Retry):
            //   Retry(0) → Change Formation and Retry / Retry from Start / Cancel
            //   Load(1) → loading screen
            //   Return to World Map(2) → confirm / Cancel
            //   Return to Title Screen(3) → confirm / Cancel
            return new()
            {
                ["RetryFromStart"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter"), Key(VK_DOWN, "Down"), Key(VK_ENTER, "Enter") },
                    Desc = "Retry battle from the start (Retry → Retry from Start)"
                },
                ["RetryChangeFormation"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter"), Key(VK_ENTER, "Enter") },
                    Desc = "Change formation and retry (Retry → Change Formation)"
                },
                ["Load"] = new PathEntry
                {
                    Keys = new[] { Key(VK_DOWN, "Down"), Key(VK_ENTER, "Enter") },
                    Desc = "Open load screen"
                },
                ["ReturnToWorldMap"] = new PathEntry
                {
                    Keys = new[] { Key(VK_DOWN, "Down"), Key(VK_DOWN, "Down"), Key(VK_ENTER, "Enter"), Key(VK_ENTER, "Enter") },
                    WaitForScreen = "WorldMap",
                    WaitTimeoutMs = 10000,
                    Desc = "Abandon battle and return to world map (with confirmation)"
                },
                ["ReturnToTitle"] = new PathEntry
                {
                    Keys = new[] { Key(VK_DOWN, "Down"), Key(VK_DOWN, "Down"), Key(VK_DOWN, "Down"), Key(VK_ENTER, "Enter"), Key(VK_ENTER, "Enter") },
                    WaitForScreen = "TitleScreen",
                    WaitTimeoutMs = 10000,
                    Desc = "Return to title screen (with confirmation)"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down" },
            };
        }

        private static Dictionary<string, PathEntry> GetBattleMyTurnPaths(DetectedScreen screen)
        {
            var paths = new Dictionary<string, PathEntry>();
            int cursor = screen.MenuCursor;

            // Build key sequence to reach each menu option from current cursor position
            // Menu: 0=Move, 1=Abilities, 2=Wait, 3=Status, 4=AutoBattle
            paths["Move"] = MenuPath(cursor, 0, "Enter Move mode to select a tile manually");
            paths["Abilities"] = MenuPath(cursor, 1, "Open abilities submenu (Attack, secondary)");
            paths["Wait"] = new PathEntry
            {
                Action = "battle_wait",
                Desc = "End turn (handles menu navigation, confirm, and facing)"
            };
            paths["Status"] = MenuPath(cursor, 3, "View unit status");
            paths["AutoBattle"] = MenuPath(cursor, 4, "Toggle auto-battle");
            paths["Pause"] = new PathEntry
            {
                Keys = new[] { Key(VK_TAB, "Tab") },
                Desc = "Open pause menu"
            };
            paths["RotateCamera"] = new PathEntry
            {
                Keys = new[] { Key(VK_Q, "Q") },
                Desc = "Rotate camera (increments rotation counter)"
            };

            return paths;
        }

        private static Dictionary<string, PathEntry> GetBattleMovingPaths()
        {
            return new()
            {
                ["ConfirmMove"] = new PathEntry
                {
                    Keys = new[] { Key(VK_F, "F") },
                    WaitUntilScreenNot = "BattleMoving",
                    WaitTimeoutMs = 5000,
                    Desc = "Confirm move to selected tile (F key)"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Cancel move, return to action menu"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move tile cursor up" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move tile cursor down" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Move tile cursor left" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Move tile cursor right" },
                ["RotateCamera"] = new PathEntry { Keys = new[] { Key(VK_Q, "Q") }, Desc = "Rotate camera (increments rotation counter)" },
            };
        }

        private static Dictionary<string, PathEntry> GetBattleTargetingPaths()
        {
            return new()
            {
                ["ConfirmTarget"] = new PathEntry
                {
                    Action = "confirm_attack",
                    Desc = "Confirm attack on selected target (selects, confirms, waits for resolution)"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Cancel targeting, go back"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move target cursor up" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move target cursor down" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Move target cursor left" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Move target cursor right" },
            };
        }

        private static Dictionary<string, PathEntry> GetBattleActingPaths()
        {
            return new()
            {
                ["Confirm"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Confirm selection"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Cancel / go back"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Move cursor left" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Move cursor right" },
            };
        }

        private static Dictionary<string, PathEntry> GetBattlePausedPaths()
        {
            // Pause menu is vertical: Units(0), Retry(1), Load(2), Settings(3),
            // Return to World Map(4), Return to Title Screen(5)
            // Cursor defaults to Units(0). Tab also closes the menu.
            return new()
            {
                ["Resume"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Close pause menu, resume battle"
                },
                ["Units"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "View all units in current battle"
                },
                ["Retry"] = new PathEntry
                {
                    Keys = new[] { Key(VK_DOWN, "Down"), Key(VK_ENTER, "Enter") },
                    Desc = "Restart this battle from the beginning"
                },
                // Pause menu remembers last cursor position between openings.
                // Press Up x6 first to force cursor to Units(0), then Down x4 to reach
                // ReturnToWorldMap(4). Up at the top is a no-op so x6 is a safe reset.
                ["ReturnToWorldMap"] = new PathEntry
                {
                    Keys = new[]
                    {
                        Key(VK_UP, "Up"), Key(VK_UP, "Up"), Key(VK_UP, "Up"),
                        Key(VK_UP, "Up"), Key(VK_UP, "Up"), Key(VK_UP, "Up"),
                        Key(VK_DOWN, "Down"), Key(VK_DOWN, "Down"),
                        Key(VK_DOWN, "Down"), Key(VK_DOWN, "Down"),
                        Key(VK_ENTER, "Enter"), Key(VK_ENTER, "Enter")
                    },
                    WaitForScreen = "WorldMap",
                    WaitTimeoutMs = 10000,
                    Desc = "Abandon battle and return to world map (with confirmation)"
                },
                ["ReturnToTitle"] = new PathEntry
                {
                    Keys = new[]
                    {
                        Key(VK_UP, "Up"), Key(VK_UP, "Up"), Key(VK_UP, "Up"),
                        Key(VK_UP, "Up"), Key(VK_UP, "Up"), Key(VK_UP, "Up"),
                        Key(VK_DOWN, "Down"), Key(VK_DOWN, "Down"),
                        Key(VK_DOWN, "Down"), Key(VK_DOWN, "Down"), Key(VK_DOWN, "Down"),
                        Key(VK_ENTER, "Enter"), Key(VK_ENTER, "Enter")
                    },
                    WaitForScreen = "TitleScreen",
                    WaitTimeoutMs = 10000,
                    Desc = "Return to title screen (with confirmation)"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up in pause menu" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down in pause menu" },
            };
        }

        private static Dictionary<string, PathEntry> GetBattleWaitingPaths()
        {
            // Not our turn — poll screen state until BattleMyTurn, observe what happens
            return new()
            {
                ["Pause"] = new PathEntry
                {
                    Keys = new[] { Key(VK_TAB, "Tab") },
                    Desc = "Open pause menu"
                },
            };
        }

        /// <summary>
        /// Builds a path to reach a target menu position from the current cursor.
        /// Menu is vertical: Down moves cursor +1, Up moves -1.
        /// </summary>
        private static PathEntry MenuPath(int currentPos, int targetPos, string desc)
        {
            var keys = new List<KeyInfo>();
            int delta = targetPos - currentPos;

            if (delta > 0)
            {
                for (int i = 0; i < delta; i++)
                    keys.Add(Key(VK_DOWN, "Down"));
            }
            else if (delta < 0)
            {
                for (int i = 0; i < -delta; i++)
                    keys.Add(Key(VK_UP, "Up"));
            }

            keys.Add(Key(VK_ENTER, "Enter"));
            return new PathEntry { Keys = keys.ToArray(), Desc = desc };
        }

        private static Dictionary<string, PathEntry> GetBackOutPaths(string message)
        {
            return new()
            {
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = message
                }
            };
        }

        private static Dictionary<string, PathEntry> GetBattleDialoguePaths()
        {
            return new()
            {
                ["Advance"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Advance dialogue (press Enter to continue)"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetBattleFormationPaths()
        {
            return new()
            {
                ["PlaceUnit"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Place/swap unit on the selected blue tile"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move formation cursor up" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move formation cursor down" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Move formation cursor left" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Move formation cursor right" },
                ["Commence"] = new PathEntry
                {
                    Keys = new[] { Key(VK_SPACE, "Space"), Key(VK_ENTER, "Enter") },
                    DelayBetweenMs = 500,
                    WaitUntilScreenNot = "BattleFormation",
                    WaitTimeoutMs = 10000,
                    Desc = "Open Commence dialog (Space) then confirm (Enter) to start battle"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetBattleAbilitiesSubPaths()
        {
            // Abilities submenu: Attack / primary skillset / secondary skillset
            return new()
            {
                ["ScrollUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up in abilities submenu" },
                ["ScrollDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down in abilities submenu" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Select highlighted skillset (Attack, primary, or secondary)"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Cancel, back to action menu"
                },
            };
        }

        /// <summary>
        /// Fallback for any Battle<Skillset> screen (BattleMettle, BattleItems, etc.)
        /// where Claude is browsing an ability list.
        /// </summary>
        private static Dictionary<string, PathEntry> GetBattleAbilityListPaths()
        {
            return new()
            {
                ["ScrollUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Scroll up in ability list" },
                ["ScrollDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Scroll down in ability list" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Use highlighted ability (enters targeting mode)"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Cancel, back to abilities submenu"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetDesertionPaths()
        {
            return new()
            {
                ["Dismiss"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Dismiss the desertion warning and continue"
                }
            };
        }

        // ============================================================
        // PartyMenu non-Units tabs (captured 2026-04-14, TODO §10.6).
        // Detection from memory not yet implemented — these fire once
        // the tab-index discriminator is scanned. Until then they're
        // only reachable by name from tests / manual dispatch.
        // ============================================================

        private static Dictionary<string, PathEntry> GetPartyMenuInventoryPaths()
        {
            // Inventory tab: full item catalog across categories. Columns
            // Item Name | Equipped/Held, right pane shows selected item's
            // description. Multi-page ("1/3" indicator).
            //
            // From Inventory: Q→Units, E→Chronicle, QQ/EE→Options.
            return new()
            {
                ["PrevTab"] = new PathEntry { Keys = new[] { Key(VK_Q, "Q") }, Desc = "Switch to previous PartyMenu tab (wraps: Units)" },
                ["NextTab"] = new PathEntry { Keys = new[] { Key(VK_E, "E") }, Desc = "Switch to next PartyMenu tab (wraps: Chronicle)" },
                ["OpenUnits"] = new PathEntry
                {
                    Keys = new[] { Key(VK_Q, "Q") },
                    WaitForScreen = "PartyMenu",
                    Desc = "Jump to Units tab"
                },
                ["OpenInventory"] = new PathEntry { Keys = System.Array.Empty<KeyInfo>(), Desc = "(already on Inventory tab — no-op)" },
                ["OpenChronicle"] = new PathEntry
                {
                    Keys = new[] { Key(VK_E, "E") },
                    WaitForScreen = "PartyMenuChronicle",
                    Desc = "Jump to Chronicle tab"
                },
                ["OpenOptions"] = new PathEntry
                {
                    Keys = new[] { Key(VK_E, "E"), Key(VK_E, "E") },
                    DelayBetweenMs = 500,
                    WaitForScreen = "PartyMenuOptions",
                    Desc = "Jump to Options tab"
                },
                ["ScrollUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Scroll up in item list (wraps)" },
                ["ScrollDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Scroll down in item list (wraps)" },
                ["ChangePage"] = new PathEntry
                {
                    Keys = new[] { Key(VK_TAB, "Tab") },
                    Desc = "Cycle item-category sub-tabs (Weapons/Shields/Helms/Armor/Accessories/Consumables)"
                },
                ["WorldMap"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "WorldMap",
                    Desc = "Close PartyMenu, return to world map"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(1),
            };
        }

        private static Dictionary<string, PathEntry> GetPartyMenuChroniclePaths()
        {
            // Chronicle tab: 7-tile grid of lore sub-screens plus a
            // Special Lectures row. Pressing Enter on a tile opens its
            // dedicated sub-screen (not modeled yet).
            //
            // From Chronicle: QQ→Units, Q→Inventory, E→Options.
            return new()
            {
                ["PrevTab"] = new PathEntry { Keys = new[] { Key(VK_Q, "Q") }, Desc = "Switch to previous PartyMenu tab (wraps: Inventory)" },
                ["NextTab"] = new PathEntry { Keys = new[] { Key(VK_E, "E") }, Desc = "Switch to next PartyMenu tab (wraps: Options)" },
                ["OpenUnits"] = new PathEntry
                {
                    Keys = new[] { Key(VK_Q, "Q"), Key(VK_Q, "Q") },
                    DelayBetweenMs = 500,
                    WaitForScreen = "PartyMenu",
                    Desc = "Jump to Units tab"
                },
                ["OpenInventory"] = new PathEntry
                {
                    Keys = new[] { Key(VK_Q, "Q") },
                    WaitForScreen = "PartyMenuInventory",
                    Desc = "Jump to Inventory tab"
                },
                ["OpenChronicle"] = new PathEntry { Keys = System.Array.Empty<KeyInfo>(), Desc = "(already on Chronicle tab — no-op)" },
                ["OpenOptions"] = new PathEntry
                {
                    Keys = new[] { Key(VK_E, "E") },
                    WaitForScreen = "PartyMenuOptions",
                    Desc = "Jump to Options tab"
                },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move tile cursor up (wraps)" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move tile cursor down (wraps)" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Move tile cursor left (wraps)" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Move tile cursor right (wraps)" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Open highlighted chronicle tile (Encyclopedia/Events/Auracite/etc. — sub-screens not yet modeled)"
                },
                ["WorldMap"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "WorldMap",
                    Desc = "Close PartyMenu, return to world map"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(1),
            };
        }

        private static Dictionary<string, PathEntry> GetPartyMenuOptionsPaths()
        {
            // Options tab: vertical list — Save / Load / Settings /
            // Return to Title Screen / Exit Game.
            //
            // From Options: E→Units (wraps), Q→Chronicle, QQ→Inventory.
            return new()
            {
                ["PrevTab"] = new PathEntry { Keys = new[] { Key(VK_Q, "Q") }, Desc = "Switch to previous PartyMenu tab (wraps: Chronicle)" },
                ["NextTab"] = new PathEntry { Keys = new[] { Key(VK_E, "E") }, Desc = "Switch to next PartyMenu tab (wraps: Units)" },
                ["OpenUnits"] = new PathEntry
                {
                    Keys = new[] { Key(VK_E, "E") },
                    WaitForScreen = "PartyMenu",
                    Desc = "Jump to Units tab (E wraps forward)"
                },
                ["OpenInventory"] = new PathEntry
                {
                    Keys = new[] { Key(VK_Q, "Q"), Key(VK_Q, "Q") },
                    DelayBetweenMs = 500,
                    WaitForScreen = "PartyMenuInventory",
                    Desc = "Jump to Inventory tab"
                },
                ["OpenChronicle"] = new PathEntry
                {
                    Keys = new[] { Key(VK_Q, "Q") },
                    WaitForScreen = "PartyMenuChronicle",
                    Desc = "Jump to Chronicle tab"
                },
                ["OpenOptions"] = new PathEntry { Keys = System.Array.Empty<KeyInfo>(), Desc = "(already on Options tab — no-op)" },
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up (wraps)" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down (wraps)" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Open highlighted option (Save/Load/Settings/Return to Title/Exit — sub-flows not yet modeled)"
                },
                ["WorldMap"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "WorldMap",
                    Desc = "Close PartyMenu, return to world map"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(1),
            };
        }

        // ============================================================
        // CharacterStatus side-flows (spacebar dialog + hold-B dismiss).
        // ============================================================

        private static Dictionary<string, PathEntry> GetCharacterDialogPaths()
        {
            // Flavor-text dialog opened by pressing Space on a unit's
            // CharacterStatus screen (e.g. Kenrick's "My father is an
            // arms merchant..." intro). Advances with Enter like
            // cutscenes. Escape does NOT close dialogs in this game.
            return new()
            {
                ["Advance"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Advance flavor dialog (press Enter). Escape does nothing on dialogs."
                },
                // Escape doesn't close flavor dialogs (Enter advances them).
                // Walk Enter once to dismiss the dialog (back to CharacterStatus),
                // then 2 Escapes to climb out CS → PartyMenu → WorldMap.
                ["ReturnToWorldMap"] = new PathEntry
                {
                    Keys = new[]
                    {
                        Key(VK_ENTER, "Enter"),
                        Key(VK_ESCAPE, "Escape"),
                        Key(VK_ESCAPE, "Escape"),
                    },
                    DelayBetweenMs = 200,
                    WaitForScreen = "WorldMap",
                    WaitTimeoutMs = 3000,
                    Desc = "Dismiss dialog (Enter) then Escape twice to world map"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetDismissUnitPaths()
        {
            // Confirmation screen opened by holding B for 3+ seconds on a
            // unit's CharacterStatus screen. Cursor DEFAULTS to
            // Back/Cancel — so a blind Enter is safe (it does NOT dismiss
            // the unit). Left/Right toggle between Confirm and Back.
            return new()
            {
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Toggle cursor between Back and Confirm (wraps)" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Toggle cursor between Back and Confirm (wraps)" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Activate highlighted option (Back=safe / Confirm=permanently dismisses unit). Cursor defaults to Back."
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "CharacterStatus",
                    Desc = "Cancel, back to character status"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(3),
            };
        }

        // ============================================================
        // CombatSets: third sidebar item under CharacterStatus. Not yet
        // explored live — best-guess ValidPaths. Revisit when detection
        // lands or a screenshot arrives.
        // ============================================================

        private static Dictionary<string, PathEntry> GetCombatSetsPaths()
        {
            return new()
            {
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up in loadout list (wraps)" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down in loadout list (wraps)" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Activate highlighted loadout (exact behavior not yet verified live)"
                },
                ["Back"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "CharacterStatus",
                    Desc = "Back to character status sidebar"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(3),
            };
        }

        // ============================================================
        // EquipmentAndAbilities: the two-column inner screen (Equipment
        // on the left, Abilities on the right) reached by pressing Enter
        // on CharacterStatus sidebar's "Equipment & Abilities". Pressing
        // R toggles between the default list view and an aggregated
        // Equipment Effects summary.
        // ============================================================

        private static Dictionary<string, PathEntry> GetEquipmentAndAbilitiesPaths()
        {
            return new()
            {
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up within column (wraps)" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down within column (wraps)" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Focus Equipment column (Weapon/Shield/Helm/Armor/Accessory)" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Focus Abilities column (Secondary/Reaction/Support/Movement)" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Open slot picker (Equippable<Type> or <Type>Abilities). Row 0 of Abilities column is job-locked — no-op."
                },
                ["ToggleEffectsView"] = new PathEntry
                {
                    Keys = new[] { Key(VK_R, "R") },
                    Desc = "Toggle Equipment Effects summary view (aggregate stat effects) ↔ default list view"
                },
                ["PrevUnit"] = new PathEntry
                {
                    Keys = new[] { Key(VK_Q, "Q") },
                    Desc = "Switch to previous unit in party, keeping this screen open (wraps)"
                },
                ["NextUnit"] = new PathEntry
                {
                    Keys = new[] { Key(VK_E, "E") },
                    Desc = "Switch to next unit in party, keeping this screen open (wraps)"
                },
                ["Back"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "CharacterStatus",
                    Desc = "Back to character status sidebar"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(3),
            };
        }

        // ============================================================
        // Equippable<Type>: type-filtered item pickers reached from
        // EquipmentAndAbilities. List of items the player owns that fit
        // the slot; Select equips (or unequips if already equipped).
        // Tab cycles category pages where applicable.
        // ============================================================

        private static Dictionary<string, PathEntry> GetEquippableItemPaths(string itemKind)
        {
            return new()
            {
                ["ScrollUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = $"Scroll up in {itemKind} list (wraps)" },
                ["ScrollDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = $"Scroll down in {itemKind} list (wraps)" },
                ["PrevPage"] = new PathEntry
                {
                    Keys = new[] { Key(VK_A, "A") },
                    Desc = $"Previous {itemKind} sub-category tab (wraps)"
                },
                ["NextPage"] = new PathEntry
                {
                    Keys = new[] { Key(VK_D, "D") },
                    Desc = $"Next {itemKind} sub-category tab (wraps)"
                },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = $"Equip highlighted {itemKind} (or unequip if it is already equipped)"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "EquipmentAndAbilities",
                    Desc = "Cancel without changing equipment"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(4),
            };
        }

        // ============================================================
        // <Slot>Abilities: pickers reached from the right column of
        // EquipmentAndAbilities. Same shape across all four slots.
        // ============================================================

        private static Dictionary<string, PathEntry> GetAbilityPickerPaths(string abilityKind)
        {
            return new()
            {
                ["ScrollUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = $"Scroll up in {abilityKind} list (wraps)" },
                ["ScrollDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = $"Scroll down in {abilityKind} list (wraps)" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = $"Assign highlighted {abilityKind} to this slot"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "EquipmentAndAbilities",
                    Desc = "Cancel without changing this ability slot"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(4),
            };
        }

        // ============================================================
        // JobSelection: grid of job tiles reached by pressing Enter on
        // CharacterStatus sidebar's "Job". Ramza's row 0 has 8 columns
        // (1 special + 7 generic); other units have 7. Pressing Enter
        // on a tile opens JobActionMenu.
        // ============================================================

        private static Dictionary<string, PathEntry> GetJobSelectionPaths()
        {
            return new()
            {
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move job cursor up (wraps)" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move job cursor down (wraps)" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Move job cursor left (wraps)" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Move job cursor right (wraps)" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    WaitForScreen = "JobActionMenu",
                    WaitTimeoutMs = 2000,
                    Desc = "Open highlighted job's Learn Abilities / Change Job modal"
                },
                ["PrevUnit"] = new PathEntry
                {
                    Keys = new[] { Key(VK_Q, "Q") },
                    Desc = "Switch to previous unit's job grid (wraps)"
                },
                ["NextUnit"] = new PathEntry
                {
                    Keys = new[] { Key(VK_E, "E") },
                    Desc = "Switch to next unit's job grid (wraps)"
                },
                ["Back"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "CharacterStatus",
                    Desc = "Back to character status sidebar"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(3),
            };
        }

        // ============================================================
        // Chronicle sub-screens: not yet implemented. Provide Back to
        // return to the Chronicle grid and ReturnToWorldMap.
        // ============================================================

        private static Dictionary<string, PathEntry> GetChronicleSubPaths()
        {
            return new()
            {
                ["Back"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "PartyMenuChronicle",
                    Desc = "Back to Chronicle grid (sub-screen not yet implemented)"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(2),
            };
        }

        // ============================================================
        // Options sub-screen: not yet implemented. Provide Back to
        // return to Options tab and ReturnToWorldMap.
        // ============================================================

        private static Dictionary<string, PathEntry> GetOptionsSubPaths()
        {
            return new()
            {
                ["Back"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "PartyMenuOptions",
                    Desc = "Back to Options tab (sub-screen not yet implemented)"
                },
                ["ReturnToWorldMap"] = ReturnToWorldMap(2),
            };
        }

        private static KeyInfo Key(int vk, string name) => new() { Vk = vk, Name = name };

        /// <summary>
        /// Build a <c>ReturnToWorldMap</c> path entry: N consecutive Escape
        /// presses with a 200ms gap so the game's per-screen close
        /// animations don't eat any of them, ending with
        /// <c>WaitForScreen = "WorldMap"</c>. Used by every PartyMenu-tree
        /// screen as a one-shot "get me home" recovery primitive — see
        /// TODO §10.6 ValidPaths block. Tweak the delay if a specific
        /// nested screen ever regresses with eaten keys.
        /// </summary>
        private static PathEntry ReturnToWorldMap(int escapeCount)
        {
            var keys = new KeyInfo[escapeCount];
            for (int i = 0; i < escapeCount; i++) keys[i] = Key(VK_ESCAPE, "Escape");
            return new PathEntry
            {
                Keys = keys,
                DelayBetweenMs = 200,
                WaitForScreen = "WorldMap",
                WaitTimeoutMs = 3000,
                Desc = $"Escape back to world map ({escapeCount} key{(escapeCount == 1 ? "" : "s")})"
            };
        }
    }

    /// <summary>
    /// A navigation path entry — contains the exact keys to press and optional wait condition.
    /// Claude can take this, add an "id" field, and send it as a command.
    /// </summary>
    public class PathEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("keys")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public KeyInfo[]? Keys { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("action")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Action { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("locationId")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int LocationId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("delayBetweenMs")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int DelayBetweenMs { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("waitForScreen")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? WaitForScreen { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("waitUntilScreenNot")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? WaitUntilScreenNot { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("waitTimeoutMs")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int WaitTimeoutMs { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("tiles")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public System.Collections.Generic.List<FFTColorCustomizer.Utilities.TilePosition>? Tiles { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("attackTiles")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public System.Collections.Generic.List<AttackTileInfo>? AttackTiles { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("facing")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public FacingInfo? Facing { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("desc")]
        public string Desc { get; set; } = "";
    }

    public class AttackTileInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("x")]
        public int X { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("y")]
        public int Y { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("arrow")]
        public string Arrow { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("occupant")]
        public string Occupant { get; set; } = "empty";

        [System.Text.Json.Serialization.JsonPropertyName("hp")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int Hp { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("maxHp")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxHp { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("jobName")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? JobName { get; set; }
    }

    public class FacingInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("dx")]
        public int Dx { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("dy")]
        public int Dy { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("direction")]
        public string Direction { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("front")]
        public int Front { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("side")]
        public int Side { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("back")]
        public int Back { get; set; }
    }

    public class KeyInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("vk")]
        public int Vk { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
