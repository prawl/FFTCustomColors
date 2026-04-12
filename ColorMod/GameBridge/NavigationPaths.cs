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
        private const int VK_F = 0x46;
        private const int VK_Q = 0x51;
        private const int VK_E = 0x45;
        private const int VK_T = 0x54;

        public static Dictionary<string, PathEntry>? GetPaths(DetectedScreen screen)
        {
            if (screen == null) return null;

            return screen.Name switch
            {
                "Cutscene" => GetCutscenePaths(),
                "TitleScreen" => GetTitleScreenPaths(),
                "WorldMap" => GetWorldMapPaths(),
                "PartyMenu" => GetPartyMenuPaths(),
                "CharacterStatus" => GetCharacterStatusPaths(screen),
                "EquipmentScreen" => GetEquipmentScreenPaths(),
                "EquipmentItemList" => GetEquipmentItemListPaths(),
                "JobScreen" => GetJobScreenPaths(),
                "JobActionMenu" => GetJobActionMenuPaths(),
                "JobChangeConfirmation" => GetJobChangeConfirmationPaths(),
                "TravelList" => GetTravelListPaths(),
                "EncounterDialog" => GetEncounterDialogPaths(),
                "GameOver" => GetGameOverPaths(),
                "Battle_MyTurn" => GetBattleMyTurnPaths(screen),
                "Battle_Moving" => GetBattleMovingPaths(),
                "Battle_Attacking" => GetBattleTargetingPaths(),
                "Battle_Casting" => GetBattleTargetingPaths(),
                "Battle_Acting" => GetBattleActingPaths(),
                "Battle_Paused" => GetBattlePausedPaths(),
                "Battle_Status" => GetBackOutPaths("You're in the Status screen! Press Escape to get back to battle."),
                "Battle_AutoBattle" => GetBackOutPaths("You're in the Auto-Battle menu! Press Escape to get back to battle before the AI takes over."),
                "Battle_Dialogue" => GetBattleDialoguePaths(),
                "Battle_Victory" => null, // auto-advances, no action needed
                "Battle_Desertion" => GetDesertionPaths(),
                "Battle_Formation" => GetBattleFormationPaths(),
                "Battle_Abilities" => GetBattleAbilitiesSubPaths(),
                "Battle_AlliesTurn" => GetBattleWaitingPaths(),
                "Battle_EnemiesTurn" => GetBattleWaitingPaths(),
                "Battle" => GetBattleWaitingPaths(),
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
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    WaitUntilScreenNot = "WorldMap",
                    WaitTimeoutMs = 5000,
                    Desc = "Enter current location (may trigger encounter, settlement, or story event — wait for result)"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetPartyMenuPaths()
        {
            return new()
            {
                ["WorldMap"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    WaitForScreen = "WorldMap",
                    Desc = "Return to world map"
                },
                ["PrevTab"] = new PathEntry { Keys = new[] { Key(VK_Q, "Q") }, Desc = "Switch to previous tab" },
                ["NextTab"] = new PathEntry { Keys = new[] { Key(VK_E, "E") }, Desc = "Switch to next tab" },
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
                ["SidebarUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move sidebar up" },
                ["SidebarDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move sidebar down" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Open selected sidebar item (Equipment & Abilities / Job / Combat Sets)"
                },
                ["Back"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Back to party unit grid"
                },
                ["PrevUnit"] = new PathEntry { Keys = new[] { Key(VK_Q, "Q") }, Desc = "View previous unit" },
                ["NextUnit"] = new PathEntry { Keys = new[] { Key(VK_E, "E") }, Desc = "View next unit" },
            };
        }

        private static Dictionary<string, PathEntry> GetEquipmentScreenPaths()
        {
            return new()
            {
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up (Weapon/Shield/Helm/Armor/Accessory or Primary/Secondary/Reaction/Support/Movement)" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Switch to equipment column" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Switch to ability column" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Open item/ability selection list for this slot"
                },
                ["Back"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Back to character status sidebar"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetEquipmentItemListPaths()
        {
            return new()
            {
                ["ScrollUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Scroll up in list" },
                ["ScrollDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Scroll down in list" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Equip selected item (or unequip if already equipped)"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Close list without changing"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetJobScreenPaths()
        {
            return new()
            {
                ["CursorUp"] = new PathEntry { Keys = new[] { Key(VK_UP, "Up") }, Desc = "Move cursor up in job grid" },
                ["CursorDown"] = new PathEntry { Keys = new[] { Key(VK_DOWN, "Down") }, Desc = "Move cursor down" },
                ["CursorLeft"] = new PathEntry { Keys = new[] { Key(VK_LEFT, "Left") }, Desc = "Move cursor left" },
                ["CursorRight"] = new PathEntry { Keys = new[] { Key(VK_RIGHT, "Right") }, Desc = "Move cursor right" },
                ["Select"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ENTER, "Enter") },
                    Desc = "Select job (opens Learn Abilities / Change Job menu)"
                },
                ["Back"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Back to character status sidebar"
                },
            };
        }

        private static Dictionary<string, PathEntry> GetJobActionMenuPaths()
        {
            return new()
            {
                ["LearnAbilities"] = new PathEntry
                {
                    Keys = new[] { Key(VK_LEFT, "Left"), Key(VK_ENTER, "Enter") },
                    Desc = "Select Learn Abilities"
                },
                ["ChangeJob"] = new PathEntry
                {
                    Keys = new[] { Key(VK_RIGHT, "Right"), Key(VK_ENTER, "Enter") },
                    Desc = "Change to this job"
                },
                ["Cancel"] = new PathEntry
                {
                    Keys = new[] { Key(VK_ESCAPE, "Escape") },
                    Desc = "Cancel, back to job grid"
                },
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
                    WaitUntilScreenNot = "Battle_Moving",
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
            // Not our turn — poll screen state until Battle_MyTurn, observe what happens
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
                    WaitUntilScreenNot = "Battle_Formation",
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
        /// Fallback for any Battle_<Skillset> screen (Battle_Mettle, Battle_Items, etc.)
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

        private static KeyInfo Key(int vk, string name) => new() { Vk = vk, Name = name };
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
