using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class ScreenStateMachineTests
{
    private const int VK_RETURN = 0x0D;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_LEFT = 0x25;
    private const int VK_UP = 0x26;
    private const int VK_RIGHT = 0x27;
    private const int VK_DOWN = 0x28;
    private const int VK_Q = 0x51;
    private const int VK_E = 0x45;
    private const int VK_T = 0x54;
    private const int VK_Y = 0x59;

    private ScreenStateMachine CreateAtScreen(GameScreen screen, bool isRamza = false)
    {
        var sm = new ScreenStateMachine();
        sm.SetRosterCount(17);
        if (isRamza)
        {
            // Navigate to Ramza: WorldMap → PartyMenu → Enter on index 0
            sm.SetScreen(GameScreen.WorldMap);
            sm.OnKeyPressed(VK_ESCAPE); // → PartyMenu
            sm.OnKeyPressed(VK_RETURN); // → CharacterStatus (Ramza at 0,0)
            Assert.True(sm.IsRamza);
            if (screen == GameScreen.CharacterStatus) return sm;
            if (screen == GameScreen.JobScreen)
            {
                sm.OnKeyPressed(VK_DOWN); // sidebar 1 = Job
                sm.OnKeyPressed(VK_RETURN);
                return sm;
            }
        }
        sm.SetScreen(screen);
        return sm;
    }

    // --- Screen Transitions ---

    [Fact]
    public void WorldMap_Escape_TransitionsToPartyMenu()
    {
        var sm = CreateAtScreen(GameScreen.WorldMap);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.PartyMenu, sm.CurrentScreen);
        Assert.Equal(PartyTab.Units, sm.Tab);
        Assert.Equal(0, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);
    }

    [Fact]
    public void PartyMenu_Escape_TransitionsToWorldMap()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.WorldMap, sm.CurrentScreen);
    }

    [Fact]
    public void PartyMenu_Enter_TransitionsToCharacterStatus()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
        Assert.Equal(0, sm.SidebarIndex);
    }

    [Fact]
    public void CharacterStatus_Escape_RestoresPartyMenuCursor()
    {
        var sm = CreateAtScreen(GameScreen.WorldMap);
        sm.OnKeyPressed(VK_ESCAPE); // → PartyMenu at (0,0)
        sm.OnKeyPressed(VK_DOWN);   // row 1
        sm.OnKeyPressed(VK_RIGHT);  // col 1
        sm.OnKeyPressed(VK_RIGHT);  // col 2
        sm.OnKeyPressed(VK_RETURN); // → CharacterStatus
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);

        sm.OnKeyPressed(VK_ESCAPE); // → PartyMenu
        Assert.Equal(GameScreen.PartyMenu, sm.CurrentScreen);
        Assert.Equal(1, sm.CursorRow);
        Assert.Equal(2, sm.CursorCol);
    }

    [Fact]
    public void CharacterStatus_EnterOnEquipment_TransitionsToEquipmentScreen()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        // SidebarIndex defaults to 0 = Equipment
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.EquipmentScreen, sm.CurrentScreen);
        Assert.Equal(2, sm.GridColumns);
        Assert.Equal(5, sm.GridRows);
    }

    [Fact]
    public void CharacterStatus_EnterOnJob_TransitionsToJobScreen()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        sm.OnKeyPressed(VK_DOWN); // sidebar 1 = Job
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.JobScreen, sm.CurrentScreen);
        Assert.Equal(6, sm.GridColumns); // generic character
    }

    [Fact]
    public void EquipmentScreen_Enter_OpensItemList()
    {
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.EquipmentItemList, sm.CurrentScreen);
    }

    [Fact]
    public void EquipmentItemList_Escape_ReturnsToEquipmentScreen()
    {
        var sm = CreateAtScreen(GameScreen.EquipmentItemList);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.EquipmentScreen, sm.CurrentScreen);
    }

    [Fact]
    public void EquipmentItemList_Enter_ReturnsToEquipmentScreen()
    {
        var sm = CreateAtScreen(GameScreen.EquipmentItemList);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.EquipmentScreen, sm.CurrentScreen);
    }

    [Fact]
    public void JobScreen_Enter_OpensActionMenu()
    {
        var sm = CreateAtScreen(GameScreen.JobScreen);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.JobActionMenu, sm.CurrentScreen);
        Assert.Equal(0, sm.JobActionIndex); // defaults to Learn Abilities
    }

    [Fact]
    public void JobActionMenu_RightThenEnter_ChangesJobAndReturns()
    {
        var sm = CreateAtScreen(GameScreen.JobScreen);
        sm.OnKeyPressed(VK_RETURN); // → JobActionMenu
        sm.OnKeyPressed(VK_RIGHT);  // Change Job
        Assert.Equal(1, sm.JobActionIndex);
        sm.OnKeyPressed(VK_RETURN); // confirm → JobScreen
        Assert.Equal(GameScreen.JobScreen, sm.CurrentScreen);
    }

    [Fact]
    public void JobActionMenu_Escape_ReturnsToJobScreen()
    {
        var sm = CreateAtScreen(GameScreen.JobActionMenu);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.JobScreen, sm.CurrentScreen);
    }

    // --- Grid Navigation ---

    [Fact]
    public void PartyMenu_GridNavigation_5Columns()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_RIGHT); // col 1
        sm.OnKeyPressed(VK_RIGHT); // col 2
        sm.OnKeyPressed(VK_RIGHT); // col 3
        sm.OnKeyPressed(VK_RIGHT); // col 4
        sm.OnKeyPressed(VK_RIGHT); // still 4 (clamped)
        Assert.Equal(4, sm.CursorCol);
        Assert.Equal(0, sm.CursorRow);
    }

    [Fact]
    public void PartyMenu_ClampsToRosterCount()
    {
        var sm = new ScreenStateMachine();
        sm.SetRosterCount(3); // Only 3 units: positions 0,1,2
        sm.SetScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_DOWN); // try to go to row 1, but only 3 units fit in row 0
        Assert.Equal(0, sm.CursorRow); // clamped back
    }

    [Fact]
    public void PartyMenu_UpAtTop_StaysAtTop()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_UP);
        Assert.Equal(0, sm.CursorRow);
    }

    [Fact]
    public void EquipmentScreen_GridBounds()
    {
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        // Navigate to bottom-right
        for (int i = 0; i < 5; i++) sm.OnKeyPressed(VK_DOWN);
        sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(4, sm.CursorRow); // clamped at 4
        Assert.Equal(1, sm.CursorCol); // clamped at 1
    }

    [Fact]
    public void JobScreen_GenericCharacter_6Columns()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        sm.OnKeyPressed(VK_DOWN); // sidebar = Job
        sm.OnKeyPressed(VK_RETURN); // → JobScreen
        Assert.Equal(6, sm.GridColumns);

        for (int i = 0; i < 10; i++) sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(5, sm.CursorCol); // clamped at 5
    }

    [Fact]
    public void JobScreen_Ramza_8ColumnsRow0()
    {
        var sm = CreateAtScreen(GameScreen.JobScreen, isRamza: true);
        Assert.Equal(8, sm.GridColumns);

        for (int i = 0; i < 10; i++) sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(7, sm.CursorCol); // clamped at 7

        // Move to row 1 — should be 6 columns
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(6, sm.GridColumns);
        Assert.Equal(5, sm.CursorCol); // clamped from 7 to 5
    }

    // --- Tab Cycling ---

    [Fact]
    public void PartyMenu_TabCycling()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        Assert.Equal(PartyTab.Units, sm.Tab);

        sm.OnKeyPressed(VK_E); // → Inventory
        Assert.Equal(PartyTab.Inventory, sm.Tab);
        sm.OnKeyPressed(VK_E); // → Chronicle
        Assert.Equal(PartyTab.Chronicle, sm.Tab);
        sm.OnKeyPressed(VK_E); // → Options
        Assert.Equal(PartyTab.Options, sm.Tab);
        sm.OnKeyPressed(VK_E); // stays at Options (clamped)
        Assert.Equal(PartyTab.Options, sm.Tab);

        sm.OnKeyPressed(VK_Q); // → Chronicle
        Assert.Equal(PartyTab.Chronicle, sm.Tab);
        sm.OnKeyPressed(VK_Q);
        sm.OnKeyPressed(VK_Q);
        sm.OnKeyPressed(VK_Q); // stays at Units (clamped)
        Assert.Equal(PartyTab.Units, sm.Tab);
    }

    [Fact]
    public void PartyMenu_EnterOnNonUnitsTab_DoesNothing()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_E); // → Inventory tab
        sm.OnKeyPressed(VK_RETURN); // should NOT transition
        Assert.Equal(GameScreen.PartyMenu, sm.CurrentScreen);
    }

    // --- Sidebar ---

    [Fact]
    public void CharacterStatus_SidebarNavigation()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        Assert.Equal(0, sm.SidebarIndex);

        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(1, sm.SidebarIndex);
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(2, sm.SidebarIndex);
        sm.OnKeyPressed(VK_DOWN); // clamped
        Assert.Equal(2, sm.SidebarIndex);

        sm.OnKeyPressed(VK_UP);
        Assert.Equal(1, sm.SidebarIndex);
    }

    // --- Round Trips ---

    [Fact]
    public void FullRoundTrip_WorldMap_To_JobScreen_And_Back()
    {
        var sm = new ScreenStateMachine();
        sm.SetRosterCount(17);
        sm.SetScreen(GameScreen.WorldMap);

        sm.OnKeyPressed(VK_ESCAPE); // → PartyMenu
        Assert.Equal(GameScreen.PartyMenu, sm.CurrentScreen);

        sm.OnKeyPressed(VK_RETURN); // → CharacterStatus
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);

        sm.OnKeyPressed(VK_DOWN);   // sidebar 1 = Job
        sm.OnKeyPressed(VK_RETURN); // → JobScreen
        Assert.Equal(GameScreen.JobScreen, sm.CurrentScreen);

        sm.OnKeyPressed(VK_ESCAPE); // → CharacterStatus
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);

        sm.OnKeyPressed(VK_ESCAPE); // → PartyMenu
        Assert.Equal(GameScreen.PartyMenu, sm.CurrentScreen);

        sm.OnKeyPressed(VK_ESCAPE); // → WorldMap
        Assert.Equal(GameScreen.WorldMap, sm.CurrentScreen);
    }

    // --- Valid Actions ---

    [Fact]
    public void GetValidActions_WorldMap_IncludesEscape()
    {
        var sm = CreateAtScreen(GameScreen.WorldMap);
        var actions = sm.GetValidActions();
        Assert.Contains(actions, a => a.Key == "escape" && a.Vk == VK_ESCAPE);
    }

    [Fact]
    public void GetValidActions_PartyMenu_IncludesTabKeys()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        var actions = sm.GetValidActions();
        Assert.Contains(actions, a => a.Key == "q");
        Assert.Contains(actions, a => a.Key == "e");
        Assert.Contains(actions, a => a.Key == "enter"); // Units tab
    }

    [Fact]
    public void GetValidActions_JobScreen_IncludesTAndY()
    {
        var sm = CreateAtScreen(GameScreen.JobScreen);
        var actions = sm.GetValidActions();
        Assert.Contains(actions, a => a.Key == "t");
        Assert.Contains(actions, a => a.Key == "y");
    }

    // --- GetScreenState ---

    [Fact]
    public void GetScreenState_PartyMenu_IncludesAllFields()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_DOWN);
        sm.OnKeyPressed(VK_RIGHT);

        var state = sm.GetScreenState();
        Assert.Equal("partymenu", state.Screen);
        Assert.Equal(1, state.CursorRow);
        Assert.Equal(1, state.CursorCol);
        Assert.Equal("Units", state.Tab);
        Assert.Equal(5, state.GridColumns);
        Assert.NotEmpty(state.ValidActions);
    }

    [Fact]
    public void GetScreenState_CharacterStatus_IncludesSidebar()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        sm.OnKeyPressed(VK_DOWN);

        var state = sm.GetScreenState();
        Assert.Equal("characterstatus", state.Screen);
        Assert.Equal(1, state.SidebarIndex);
        Assert.Null(state.CursorRow); // no grid on this screen
    }

    // --- SetScreen ---

    [Fact]
    public void SetScreen_ResetsContext()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_DOWN);
        sm.OnKeyPressed(VK_RIGHT);
        sm.OnKeyPressed(VK_E); // Inventory tab

        sm.SetScreen(GameScreen.WorldMap);
        Assert.Equal(GameScreen.WorldMap, sm.CurrentScreen);
        Assert.Equal(0, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);
    }

    // --- Ramza Detection ---

    [Fact]
    public void PartyMenu_EnterAtIndex0_SetsIsRamza()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_RETURN); // index 0 = Ramza
        Assert.True(sm.IsRamza);
    }

    [Fact]
    public void PartyMenu_EnterAtIndex1_DoesNotSetIsRamza()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenu);
        sm.OnKeyPressed(VK_RIGHT); // col 1
        sm.OnKeyPressed(VK_RETURN); // index 1 = not Ramza
        Assert.False(sm.IsRamza);
    }

    // --- Screen Description ---

    [Fact]
    public void GetScreenDescription_JobActionMenu_ShowsSelection()
    {
        var sm = CreateAtScreen(GameScreen.JobActionMenu);
        Assert.Contains("Learn Abilities", sm.GetScreenDescription());

        sm.OnKeyPressed(VK_RIGHT);
        Assert.Contains("Change Job", sm.GetScreenDescription());
    }
}
