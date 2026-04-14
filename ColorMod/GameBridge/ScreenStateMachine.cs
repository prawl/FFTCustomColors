using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure logic state machine mirroring FFT's deterministic menu system.
    /// Tracks current screen, cursor position, and provides valid actions.
    /// No dependencies on memory, files, or Win32.
    /// </summary>
    public class ScreenStateMachine
    {
        // VK codes
        private const int VK_RETURN = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_DOWN = 0x28;
        private const int VK_SPACE = 0x20;
        private const int VK_1 = 0x31;
        private const int VK_B = 0x42;
        private const int VK_Q = 0x51;
        private const int VK_E = 0x45;
        private const int VK_R = 0x52;
        private const int VK_T = 0x54;
        private const int VK_X = 0x58;
        private const int VK_Y = 0x59;

        public GameScreen CurrentScreen { get; private set; } = GameScreen.Unknown;
        public int CursorRow { get; private set; }
        public int CursorCol { get; private set; }
        public PartyTab Tab { get; private set; } = PartyTab.Units;
        public int SidebarIndex { get; private set; }
        public int GridColumns { get; private set; }
        public int GridRows { get; private set; }
        public bool IsRamza { get; private set; }
        public int RosterCount { get; set; } = 17;
        public int JobActionIndex { get; private set; } // 0=Learn, 1=Change

        /// <summary>
        /// CharacterStatus view toggle: `1` key expands the header to show the
        /// full stat grid (Movement/Jump/PA/MA/PE/ME/etc.). Toggle in-place.
        /// </summary>
        public bool StatsExpanded { get; private set; }

        /// <summary>
        /// EquipmentAndAbilities view toggle: `R` key flips the center panel
        /// between the default list view and the Equipment Effects summary.
        /// Toggle in-place.
        /// </summary>
        public bool EquipmentEffectsView { get; private set; }

        /// <summary>
        /// DismissUnit cursor position: false = Back (default, safe), true = Confirm.
        /// Left/Right toggle. Enter on Confirm permanently dismisses the unit.
        /// </summary>
        public bool DismissConfirmSelected { get; private set; }

        /// <summary>
        /// Which equipment slot was highlighted when Enter opened the picker.
        /// Used to surface the correct Equippable&lt;Type&gt; screen name at
        /// detection time even though the state machine uses a single
        /// EquipmentItemList game-screen internally.
        /// </summary>
        public EquipmentSlot CurrentEquipmentSlot { get; private set; } = EquipmentSlot.Weapon;

        /// <summary>
        /// Which ability slot was highlighted when Enter opened the ability picker.
        /// Rows 0 and 1 both route to ActionAbilities — game treats them as
        /// separate slots (primary + secondary) but the picker is the same.
        /// </summary>
        public AbilitySlot CurrentAbilitySlot { get; private set; } = AbilitySlot.PrimaryAction;

        // Saved party menu cursor for returning from CharacterStatus
        private int _savedPartyRow;
        private int _savedPartyCol;

        // Saved EquipmentAndAbilities cursor for returning from a picker.
        private int _savedEquipmentRow;
        private int _savedEquipmentCol;

        public void SetScreen(GameScreen screen)
        {
            CurrentScreen = screen;
            CursorRow = 0;
            CursorCol = 0;
            SidebarIndex = 0;
            JobActionIndex = 0;

            switch (screen)
            {
                case GameScreen.PartyMenu:
                    Tab = PartyTab.Units;
                    GridColumns = 5;
                    GridRows = (RosterCount + 4) / 5;
                    break;
                case GameScreen.EquipmentScreen:
                    GridColumns = 2;
                    GridRows = 5;
                    break;
                case GameScreen.JobScreen:
                    GridColumns = IsRamza ? 8 : 6;
                    GridRows = 3;
                    break;
            }
        }

        public void SetRosterCount(int count)
        {
            RosterCount = count;
            if (CurrentScreen == GameScreen.PartyMenu)
                GridRows = (RosterCount + 4) / 5;
        }

        public void OnKeyPressed(int vkCode)
        {
            switch (CurrentScreen)
            {
                case GameScreen.WorldMap:
                    HandleWorldMap(vkCode);
                    break;
                case GameScreen.PartyMenu:
                    HandlePartyMenu(vkCode);
                    break;
                case GameScreen.CharacterStatus:
                    HandleCharacterStatus(vkCode);
                    break;
                case GameScreen.EquipmentScreen:
                    HandleEquipmentScreen(vkCode);
                    break;
                case GameScreen.EquipmentItemList:
                    HandleEquipmentItemList(vkCode);
                    break;
                case GameScreen.JobScreen:
                    HandleJobScreen(vkCode);
                    break;
                case GameScreen.JobActionMenu:
                    HandleJobActionMenu(vkCode);
                    break;
                case GameScreen.JobChangeConfirmation:
                    HandleJobChangeConfirmation(vkCode);
                    break;
                case GameScreen.SecondaryAbilities:
                case GameScreen.ReactionAbilities:
                case GameScreen.SupportAbilities:
                case GameScreen.MovementAbilities:
                    HandleAbilityPicker(vkCode);
                    break;
                case GameScreen.CombatSets:
                    HandleCombatSets(vkCode);
                    break;
                case GameScreen.CharacterDialog:
                    HandleCharacterDialog(vkCode);
                    break;
                case GameScreen.DismissUnit:
                    HandleDismissUnit(vkCode);
                    break;
            }
        }

        private void HandleWorldMap(int vk)
        {
            if (vk == VK_ESCAPE)
            {
                CurrentScreen = GameScreen.PartyMenu;
                Tab = PartyTab.Units;
                CursorRow = 0;
                CursorCol = 0;
                GridColumns = 5;
                GridRows = (RosterCount + 4) / 5;
            }
        }

        private void HandlePartyMenu(int vk)
        {
            switch (vk)
            {
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.WorldMap;
                    break;
                case VK_Q:
                    // Q wraps: Units → Options (leftmost → rightmost).
                    Tab = Tab == PartyTab.Units ? PartyTab.Options : (PartyTab)(Tab - 1);
                    break;
                case VK_E:
                    // E wraps: Options → Units (rightmost → leftmost).
                    Tab = Tab == PartyTab.Options ? PartyTab.Units : (PartyTab)(Tab + 1);
                    break;
                case VK_UP:
                    if (CursorRow > 0) CursorRow--;
                    break;
                case VK_DOWN:
                    if (CursorRow < GridRows - 1) CursorRow++;
                    ClampCursorToRoster();
                    break;
                case VK_LEFT:
                    if (CursorCol > 0) CursorCol--;
                    break;
                case VK_RIGHT:
                    if (CursorCol < GridColumns - 1) CursorCol++;
                    ClampCursorToRoster();
                    break;
                case VK_RETURN:
                    if (Tab == PartyTab.Units)
                    {
                        int gridIndex = CursorRow * GridColumns + CursorCol;
                        if (gridIndex < RosterCount)
                        {
                            _savedPartyRow = CursorRow;
                            _savedPartyCol = CursorCol;
                            IsRamza = gridIndex == 0;
                            CurrentScreen = GameScreen.CharacterStatus;
                            SidebarIndex = 0;
                        }
                    }
                    break;
            }
        }

        private void HandleCharacterStatus(int vk)
        {
            switch (vk)
            {
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.PartyMenu;
                    CursorRow = _savedPartyRow;
                    CursorCol = _savedPartyCol;
                    GridColumns = 5;
                    GridRows = (RosterCount + 4) / 5;
                    StatsExpanded = false; // reset view toggle when leaving
                    break;
                case VK_UP:
                    // Sidebar wraps: Equipment (0) → Combat Sets (2).
                    SidebarIndex = SidebarIndex == 0 ? 2 : SidebarIndex - 1;
                    break;
                case VK_DOWN:
                    // Sidebar wraps: Combat Sets (2) → Equipment (0).
                    SidebarIndex = SidebarIndex == 2 ? 0 : SidebarIndex + 1;
                    break;
                case VK_1:
                    // `1` toggles the full stat grid expansion in-place.
                    // Hint at bottom-right flips between [1] More / [1] Less.
                    StatsExpanded = !StatsExpanded;
                    break;
                case VK_SPACE:
                    // Space opens the unit's flavor-text dialog.
                    CurrentScreen = GameScreen.CharacterDialog;
                    break;
                // VK_B (hold 3s) opens DismissUnit. Held-key detection lives outside
                // the state machine — the hold_key action in CommandWatcher will
                // invoke SetScreen(DismissUnit) directly after the timer completes.
                case VK_RETURN:
                    if (SidebarIndex == 0)
                    {
                        CurrentScreen = GameScreen.EquipmentScreen;
                        CursorRow = 0;
                        CursorCol = 0;
                        GridColumns = 2;
                        GridRows = 5;
                    }
                    else if (SidebarIndex == 1)
                    {
                        CurrentScreen = GameScreen.JobScreen;
                        CursorRow = 0;
                        CursorCol = 0;
                        GridColumns = IsRamza ? 8 : 6;
                        GridRows = 3;
                    }
                    else if (SidebarIndex == 2)
                    {
                        CurrentScreen = GameScreen.CombatSets;
                        CursorRow = 0;
                        CursorCol = 0;
                    }
                    break;
            }
        }

        private void HandleEquipmentScreen(int vk)
        {
            switch (vk)
            {
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.CharacterStatus;
                    EquipmentEffectsView = false; // reset view toggle when leaving
                    break;
                case VK_UP:
                    // Column-local vertical wrap: 5 rows per column.
                    CursorRow = CursorRow == 0 ? 4 : CursorRow - 1;
                    break;
                case VK_DOWN:
                    CursorRow = CursorRow == 4 ? 0 : CursorRow + 1;
                    break;
                case VK_LEFT:
                    // Horizontal wrap: col 0 (Equipment) ↔ col 1 (Abilities).
                    CursorCol = CursorCol == 0 ? 1 : 0;
                    break;
                case VK_RIGHT:
                    CursorCol = CursorCol == 1 ? 0 : 1;
                    break;
                case VK_R:
                    // R toggles the Equipment Effects summary view in-place.
                    EquipmentEffectsView = !EquipmentEffectsView;
                    break;
                case VK_RETURN:
                    // Route to slot-specific picker based on cursor position.
                    // Left column (CursorCol == 0) = equipment slots by row:
                    //   0=Weapon, 1=Shield, 2=Headware, 3=CombatGarb, 4=Accessory
                    // Right column (CursorCol == 1) = ability slots by row:
                    //   0=PrimaryAction (LOCKED — no-op), 1=Secondary,
                    //   2=Reaction, 3=Support, 4=Movement
                    if (CursorCol == 1 && CursorRow == 0)
                    {
                        // PrimaryAction is job-locked. Enter does nothing in-game —
                        // the primary skillset is determined by the unit's current
                        // job (change it via JobSelection instead). Intentionally
                        // fall through without transitioning.
                        break;
                    }
                    if (CursorCol == 0)
                    {
                        // All five equipment slots share the generic EquipmentItemList
                        // state machine screen. The slot identity is recoverable at
                        // detection time via CurrentEquipmentSlot (read from CursorRow
                        // at the moment of Enter) — CommandWatcher uses this to
                        // surface the correct Equippable<Type> screen name.
                        CurrentEquipmentSlot = (EquipmentSlot)System.Math.Min(CursorRow, 4);
                        CurrentScreen = GameScreen.EquipmentItemList;
                    }
                    else
                    {
                        // Abilities column (rows 1-4 only; row 0 handled above).
                        CurrentAbilitySlot = (AbilitySlot)System.Math.Min(CursorRow, 4);
                        CurrentScreen = CurrentAbilitySlot switch
                        {
                            AbilitySlot.SecondaryAction => GameScreen.SecondaryAbilities,
                            AbilitySlot.Reaction => GameScreen.ReactionAbilities,
                            AbilitySlot.Support => GameScreen.SupportAbilities,
                            AbilitySlot.Movement => GameScreen.MovementAbilities,
                            _ => GameScreen.SecondaryAbilities
                        };
                    }
                    // Reset picker cursor
                    int savedRow = CursorRow;
                    int savedCol = CursorCol;
                    CursorRow = 0;
                    CursorCol = 0;
                    _savedEquipmentRow = savedRow;
                    _savedEquipmentCol = savedCol;
                    break;
            }
        }

        private void HandleEquipmentItemList(int vk)
        {
            switch (vk)
            {
                case VK_ESCAPE:
                case VK_RETURN:
                    // Both Escape (cancel) and Enter (equip) return to the
                    // two-column EquipmentAndAbilities screen. Restore the
                    // cursor position so the player lands on the same slot
                    // they opened.
                    CurrentScreen = GameScreen.EquipmentScreen;
                    CursorRow = _savedEquipmentRow;
                    CursorCol = _savedEquipmentCol;
                    GridColumns = 2;
                    GridRows = 5;
                    break;
            }
        }

        // Shared handler for Action/Reaction/Support/MovementAbilities pickers.
        // Same transitions as EquipmentItemList — cancel or equip both return
        // to EquipmentAndAbilities with cursor restored.
        private void HandleAbilityPicker(int vk)
        {
            switch (vk)
            {
                case VK_ESCAPE:
                case VK_RETURN:
                    CurrentScreen = GameScreen.EquipmentScreen;
                    CursorRow = _savedEquipmentRow;
                    CursorCol = _savedEquipmentCol;
                    GridColumns = 2;
                    GridRows = 5;
                    break;
            }
        }

        // CombatSets: third sidebar item under CharacterStatus. Inner navigation
        // not yet explored live — treat as a terminal state that only Escape exits.
        // When the feature is used and its screen shape is known, expand the
        // handler with Up/Down/Enter semantics.
        private void HandleCombatSets(int vk)
        {
            if (vk == VK_ESCAPE)
                CurrentScreen = GameScreen.CharacterStatus;
        }

        // CharacterDialog: flavor-text dialog opened by Space on CharacterStatus.
        // Enter advances / closes; Escape does nothing in this game's dialogs.
        private void HandleCharacterDialog(int vk)
        {
            if (vk == VK_RETURN)
                CurrentScreen = GameScreen.CharacterStatus;
        }

        // DismissUnit: confirmation opened by holding B for 3s on CharacterStatus.
        // Cursor defaults to Back (safe). Left/Right toggle. Enter activates.
        private void HandleDismissUnit(int vk)
        {
            switch (vk)
            {
                case VK_LEFT:
                case VK_RIGHT:
                    DismissConfirmSelected = !DismissConfirmSelected;
                    break;
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.CharacterStatus;
                    DismissConfirmSelected = false;
                    break;
                case VK_RETURN:
                    // Regardless of Confirm/Back, both transition back to
                    // CharacterStatus. If Confirm fires, the unit is dismissed
                    // (roster change) — detection will pick that up on the next
                    // scan; the state machine just tracks the menu transition.
                    CurrentScreen = GameScreen.CharacterStatus;
                    DismissConfirmSelected = false;
                    break;
            }
        }

        private void HandleJobScreen(int vk)
        {
            switch (vk)
            {
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.CharacterStatus;
                    break;
                case VK_UP:
                    if (CursorRow > 0) CursorRow--;
                    // Ramza row 0 is wider (8 cols), rows 1-2 are narrower
                    AdjustJobGridColumns();
                    break;
                case VK_DOWN:
                    if (CursorRow < GridRows - 1) CursorRow++;
                    AdjustJobGridColumns();
                    break;
                case VK_LEFT:
                    if (CursorCol > 0) CursorCol--;
                    break;
                case VK_RIGHT:
                    if (CursorCol < GridColumns - 1) CursorCol++;
                    break;
                case VK_RETURN:
                    CurrentScreen = GameScreen.JobActionMenu;
                    JobActionIndex = 0;
                    break;
                case VK_T:
                case VK_Y:
                    // View toggles, no state change
                    break;
            }
        }

        private void HandleJobActionMenu(int vk)
        {
            switch (vk)
            {
                case VK_LEFT:
                    JobActionIndex = 0;
                    break;
                case VK_RIGHT:
                    JobActionIndex = 1;
                    break;
                case VK_RETURN:
                    if (JobActionIndex == 1)
                        CurrentScreen = GameScreen.JobChangeConfirmation;
                    else
                        CurrentScreen = GameScreen.JobScreen;
                    break;
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.JobScreen;
                    break;
            }
        }

        private void HandleJobChangeConfirmation(int vk)
        {
            if (vk == VK_RETURN || vk == VK_ESCAPE)
            {
                CurrentScreen = GameScreen.CharacterStatus;
                SidebarIndex = 0; // Returns to sidebar with Equipment selected
            }
        }

        private void AdjustJobGridColumns()
        {
            if (IsRamza)
            {
                // Row 0 has 8 cols (includes Dark Knight, Golden Knight)
                // Rows 1-2 have 6 and 7 cols respectively in the doc,
                // but standard FFT layout is 6 cols for rows 1+
                GridColumns = CursorRow == 0 ? 8 : 6;
                if (CursorCol >= GridColumns)
                    CursorCol = GridColumns - 1;
            }
        }

        private void ClampCursorToRoster()
        {
            int gridIndex = CursorRow * GridColumns + CursorCol;
            if (gridIndex >= RosterCount)
            {
                // Back up to last valid position
                gridIndex = RosterCount - 1;
                CursorRow = gridIndex / GridColumns;
                CursorCol = gridIndex % GridColumns;
            }
        }

        public ScreenState GetScreenState()
        {
            var state = new ScreenState
            {
                Screen = CurrentScreen.ToString().ToLowerInvariant(),
                Description = GetScreenDescription(),
                ValidActions = GetValidActions()
            };

            switch (CurrentScreen)
            {
                case GameScreen.PartyMenu:
                    state.CursorRow = CursorRow;
                    state.CursorCol = CursorCol;
                    state.Tab = Tab.ToString();
                    state.GridColumns = GridColumns;
                    state.GridRows = GridRows;
                    break;
                case GameScreen.CharacterStatus:
                    state.SidebarIndex = SidebarIndex;
                    break;
                case GameScreen.EquipmentScreen:
                    state.CursorRow = CursorRow;
                    state.CursorCol = CursorCol;
                    state.GridColumns = GridColumns;
                    state.GridRows = GridRows;
                    break;
                case GameScreen.JobScreen:
                    state.CursorRow = CursorRow;
                    state.CursorCol = CursorCol;
                    state.GridColumns = GridColumns;
                    state.GridRows = GridRows;
                    break;
                case GameScreen.JobActionMenu:
                    state.CursorCol = JobActionIndex;
                    break;
            }

            return state;
        }

        public string GetScreenDescription()
        {
            return CurrentScreen switch
            {
                GameScreen.Unknown => "Unknown screen",
                GameScreen.TitleScreen => "Title screen",
                GameScreen.WorldMap => "World map",
                GameScreen.PartyMenu => $"Party Menu - {Tab} tab, cursor at row {CursorRow} col {CursorCol} (index {CursorRow * GridColumns + CursorCol})",
                GameScreen.CharacterStatus => $"Character Status - sidebar position {SidebarIndex} ({GetSidebarLabel()}){(IsRamza ? " [Ramza]" : "")}",
                GameScreen.EquipmentScreen => $"Equipment & Abilities - row {CursorRow} col {CursorCol} ({GetEquipmentSlotLabel()})",
                GameScreen.EquipmentItemList => "Equipment item selection list",
                GameScreen.JobScreen => $"Job Screen - row {CursorRow} col {CursorCol}, {GridColumns}-column grid{(IsRamza ? " [Ramza]" : "")}",
                GameScreen.JobActionMenu => $"Job Action Menu - {(JobActionIndex == 0 ? "Learn Abilities" : "Change Job")} selected",
                GameScreen.JobChangeConfirmation => "Job change confirmation dialog - press Enter or Escape to dismiss",
                _ => "Unknown"
            };
        }

        private string GetSidebarLabel() => SidebarIndex switch
        {
            0 => "Equipment & Abilities",
            1 => "Job",
            2 => "Combat Sets",
            _ => "Unknown"
        };

        private string GetEquipmentSlotLabel()
        {
            string col = CursorCol == 0 ? "Equipment" : "Ability";
            string row = CursorRow switch
            {
                0 => CursorCol == 0 ? "Weapon" : "Primary",
                1 => CursorCol == 0 ? "Shield" : "Secondary",
                2 => CursorCol == 0 ? "Helm" : "Reaction",
                3 => CursorCol == 0 ? "Armor" : "Support",
                4 => CursorCol == 0 ? "Accessory" : "Movement",
                _ => "Unknown"
            };
            return $"{col}: {row}";
        }

        public List<ValidAction> GetValidActions()
        {
            return CurrentScreen switch
            {
                GameScreen.WorldMap => new List<ValidAction>
                {
                    new() { Key = "up", Vk = VK_UP, Description = "Move on world map" },
                    new() { Key = "down", Vk = VK_DOWN, Description = "Move on world map" },
                    new() { Key = "left", Vk = VK_LEFT, Description = "Move on world map" },
                    new() { Key = "right", Vk = VK_RIGHT, Description = "Move on world map" },
                    new() { Key = "enter", Vk = VK_RETURN, Description = "Enter location / travel" },
                    new() { Key = "escape", Vk = VK_ESCAPE, Description = "Open party menu", ResultScreen = "partymenu" },
                },
                GameScreen.PartyMenu => GetPartyMenuActions(),
                GameScreen.CharacterStatus => GetCharacterStatusActions(),
                GameScreen.EquipmentScreen => new List<ValidAction>
                {
                    new() { Key = "up", Vk = VK_UP, Description = "Move cursor up" },
                    new() { Key = "down", Vk = VK_DOWN, Description = "Move cursor down" },
                    new() { Key = "left", Vk = VK_LEFT, Description = "Switch to equipment column" },
                    new() { Key = "right", Vk = VK_RIGHT, Description = "Switch to ability column" },
                    new() { Key = "enter", Vk = VK_RETURN, Description = "Open item selection list", ResultScreen = "equipmentitemlist" },
                    new() { Key = "escape", Vk = VK_ESCAPE, Description = "Back to character status", ResultScreen = "characterstatus" },
                },
                GameScreen.EquipmentItemList => new List<ValidAction>
                {
                    new() { Key = "up", Vk = VK_UP, Description = "Scroll up in list" },
                    new() { Key = "down", Vk = VK_DOWN, Description = "Scroll down in list" },
                    new() { Key = "enter", Vk = VK_RETURN, Description = "Select/unequip item", ResultScreen = "equipmentscreen" },
                    new() { Key = "escape", Vk = VK_ESCAPE, Description = "Cancel selection", ResultScreen = "equipmentscreen" },
                },
                GameScreen.JobScreen => new List<ValidAction>
                {
                    new() { Key = "up", Vk = VK_UP, Description = "Move cursor up in job grid" },
                    new() { Key = "down", Vk = VK_DOWN, Description = "Move cursor down in job grid" },
                    new() { Key = "left", Vk = VK_LEFT, Description = "Move cursor left in job grid" },
                    new() { Key = "right", Vk = VK_RIGHT, Description = "Move cursor right in job grid" },
                    new() { Key = "enter", Vk = VK_RETURN, Description = "Select job (opens action menu)", ResultScreen = "jobactionmenu" },
                    new() { Key = "t", Vk = VK_T, Description = "View job abilities" },
                    new() { Key = "y", Vk = VK_Y, Description = "View job summary" },
                    new() { Key = "escape", Vk = VK_ESCAPE, Description = "Back to character status", ResultScreen = "characterstatus" },
                },
                GameScreen.JobActionMenu => new List<ValidAction>
                {
                    new() { Key = "left", Vk = VK_LEFT, Description = "Select 'Learn Abilities'" },
                    new() { Key = "right", Vk = VK_RIGHT, Description = "Select 'Change Job'" },
                    new() { Key = "enter", Vk = VK_RETURN, Description = $"Confirm: {(JobActionIndex == 0 ? "Learn Abilities" : "Change Job")}", ResultScreen = "jobscreen" },
                    new() { Key = "escape", Vk = VK_ESCAPE, Description = "Cancel", ResultScreen = "jobscreen" },
                },
                GameScreen.JobChangeConfirmation => new List<ValidAction>
                {
                    new() { Key = "enter", Vk = VK_RETURN, Description = "Dismiss confirmation", ResultScreen = "characterstatus" },
                    new() { Key = "escape", Vk = VK_ESCAPE, Description = "Dismiss confirmation", ResultScreen = "characterstatus" },
                },
                _ => new List<ValidAction>()
            };
        }

        private List<ValidAction> GetPartyMenuActions()
        {
            var actions = new List<ValidAction>
            {
                new() { Key = "up", Vk = VK_UP, Description = "Move cursor up" },
                new() { Key = "down", Vk = VK_DOWN, Description = "Move cursor down" },
                new() { Key = "left", Vk = VK_LEFT, Description = "Move cursor left" },
                new() { Key = "right", Vk = VK_RIGHT, Description = "Move cursor right" },
                new() { Key = "q", Vk = VK_Q, Description = "Previous tab" },
                new() { Key = "e", Vk = VK_E, Description = "Next tab" },
                new() { Key = "escape", Vk = VK_ESCAPE, Description = "Return to world map", ResultScreen = "worldmap" },
            };

            if (Tab == PartyTab.Units)
            {
                actions.Add(new ValidAction
                {
                    Key = "enter", Vk = VK_RETURN,
                    Description = "Open character status",
                    ResultScreen = "characterstatus"
                });
                actions.Add(new ValidAction { Key = "r", Vk = VK_R, Description = "Set favorite" });
                actions.Add(new ValidAction { Key = "x", Vk = VK_X, Description = "Change combat sets" });
                actions.Add(new ValidAction { Key = "t", Vk = VK_T, Description = "Sort units" });
                actions.Add(new ValidAction { Key = "1", Vk = VK_1, Description = "Change display (toggle level/HP/etc.)" });
            }

            return actions;
        }

        private List<ValidAction> GetCharacterStatusActions()
        {
            string enterDesc = SidebarIndex switch
            {
                0 => "Open Equipment & Abilities",
                1 => "Open Job screen",
                2 => "Open Combat Sets",
                _ => "Select"
            };
            string? enterResult = SidebarIndex switch
            {
                0 => "equipmentscreen",
                1 => "jobscreen",
                _ => null
            };

            return new List<ValidAction>
            {
                new() { Key = "up", Vk = VK_UP, Description = "Move sidebar up" },
                new() { Key = "down", Vk = VK_DOWN, Description = "Move sidebar down" },
                new() { Key = "enter", Vk = VK_RETURN, Description = enterDesc, ResultScreen = enterResult },
                new() { Key = "escape", Vk = VK_ESCAPE, Description = "Back to party menu", ResultScreen = "partymenu" },
            };
        }
    }
}
