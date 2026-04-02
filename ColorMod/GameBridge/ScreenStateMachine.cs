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
        private const int VK_1 = 0x31;
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

        // Saved party menu cursor for returning from CharacterStatus
        private int _savedPartyRow;
        private int _savedPartyCol;

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
                    if (Tab > PartyTab.Units)
                        Tab--;
                    break;
                case VK_E:
                    if (Tab < PartyTab.Options)
                        Tab++;
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
                    break;
                case VK_UP:
                    if (SidebarIndex > 0) SidebarIndex--;
                    break;
                case VK_DOWN:
                    if (SidebarIndex < 2) SidebarIndex++;
                    break;
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
                    break;
            }
        }

        private void HandleEquipmentScreen(int vk)
        {
            switch (vk)
            {
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.CharacterStatus;
                    break;
                case VK_UP:
                    if (CursorRow > 0) CursorRow--;
                    break;
                case VK_DOWN:
                    if (CursorRow < GridRows - 1) CursorRow++;
                    break;
                case VK_LEFT:
                    if (CursorCol > 0) CursorCol--;
                    break;
                case VK_RIGHT:
                    if (CursorCol < GridColumns - 1) CursorCol++;
                    break;
                case VK_RETURN:
                    CurrentScreen = GameScreen.EquipmentItemList;
                    CursorRow = 0;
                    CursorCol = 0;
                    break;
            }
        }

        private void HandleEquipmentItemList(int vk)
        {
            switch (vk)
            {
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.EquipmentScreen;
                    break;
                case VK_RETURN:
                    CurrentScreen = GameScreen.EquipmentScreen;
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
