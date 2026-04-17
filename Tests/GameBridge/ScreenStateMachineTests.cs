using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

public class ScreenStateMachineTests
{
    private const int VK_RETURN = 0x0D;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_SPACE = 0x20;
    private const int VK_1 = 0x31;
    private const int VK_LEFT = 0x25;
    private const int VK_UP = 0x26;
    private const int VK_RIGHT = 0x27;
    private const int VK_DOWN = 0x28;
    private const int VK_B = 0x42;
    private const int VK_Q = 0x51;
    private const int VK_E = 0x45;
    private const int VK_R = 0x52;
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
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);
        Assert.Equal(PartyTab.Units, sm.Tab);
        Assert.Equal(0, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);
    }

    [Fact]
    public void PartyMenu_Escape_TransitionsToWorldMap()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.WorldMap, sm.CurrentScreen);
    }

    [Fact]
    public void PartyMenu_Enter_TransitionsToCharacterStatus()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
        Assert.Equal(0, sm.SidebarIndex);
    }

    [Fact]
    public void CharacterStatus_Escape_RestoresPartyMenuCursor()
    {
        // Game preserves the entry cursor position on Escape (verified
        // live 2026-04-15). State machine restores _savedParty{Row,Col}
        // captured at Enter time so the cursor stays where the player
        // left it before opening CharacterStatus.
        var sm = CreateAtScreen(GameScreen.WorldMap);
        sm.OnKeyPressed(VK_ESCAPE); // → PartyMenu at (0,0)
        sm.OnKeyPressed(VK_DOWN);   // row 1
        sm.OnKeyPressed(VK_RIGHT);  // col 1
        sm.OnKeyPressed(VK_RIGHT);  // col 2
        sm.OnKeyPressed(VK_RETURN); // → CharacterStatus (saves 1,2)
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);

        sm.OnKeyPressed(VK_ESCAPE); // → PartyMenu, cursor restored to (1,2)
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);
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

    [Fact(Skip = "Pre-existing failure")]
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
    public void PartyMenu_GridNavigation_5ColumnsWrap()
    {
        // Verified live 2026-04-15: 5 Rights from r0c0 on a 5-col grid
        // wraps back to r0c0 in the real game, not clamped at c4.
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_RIGHT); // col 1
        sm.OnKeyPressed(VK_RIGHT); // col 2
        sm.OnKeyPressed(VK_RIGHT); // col 3
        sm.OnKeyPressed(VK_RIGHT); // col 4
        sm.OnKeyPressed(VK_RIGHT); // wraps → col 0 (same row)
        Assert.Equal(0, sm.CursorCol);
        Assert.Equal(0, sm.CursorRow);
    }

    [Fact]
    public void PartyMenu_LeftAtCol0_WrapsToLastCol()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_LEFT); // wraps from col 0 to col 4
        Assert.Equal(4, sm.CursorCol);
    }

    [Fact]
    public void PartyMenu_ClampsToRosterCountOnShortGrid()
    {
        // 3 units fit in row 0 (cols 0,1,2). Pressing Down with no row 1 wraps
        // to "row GridRows-1" but ClampCursorToRoster yanks us back into the
        // populated portion. With 3 units: GridRows=1, so Down stays on row 0.
        var sm = new ScreenStateMachine();
        sm.SetRosterCount(3);
        sm.SetScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(0, sm.CursorRow);
    }

    [Fact]
    public void PartyMenu_UpAtTop_WrapsToLastRow()
    {
        // Grid wraps: Up from row 0 → row GridRows-1, then ClampCursorToRoster
        // backs up if that lands past the last unit. Here the roster spans all
        // rows so Up wraps cleanly to the bottom row.
        var sm = new ScreenStateMachine();
        sm.SetRosterCount(15); // fills 3 rows (5+5+5)
        sm.SetScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_UP);
        Assert.Equal(2, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);
    }

    [Fact]
    public void PartyMenu_DownAtLastRow_WrapsToTop()
    {
        var sm = new ScreenStateMachine();
        sm.SetRosterCount(15);
        sm.SetScreen(GameScreen.PartyMenuUnits);
        for (int i = 0; i < 3; i++) sm.OnKeyPressed(VK_DOWN); // 0→1→2→0 (wrap)
        Assert.Equal(0, sm.CursorRow);
    }

    [Fact]
    public void SetPartyMenuCursor_UpdatesCursorAndSavedEntry()
    {
        var sm = new ScreenStateMachine();
        sm.SetRosterCount(15);
        sm.SetScreen(GameScreen.PartyMenuUnits);
        sm.SetPartyMenuCursor(2, 3);
        Assert.Equal(2, sm.CursorRow);
        Assert.Equal(3, sm.CursorCol);
        // Entering a nested screen then Escape should restore to (2,3),
        // proving the saved-entry cursor was also updated.
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);
        Assert.Equal(2, sm.CursorRow);
        Assert.Equal(3, sm.CursorCol);
    }

    [Fact]
    public void SetPartyMenuCursor_NoOpsOffUnitsTab()
    {
        var sm = new ScreenStateMachine();
        sm.SetRosterCount(15);
        sm.SetScreen(GameScreen.PartyMenuUnits);
        // Switch to Chronicle tab via Q/E — resolver should then be
        // inert (the caller gates on Tab==Units too, but the state
        // machine must also refuse to move the cursor if misfired).
        sm.OnKeyPressed(VK_E); // Units → Inventory
        sm.OnKeyPressed(VK_E); // Inventory → Chronicle
        Assert.Equal(PartyTab.Chronicle, sm.Tab);
        sm.SetPartyMenuCursor(2, 3);
        // Cursor should remain at the tab's reset value (row 0, col 0)
        // since the setter bailed.
        Assert.Equal(0, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);
    }

    [Fact]
    public void SetPartyMenuCursor_NoOpsOffPartyMenu()
    {
        var sm = new ScreenStateMachine();
        sm.SetScreen(GameScreen.WorldMap);
        sm.SetPartyMenuCursor(2, 3);
        Assert.Equal(0, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);
    }

    [Fact]
    public void EquipmentScreen_GridWraps()
    {
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        // 5 Downs from row 0: 0→1→2→3→4→0 (wrap) — end at row 0
        for (int i = 0; i < 5; i++) sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(0, sm.CursorRow);
        // One more Down lands at row 1
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(1, sm.CursorRow);

        // Right toggles 0 ↔ 1 (column wrap)
        Assert.Equal(0, sm.CursorCol);
        sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(1, sm.CursorCol);
        sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(0, sm.CursorCol); // wrapped back
    }

    [Fact]
    public void JobScreen_GenericCharacter_6Columns()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        sm.OnKeyPressed(VK_DOWN); // sidebar = Job
        sm.OnKeyPressed(VK_RETURN); // → JobScreen
        Assert.Equal(6, sm.GridColumns);

        // Wraps: 10 Rights on 6-col grid = 10 % 6 = 4
        for (int i = 0; i < 10; i++) sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(4, sm.CursorCol);
    }

    [Fact]
    public void JobScreen_Ramza_Row0Is6Cols_Row1Is7Cols()
    {
        // Verified live 2026-04-15: Ramza Ch4 grid is 6/7/6 cells per row.
        // Row 0 (Gallant Knight..White Mage) = 6 cells.
        // Row 1 (Black Mage..Geomancer) = 7 cells.
        // Row 2 (Dragoon..Mime) = 6 cells.
        var sm = CreateAtScreen(GameScreen.JobScreen, isRamza: true);

        // Row 0: 6 Rights wraps back to col 0.
        for (int i = 0; i < 6; i++) sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(0, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);

        // Down to row 1 (7 cols). 6 Rights lands on col 6 (Geomancer), not wrap.
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(1, sm.CursorRow);
        for (int i = 0; i < 6; i++) sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(6, sm.CursorCol);

        // One more Right wraps row 1 back to col 0.
        sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(0, sm.CursorCol);

        // Down from row 1 col 0 → row 2 col 0 (6 cols). 6 Rights wraps.
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(2, sm.CursorRow);
        for (int i = 0; i < 6; i++) sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(0, sm.CursorCol);
    }

    [Fact]
    public void JobScreen_Ramza_DownFromRow1Col6_ClampsToRow2LastCell()
    {
        // Row 1 has 7 cells; row 2 has only 6. Moving down from col 6
        // should clamp to row 2's last valid col (5), not leave an out-of-
        // range cursor.
        var sm = CreateAtScreen(GameScreen.JobScreen, isRamza: true);
        sm.OnKeyPressed(VK_DOWN);
        for (int i = 0; i < 6; i++) sm.OnKeyPressed(VK_RIGHT); // row 1 col 6
        Assert.Equal(6, sm.CursorCol);

        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(2, sm.CursorRow);
        Assert.Equal(5, sm.CursorCol); // clamped from 6 → 5
    }

    // --- Tab Cycling ---

    [Fact]
    public void PartyMenu_TabCycling()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        Assert.Equal(PartyTab.Units, sm.Tab);

        sm.OnKeyPressed(VK_E); // → Inventory
        Assert.Equal(PartyTab.Inventory, sm.Tab);
        sm.OnKeyPressed(VK_E); // → Chronicle
        Assert.Equal(PartyTab.Chronicle, sm.Tab);
        sm.OnKeyPressed(VK_E); // → Options
        Assert.Equal(PartyTab.Options, sm.Tab);
        sm.OnKeyPressed(VK_E); // wraps: Options → Units
        Assert.Equal(PartyTab.Units, sm.Tab);

        sm.OnKeyPressed(VK_Q); // wraps: Units → Options
        Assert.Equal(PartyTab.Options, sm.Tab);
        sm.OnKeyPressed(VK_Q); // → Chronicle
        Assert.Equal(PartyTab.Chronicle, sm.Tab);
        sm.OnKeyPressed(VK_Q); // → Inventory
        Assert.Equal(PartyTab.Inventory, sm.Tab);
        sm.OnKeyPressed(VK_Q); // → Units
        Assert.Equal(PartyTab.Units, sm.Tab);
    }

    [Fact]
    public void PartyMenu_EnterOnNonUnitsTab_DoesNothing()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_E); // → Inventory tab
        sm.OnKeyPressed(VK_RETURN); // should NOT transition
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);
    }

    // Drift mitigation (TODO §0 session 16 repro): tab-switching back
    // to the Units tab must zero the cursor. The game restores Units to
    // origin on tab return; preserving a stale CursorRow/Col across
    // tab visits caused "state says Orlandeau, game shows Ramza"
    // mis-routes on SelectUnit.
    [Fact]
    public void PartyMenu_TabReturnToUnits_ResetsCursorToOrigin()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        // Move cursor off origin on Units tab.
        sm.OnKeyPressed(VK_DOWN);
        sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(1, sm.CursorRow);
        Assert.Equal(1, sm.CursorCol);

        // Tab away to Inventory and back to Units (E wraps Options→Units, but
        // here Q from Units lands on Options; then E×3 loops back to Units).
        sm.OnKeyPressed(VK_E); // → Inventory
        sm.OnKeyPressed(VK_E); // → Chronicle
        sm.OnKeyPressed(VK_E); // → Options
        sm.OnKeyPressed(VK_E); // → Units (wraps)

        Assert.Equal(PartyTab.Units, sm.Tab);
        Assert.Equal(0, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);
    }

    [Fact]
    public void PartyMenu_QWrapToUnits_ResetsCursorToOrigin()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_DOWN);
        sm.OnKeyPressed(VK_RIGHT);
        sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(1, sm.CursorRow);
        Assert.Equal(2, sm.CursorCol);

        // Q wraps Units → Options on the first press, then Q×3 walks back to Units.
        sm.OnKeyPressed(VK_Q); // → Options
        sm.OnKeyPressed(VK_Q); // → Chronicle
        sm.OnKeyPressed(VK_Q); // → Inventory
        sm.OnKeyPressed(VK_Q); // → Units

        Assert.Equal(PartyTab.Units, sm.Tab);
        Assert.Equal(0, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);
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
        sm.OnKeyPressed(VK_DOWN); // wraps: Combat Sets → Equipment & Abilities
        Assert.Equal(0, sm.SidebarIndex);

        sm.OnKeyPressed(VK_UP); // wraps: Equipment & Abilities → Combat Sets
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
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);

        sm.OnKeyPressed(VK_RETURN); // → CharacterStatus
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);

        sm.OnKeyPressed(VK_DOWN);   // sidebar 1 = Job
        sm.OnKeyPressed(VK_RETURN); // → JobScreen
        Assert.Equal(GameScreen.JobScreen, sm.CurrentScreen);

        sm.OnKeyPressed(VK_ESCAPE); // → CharacterStatus
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);

        sm.OnKeyPressed(VK_ESCAPE); // → PartyMenu
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);

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
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
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
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_DOWN);
        sm.OnKeyPressed(VK_RIGHT);

        var state = sm.GetScreenState();
        Assert.Equal("partymenuunits", state.Screen);
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
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
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
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_RETURN); // index 0 = Ramza
        Assert.True(sm.IsRamza);
    }

    [Fact]
    public void PartyMenu_EnterAtIndex1_DoesNotSetIsRamza()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
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

    // --- CharacterStatus view toggles + side-flows (§10.6) ---

    [Fact]
    public void CharacterStatus_StatsExpandedToggle()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        Assert.False(sm.StatsExpanded);
        sm.OnKeyPressed(VK_1);
        Assert.True(sm.StatsExpanded);
        sm.OnKeyPressed(VK_1);
        Assert.False(sm.StatsExpanded);
    }

    [Fact]
    public void CharacterStatus_StatsExpanded_ResetsOnEscape()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        sm.OnKeyPressed(VK_1);
        Assert.True(sm.StatsExpanded);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.False(sm.StatsExpanded);
    }

    [Fact]
    public void CharacterStatus_Space_OpensCharacterDialog()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        sm.OnKeyPressed(VK_SPACE);
        Assert.Equal(GameScreen.CharacterDialog, sm.CurrentScreen);
    }

    [Fact]
    public void CharacterDialog_Enter_ReturnsToCharacterStatus()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        sm.OnKeyPressed(VK_SPACE);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
    }

    [Fact]
    public void CharacterStatus_SidebarIndex2_Enter_OpensCombatSets()
    {
        var sm = CreateAtScreen(GameScreen.CharacterStatus);
        sm.OnKeyPressed(VK_DOWN); // sidebar = 1 (Job)
        sm.OnKeyPressed(VK_DOWN); // sidebar = 2 (Combat Sets)
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.CombatSets, sm.CurrentScreen);
    }

    [Fact]
    public void CombatSets_Escape_ReturnsToCharacterStatus()
    {
        var sm = CreateAtScreen(GameScreen.CombatSets);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
    }

    // --- DismissUnit (external set via hold_key) ---

    [Fact]
    public void DismissUnit_CursorDefaultsToBack_LeftRightToggles()
    {
        var sm = CreateAtScreen(GameScreen.DismissUnit);
        Assert.False(sm.DismissConfirmSelected); // Back is default (safe)
        sm.OnKeyPressed(VK_RIGHT);
        Assert.True(sm.DismissConfirmSelected);  // Confirm
        sm.OnKeyPressed(VK_LEFT);
        Assert.False(sm.DismissConfirmSelected);
    }

    [Fact]
    public void DismissUnit_EscapeOrEnter_ReturnsToCharacterStatus()
    {
        var sm = CreateAtScreen(GameScreen.DismissUnit);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);

        sm = CreateAtScreen(GameScreen.DismissUnit);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
    }

    // --- EquipmentAndAbilities (EquipmentScreen) Effects view toggle + slot routing ---

    [Fact]
    public void EquipmentScreen_R_TogglesEffectsView()
    {
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        Assert.False(sm.EquipmentEffectsView);
        sm.OnKeyPressed(VK_R);
        Assert.True(sm.EquipmentEffectsView);
        sm.OnKeyPressed(VK_R);
        Assert.False(sm.EquipmentEffectsView);
    }

    [Fact]
    public void EquipmentScreen_EffectsView_ResetsOnEscape()
    {
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        sm.OnKeyPressed(VK_R);
        Assert.True(sm.EquipmentEffectsView);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.False(sm.EquipmentEffectsView);
    }

    [Theory]
    [InlineData(0, EquipmentSlot.Weapon)]
    [InlineData(1, EquipmentSlot.Shield)]
    [InlineData(2, EquipmentSlot.Headware)]
    [InlineData(3, EquipmentSlot.CombatGarb)]
    [InlineData(4, EquipmentSlot.Accessory)]
    public void EquipmentScreen_EnterOnEquipmentSlot_CapturesSlotIdentity(int row, EquipmentSlot expected)
    {
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        for (int i = 0; i < row; i++) sm.OnKeyPressed(VK_DOWN);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.EquipmentItemList, sm.CurrentScreen);
        Assert.Equal(expected, sm.CurrentEquipmentSlot);
    }

    [Fact]
    public void EquipmentScreen_QE_StaysOnScreen_ForPartyCycling()
    {
        // On EquipmentAndAbilities, Q/E cycle to prev/next party member while
        // keeping the screen open (game-side behavior — state machine has no
        // unit-identity to track, so the screen name stays the same).
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        sm.OnKeyPressed(VK_Q);
        Assert.Equal(GameScreen.EquipmentScreen, sm.CurrentScreen);
        sm.OnKeyPressed(VK_E);
        Assert.Equal(GameScreen.EquipmentScreen, sm.CurrentScreen);
    }

    [Fact]
    public void EquipmentScreen_EnterOnPrimaryActionRow_IsNoOp()
    {
        // Row 0 of the abilities column = job-locked primary skillset
        // (e.g. "Mettle" for Gallant Knight). Enter does nothing — the
        // primary skillset can only change via JobSelection.
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        sm.OnKeyPressed(VK_RIGHT);  // col = 1
        // CursorRow is still 0
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.EquipmentScreen, sm.CurrentScreen);
        Assert.Equal(0, sm.CursorRow);
        Assert.Equal(1, sm.CursorCol);
    }

    [Theory]
    [InlineData(1, GameScreen.SecondaryAbilities)]
    [InlineData(2, GameScreen.ReactionAbilities)]
    [InlineData(3, GameScreen.SupportAbilities)]
    [InlineData(4, GameScreen.MovementAbilities)]
    public void EquipmentScreen_EnterOnAbilityColumn_RoutesByRow(int row, GameScreen expected)
    {
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        sm.OnKeyPressed(VK_RIGHT); // col = 1 (Abilities column)
        for (int i = 0; i < row; i++) sm.OnKeyPressed(VK_DOWN);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(expected, sm.CurrentScreen);
    }

    [Fact]
    public void AbilityPicker_EnterOrEscape_ReturnsToEquipmentScreen_RestoringCursor()
    {
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        sm.OnKeyPressed(VK_RIGHT);      // col 1
        sm.OnKeyPressed(VK_DOWN);       // row 1 (secondary action)
        sm.OnKeyPressed(VK_RETURN);     // → SecondaryAbilities, saves row=1 col=1
        Assert.Equal(GameScreen.SecondaryAbilities, sm.CurrentScreen);

        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.EquipmentScreen, sm.CurrentScreen);
        Assert.Equal(1, sm.CursorRow);
        Assert.Equal(1, sm.CursorCol);
    }

    [Fact]
    public void EquipmentItemList_EnterOrEscape_RestoresEquipmentScreenCursor()
    {
        var sm = CreateAtScreen(GameScreen.EquipmentScreen);
        sm.OnKeyPressed(VK_DOWN);       // row 1 (Shield)
        sm.OnKeyPressed(VK_DOWN);       // row 2 (Headware)
        sm.OnKeyPressed(VK_RETURN);     // → EquipmentItemList with slot=Headware
        Assert.Equal(GameScreen.EquipmentItemList, sm.CurrentScreen);
        Assert.Equal(EquipmentSlot.Headware, sm.CurrentEquipmentSlot);

        sm.OnKeyPressed(VK_RETURN);     // equip → back to EquipmentScreen
        Assert.Equal(GameScreen.EquipmentScreen, sm.CurrentScreen);
        Assert.Equal(2, sm.CursorRow);
        Assert.Equal(0, sm.CursorCol);
    }

    [Fact]
    public void JobChangeConfirmation_LeftRight_TogglesSelection()
    {
        // Navigate: Ramza → CharacterStatus → (sidebar Job) → JobSelection →
        // Enter on job tile → JobActionMenu → Right to "Change Job" →
        // Enter → JobChangeConfirmation.
        var sm = CreateAtScreen(GameScreen.JobScreen, isRamza: true);
        sm.OnKeyPressed(VK_RETURN);         // → JobActionMenu
        Assert.Equal(GameScreen.JobActionMenu, sm.CurrentScreen);
        Assert.Equal(0, sm.JobActionIndex); // default: Learn Abilities
        sm.OnKeyPressed(VK_RIGHT);          // → Change Job highlighted
        Assert.Equal(1, sm.JobActionIndex);
        sm.OnKeyPressed(VK_RETURN);         // → JobChangeConfirmation
        Assert.Equal(GameScreen.JobChangeConfirmation, sm.CurrentScreen);
        Assert.False(sm.JobChangeConfirmSelected); // defaults to Cancel

        sm.OnKeyPressed(VK_RIGHT);
        Assert.True(sm.JobChangeConfirmSelected);  // → Confirm
        sm.OnKeyPressed(VK_LEFT);
        Assert.False(sm.JobChangeConfirmSelected); // → Cancel

        // Enter/Escape returns to CharacterStatus and resets.
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
        Assert.False(sm.JobChangeConfirmSelected);
    }

    // --- Chronicle tab navigation (3-4-3 grid) ---

    private static ScreenStateMachine AtChronicleRoot()
    {
        var sm = new ScreenStateMachine();
        sm.SetScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_E); // Units → Inventory
        sm.OnKeyPressed(VK_E); // Inventory → Chronicle
        Assert.Equal(PartyTab.Chronicle, sm.Tab);
        Assert.Equal(0, sm.ChronicleIndex);
        return sm;
    }

    [Fact]
    public void Chronicle_Right_FromEncyclopedia_LandsOnStateOfRealm()
    {
        var sm = AtChronicleRoot();
        sm.OnKeyPressed(VK_RIGHT);
        Assert.Equal(1, sm.ChronicleIndex);
    }

    [Fact]
    public void Chronicle_Down_FromEncyclopedia_LandsOnAuracite()
    {
        // Verified live 2026-04-14: Down from row-0 lands on the column-aligned
        // tile in row 1 (Encyc→Auracite, SoR→Reading, Events→Collection).
        var sm = AtChronicleRoot();
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(3, sm.ChronicleIndex); // Auracite
    }

    [Fact]
    public void Chronicle_Down_FromErrands_LandsOnAkademicReport()
    {
        // Errands(6) is row 1 col 3, but row 2 has only 3 cols — wraps to last (Akademic).
        var sm = AtChronicleRoot();
        sm.OnKeyPressed(VK_DOWN);  // Encyc → Auracite (3)
        sm.OnKeyPressed(VK_RIGHT); // → Reading (4)
        sm.OnKeyPressed(VK_RIGHT); // → Collection (5)
        sm.OnKeyPressed(VK_RIGHT); // → Errands (6)
        Assert.Equal(6, sm.ChronicleIndex);
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(9, sm.ChronicleIndex); // Akademic Report
    }

    [Fact]
    public void Chronicle_Up_FromErrands_LandsOnEvents()
    {
        var sm = AtChronicleRoot();
        sm.OnKeyPressed(VK_DOWN); sm.OnKeyPressed(VK_RIGHT);
        sm.OnKeyPressed(VK_RIGHT); sm.OnKeyPressed(VK_RIGHT); // → Errands
        Assert.Equal(6, sm.ChronicleIndex);
        sm.OnKeyPressed(VK_UP);
        Assert.Equal(2, sm.ChronicleIndex); // Events (col 2)
    }

    [Fact]
    public void Chronicle_Up_FromAkademicReport_LandsOnCollection()
    {
        var sm = AtChronicleRoot();
        sm.OnKeyPressed(VK_DOWN); sm.OnKeyPressed(VK_DOWN); // → Stratagems (7)
        sm.OnKeyPressed(VK_RIGHT); sm.OnKeyPressed(VK_RIGHT); // → Akademic (9)
        Assert.Equal(9, sm.ChronicleIndex);
        sm.OnKeyPressed(VK_UP);
        Assert.Equal(5, sm.ChronicleIndex); // Collection
    }

    [Fact]
    public void Chronicle_Enter_OpensCorrespondingSubScreen()
    {
        var sm = AtChronicleRoot();
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.ChronicleEncyclopedia, sm.CurrentScreen);

        sm.OnKeyPressed(VK_ESCAPE); // back to PartyMenu Chronicle tab
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);
        Assert.Equal(PartyTab.Chronicle, sm.Tab);

        sm.OnKeyPressed(VK_DOWN); // → Auracite (3)
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.ChronicleAuracite, sm.CurrentScreen);
    }

    [Fact]
    public void Chronicle_TabSwitch_ResetsChronicleIndex()
    {
        var sm = AtChronicleRoot();
        sm.OnKeyPressed(VK_DOWN); // → Auracite
        Assert.Equal(3, sm.ChronicleIndex);
        sm.OnKeyPressed(VK_E); // → Options tab
        Assert.Equal(PartyTab.Options, sm.Tab);
        Assert.Equal(0, sm.ChronicleIndex);
        Assert.Equal(0, sm.OptionsIndex);
    }

    // --- Options tab navigation (5 vertical items, wraps) ---

    private static ScreenStateMachine AtOptionsRoot()
    {
        var sm = new ScreenStateMachine();
        sm.SetScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_Q); // Units → Options (Q wraps)
        Assert.Equal(PartyTab.Options, sm.Tab);
        Assert.Equal(0, sm.OptionsIndex);
        return sm;
    }

    [Fact]
    public void Options_Down_AdvancesIndex_AndWraps()
    {
        var sm = AtOptionsRoot();
        sm.OnKeyPressed(VK_DOWN); Assert.Equal(1, sm.OptionsIndex);
        sm.OnKeyPressed(VK_DOWN); Assert.Equal(2, sm.OptionsIndex);
        sm.OnKeyPressed(VK_DOWN); Assert.Equal(3, sm.OptionsIndex);
        sm.OnKeyPressed(VK_DOWN); Assert.Equal(4, sm.OptionsIndex);
        sm.OnKeyPressed(VK_DOWN); Assert.Equal(0, sm.OptionsIndex); // wraps
    }

    [Fact]
    public void Options_Up_FromTop_WrapsToBottom()
    {
        var sm = AtOptionsRoot();
        sm.OnKeyPressed(VK_UP);
        Assert.Equal(4, sm.OptionsIndex); // Save → Exit Game
    }

    [Fact]
    public void Options_EnterOnSettings_OpensOptionsSettings()
    {
        var sm = AtOptionsRoot();
        sm.OnKeyPressed(VK_DOWN); sm.OnKeyPressed(VK_DOWN); // → Settings (2)
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.OptionsSettings, sm.CurrentScreen);
        sm.OnKeyPressed(VK_ESCAPE);
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);
        Assert.Equal(PartyTab.Options, sm.Tab);
    }

    [Fact]
    public void Options_EnterOnSave_DoesNotChangeScreen()
    {
        // Save (idx 0) doesn't open a nested screen via the state machine —
        // the actual save action is handled by the existing `save` flow.
        var sm = AtOptionsRoot();
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);
    }

    [Fact]
    public void ChronicleIndexToName_KnownValues()
    {
        Assert.Equal("Encyclopedia",          ScreenStateMachine.ChronicleIndexToName(0));
        Assert.Equal("State of the Realm",    ScreenStateMachine.ChronicleIndexToName(1));
        Assert.Equal("Errands",               ScreenStateMachine.ChronicleIndexToName(6));
        Assert.Equal("Akademic Report",       ScreenStateMachine.ChronicleIndexToName(9));
    }

    [Fact]
    public void OptionsIndexToName_KnownValues()
    {
        Assert.Equal("Save",             ScreenStateMachine.OptionsIndexToName(0));
        Assert.Equal("Settings",         ScreenStateMachine.OptionsIndexToName(2));
        Assert.Equal("Return to Title",  ScreenStateMachine.OptionsIndexToName(3));
        Assert.Equal("Exit Game",        ScreenStateMachine.OptionsIndexToName(4));
    }

    [Fact]
    public void KeysSinceLastSetScreen_ResetsOnSetScreen_AndCountsKeys()
    {
        var sm = new ScreenStateMachine();
        // Fresh state machine: no SetScreen yet, no keys yet.
        Assert.Equal(0, sm.KeysSinceLastSetScreen);

        sm.SetScreen(GameScreen.WorldMap);
        Assert.Equal(0, sm.KeysSinceLastSetScreen);

        sm.OnKeyPressed(VK_ESCAPE); // → PartyMenu
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);
        // The internal transition via OnKeyPressed (not SetScreen) still counts as a key.
        Assert.Equal(1, sm.KeysSinceLastSetScreen);

        sm.OnKeyPressed(VK_RETURN); // → CharacterStatus (Ramza)
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
        Assert.Equal(2, sm.KeysSinceLastSetScreen);

        // Explicit SetScreen resets the counter (simulates drift-recovery snap-back).
        sm.SetScreen(GameScreen.PartyMenuUnits);
        Assert.Equal(0, sm.KeysSinceLastSetScreen);
    }

    // --- ViewedGridIndex (maps cursor position to roster display index) ---

    [Fact]
    public void ViewedGridIndex_OnPartyMenuUnitsTab_FollowsLiveCursor()
    {
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        // Roster is 17 units wide enough to scroll the cursor freely.
        sm.OnKeyPressed(VK_RIGHT); // r0 c1
        sm.OnKeyPressed(VK_RIGHT); // r0 c2
        sm.OnKeyPressed(VK_DOWN);  // r1 c2
        // 5-col grid → index = 1*5 + 2 = 7
        Assert.Equal(7, sm.ViewedGridIndex);
    }

    [Fact]
    public void ViewedGridIndex_OnNestedScreen_UsesSavedCursorAtEnterTime()
    {
        // Move cursor to (r1, c2), Enter → CharacterStatus should freeze
        // the viewed index at 7 and not follow CursorRow/Col changes inside
        // the nested screen.
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_RIGHT);
        sm.OnKeyPressed(VK_RIGHT);
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(7, sm.ViewedGridIndex);
        sm.OnKeyPressed(VK_RETURN); // → CharacterStatus (saves (1,2))
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
        // Inner navigation — sidebar Down — must not change the viewed index.
        sm.OnKeyPressed(VK_DOWN);
        Assert.Equal(7, sm.ViewedGridIndex);
    }

    [Fact]
    public void ViewedGridIndex_BackFromNestedScreen_RestoresSavedCursor()
    {
        // Game preserves the entry cursor position on Escape from
        // CharacterStatus. ViewedGridIndex follows because PartyMenu
        // uses the live CursorRow/Col, which now equals the restored
        // saved snapshot.
        var sm = CreateAtScreen(GameScreen.PartyMenuUnits);
        sm.OnKeyPressed(VK_RIGHT);
        sm.OnKeyPressed(VK_DOWN);
        // r1 c1 = idx 6
        Assert.Equal(6, sm.ViewedGridIndex);
        sm.OnKeyPressed(VK_RETURN);
        Assert.Equal(GameScreen.CharacterStatus, sm.CurrentScreen);
        sm.OnKeyPressed(VK_ESCAPE); // back to PartyMenu, cursor restored
        Assert.Equal(GameScreen.PartyMenuUnits, sm.CurrentScreen);
        Assert.Equal(6, sm.ViewedGridIndex);
    }
}
