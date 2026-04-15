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

        /// <summary>
        /// Count of OnKeyPressed calls since the last SetScreen(). Zero immediately
        /// after a SetScreen — including the initial Unknown→anything transition
        /// on mod startup. Used by CommandWatcher drift-recovery to detect a stale
        /// state machine that's disagreeing with raw memory detection because no
        /// keys have flowed through the state machine to drive it forward.
        /// </summary>
        public int KeysSinceLastSetScreen { get; private set; }
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
        /// JobChangeConfirmation cursor position: false = Cancel (default,
        /// safe), true = Confirm. Left/Right toggle. Enter on Confirm
        /// actually changes the unit's job.
        /// </summary>
        public bool JobChangeConfirmSelected { get; private set; }

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

        /// <summary>
        /// Cursor position in the Chronicle tab grid (0..9 flat enumeration):
        ///   0 Encyclopedia        1 StateOfRealm   2 Events
        ///   3 Auracite            4 Reading        5 Collection   6 Errands
        ///   7 Stratagems          8 Lessons        9 AkademicReport
        /// Uses a flat index because the grid is non-uniform (3-4-3 rows).
        /// Up/Down logic handles row transitions explicitly.
        /// </summary>
        public int ChronicleIndex { get; private set; }

        /// <summary>
        /// Cursor position in the Options tab vertical list (0..4):
        ///   0 Save  1 Load  2 Settings  3 ReturnToTitle  4 ExitGame
        /// </summary>
        public int OptionsIndex { get; private set; }

        // Saved party menu cursor for returning from CharacterStatus
        private int _savedPartyRow;
        private int _savedPartyCol;

        /// <summary>
        /// The 0-indexed display-grid position of the currently-viewed party
        /// member. On PartyMenu root this is `CursorRow * GridColumns +
        /// CursorCol` (what the cursor is currently on); on nested screens
        /// (CharacterStatus, EquipmentAndAbilities, pickers, JobSelection)
        /// it's the position at the moment Enter was pressed — preserved via
        /// the saved row/col fields so the viewed unit doesn't change as the
        /// user navigates within an inner screen.
        ///
        /// To resolve this to a roster slot, pass it to
        /// <see cref="RosterReader.GetSlotByDisplayOrder"/>. The display
        /// order is driven by roster byte +0x122 (Time Recruited default).
        /// </summary>
        public int ViewedGridIndex
        {
            get
            {
                // PartyMenu Units grid is always 5-wide. Nested screens may
                // set GridColumns to something else (JobScreen=6 or 8,
                // EquipmentScreen=2), so we hardcode 5 here to always refer
                // to the party grid width.
                const int PartyGridCols = 5;
                if (CurrentScreen == GameScreen.PartyMenu && Tab == PartyTab.Units)
                    return CursorRow * PartyGridCols + CursorCol;
                // Nested screens: use the saved snapshot captured at Enter time.
                return _savedPartyRow * PartyGridCols + _savedPartyCol;
            }
        }

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
            ChronicleIndex = 0;
            OptionsIndex = 0;
            KeysSinceLastSetScreen = 0;

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

        /// <summary>
        /// Bumps KeysSinceLastSetScreen without routing a key through the
        /// per-screen handlers. Used by the drift-recovery path in
        /// CommandWatcher to mark a SetScreen() as "intentional / not
        /// stale" so the existing stale-check drift recovery (which gates
        /// on KeysSinceLastSetScreen == 0) doesn't cascade-snap us further
        /// than we meant.
        /// </summary>
        public void MarkKeyProcessed()
        {
            KeysSinceLastSetScreen++;
        }

        public void OnKeyPressed(int vkCode)
        {
            KeysSinceLastSetScreen++;
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
                case GameScreen.ChronicleEncyclopedia:
                case GameScreen.ChronicleStateOfRealm:
                case GameScreen.ChronicleEvents:
                case GameScreen.ChronicleAuracite:
                case GameScreen.ChronicleReadingMaterials:
                case GameScreen.ChronicleCollection:
                case GameScreen.ChronicleErrands:
                case GameScreen.ChronicleStratagems:
                case GameScreen.ChronicleLessons:
                case GameScreen.ChronicleAkademicReport:
                    HandleChronicleSubScreen(vkCode);
                    break;
                case GameScreen.OptionsSettings:
                    HandleOptionsSettings(vkCode);
                    break;
            }
        }

        // All Chronicle sub-screens currently model only the boundary (Escape back).
        // Inner-state navigation (Encyclopedia tabs, scrollable lists, etc.) is
        // deferred — see TODO §10.7.
        private void HandleChronicleSubScreen(int vk)
        {
            if (vk == VK_ESCAPE)
            {
                CurrentScreen = GameScreen.PartyMenu;
                Tab = PartyTab.Chronicle;
                // Cursor returns to whichever tile we entered from.
            }
        }

        private void HandleOptionsSettings(int vk)
        {
            if (vk == VK_ESCAPE)
            {
                CurrentScreen = GameScreen.PartyMenu;
                Tab = PartyTab.Options;
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
                    // Reset per-tab cursor on tab change so each tab starts at index 0.
                    ChronicleIndex = 0;
                    OptionsIndex = 0;
                    break;
                case VK_E:
                    // E wraps: Options → Units (rightmost → leftmost).
                    Tab = Tab == PartyTab.Options ? PartyTab.Units : (PartyTab)(Tab + 1);
                    ChronicleIndex = 0;
                    OptionsIndex = 0;
                    break;
                // Grid navigation WRAPS in-game on all four axes (verified 2026-04-15
                // live: 5 Rights from r0c0 on a 5-col PartyMenu grid returns to r0c0,
                // not clamped at r0c4). Clamping was codified in early tests but
                // doesn't match real UI behavior and causes the state machine to drift
                // on rapid cursor chains.
                // Grid navigation WRAPS in-game on all four axes (verified 2026-04-15
                // live: 5 Rights from r0c0 on a 5-col PartyMenu grid returns to r0c0,
                // not clamped at r0c4). Clamping was codified in early tests but
                // doesn't match real UI behavior and causes the state machine to drift
                // on rapid cursor chains.
                case VK_UP:
                    if (Tab == PartyTab.Chronicle) ChronicleIndex = ChronicleUp(ChronicleIndex);
                    else if (Tab == PartyTab.Options) OptionsIndex = OptionsIndex > 0 ? OptionsIndex - 1 : 4;
                    else
                    {
                        CursorRow = CursorRow > 0 ? CursorRow - 1 : GridRows - 1;
                        ClampCursorToRoster(ClampDirection.Up);
                    }
                    break;
                case VK_DOWN:
                    if (Tab == PartyTab.Chronicle) ChronicleIndex = ChronicleDown(ChronicleIndex);
                    else if (Tab == PartyTab.Options) OptionsIndex = OptionsIndex < 4 ? OptionsIndex + 1 : 0;
                    else
                    {
                        CursorRow = CursorRow < GridRows - 1 ? CursorRow + 1 : 0;
                        ClampCursorToRoster(ClampDirection.Down);
                    }
                    break;
                case VK_LEFT:
                    if (Tab == PartyTab.Chronicle) ChronicleIndex = ChronicleLeft(ChronicleIndex);
                    else if (Tab == PartyTab.Options) { /* Options list has no horizontal nav */ }
                    else
                    {
                        CursorCol = CursorCol > 0 ? CursorCol - 1 : GridColumns - 1;
                        ClampCursorToRoster(ClampDirection.Left);
                    }
                    break;
                case VK_RIGHT:
                    if (Tab == PartyTab.Chronicle) ChronicleIndex = ChronicleRight(ChronicleIndex);
                    else if (Tab == PartyTab.Options) { /* Options list has no horizontal nav */ }
                    else
                    {
                        CursorCol = CursorCol < GridColumns - 1 ? CursorCol + 1 : 0;
                        ClampCursorToRoster(ClampDirection.Right);
                    }
                    break;
                case VK_RETURN:
                    if (Tab == PartyTab.Units)
                    {
                        int gridIndex = CursorRow * GridColumns + CursorCol;
                        if (gridIndex < RosterCount)
                        {
                            _savedPartyRow = CursorRow;
                            _savedPartyCol = CursorCol;
                            // Ramza is always display-position 0 under the
                            // default "Time Recruited" sort. Under other
                            // sort modes (Level / Job) Ramza can appear
                            // elsewhere; if/when we support those, resolve
                            // IsRamza via a roster lookup by slot identity
                            // instead of grid position.
                            IsRamza = gridIndex == 0;
                            CurrentScreen = GameScreen.CharacterStatus;
                            SidebarIndex = 0;
                        }
                    }
                    else if (Tab == PartyTab.Chronicle)
                    {
                        CurrentScreen = ChronicleIndex switch
                        {
                            0 => GameScreen.ChronicleEncyclopedia,
                            1 => GameScreen.ChronicleStateOfRealm,
                            2 => GameScreen.ChronicleEvents,
                            3 => GameScreen.ChronicleAuracite,
                            4 => GameScreen.ChronicleReadingMaterials,
                            5 => GameScreen.ChronicleCollection,
                            6 => GameScreen.ChronicleErrands,
                            7 => GameScreen.ChronicleStratagems,
                            8 => GameScreen.ChronicleLessons,
                            9 => GameScreen.ChronicleAkademicReport,
                            _ => GameScreen.PartyMenu
                        };
                    }
                    else if (Tab == PartyTab.Options && OptionsIndex == 2)
                    {
                        // Settings is the only Options entry that opens a nested
                        // screen we model here. Save/Load/ReturnToTitle/ExitGame
                        // trigger their own flows handled outside the menu tree.
                        CurrentScreen = GameScreen.OptionsSettings;
                    }
                    break;
            }
        }

        // --- Chronicle grid navigation (3-4-3 layout, flat index 0..9) ---
        // Layout:
        //   row 0 (3 cols): 0 Encyclopedia      1 StateOfRealm    2 Events
        //   row 1 (4 cols): 3 Auracite          4 Reading         5 Collection      6 Errands
        //   row 2 (3 cols): 7 Stratagems        8 Lessons         9 AkademicReport
        // Down-mapping confirmed live 2026-04-14:
        //   Encyc(0)→Auracite(3), SoR(1)→Reading(4), Events(2)→Collection(5),
        //   Auracite(3)→Stratagems(7), Reading(4)→Lessons(8), Collection(5)→Akademic(9),
        //   Errands(6)→Akademic(9) (last col wraps left since row 2 is shorter).
        // Up-mapping is the inverse. Left/Right within a row, no wrap.
        private static int ChronicleDown(int idx) => idx switch
        {
            0 => 3, 1 => 4, 2 => 5,
            3 => 7, 4 => 8, 5 => 9, 6 => 9,
            _ => idx // bottom row stays
        };
        private static int ChronicleUp(int idx) => idx switch
        {
            3 => 0, 4 => 1, 5 => 2, 6 => 2, // Errands up → Events (col 2)
            7 => 3, 8 => 4, 9 => 5,
            _ => idx // top row stays
        };
        private static int ChronicleLeft(int idx) => idx switch
        {
            1 => 0, 2 => 1,
            4 => 3, 5 => 4, 6 => 5,
            8 => 7, 9 => 8,
            _ => idx // leftmost stays
        };
        private static int ChronicleRight(int idx) => idx switch
        {
            0 => 1, 1 => 2,
            3 => 4, 4 => 5, 5 => 6,
            7 => 8, 8 => 9,
            _ => idx // rightmost stays
        };

        public static string ChronicleIndexToName(int idx) => idx switch
        {
            0 => "Encyclopedia",
            1 => "State of the Realm",
            2 => "Events",
            3 => "Auracite",
            4 => "Reading Materials",
            5 => "Collection",
            6 => "Errands",
            7 => "Stratagems for Battle",
            8 => "Lessons in Leadership",
            9 => "Akademic Report",
            _ => "Unknown"
        };

        public static string OptionsIndexToName(int idx) => idx switch
        {
            0 => "Save",
            1 => "Load",
            2 => "Settings",
            3 => "Return to Title",
            4 => "Exit Game",
            _ => "Unknown"
        };

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
            // On the ability picker: Enter EQUIPS the highlighted ability but
            // keeps the picker OPEN (game shows a checkmark next to the new
            // equipped entry, cursor stays). Only Escape actually closes the
            // picker and returns to EquipmentAndAbilities. Logged in TODO §0
            // 2026-04-14 session 13 — previously this handler treated both
            // keys as close events, causing the state machine to desync from
            // the real game after the fft.sh helpers Select'd an ability.
            switch (vk)
            {
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.EquipmentScreen;
                    CursorRow = _savedEquipmentRow;
                    CursorCol = _savedEquipmentCol;
                    GridColumns = 2;
                    GridRows = 5;
                    break;
                case VK_RETURN:
                    // Equip-in-place: no screen change, no cursor reset.
                    // The picker is still open. The caller must send Escape
                    // to close it (or another Enter to re-equip/unequip).
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
                // JobScreen grid wraps on all axes (FFT UI convention).
                case VK_UP:
                    CursorRow = CursorRow > 0 ? CursorRow - 1 : GridRows - 1;
                    // Ramza row 0 is wider (8 cols), rows 1-2 are narrower
                    AdjustJobGridColumns();
                    break;
                case VK_DOWN:
                    CursorRow = CursorRow < GridRows - 1 ? CursorRow + 1 : 0;
                    AdjustJobGridColumns();
                    break;
                case VK_LEFT:
                    CursorCol = CursorCol > 0 ? CursorCol - 1 : GridColumns - 1;
                    break;
                case VK_RIGHT:
                    CursorCol = CursorCol < GridColumns - 1 ? CursorCol + 1 : 0;
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
                    {
                        CurrentScreen = GameScreen.JobChangeConfirmation;
                        JobChangeConfirmSelected = false; // default to Cancel (safe)
                    }
                    else
                    {
                        CurrentScreen = GameScreen.JobScreen;
                    }
                    break;
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.JobScreen;
                    break;
            }
        }

        private void HandleJobChangeConfirmation(int vk)
        {
            switch (vk)
            {
                case VK_LEFT:
                    JobChangeConfirmSelected = false; // Cancel
                    break;
                case VK_RIGHT:
                    JobChangeConfirmSelected = true; // Confirm
                    break;
                case VK_RETURN:
                case VK_ESCAPE:
                    CurrentScreen = GameScreen.CharacterStatus;
                    SidebarIndex = 0; // Returns to sidebar with Equipment selected
                    JobChangeConfirmSelected = false; // reset
                    break;
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

        /// <summary>
        /// Keeps the cursor within the populated portion of the roster. If the
        /// cursor lands on an empty cell past the last unit, wraps according to
        /// the direction of the move that caused it (verified live 2026-04-15):
        ///   - Right past last unit → col 0 same row (if row exists), else r0c0.
        ///   - Down past last unit → r0 same col.
        ///   - Up/Left: the wrap already moved us into a populated row/col in
        ///     most cases; fallback is last-populated cell.
        /// </summary>
        private void ClampCursorToRoster(ClampDirection dir = ClampDirection.None)
        {
            int gridIndex = CursorRow * GridColumns + CursorCol;
            if (gridIndex < RosterCount) return;

            if (dir == ClampDirection.Right && CursorRow * GridColumns < RosterCount)
            {
                // Past last unit on this row → wrap to col 0 same row.
                CursorCol = 0;
                return;
            }
            if (dir == ClampDirection.Down)
            {
                // Past last unit going down → wrap to r0 same col.
                CursorRow = 0;
                return;
            }
            // Fallback (Up/Left/None or pathological): last valid position.
            gridIndex = RosterCount - 1;
            CursorRow = gridIndex / GridColumns;
            CursorCol = gridIndex % GridColumns;
        }

        private enum ClampDirection { None, Right, Left, Up, Down }

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
                // Chronicle sub-screens: only the boundary is modelled. Inner-state
                // navigation (Encyclopedia tabs, scrollable lists, etc.) is deferred
                // — see TODO §10.7. Each sub-screen surfaces only Escape back.
                GameScreen.ChronicleEncyclopedia or
                GameScreen.ChronicleStateOfRealm or
                GameScreen.ChronicleEvents or
                GameScreen.ChronicleAuracite or
                GameScreen.ChronicleReadingMaterials or
                GameScreen.ChronicleCollection or
                GameScreen.ChronicleErrands or
                GameScreen.ChronicleStratagems or
                GameScreen.ChronicleLessons or
                GameScreen.ChronicleAkademicReport => new List<ValidAction>
                {
                    new() { Key = "escape", Vk = VK_ESCAPE, Description = "Back to Chronicle tab", ResultScreen = "partymenuchronicle" },
                },
                GameScreen.OptionsSettings => new List<ValidAction>
                {
                    new() { Key = "escape", Vk = VK_ESCAPE, Description = "Back to Options tab", ResultScreen = "partymenuoptions" },
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
            else if (Tab == PartyTab.Chronicle)
            {
                string highlighted = ChronicleIndexToName(ChronicleIndex);
                string resultScreen = ChronicleIndex switch
                {
                    0 => "chronicleencyclopedia",
                    1 => "chroniclestateofrealm",
                    2 => "chronicleevents",
                    3 => "chronicleauracite",
                    4 => "chroniclereadingmaterials",
                    5 => "chroniclecollection",
                    6 => "chronicleerrands",
                    7 => "chroniclestratagems",
                    8 => "chroniclelessons",
                    9 => "chronicleakademicreport",
                    _ => "partymenuchronicle"
                };
                actions.Add(new ValidAction
                {
                    Key = "enter", Vk = VK_RETURN,
                    Description = $"Open {highlighted}",
                    ResultScreen = resultScreen
                });
            }
            else if (Tab == PartyTab.Options)
            {
                string highlighted = OptionsIndexToName(OptionsIndex);
                string? resultScreen = OptionsIndex switch
                {
                    2 => "optionssettings",
                    _ => null // Save/Load/ReturnToTitle/ExitGame have their own flows
                };
                actions.Add(new ValidAction
                {
                    Key = "enter", Vk = VK_RETURN,
                    Description = $"Confirm {highlighted}",
                    ResultScreen = resultScreen
                });
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
