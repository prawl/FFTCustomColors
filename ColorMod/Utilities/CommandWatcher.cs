using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FFTColorCustomizer.GameBridge;

namespace FFTColorCustomizer.Utilities
{
    public class CommandWatcher : IDisposable
    {
        private readonly IInputSimulator _inputSimulator;
        private readonly string _bridgeDirectory;
        private readonly string _commandFilePath;
        private readonly string _responseFilePath;

        private FileSystemWatcher? _watcher;
        private string _lastProcessedCommandId = "";
        private readonly object _processingLock = new();
        private bool _disposed;

        // Game bridge components (optional, set after initialization)
        public GameStateReporter? StateReporter { get; set; }
        public MemoryExplorer? Explorer { get; set; }
        public ScreenStateMachine? ScreenMachine { get; set; }

        /// <summary>
        /// Background monitor that observes user-typed keys on the game
        /// window and forwards them to the SM so manual input doesn't
        /// desync our state tracking. Set during bootstrap. Bridge-sent
        /// keys call <c>MarkBridgeSent</c> on it to avoid double-counting.
        /// </summary>
        public FFTColorCustomizer.GameBridge.UserInputMonitor? UserInputMonitor { get; set; }
        public BattleTracker? BattleTracker { get; set; }
        public BattleStatTracker? StatTracker { get; set; }
        public EventScriptLookup? ScriptLookup { get; set; }
        public RumorLookup? RumorLookup { get; set; }

        // Remembered screen name, used by BattleLifecycleClassifier to
        // edge-trigger StartBattle / EndBattle on transitions. Updated
        // once per DetectScreen dispatch in HandleBattleLifecycle.
        private string? _lastClassifiedScreen;
        internal readonly DialogueProgressTracker _dialogueTracker = new();
        private NavigationActions? _navActions;
        private MapLoader? _mapLoader;
        private RosterReader? _rosterReader;
        private NameTableLookup? _rosterNameTable;
        private HoveredUnitArray? _hoveredArray;
        private HpMpCache? _hpMpCache;
        // TODO(audit session 47): PickerListReader is declared but never wired
        // into any dispatch path. If it gets wired, it MUST add a per-call guard
        // before the inner 32-iteration SearchBytesInAllMemory loop (see
        // HoveredUnitArray._discoveryAttempted pattern, session 46 fix).
        private PickerListReader? _pickerListReader;
        private readonly BattleTurnTracker _turnTracker = new();
        private bool _movedThisTurn;
        // Symmetric "we just acted this turn" flag used to override the
        // battleActed byte when it transiently reads 0 after a confirmed
        // action (known byte-drift issue — see TODO §0 Phase 3 memory
        // hunts). Without this, EffectiveMenuCursor can't correct the
        // post-action stale cursor read and ui= shows "Abilities" when
        // the game already moved the cursor back to "Move". Reset at the
        // same turn-boundary sites as _movedThisTurn.
        private bool _actedThisTurn;
        private int _postMoveX = -1, _postMoveY = -1; // confirmed position after battle_move
        private int _lastLoggedCursor = -1; // for UI cursor change logging

        /// <summary>
        /// Count of consecutive DetectScreen calls where the state machine
        /// thinks we're on an inner panel (EquipmentScreen / picker) but the
        /// menu-depth byte reads 0 (outer). Used to debounce the drift-
        /// recovery snap so it doesn't false-trigger during the brief
        /// render lag right after a panel-opening Enter.
        /// </summary>
        private int _menuDepthDriftStreak = 0;
        // Mirror counter for the upward drift case: state machine still
        // reports a party-tree screen but raw + MenuDepth show we're back
        // on WorldMap. Same 3-frame debounce logic.
        private int _worldMapDriftStreak = 0;
        private bool _waitConfirmPending; // Set when battle_wait rejected for no move/act; next battle_wait goes through
        private string? _lastAbilityName; // Last ability used via battle_ability, shown in ui= during targeting
        private readonly BattleMenuTracker _battleMenuTracker = new();
        private HashSet<int>? _cachedLearnedAbilityIds;
        private string? _cachedPrimarySkillset;
        private string? _cachedSecondarySkillset;
        // Active unit's Support ability name. Used to detect Reequip /
        // Evasive Stance which add a Reequip / Defend command row to
        // the in-battle Abilities submenu (per
        // SupportAbilityBattleCommand). Cleared on turn boundaries
        // alongside the other cached unit fields.
        private string? _cachedSupportAbility;

        // Active unit snapshot captured at turn start (first scan_move of each
        // friendly turn). Shell renders these on the compact battle line so
        // Claude knows whose turn it is without a re-scan. Cleared on turn reset.
        private string? _cachedActiveUnitName;
        private string? _cachedActiveUnitJob;
        private int _cachedActiveUnitX = -1;
        private int _cachedActiveUnitY = -1;
        private int _cachedActiveUnitHp;
        private int _cachedActiveUnitMaxHp;
        // S60: weapon banner tag (e.g. "Chaos Blade onHit:chance to add Stone").
        // Populated from the active unit's equipment list via ItemData.ComposeWeaponTag.
        // Empty string when unarmed or equipment unknown.
        private string? _cachedActiveUnitWeaponTag;

        /// <summary>
        /// When true, game actions must go through validPaths. Raw key presses and
        /// actions not in the current screen's validPaths are blocked.
        /// Info actions (scan_move, screen, memory reads) are always allowed.
        /// </summary>
        // S58: re-enabled by default. S56+S57 stabilized menu navigation
        // (escape-to-known-state, submenu retry, post-cast settle). Raw
        // key input should be opt-in via `strict 0` when the caller
        // genuinely needs it, not the default. See Instructions/Rules.md:
        // "Always enable strict mode for play sessions."
        public bool StrictMode { get; set; } = true;

        /// <summary>
        /// When true, any command that sends keys or game actions is blocked unless
        /// the previous command was a state query (no-op key press, read, or infrastructure action).
        /// Forces Claude to check state before every action.
        /// </summary>
        public bool RequireStateCheck { get; set; } = false;
        private bool _lastCommandWasQuery = false;

        /// <summary>
        /// Minimum ms between consecutive game-affecting commands. Enforced by
        /// sleeping the newer command until the floor is met. Prevents
        /// `&&`-chained shell commands from racing the game's key-input handler
        /// (which drops presses that arrive during tab-switch / menu-open
        /// animations). Observational commands (screen queries, memory reads,
        /// snapshots) are exempt. Batched `keys:[...]` commands pace themselves
        /// internally via DelayBetweenMs and don't trip this.
        /// </summary>
        private const int ChainFloorMs = 250;
        private DateTime _lastGameCommandCompletedAt = DateTime.MinValue;

        // CharacterStatusLeakGuard state — S58 §0: holds the screen name
        // from the PREVIOUS settled DetectScreen so we can filter a
        // spurious CharacterStatus/CombatSets flicker during battle_wait
        // animations. See GameBridge/CharacterStatusLeakGuard.cs.
        private string? _previousSettledScreen;

        // WorldMapBattleResidueClassifier state — tracks the tick and name
        // of the most recent Detect() call that returned a Battle* state.
        // Used to suppress WorldMap false positives that flicker during
        // enemy-turn animations. -1 = no battle state seen yet.
        private long _lastBattleStateTickMs = -1;
        private string? _lastBattleStateName;

        // FreshBattleMyTurnEntryClassifier state — tracks the screen name
        // from the PREVIOUS Detect() call so we can identify fresh-entry
        // transitions into BattleMyTurn (turn-boundary states where the
        // game resets the action-menu cursor). On fresh entry we write
        // 0 to 0x1407FC620 so the byte reflects the game's reset state.
        // See PROPOSAL_menucursor_drift.md for the full design.
        private string? _prevDetectedScreenName;

        /// <summary>
        /// Cached picker-cursor address resolved by the `resolve_picker_cursor`
        /// action. Set when state machine enters a picker; used to surface
        /// `ui=<highlighted ability>` by reading the byte and indexing into the
        /// picker's `availableAbilities` list. Heap addresses shuffle across
        /// game sessions — this MUST be re-resolved each picker open.
        /// </summary>
        private long _resolvedPickerCursorAddr = 0L;

        /// <summary>
        /// Cached JobSelection grid-cursor address resolved by the
        /// `resolve_job_cursor` action. Same heap-shuffle story as the
        /// picker — re-resolved on every JobSelection open. Byte value is a
        /// flat linear index into JobGridLayout (0..18 for Ramza Ch4).
        /// </summary>
        private long _resolvedJobCursorAddr = 0L;

        /// <summary>
        /// Tracks whether the auto-resolver has ALREADY fired for the
        /// current JobSelection visit (whether it succeeded or failed).
        /// Prevents the resolver from re-firing on every subsequent
        /// `screen` call — which would fire 6 keys each time, interfering
        /// with user navigation. Reset to false when screen transitions
        /// away from JobSelection AND on every Up/Down key (the JobSelection
        /// widget heap reallocates per row cross — confirmed live: a
        /// resolved address was 0x11EC34D3C, after a single Down it
        /// shuffled to 0x1370CF4A0).
        /// </summary>
        private bool _jobCursorResolveAttempted = false;

        /// <summary>
        /// Invalidates the cached JobSelection cursor address when the
        /// player presses Up or Down while on JobSelection. The
        /// underlying widget allocates a fresh memory block per row,
        /// so the cached pointer stops being valid the moment a vertical
        /// movement key fires. Horizontal movement (Left/Right within a
        /// row) doesn't trigger this — those reads stay reliable.
        /// </summary>
        private void InvalidateJobCursorOnRowCross(int vk)
        {
            const int VK_UP = 0x26, VK_DOWN = 0x28;
            if (vk != VK_UP && vk != VK_DOWN) return;
            if (ScreenMachine == null) return;
            if (ScreenMachine.CurrentScreen != GameScreen.JobScreen) return;
            _resolvedJobCursorAddr = 0L;
            _jobCursorResolveAttempted = false;
        }

        /// <summary>
        /// Cached PartyMenu (Units tab) grid-cursor address. Same
        /// heap-shuffle story as the picker and JobSelection cursors —
        /// re-resolved on every PartyMenu entry. Byte value is the
        /// flat linear index into the 5-col roster grid
        /// (<c>row * 5 + col</c>), capped by roster size.
        /// </summary>
        private long _resolvedPartyMenuCursorAddr = 0L;

        /// <summary>
        /// Cached BattlePaused menu cursor address — heap byte tracking the
        /// 0..5 cursor row across the 6-item pause menu (Data, Retry, Load,
        /// Settings, ReturnToWorldMap, ReturnToTitle). Shuffles across game
        /// restarts per memory note `project_battle_pause_cursor.md`.
        /// Session 44 found it via the triple-diff technique; this field
        /// caches the resolved address for the current BattlePaused visit.
        /// </summary>
        private long _resolvedBattlePauseCursorAddr = 0L;
        private bool _battlePauseCursorResolveAttempted = false;

        /// <summary>
        /// Cached TavernRumors / TavernErrands cursor address — heap byte
        /// tracking the cursor row within the rumor/errand list. Shuffles
        /// across game restarts per memory note `project_tavern_rumor_cursor.md`.
        /// Cleared when leaving the tavern screen so re-entry re-resolves.
        /// </summary>
        private long _resolvedTavernCursorAddr = 0L;
        private bool _tavernCursorResolveAttempted = false;

        /// <summary>
        /// Tracks whether the auto-resolver has ALREADY fired for the
        /// current PartyMenu visit. Same role as
        /// <see cref="_jobCursorResolveAttempted"/> — prevents re-firing
        /// the 6 oscillation keys on every subsequent <c>screen</c> call.
        /// </summary>
        private bool _partyMenuCursorResolveAttempted = false;

        /// <summary>
        /// Invalidates the cached PartyMenu cursor on any Up/Down/Left/Right
        /// while on the Units tab. Unlike JobSelection where only Up/Down
        /// forces a re-resolve, PartyMenu's widget pattern is not yet
        /// characterized — so we clear on every directional key. This is
        /// the conservative option: a spurious re-resolve costs ~2s of cursor
        /// flash; a stale address produces wrong-unit decisions
        /// (the drift this whole resolver targets). Enter/Escape don't
        /// trigger — those transition screens, which has its own cleanup path.
        /// </summary>
        private void InvalidatePartyMenuCursorOnMove(int vk)
        {
            const int VK_UP = 0x26, VK_DOWN = 0x28, VK_LEFT = 0x25, VK_RIGHT = 0x27;
            if (vk != VK_UP && vk != VK_DOWN && vk != VK_LEFT && vk != VK_RIGHT) return;
            if (ScreenMachine == null) return;
            if (ScreenMachine.CurrentScreen != GameScreen.PartyMenuUnits) return;
            if (ScreenMachine.Tab != PartyTab.Units) return;
            _resolvedPartyMenuCursorAddr = 0L;
            _partyMenuCursorResolveAttempted = false;
        }

        /// <summary>
        /// Cached equipment-picker row cursor byte. Live-resolved 2026-04-15
        /// session 16 at heap `0x12ECCF6B0` (plus 3 aliased copies at +0x78,
        /// +0xE0, +0x120). Same heap-shuffle story as other UE4 widget
        /// cursors — re-resolved per picker open and after every Up/Down/A/D
        /// while the picker is active.
        /// </summary>
        private long _resolvedEquipPickerCursorAddr = 0L;

        /// <summary>
        /// One-shot latch for the equipment-picker resolver, mirroring
        /// <see cref="_jobCursorResolveAttempted"/>. Cleared on picker exit
        /// and on every directional / tab-cycle key so the next screen call
        /// triggers a fresh resolve.
        /// </summary>
        private bool _equipPickerCursorResolveAttempted = false;

        /// <summary>
        /// Resolved heap address of the EquipmentAndAbilities outer panel
        /// column 0 (equipment column) cursor row byte. Tracks which row
        /// 0..5 (Weapon/LHand/Shield/Helm/Body/Accessory) the cursor is on
        /// while the outer EqA panel is active (NOT the inner picker).
        /// Same heap-shuffle story as the picker cursor — resolved on
        /// every EqA entry and invalidated on every Up/Down/Left/Right
        /// while EqA is active. Zero when not resolved. Live-verified
        /// 2026-04-15 session 19: addresses in 0x7FFDD006F9xx range
        /// appear with 6-step monotonic ascending trajectory on a
        /// 6-snapshot Down sequence, but drift within seconds. Resolver
        /// re-runs per entry to keep the address fresh.
        /// </summary>
        private long _resolvedEqaColumnCursorAddr = 0L;

        /// <summary>
        /// One-shot latch for the EqA outer column 0 cursor resolver.
        /// Cleared on EqA exit and on every directional key while EqA
        /// is active.
        /// </summary>
        private bool _eqaColumnCursorResolveAttempted = false;

        /// <summary>
        /// One-shot latch for the DoEqaRowResolve auto-fire on EqA entry.
        /// The mirror-diff resolver is expensive (~2s, 3-4 keypresses) so
        /// we only run it ONCE per EqA entry cycle — right after detection
        /// first reports EquipmentAndAbilities. After it sets the
        /// ScreenMachine EquipmentCursor, Up/Down key tracking keeps
        /// `ui=` fresh until we leave EqA (screen transition clears
        /// this flag). Fixes the stale `ui=Right Hand (none)` surface
        /// when the SM row drifts on entry.
        /// </summary>
        private bool _eqaRowAutoResolveAttempted = false;
        // Tracks the previous DetectScreen's MenuDepth while we're on EqA, so
        // we can re-fire the row resolver after a picker opens and closes.
        // 0 = no prior read (start of EqA session). See session 24 carryover
        // in TODO §0 for the drift scenario this addresses.
        private int _lastEqaMenuDepth = 0;

        /// <summary>
        /// Invalidates the cached equipment-picker cursor on any
        /// Up/Down/A/D while on an Equippable* picker screen. Up/Down shift
        /// the row index; A/D cycle tabs (which re-roll the underlying list
        /// and typically reallocate the widget). Enter/Escape transition
        /// screens — cleanup handled by the on-exit path in the screen
        /// handler below.
        /// </summary>
        private void InvalidateEquipPickerCursorOnMove(int vk)
        {
            const int VK_UP = 0x26, VK_DOWN = 0x28, VK_A = 0x41, VK_D = 0x44;
            if (vk != VK_UP && vk != VK_DOWN && vk != VK_A && vk != VK_D) return;
            if (ScreenMachine == null) return;
            if (!IsOnEquipPicker(ScreenMachine.CurrentScreen)) return;
            _resolvedEquipPickerCursorAddr = 0L;
            _equipPickerCursorResolveAttempted = false;
        }

        /// <summary>
        /// True when the state machine is on any of the five equipment
        /// pickers. Centralizes the set so new picker types only need one
        /// touchpoint.
        /// </summary>
        private static bool IsOnEquipPicker(GameScreen s)
            => s == GameScreen.EquipmentItemList; // state-machine routes all 5 slot pickers through one internal screen

        /// <summary>
        /// Invalidates the cached EqA outer-panel column 0 cursor on any
        /// Up/Down/Left/Right/Enter while the state machine thinks we're
        /// on EquipmentAndAbilities. Up/Down shifts the row; Left/Right
        /// shifts between equipment and ability columns (which changes
        /// which column the resolver should track next); Enter opens a
        /// picker (resolved address becomes irrelevant).
        /// </summary>
        private void InvalidateEqaColumnCursorOnMove(int vk)
        {
            const int VK_UP = 0x26, VK_DOWN = 0x28, VK_LEFT = 0x25, VK_RIGHT = 0x27, VK_RETURN = 0x0D;
            if (vk != VK_UP && vk != VK_DOWN && vk != VK_LEFT && vk != VK_RIGHT && vk != VK_RETURN) return;
            if (ScreenMachine == null) return;
            if (ScreenMachine.CurrentScreen != GameScreen.EquipmentScreen) return;
            _resolvedEqaColumnCursorAddr = 0L;
            _eqaColumnCursorResolveAttempted = false;
        }

        /// <summary>
        /// Pure helper for the chained-command rate-limit decision. Returns the
        /// number of ms the caller should sleep before processing a new command
        /// (0 if no delay needed). Returns a chain-warning string only when a
        /// delay is actually required. Kept static + pure so it's unit-testable.
        /// </summary>
        public static (int SleepMs, string? Warning) ComputeChainDelay(
            bool isObservational,
            DateTime lastCommandCompletedAt,
            DateTime now,
            int floorMs)
        {
            if (isObservational) return (0, null);
            if (lastCommandCompletedAt == DateTime.MinValue) return (0, null);
            var elapsed = (now - lastCommandCompletedAt).TotalMilliseconds;
            if (elapsed >= floorMs) return (0, null);
            var sleepMs = (int)System.Math.Ceiling(floorMs - elapsed);
            var warning = $"auto-delayed {sleepMs}ms (prev game command {(int)elapsed}ms ago; floor={floorMs}ms). Use keys:[...] batch for multi-key flows instead of chaining with &&.";
            return (sleepMs, warning);
        }

        /// <summary>
        /// Rescan-on-entry for picker cursors that live in UE4 heap (raw
        /// addresses shuffle across game sessions — verified 2026-04-15 three
        /// launches got three different live addresses for the same widget).
        /// Flow:
        ///   1. Baseline heap snapshot.
        ///   2. Send Down key → game advances cursor by 1.
        ///   3. Heap snapshot (advanced).
        ///   4. Send Up key → game restores cursor.
        ///   5. Heap snapshot (back).
        ///   6. Intersect for clean (a→b, b→a, |Δ|=1) toggles.
        ///   7. Verify: fire one more Down, confirm the chosen byte
        ///      incremented by 1 (kills false positives that satisfied the
        ///      toggle filter coincidentally — animation counters, sibling
        ///      widget state, etc).
        ///   8. Restore cursor to original position with one Up.
        /// Caches the winner on _resolvedPickerCursorAddr; returns a human
        /// info string for logging, or null on setup failure.
        /// Side effects: 2 net-zero key pairs fired on the game window (cursor
        /// visibly flashes Down/Up/Down/Up for ~880ms). Noticeable but
        /// acceptable given no cleaner option without a stable pointer chain.
        /// </summary>
        private string? ResolvePickerCursor(out int candidateCount)
        {
            candidateCount = 0;
            if (Explorer == null) return null;
            IntPtr win = Process.GetCurrentProcess().MainWindowHandle;
            if (win == IntPtr.Zero) return null;
            const int VK_DOWN = 0x28, VK_UP = 0x26;

            // Let picker-open animation fully settle before baselining.
            // Without this, the baseline snap captures mid-animation bytes that
            // coincidentally satisfy the toggle filter and outrank the real cursor.
            // 700ms empirically chosen — picker-open has a fade-in of ~400ms and
            // a cursor-rest settle of ~200ms on top.
            Thread.Sleep(700);
            Explorer.TakeHeapSnapshot("_picker_base");
            _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
            Thread.Sleep(300);  // slightly longer — some widgets lag 1 frame
            Explorer.TakeHeapSnapshot("_picker_adv");
            _inputSimulator.SendKeyPressToWindow(win, VK_UP);
            Thread.Sleep(300);
            Explorer.TakeHeapSnapshot("_picker_back");
            var cands = Explorer.FindToggleCandidates("_picker_base", "_picker_adv", "_picker_back", maxResults: 32, expectedDelta: 1);
            candidateCount = cands.Count;

            // Verify candidates with a stricter two-step test: fire Down (cursor
            // must be now X+1 where X was the baseline), then fire Down AGAIN
            // (cursor must be X+2). A simple one-Down verify lets animation
            // counters through since they happen to increment too; two-in-a-row
            // filters them because they typically reset between presses or
            // advance on a different pattern. Finally restore position with 2 Ups.
            long verified = 0L;
            if (cands.Count > 0)
            {
                var baselineVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) baselineVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(300);
                var afterOneVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) afterOneVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(300);
                foreach (var candAddr in cands)
                {
                    if (!baselineVals.TryGetValue((long)candAddr, out var baseline)) continue;
                    if (!afterOneVals.TryGetValue((long)candAddr, out var afterOne)) continue;
                    var afterTwo = Explorer.ReadAbsolute(candAddr, 1);
                    if (!afterTwo.HasValue) continue;
                    byte finalVal = (byte)afterTwo.Value.value;
                    // Must track: baseline → baseline+1 → baseline+2 (with mod wrap).
                    // Allows wrap to 0 from list-end.
                    bool step1Ok = afterOne == baseline + 1 || afterOne == 0;
                    bool step2Ok = finalVal == afterOne + 1 || finalVal == 0;
                    if (step1Ok && step2Ok)
                    {
                        verified = (long)candAddr;
                        break;
                    }
                }
                // Restore cursor to original position: 2 Ups to undo the 2 Downs.
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(220);
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(220);
            }
            _resolvedPickerCursorAddr = verified;
            return cands.Count > 0
                ? $"Resolved picker cursor: 0x{_resolvedPickerCursorAddr:X} ({cands.Count} candidate{(cands.Count > 1 ? "s" : "")})"
                : "No stable picker cursor byte found (0 candidates)";
        }

        /// <summary>
        /// Rescan-on-entry for the BattlePaused menu cursor. 6-item menu
        /// (Data / Retry / Load / Settings / ReturnToWorldMap /
        /// ReturnToTitleScreen). Byte is on the heap and shuffles across
        /// game restarts — session 44 hunt documented in
        /// `project_battle_pause_cursor.md`. Oscillates Down/Up like
        /// <see cref="ResolvePickerCursor"/>. Net-zero cursor motion after
        /// restore. Visible ~2s flash through the menu.
        /// </summary>
        private string? ResolveBattlePauseCursor(out int candidateCount)
        {
            candidateCount = 0;
            if (Explorer == null) return null;
            IntPtr win = Process.GetCurrentProcess().MainWindowHandle;
            if (win == IntPtr.Zero) return null;
            const int VK_DOWN = 0x28, VK_UP = 0x26;

            // Pause menu opens snappier than pickers — 400ms settle is enough
            // per live observations. But we need the widget state to stabilize
            // before baselining or we catch transient animation bytes.
            Thread.Sleep(400);
            Explorer.TakeHeapSnapshot("_bpause_base");
            _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
            Thread.Sleep(250);
            Explorer.TakeHeapSnapshot("_bpause_adv");
            _inputSimulator.SendKeyPressToWindow(win, VK_UP);
            Thread.Sleep(250);
            Explorer.TakeHeapSnapshot("_bpause_back");
            var cands = Explorer.FindToggleCandidates("_bpause_base", "_bpause_adv", "_bpause_back", maxResults: 32, expectedDelta: 1);
            candidateCount = cands.Count;

            // Two-step verify matching ResolvePickerCursor exactly. The
            // first candidate that passes `baseline → baseline+1 → baseline+2`
            // (with wrap-to-0 allowed) wins. BattlePaused yields ~32
            // toggle candidates in practice vs ~4 for pickers, so the first
            // accepted may not be the real cursor — see TODO entry for
            // known-limitation follow-up.
            long verified = 0L;
            if (cands.Count > 0)
            {
                var baselineVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) baselineVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(250);
                var afterOneVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) afterOneVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(250);
                foreach (var candAddr in cands)
                {
                    if (!baselineVals.TryGetValue((long)candAddr, out var baseline)) continue;
                    if (!afterOneVals.TryGetValue((long)candAddr, out var afterOne)) continue;
                    var afterTwo = Explorer.ReadAbsolute(candAddr, 1);
                    if (!afterTwo.HasValue) continue;
                    byte finalVal = (byte)afterTwo.Value.value;
                    bool step1Ok = afterOne == baseline + 1 || afterOne == 0;
                    bool step2Ok = finalVal == afterOne + 1 || finalVal == 0;
                    if (step1Ok && step2Ok)
                    {
                        verified = (long)candAddr;
                        break;
                    }
                }
                // Restore: 2 Ups to undo the 2 Downs.
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(200);
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(200);
            }
            _resolvedBattlePauseCursorAddr = verified;
            return cands.Count > 0
                ? $"Resolved BattlePaused cursor: 0x{_resolvedBattlePauseCursorAddr:X} ({cands.Count} candidate{(cands.Count > 1 ? "s" : "")})"
                : "No stable BattlePaused cursor byte found (0 candidates)";
        }

        /// <summary>
        /// Rescan-on-entry for the TavernRumors / TavernErrands list cursor.
        /// Byte is on the heap and shuffles across game restarts — session 44
        /// hunt documented in `project_tavern_rumor_cursor.md` (Dorter +
        /// Bervenia both showed the byte at widget_base+0x28). Oscillates
        /// Down/Up like <see cref="ResolvePickerCursor"/>. Net-zero cursor
        /// motion after restore. Visible ~2s flash through the list.
        /// </summary>
        private string? ResolveTavernCursor(out int candidateCount)
        {
            candidateCount = 0;
            if (Explorer == null) return null;
            IntPtr win = Process.GetCurrentProcess().MainWindowHandle;
            if (win == IntPtr.Zero) return null;
            const int VK_DOWN = 0x28, VK_UP = 0x26;

            // Tavern list opens fast — 400ms settle.
            Thread.Sleep(400);
            Explorer.TakeHeapSnapshot("_tavern_base");
            _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
            Thread.Sleep(250);
            Explorer.TakeHeapSnapshot("_tavern_adv");
            _inputSimulator.SendKeyPressToWindow(win, VK_UP);
            Thread.Sleep(250);
            Explorer.TakeHeapSnapshot("_tavern_back");
            var cands = Explorer.FindToggleCandidates("_tavern_base", "_tavern_adv", "_tavern_back", maxResults: 32, expectedDelta: 1);
            candidateCount = cands.Count;

            long verified = 0L;
            if (cands.Count > 0)
            {
                var baselineVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) baselineVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(250);
                var afterOneVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) afterOneVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(250);
                foreach (var candAddr in cands)
                {
                    if (!baselineVals.TryGetValue((long)candAddr, out var baseline)) continue;
                    if (!afterOneVals.TryGetValue((long)candAddr, out var afterOne)) continue;
                    var afterTwo = Explorer.ReadAbsolute(candAddr, 1);
                    if (!afterTwo.HasValue) continue;
                    byte finalVal = (byte)afterTwo.Value.value;
                    bool step1Ok = afterOne == baseline + 1 || afterOne == 0;
                    bool step2Ok = finalVal == afterOne + 1 || finalVal == 0;
                    if (step1Ok && step2Ok)
                    {
                        verified = (long)candAddr;
                        break;
                    }
                }
                // Restore: 2 Ups.
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(200);
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(200);
            }
            _resolvedTavernCursorAddr = verified;
            return cands.Count > 0
                ? $"Resolved Tavern cursor: 0x{_resolvedTavernCursorAddr:X} ({cands.Count} candidate{(cands.Count > 1 ? "s" : "")})"
                : "No stable Tavern cursor byte found (0 candidates)";
        }

        /// <summary>
        /// Rescan-on-entry for the JobSelection grid cursor. Same
        /// oscillation+verify+priority strategy as
        /// <see cref="ResolvePickerCursor"/>, but oscillates Right/Left
        /// instead of Down/Up because JobSelection is a horizontal-first
        /// grid and the cursor byte is a flat linear index that advances on
        /// Right. Net-zero cursor motion (visible ~2s flash).
        /// </summary>
        private string? ResolveJobCursor(out int candidateCount)
        {
            candidateCount = 0;
            if (Explorer == null) return null;
            IntPtr win = Process.GetCurrentProcess().MainWindowHandle;
            if (win == IntPtr.Zero) return null;
            const int VK_RIGHT = 0x27, VK_LEFT = 0x25;

            // Let JobSelection open animation settle before baselining.
            // Same 700ms empirical figure as pickers.
            Thread.Sleep(700);
            Explorer.TakeHeapSnapshot("_jobcur_base");
            _inputSimulator.SendKeyPressToWindow(win, VK_RIGHT);
            Thread.Sleep(300);
            Explorer.TakeHeapSnapshot("_jobcur_adv");
            _inputSimulator.SendKeyPressToWindow(win, VK_LEFT);
            Thread.Sleep(300);
            Explorer.TakeHeapSnapshot("_jobcur_back");
            var cands = Explorer.FindToggleCandidates(
                "_jobcur_base", "_jobcur_adv", "_jobcur_back",
                maxResults: 32, expectedDelta: 1);
            candidateCount = cands.Count;

            // Verify with a 2-step delta: Right, Right expects baseline +2.
            // Catches animation counters that happen to satisfy the single-
            // toggle filter. Then restore position with 2 Lefts.
            long verified = 0L;
            if (cands.Count > 0)
            {
                var baselineVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) baselineVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_RIGHT);
                Thread.Sleep(300);
                var afterOneVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) afterOneVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_RIGHT);
                Thread.Sleep(300);
                foreach (var candAddr in cands)
                {
                    if (!baselineVals.TryGetValue((long)candAddr, out var baseline)) continue;
                    if (!afterOneVals.TryGetValue((long)candAddr, out var afterOne)) continue;
                    var afterTwo = Explorer.ReadAbsolute(candAddr, 1);
                    if (!afterTwo.HasValue) continue;
                    byte finalVal = (byte)afterTwo.Value.value;
                    // Must track: baseline → baseline+1 → baseline+2 (with mod wrap).
                    bool step1Ok = afterOne == baseline + 1 || afterOne == 0;
                    bool step2Ok = finalVal == afterOne + 1 || finalVal == 0;
                    if (step1Ok && step2Ok)
                    {
                        verified = (long)candAddr;
                        break;
                    }
                }
                // Restore cursor: 2 Lefts to undo the 2 Rights.
                _inputSimulator.SendKeyPressToWindow(win, VK_LEFT);
                Thread.Sleep(220);
                _inputSimulator.SendKeyPressToWindow(win, VK_LEFT);
                Thread.Sleep(220);

                // Liveness check: confirm the verified byte is still
                // tracking after the oscillation window. Some candidates
                // pass the 2-step verify because they're widget-state
                // counters that increment on cursor CHANGE events during
                // the rapid oscillation, but stay at 0 during real nav
                // (observed session 27). Press Right three times and expect
                // the byte to advance by exactly 3 (with wrap). A 3-step
                // probe is noticeably more selective than a 1-step probe —
                // change-count widgets typically stall after the first press.
                // Restore with 3 Lefts AND verify they return the byte to
                // its pre-probe value. Two-phase liveness:
                //   Phase 1: 3 Rights should advance by +3 (with wrap).
                //   Phase 2: 3 Lefts should reverse that: byte returns to start.
                // Change-count widgets (which track any cursor change) would
                // advance by +6 total instead of returning to baseline, so
                // this catches them that the old 1-direction probe didn't.
                if (verified != 0L)
                {
                    var postRestore = Explorer.ReadAbsolute((nint)verified, 1);
                    byte postRestoreVal = postRestore.HasValue ? (byte)postRestore.Value.value : (byte)0;
                    for (int i = 0; i < 3; i++)
                    {
                        _inputSimulator.SendKeyPressToWindow(win, VK_RIGHT);
                        Thread.Sleep(250);
                    }
                    var afterRight = Explorer.ReadAbsolute((nint)verified, 1);
                    byte afterRightVal = afterRight.HasValue ? (byte)afterRight.Value.value : (byte)0;
                    for (int i = 0; i < 3; i++)
                    {
                        _inputSimulator.SendKeyPressToWindow(win, VK_LEFT);
                        Thread.Sleep(220);
                    }
                    var afterLeft = Explorer.ReadAbsolute((nint)verified, 1);
                    byte afterLeftVal = afterLeft.HasValue ? (byte)afterLeft.Value.value : (byte)0;

                    // Phase 1: after 3 Rights, byte should be baseline + 3 (mod 256).
                    byte expectedRight = (byte)(postRestoreVal + 3);
                    bool phase1Ok = afterRightVal == expectedRight;
                    // Phase 2: after 3 Lefts, byte should return to baseline.
                    bool phase2Ok = afterLeftVal == postRestoreVal;

                    if (!phase1Ok || !phase2Ok)
                    {
                        ModLogger.Log($"[ResolveJobCursor] liveness failed: 0x{verified:X} base={postRestoreVal}→R3={afterRightVal}(expected {expectedRight}) →L3={afterLeftVal}(expected back to {postRestoreVal}). Rejecting.");
                        verified = 0L;
                    }
                }
            }
            _resolvedJobCursorAddr = verified;
            return verified != 0L
                ? $"Resolved job cursor: 0x{_resolvedJobCursorAddr:X} ({cands.Count} candidate{(cands.Count > 1 ? "s" : "")})"
                : cands.Count > 0
                    ? $"No live job cursor byte found ({cands.Count} candidates failed liveness)"
                    : "No stable job cursor byte found (0 candidates)";
        }

        /// <summary>
        /// Rescan-on-entry for the PartyMenu (Units tab) grid cursor.
        /// Mirrors <see cref="ResolveJobCursor"/> in structure but
        /// **probes both axes** — Right/Left oscillation for the column
        /// and Down/Up verification for the row — then keeps only bytes
        /// that advance by +5 on Down (the flat-linear-index signature,
        /// <c>row*5+col</c>). Necessary because the naive Right/Left-only
        /// probe picks up column-only bytes that don't encode the row,
        /// producing a wrong cursor decode past row 0 (live-verified
        /// 2026-04-15 session 16: resolver found a col-only byte on its
        /// first pass).
        ///
        /// Flow:
        ///   1. Baseline snapshot, Right, snapshot, Left, snapshot
        ///      → intersect toggles to find bytes that advance on Right.
        ///   2. Two-step verify (Right, Right): advances +2 → still a
        ///      valid cursor-shape byte (could be col-only OR flat-linear).
        ///   3. Restore with 2 Lefts.
        ///   4. Axis verify: Down, check winner advanced by +5 (flat
        ///      linear) instead of 0 (col-only). Up restores.
        /// Net-zero cursor motion. Visible ~2.5s flash.
        /// Caller MUST gate on <c>screen.Name == "PartyMenuUnits"</c> +
        /// <c>MenuDepth == 0</c> + <c>Tab == Units</c>.
        /// Additional caller precondition: the cursor must be at a
        /// position where Down won't cause a short-grid wrap back to the
        /// same row (wraps defeat the +5 test). Safe if resolver fires
        /// on first PartyMenu entry — cursor is at (0,0) and Down moves
        /// to (1,0) cleanly.
        /// </summary>
        private string? ResolvePartyMenuCursor(out int candidateCount)
        {
            candidateCount = 0;
            if (Explorer == null) return null;
            IntPtr win = Process.GetCurrentProcess().MainWindowHandle;
            if (win == IntPtr.Zero) return null;
            const int VK_RIGHT = 0x27, VK_LEFT = 0x25, VK_DOWN = 0x28, VK_UP = 0x26;

            // Let settle before baselining — 700ms matches JobSelection/pickers.
            Thread.Sleep(700);
            Explorer.TakeHeapSnapshot("_party_base");
            _inputSimulator.SendKeyPressToWindow(win, VK_RIGHT);
            Thread.Sleep(300);
            Explorer.TakeHeapSnapshot("_party_adv");
            _inputSimulator.SendKeyPressToWindow(win, VK_LEFT);
            Thread.Sleep(300);
            Explorer.TakeHeapSnapshot("_party_back");
            var cands = Explorer.FindToggleCandidates(
                "_party_base", "_party_adv", "_party_back",
                maxResults: 32, expectedDelta: 1);
            candidateCount = cands.Count;

            // Two-step horizontal verify: Right, Right expects baseline +2.
            // Narrows candidates to ones whose column genuinely advances.
            var survivors = new List<long>();
            if (cands.Count > 0)
            {
                var baselineVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) baselineVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_RIGHT);
                Thread.Sleep(300);
                var afterOneVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) afterOneVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_RIGHT);
                Thread.Sleep(300);
                foreach (var candAddr in cands)
                {
                    if (!baselineVals.TryGetValue((long)candAddr, out var baseline)) continue;
                    if (!afterOneVals.TryGetValue((long)candAddr, out var afterOne)) continue;
                    var afterTwo = Explorer.ReadAbsolute(candAddr, 1);
                    if (!afterTwo.HasValue) continue;
                    byte finalVal = (byte)afterTwo.Value.value;
                    bool step1Ok = afterOne == baseline + 1 || afterOne == 0;
                    bool step2Ok = finalVal == afterOne + 1 || finalVal == 0;
                    if (step1Ok && step2Ok) survivors.Add((long)candAddr);
                }
                // Restore column to 0: 2 Lefts.
                _inputSimulator.SendKeyPressToWindow(win, VK_LEFT);
                Thread.Sleep(220);
                _inputSimulator.SendKeyPressToWindow(win, VK_LEFT);
                Thread.Sleep(220);
            }

            // Row axis verify: a flat-linear-index byte advances by +5 on
            // Down (5-col grid). A column-only byte advances by 0. Down
            // once, pick the first survivor that advanced by exactly 5.
            // Restore cursor with one Up.
            long verified = 0L;
            if (survivors.Count > 0)
            {
                var preDownVals = new Dictionary<long, byte>();
                foreach (var candAddr in survivors)
                {
                    var r = Explorer.ReadAbsolute((nint)candAddr, 1);
                    if (r.HasValue) preDownVals[candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(300);
                foreach (var candAddr in survivors)
                {
                    if (!preDownVals.TryGetValue(candAddr, out var before)) continue;
                    var after = Explorer.ReadAbsolute((nint)candAddr, 1);
                    if (!after.HasValue) continue;
                    byte afterVal = (byte)after.Value.value;
                    // Flat linear (5-col grid): Down moves index by +5.
                    // Exact equality — no wrap tolerance here (Down from
                    // row 0 lands cleanly on row 1 for any non-empty roster).
                    if (afterVal == before + 5)
                    {
                        verified = candAddr;
                        break;
                    }
                }
                // Restore row with one Up. Always fire even if verify failed
                // (we don't want to strand the player on row 1).
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(220);
            }

            _resolvedPartyMenuCursorAddr = verified;
            if (verified != 0)
                return $"Resolved party-menu cursor: 0x{verified:X} ({cands.Count} candidate{(cands.Count > 1 ? "s" : "")}, {survivors.Count} col-verified)";
            if (survivors.Count > 0)
                return $"Party-menu cursor: {survivors.Count} col-only candidate{(survivors.Count > 1 ? "s" : "")}, no flat-linear byte survived +5-on-Down verify";
            return cands.Count > 0
                ? $"Party-menu cursor: {cands.Count} raw toggle{(cands.Count > 1 ? "s" : "")}, none passed +2 horizontal verify"
                : "No stable party-menu cursor byte found (0 candidates)";
        }

        /// <summary>
        /// Rescan-on-entry for the equipment-picker (EquippableWeapons /
        /// Shields / Headware / CombatGarb / Accessories) row cursor.
        /// Mirrors <see cref="ResolvePickerCursor"/> exactly — the picker
        /// is a vertical list, so Down/Up oscillation + 2-step verify + wrap
        /// tolerance is the right shape. Live-verified 2026-04-15: the
        /// resolved byte tracks row index directly (0 = top, advances by 1
        /// per Down). No horizontal axis (each tab is a linear list). Net-
        /// zero cursor motion, ~2s visible flash.
        /// </summary>
        /// <summary>
        /// Reads a 5-element u16 array from one of the EqA equipment
        /// mirrors (0x141870854, 0x14373B004, 0x143743704) and returns
        /// it as int[] of length 5 (UI row order: Weapon, LHand, Helm,
        /// Body, Accessory). Returns null on read failure.
        ///
        /// See project_eqa_equipment_mirror.md for full context.
        /// </summary>
        private int[]? ReadEqaMirror(long baseAddr)
        {
            if (Explorer == null) return null;
            var arr = new int[5];
            for (int i = 0; i < 5; i++)
            {
                var r = Explorer.ReadAbsolute((nint)(baseAddr + i * 2), 2);
                if (!r.HasValue) return null;
                arr[i] = (int)r.Value.value;
            }
            return arr;
        }

        /// <summary>
        /// Reads the passive ability section of the EqA mirror struct at
        /// offset +0x0E..+0x13. Returns (reactionId, supportId, movementId)
        /// as a tuple — each only non-zero when the equipped-flag byte at
        /// +0x0F/+0x11/+0x13 is set. Returns null on read failure.
        ///
        /// Discovered session 19: mirror struct has {u8 id, u8 flag}
        /// pairs at offsets +0x0E..+0x13. Flag=0 means the slot is empty
        /// regardless of the id byte (game still reads the id, so don't
        /// trust id alone).
        /// </summary>
        private (int reactionId, int supportId, int movementId)? ReadEqaMirrorPassives(long baseAddr)
        {
            if (Explorer == null) return null;
            var reads = new (System.IntPtr addr, int size)[6];
            for (int i = 0; i < 6; i++) reads[i] = ((System.IntPtr)(baseAddr + 0x0E + i), 1);
            var vals = Explorer.ReadMultiple(reads);
            if (vals == null || vals.Length != 6) return null;
            int reactionId  = (int)vals[1] != 0 ? (int)vals[0] : 0; // flag at +0x0F
            int supportId   = (int)vals[3] != 0 ? (int)vals[2] : 0; // flag at +0x11
            int movementId  = (int)vals[5] != 0 ? (int)vals[4] : 0; // flag at +0x13
            return (reactionId, supportId, movementId);
        }

        /// <summary>
        /// Reads the primary/secondary skillset indices from the EqA
        /// mirror struct at +0x0A and +0x0C. Values are raw u8 indices
        /// into CommandWatcher.GetSkillsetName (e.g. 7=Arts of War,
        /// 9=Martial Arts, 10=White Magicks). Returns (0, 0) when no
        /// skillsets are set (shouldn't happen in normal play — Primary
        /// is always set; Secondary may legitimately be 0 for units
        /// without a secondary equipped).
        ///
        /// Verified 2026-04-15 session 19 on Kenrick:
        ///   Arts of War primary → +0x0A = 7
        ///   Martial Arts → White Magicks secondary → +0x0C went 9 → 10
        /// </summary>
        private (int primaryIdx, int secondaryIdx)? ReadEqaMirrorSkillsets(long baseAddr)
        {
            if (Explorer == null) return null;
            var pr = Explorer.ReadAbsolute((nint)(baseAddr + 0x0A), 1);
            var sr = Explorer.ReadAbsolute((nint)(baseAddr + 0x0C), 1);
            if (!pr.HasValue || !sr.HasValue) return null;
            return ((int)pr.Value.value, (int)sr.Value.value);
        }

        /// <summary>True iff two 5-element mirror arrays hold identical
        /// values at every position.</summary>
        private static bool MirrorsAgree(int[]? a, int[]? b)
        {
            if (a == null || b == null || a.Length != 5 || b.Length != 5) return false;
            for (int i = 0; i < 5; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// Verifies the mirror's 5 values match the viewed unit's actual
        /// equipment as read from the stable roster slot. The mirror MAY
        /// be in a different order than the roster (mirror is UI row
        /// order; roster uses a different layout), so we compare as
        /// multisets rather than element-wise. An empty slot in the
        /// mirror (value 0) matches any empty slot in the loadout (null
        /// or 0).
        /// </summary>
        private static bool MirrorMatchesLoadout(int[] mirror, EquipmentReader.Loadout loadout)
        {
            // Collect roster's equipped ids into a multiset, treating
            // unequipped as 0.
            var rosterCounts = new Dictionary<int, int>();
            void Add(int? id)
            {
                int v = id.GetValueOrDefault(0);
                rosterCounts.TryGetValue(v, out int c);
                rosterCounts[v] = c + 1;
            }
            Add(loadout.WeaponId);
            Add(loadout.LeftHandId);
            Add(loadout.ShieldId);     // game stores shields in LHand UI row, but roster has separate shield field
            Add(loadout.HelmId);
            Add(loadout.BodyId);
            Add(loadout.AccessoryId);

            // Collect mirror values into the same shape. Mirror has 5 slots.
            // Because roster has 6 (RH/LH/Shield split) vs mirror's 5
            // (RH/LH-or-Shield combined), we compare by building BOTH as
            // multisets and checking that every NONZERO mirror value
            // appears in the roster multiset.
            var mirrorNonZero = new List<int>();
            foreach (int v in mirror) if (v != 0 && v != 0xFFFF) mirrorNonZero.Add(v);

            foreach (int v in mirrorNonZero)
            {
                if (!rosterCounts.TryGetValue(v, out int c) || c <= 0) return false;
                rosterCounts[v] = c - 1;
            }
            return true;
        }

        /// <summary>
        /// Resolves the heap address tracking the EqA outer panel column 0
        /// (equipment column) cursor row byte. Mirrors the existing
        /// ResolveEquipPickerCursor / ResolveJobCursor pattern: take a
        /// heap snapshot, press Down, snapshot again, press Up, snapshot
        /// a third time, look for bytes that went +1/-1, then verify with
        /// two more Downs that the byte continues to track.
        ///
        /// Gated on the outer EqA panel (state machine reports
        /// EquipmentScreen). Returns an info string for logging, null if
        /// no cursor byte could be verified this attempt. Sets
        /// <see cref="_resolvedEqaColumnCursorAddr"/> on success.
        /// </summary>
        private string? ResolveEqaColumnCursor(out int candidateCount)
        {
            candidateCount = 0;
            if (Explorer == null) return null;
            IntPtr win = Process.GetCurrentProcess().MainWindowHandle;
            if (win == IntPtr.Zero) return null;
            const int VK_DOWN = 0x28, VK_UP = 0x26;

            Thread.Sleep(700);
            Explorer.TakeHeapSnapshot("_eqa_base");
            _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
            Thread.Sleep(300);
            Explorer.TakeHeapSnapshot("_eqa_adv");
            _inputSimulator.SendKeyPressToWindow(win, VK_UP);
            Thread.Sleep(300);
            Explorer.TakeHeapSnapshot("_eqa_back");
            var cands = Explorer.FindToggleCandidates(
                "_eqa_base", "_eqa_adv", "_eqa_back",
                maxResults: 32, expectedDelta: 1);
            candidateCount = cands.Count;

            long verified = 0L;
            if (cands.Count > 0)
            {
                var baselineVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) baselineVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(300);
                var afterOneVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) afterOneVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(300);
                foreach (var candAddr in cands)
                {
                    if (!baselineVals.TryGetValue((long)candAddr, out var baseline)) continue;
                    if (!afterOneVals.TryGetValue((long)candAddr, out var afterOne)) continue;
                    var afterTwo = Explorer.ReadAbsolute(candAddr, 1);
                    if (!afterTwo.HasValue) continue;
                    byte finalVal = (byte)afterTwo.Value.value;
                    // Accept wrap-around too — the EqA column is a 6-row
                    // wrapping list so Down from row 5 → row 0.
                    bool step1Ok = afterOne == baseline + 1 || afterOne == 0;
                    bool step2Ok = finalVal == afterOne + 1 || finalVal == 0;
                    if (step1Ok && step2Ok)
                    {
                        verified = (long)candAddr;
                        break;
                    }
                }
                // Restore cursor: 2 Ups to undo the 2 Downs.
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(220);
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(220);
            }
            _resolvedEqaColumnCursorAddr = verified;

            if (verified != 0)
                return $"EqA column cursor: {cands.Count} candidates, verified 0x{verified:X}";
            return $"EqA column cursor: {cands.Count} candidates, none verified";
        }

        private string? ResolveEquipPickerCursor(out int candidateCount)
        {
            candidateCount = 0;
            if (Explorer == null) return null;
            IntPtr win = Process.GetCurrentProcess().MainWindowHandle;
            if (win == IntPtr.Zero) return null;
            const int VK_DOWN = 0x28, VK_UP = 0x26;

            Thread.Sleep(700);
            Explorer.TakeHeapSnapshot("_equip_base");
            _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
            Thread.Sleep(300);
            Explorer.TakeHeapSnapshot("_equip_adv");
            _inputSimulator.SendKeyPressToWindow(win, VK_UP);
            Thread.Sleep(300);
            Explorer.TakeHeapSnapshot("_equip_back");
            var cands = Explorer.FindToggleCandidates(
                "_equip_base", "_equip_adv", "_equip_back",
                maxResults: 32, expectedDelta: 1);
            candidateCount = cands.Count;

            long verified = 0L;
            if (cands.Count > 0)
            {
                var baselineVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) baselineVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(300);
                var afterOneVals = new Dictionary<long, byte>();
                foreach (var candAddr in cands)
                {
                    var r = Explorer.ReadAbsolute(candAddr, 1);
                    if (r.HasValue) afterOneVals[(long)candAddr] = (byte)r.Value.value;
                }
                _inputSimulator.SendKeyPressToWindow(win, VK_DOWN);
                Thread.Sleep(300);
                foreach (var candAddr in cands)
                {
                    if (!baselineVals.TryGetValue((long)candAddr, out var baseline)) continue;
                    if (!afterOneVals.TryGetValue((long)candAddr, out var afterOne)) continue;
                    var afterTwo = Explorer.ReadAbsolute(candAddr, 1);
                    if (!afterTwo.HasValue) continue;
                    byte finalVal = (byte)afterTwo.Value.value;
                    bool step1Ok = afterOne == baseline + 1 || afterOne == 0;
                    bool step2Ok = finalVal == afterOne + 1 || finalVal == 0;
                    if (step1Ok && step2Ok)
                    {
                        verified = (long)candAddr;
                        break;
                    }
                }
                // Restore cursor: 2 Ups to undo the 2 Downs.
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(220);
                _inputSimulator.SendKeyPressToWindow(win, VK_UP);
                Thread.Sleep(220);
            }
            _resolvedEquipPickerCursorAddr = verified;

            // Investigation aid (added 2026-04-15): once we know the cursor byte
            // address, dump 256 bytes centered on it. UE4 ListView widgets
            // typically store row data in a struct: [vtable ptr][next ptr][prev
            // ptr][data ptr][index/state bytes]. The cursor byte is the index;
            // a data pointer to the row's item ID likely sits within ±64 bytes.
            // This log line is the cheapest way to capture the layout for a
            // future session — no action needed beyond resolving the cursor.
            // Same blocker on EquippableWeapons + shop pickers + PartyMenuInventory:
            // the real unlock is decoding what surrounds the cursor byte. See
            // project_inventory_widget_buffer.md for prior 0x7DB0xxxx decode work.
            if (verified != 0)
            {
                try
                {
                    long dumpStart = verified - 128;
                    var dumpBytes = Explorer.ReadBlock((nint)dumpStart, 256);
                    if (dumpBytes != null)
                    {
                        ModLogger.Log($"[EquipPickerProbe] cursor=0x{verified:X} bytes -128..+128:\n{dumpBytes}");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Log($"[EquipPickerProbe] dump failed: {ex.Message}");
                }
            }

            return cands.Count > 0
                ? $"Resolved equip-picker cursor: 0x{verified:X} ({cands.Count} candidate{(cands.Count > 1 ? "s" : "")})"
                : "No stable equip-picker cursor byte found (0 candidates)";
        }

        /// <summary>
        /// Resolves the EqA column-0 cursor row via the unequip-diff trick.
        /// Cursor row lives in UE4 widget heap that reallocates per keypress
        /// so no stable memory byte exists (confirmed negative across 4 diff
        /// test shapes session 19 + 2026-04-16). Instead we exploit the
        /// equipment-picker toggle: Enter opens the picker for the hovered
        /// slot, a second Enter on the currently-equipped item unequips it,
        /// which flips that slot in the EqA mirror (0x141870854) from its
        /// item ID to 0. We diff the mirror to find which slot transitioned,
        /// that index IS the cursor row, then either restore (for pure
        /// resolution) or leave the slot empty (for the remove flow).
        ///
        /// Edge case: if the hovered slot was already empty, the second
        /// Enter will EQUIP the first item from the picker instead of
        /// unequipping. The inverse transition (0 → X) is also detectable.
        /// In that case the "restore" path unequips it again before Escape;
        /// the "leave-empty" path ALSO has to unequip it (since we want the
        /// final state to match the pre-call empty slot).
        ///
        /// Cost: 4 keypresses + 2 mirror reads, ~1.5s end-to-end.
        /// </summary>
        private (int row, string direction)? DoEqaRowResolve(bool restore)
        {
            if (Explorer == null) return null;
            IntPtr win = Process.GetCurrentProcess().MainWindowHandle;
            if (win == IntPtr.Zero) return null;
            const int VK_ENTER = 0x0D, VK_ESCAPE = 0x1B;
            const long MIRROR = 0x141870854;

            var before = ReadEqaMirror(MIRROR);
            if (before == null) return null;

            _inputSimulator.SendKeyPressToWindow(win, VK_ENTER);
            Thread.Sleep(450);
            _inputSimulator.SendKeyPressToWindow(win, VK_ENTER);
            Thread.Sleep(450);

            var after = ReadEqaMirror(MIRROR);
            if (after == null)
            {
                _inputSimulator.SendKeyPressToWindow(win, VK_ESCAPE);
                Thread.Sleep(300);
                return null;
            }

            int resolvedRow = -1;
            string direction = "none";
            bool wasEmpty = false;
            for (int i = 0; i < 5; i++)
            {
                if (before[i] != 0 && after[i] == 0)
                {
                    resolvedRow = i;
                    direction = $"unequip {before[i]} → 0";
                    break;
                }
            }
            if (resolvedRow == -1)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (before[i] == 0 && after[i] != 0)
                    {
                        resolvedRow = i;
                        direction = $"equip 0 → {after[i]}";
                        wasEmpty = true;
                        break;
                    }
                }
            }

            // Final-state logic:
            //   restore=true  → match pre-call state exactly (re-toggle if needed)
            //   restore=false → leave slot empty (final mirror[row] == 0)
            //
            // After the two opening Enters:
            //   - slot was populated  → now empty (toggle-off did the unequip)
            //   - slot was empty      → now populated (first picker item auto-equipped)
            //
            // To restore: always one more Enter to re-toggle.
            // To leave empty:
            //   - if wasEmpty (now populated) → one more Enter to unequip
            //   - if was populated (now empty) → no more toggles needed
            bool needsOneMoreEnter = restore || wasEmpty;
            if (needsOneMoreEnter)
            {
                _inputSimulator.SendKeyPressToWindow(win, VK_ENTER);
                Thread.Sleep(450);
            }
            _inputSimulator.SendKeyPressToWindow(win, VK_ESCAPE);
            Thread.Sleep(350);

            if (resolvedRow == -1) return null;
            ScreenMachine.SetEquipmentCursor(resolvedRow);
            return (resolvedRow, direction);
        }

        private string? ResolveEqaRow()
        {
            var result = DoEqaRowResolve(restore: true);
            if (result == null) return "ResolveEqaRow failed: mirror unreadable or no slot transition";
            ModLogger.Log($"[ResolveEqaRow] resolved row={result.Value.row} ({result.Value.direction})");
            return $"Resolved EqA row: {result.Value.row} ({result.Value.direction})";
        }

        private string? RemoveEquipmentAtCursor()
        {
            var result = DoEqaRowResolve(restore: false);
            if (result == null) return "RemoveEquipmentAtCursor failed: mirror unreadable or no slot transition";
            ModLogger.Log($"[RemoveEquipmentAtCursor] removed at row={result.Value.row} ({result.Value.direction})");
            return $"Removed equipment at row: {result.Value.row} ({result.Value.direction})";
        }

        // Actions that are always allowed regardless of strict mode (info/infrastructure)
        private static readonly HashSet<string> InfrastructureActions = new()
        {
            "scan_move", "scan_units", "set_map", "report_state",
            "read_address", "read_block", "batch_read",
            "mark_blocked", "snapshot", "heap_snapshot", "diff", "find_toggle", "find_monotonic", "dump_unit_struct",
            "dry_run_nav", "cursor_walk",
            "search_bytes", "search_all", "search_memory", "search_near",
            "dump_unit", "dump_all", "write_address", "set_strict", "set_map",
            "read_dialogue", "get_rumor", "list_rumors", "write_byte", "dump_detection_inputs",
            "render_lifetime_summary", "render_battle_summary", "session_stats",
            "scrape_shop_items",
            "shop_stock",
            "seed_shop_stock",
            "hold_key",
            "get_flag", "set_flag", "list_flags",
            "reset_state_machine",
            "resolve_picker_cursor",
            "resolve_job_cursor",
            "resolve_party_menu_cursor",
            "resolve_equip_picker_cursor",
            "resolve_eqa_row",
            "remove_equipment_at_cursor",
            "scan_snapshot", "scan_diff",
            "memory_diff"
        };

        // Named game actions allowed in strict mode (from fft.sh helpers)
        private static readonly HashSet<string> AllowedGameActions = new()
        {
            "execute_action", "execute_turn", "battle_wait", "battle_flee", "battle_attack", "battle_ability",
            "battle_move", "world_travel_to", "auto_move", "get_arrows",
            "advance_dialogue", "save", "load",
            "battle_retry", "battle_retry_formation",
            "buy", "sell", "change_job",
            "open_eqa", "open_job_selection", "open_character_status",
            "swap_unit_to",
            "auto_place_units"
        };

        /// <summary>
        /// Disk-backed named-flag store. Lazily initialized on first access
        /// since the bridge directory may not exist yet at construction time.
        /// Use for session-scoped caches (e.g. "last-observed party Tab"),
        /// cross-bridge-session counters, diagnostic toggles. See
        /// <see cref="ModStateFlags"/> docs for when to use and NOT use.
        /// </summary>
        private ModStateFlags? _stateFlags;
        public ModStateFlags StateFlags
        {
            get
            {
                if (_stateFlags == null)
                {
                    if (!Directory.Exists(_bridgeDirectory))
                        Directory.CreateDirectory(_bridgeDirectory);
                    _stateFlags = new ModStateFlags(_bridgeDirectory);
                }
                return _stateFlags;
            }
        }

        /// <summary>
        /// Per-session append-only JSONL log of every command processed.
        /// Lazy-initialized so the log file is stamped at first command,
        /// not at mod load (keeps log files in sync with actual activity).
        /// </summary>
        private SessionCommandLog? _sessionLog;
        private SessionCommandLog SessionLog
        {
            get
            {
                if (_sessionLog == null)
                {
                    if (!Directory.Exists(_bridgeDirectory))
                        Directory.CreateDirectory(_bridgeDirectory);
                    _sessionLog = new SessionCommandLog(_bridgeDirectory);
                }
                return _sessionLog;
            }
        }

        public CommandWatcher(string modPath, IInputSimulator inputSimulator)
        {
            _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
            _bridgeDirectory = Path.Combine(modPath, "claude_bridge");
            _commandFilePath = Path.Combine(_bridgeDirectory, "command.json");
            _responseFilePath = Path.Combine(_bridgeDirectory, "response.json");
        }

        public void Start()
        {
            if (!Directory.Exists(_bridgeDirectory))
                Directory.CreateDirectory(_bridgeDirectory);

            // Clean up stale command file from previous session
            if (File.Exists(_commandFilePath))
            {
                try { File.Delete(_commandFilePath); }
                catch { /* ignore */ }
            }

            _watcher = new FileSystemWatcher(_bridgeDirectory, "command.json");
            _watcher.Changed += OnCommandFileChanged;
            _watcher.Created += OnCommandFileChanged;
            _watcher.Renamed += (s, e) => OnCommandFileChanged(s, e);
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size;
            _watcher.EnableRaisingEvents = true;

            // Also start a polling fallback in case FileSystemWatcher misses events
            Task.Run(async () =>
            {
                ModLogger.Log("[CommandBridge] Starting polling fallback (every 20ms)");
                while (_watcher != null && !_disposed)
                {
                    try
                    {
                        if (File.Exists(_commandFilePath))
                        {
                            ProcessCommandFile();
                        }
                    }
                    catch { /* ignore polling errors */ }
                    // 50ms → 20ms. CPU cost trivial (File.Exists on an
                    // SSD is microseconds). Saves up to 30ms on every
                    // command where FileSystemWatcher misses/lags.
                    await Task.Delay(20);
                }
            });

            ModLogger.Log($"[CommandBridge] Watching for commands at: {_commandFilePath}");

            // Background logger: samples battle acted/moved + submenuFlag every 200ms.
            // Writes only on CHANGE to claude_bridge/acted_moved_log.csv so the file
            // stays small and easy to scan. Audit tool — remove once the flags are
            // understood.
            Task.Run(async () =>
            {
                var logPath = Path.Combine(_bridgeDirectory, "acted_moved_log.csv");
                try
                {
                    File.WriteAllText(logPath,
                        "timestamp,screen,team,acted,moved,submenuFlag,menuCursor,battleMode,slot0,note\n");
                }
                catch { /* ignore */ }

                long lastAm = -1; // packed key of (acted,moved,submenu,cursor,mode,team)
                while (!_disposed)
                {
                    try
                    {
                        if (Explorer != null)
                        {
                            var v = Explorer.ReadMultiple(ScreenAddresses);
                            int acted = (int)v[8];
                            int moved = (int)v[9];
                            int sub = (int)v[18];
                            int cursor = (int)v[4];
                            int bm = (int)v[16];
                            int team = (int)v[7];
                            long s0 = v[12];
                            long s9 = v[13];

                            bool inBattle = (s0 == 255 && s9 == 0xFFFFFFFF)
                                || (s9 == 0xFFFFFFFF && (bm == 2 || bm == 3 || bm == 4));
                            if (!inBattle) { await Task.Delay(200); continue; }

                            long key = ((long)acted << 0) | ((long)moved << 4)
                                     | ((long)sub << 8) | ((long)cursor << 12)
                                     | ((long)bm << 20) | ((long)team << 28);
                            if (key != lastAm)
                            {
                                lastAm = key;
                                var line = string.Format(
                                    "{0:o},{1},{2},{3},{4},{5},{6},{7},0x{8:X8},\n",
                                    DateTime.UtcNow, "", team, acted, moved, sub, cursor, bm, s0);
                                try { File.AppendAllText(logPath, line); } catch { }
                            }
                        }
                    }
                    catch { /* ignore sampler errors */ }
                    await Task.Delay(200);
                }
            });
        }

        public void Stop()
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        private void OnCommandFileChanged(object sender, FileSystemEventArgs e)
        {
            Task.Run(() => ProcessCommandFile());
        }

        private void ProcessCommandFile()
        {
            lock (_processingLock)
            {
                try
                {
                    var command = ReadCommandFile();
                    if (command == null) return;

                    if (command.Id == _lastProcessedCommandId)
                    {
                        ModLogger.LogDebug("[CommandBridge] Skipping duplicate command: " + command.Id);
                        return;
                    }

                    ModLogger.Log($"[CommandBridge] Processing command {command.Id}: {command.Description}");

                    // Only a zero-key no-op command counts as a state check (the "screen" helper)
                    bool isScreenQuery = (command.Keys != null && command.Keys.Count == 0)
                        && command.Action == null;

                    // Chained-command rate limit: if a game-affecting command arrives
                    // within ChainFloorMs of the previous one finishing, sleep the
                    // newer command until the floor is met and flag the response.
                    // Observational commands (screen query, infra/read actions) are
                    // exempt — they don't press keys or trigger state transitions.
                    bool isObservational = isScreenQuery
                        || (command.Action != null && InfrastructureActions.Contains(command.Action));
                    var (sleepMs, chainWarning) = ComputeChainDelay(
                        isObservational, _lastGameCommandCompletedAt, DateTime.UtcNow, ChainFloorMs);
                    if (sleepMs > 0)
                    {
                        ModLogger.Log($"[CommandBridge] WARN rapid-fire chain: {chainWarning}");
                        System.Threading.Thread.Sleep(sleepMs);
                    }

                    // Enforce: must call screen before any game command
                    if (RequireStateCheck && !isScreenQuery && !_lastCommandWasQuery)
                    {
                        var blocked = new CommandResponse
                        {
                            Id = command.Id,
                            Status = "blocked",
                            Error = "[STATE CHECK REQUIRED] Call 'screen' before sending game commands.",
                            ProcessedAt = DateTime.UtcNow.ToString("o"),
                            GameWindowFound = true
                        };
                        blocked.Screen = DetectScreenSettled();
                        SyncBattleMenuTracker(blocked.Screen);
                        HandleBattleLifecycle(blocked.Screen);
                        WriteResponse(blocked);
                        _lastProcessedCommandId = command.Id;
                        return;
                    }

                    _lastCommandWasQuery = isScreenQuery;

                    // Session-log: capture source screen + start time before
                    // the command runs, so we can record the before→after
                    // transition and round-trip latency. Prefer the SM's
                    // string mirror (LastDetectedScreen) since it tracks
                    // ALL screen names — including detection-only states
                    // like BattleMyTurn/Cutscene that the enum-typed
                    // CurrentScreen doesn't model. Without this, the
                    // sourceScreen column sticks at the initial enum value
                    // (TitleScreen, etc.) for entire sessions even as the
                    // bridge moves through dozens of battle screens.
                    string? sourceScreenName =
                        ScreenMachine?.LastDetectedScreen
                        ?? ScreenMachine?.CurrentScreen.ToString();
                    var commandStartedAt = DateTime.UtcNow;

                    var response = ExecuteCommand(command);
                    // Screen-query commands can't be mid-transition (no keys just
                    // fired), so skip the 150ms+ settle loop. Key-sending and
                    // action commands still settle to let UI animations finish.
                    response.Screen ??= DetectScreenSettled(requireSettle: !isScreenQuery);
                    // CharacterStatusLeakGuard: during battle_wait animations,
                    // unit-slot/ui bytes transiently match CharacterStatus —
                    // hold the previous battle state when no key input could
                    // have caused a real drill-in. Observational commands
                    // (screen queries, infra actions) don't press keys.
                    if (response.Screen != null)
                    {
                        var keysCount = isObservational ? 0 : 1;
                        var filtered = GameBridge.CharacterStatusLeakGuard.Filter(
                            _previousSettledScreen, response.Screen.Name, keysCount);
                        if (filtered != response.Screen.Name)
                        {
                            ModLogger.Log($"[LeakGuard] Detection={response.Screen.Name} → {filtered} (prev={_previousSettledScreen}, keys={keysCount}).");
                            response.Screen.Name = filtered;
                        }
                        // EqA leak guard: GameOver/Battle*/Victory/Desertion
                        // can never legitimately transition to EqA in one
                        // detection cycle. Live-captured 2026-04-25: bridge
                        // sent Enter on GameOver, first post-key detection
                        // returned EquipmentAndAbilities. Filter back.
                        var eqaFiltered = GameBridge.EqaLeakGuard.Filter(
                            _previousSettledScreen, response.Screen.Name);
                        if (eqaFiltered != response.Screen.Name)
                        {
                            ModLogger.Log($"[EqaLeakGuard] Detection={response.Screen.Name} → {eqaFiltered} (prev={_previousSettledScreen}).");
                            response.Screen.Name = eqaFiltered;
                        }
                    }
                    // Override detection-ambiguous names where the SM has a
                    // stronger signal (e.g. SaveSlotPicker vs TravelList).
                    bool screenQueryOverrode = false;
                    if (response.Screen != null && ScreenMachine != null)
                    {
                        var resolved = ScreenDetectionLogic.ResolveAmbiguousScreen(
                            ScreenMachine.CurrentScreen, response.Screen.Name,
                            ScreenMachine.KeysSinceLastSetScreen,
                            ScreenMachine.LastSetScreenFromKey);
                        if (resolved != response.Screen.Name)
                        {
                            ModLogger.Log($"[SM-Override] Detection={response.Screen.Name} → {resolved} (SM={ScreenMachine.CurrentScreen}).");
                            response.Screen.Name = resolved;
                            screenQueryOverrode = true;
                        }
                    }
                    // Mirror the detected screen name into the SM regardless
                    // of whether the enum models a transition for it. Fixes
                    // the session-45 desync where sourceScreen stuck at the
                    // boot-time TitleScreen for every command because the
                    // enum-sync table below only covers 4 screens.
                    if (response.Screen != null && ScreenMachine != null)
                    {
                        ScreenMachine.ObserveDetectedScreen(response.Screen.Name);
                        // Session 46: auto-snap the enum-typed CurrentScreen
                        // when its category disagrees with detection. Silent
                        // pure-C# realignment — no keypresses fire. Catches
                        // "SM=CharacterStatus but detection=BattleMyTurn"
                        // style leaks seen during live stress testing.
                        var smBefore = ScreenMachine.CurrentScreen;
                        var snappedTo = ScreenMachine.AutoSnapIfCategoryMismatch(response.Screen.Name);
                        if (snappedTo.HasValue)
                            ModLogger.Log($"[SM-AutoSnap] Detection={response.Screen.Name}, SM was {smBefore} — snapped to {snappedTo.Value}.");
                        // Within-PartyTree auto-snap: detection can't tell
                        // PartyMenuUnits from CharacterStatus/EqA/etc. (all
                        // show the same memory pattern), so we use menuDepth
                        // as the authoritative "are we at the outer grid"
                        // signal. If SM thinks we're deeper but menuDepth==0,
                        // realign. Live repro session 46 at Dorter: bridge
                        // stayed reporting CharacterStatus after a failed
                        // SelectUnit attempt while game was on PartyMenu grid.
                        var smBeforePT = ScreenMachine.CurrentScreen;
                        if (ScreenMachine.SnapPartyTreeOuterIfDrifted(
                                response.Screen.Name, response.Screen.MenuDepth))
                            ModLogger.Log($"[SM-AutoSnap/PartyTree] Detection={response.Screen.Name}, menuDepth=0, SM was {smBeforePT} — snapped to PartyMenuUnits.");
                    }

                    // Sync the SM to detection for screens it doesn't model
                    // transitions into (WorldMap ↔ LocationMenu ↔ Tavern, etc.).
                    // Without this, a `screen` query that sees "Tavern" never
                    // updates the SM, so a later Select key won't fire
                    // HandleTavern → TavernRumors/TavernErrands.
                    if (!screenQueryOverrode && response.Screen != null && ScreenMachine != null)
                    {
                        var detectedGs = response.Screen.Name switch
                        {
                            "WorldMap" => GameScreen.WorldMap,
                            "TravelList" => GameScreen.TravelList,
                            "LocationMenu" => GameScreen.LocationMenu,
                            "Tavern" => GameScreen.Tavern,
                            _ => (GameScreen?)null
                        };
                        if (detectedGs.HasValue && ScreenMachine.CurrentScreen != detectedGs.Value)
                        {
                            ModLogger.Log($"[SM-Sync/query] Detection={response.Screen.Name}, SM={ScreenMachine.CurrentScreen}. Syncing SM to {detectedGs.Value}.");
                            ScreenMachine.SetScreen(detectedGs.Value);
                        }
                    }
                    SyncBattleMenuTracker(response.Screen);
                    HandleBattleLifecycle(response.Screen);

                    // Attach rate-limit warning (set above if we auto-delayed).
                    if (chainWarning != null) response.ChainWarning = chainWarning;

                    // Stamp completion time for the rate-limit floor — but only
                    // for game-affecting commands. Observational queries don't
                    // press keys, so they shouldn't reset the clock.
                    if (!isObservational)
                        _lastGameCommandCompletedAt = DateTime.UtcNow;

                    // Auto-scan-on-screen-query (speed optimization):
                    // when a plain `screen` call lands on a fresh BattleMyTurn,
                    // piggy-back a scan_move into the response so Claude has
                    // valid-move/attack tiles without a second round-trip.
                    // The older unconditional auto-scan caused the "Reset Move"
                    // bug via C+Up during mid-animation settling — we avoid
                    // that here by:
                    //   (a) only firing after DetectScreenSettled has already
                    //       returned (settle is done),
                    //   (b) gating on _turnTracker.ShouldAutoScan which fires
                    //       at most once per friendly turn,
                    //   (c) wrapping in try/catch — basic screen data is kept
                    //       even if scan_move fails.
                    if (isScreenQuery && response.Screen != null
                        && _turnTracker.ShouldAutoScan(
                            response.Screen.Name,
                            response.Screen.BattleTeam,
                            response.Screen.BattleUnitId,
                            response.Screen.BattleUnitHp))
                    {
                        try
                        {
                            var scanCommand = new CommandRequest { Id = command.Id, Action = "scan_move" };
                            var scanResponse = ExecuteNavAction(scanCommand);
                            if (scanResponse.Status == "completed")
                            {
                                response.Battle = scanResponse.Battle;
                                response.ValidPaths = scanResponse.ValidPaths;
                                response.Screen = scanResponse.Screen ?? response.Screen;
                                response.Info = scanResponse.Info;
                                _turnTracker.MarkScanned();
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogError($"[CommandBridge] Screen-query auto-scan failed: {ex.Message}");
                        }
                    }

                    response.Battle ??= BattleTracker?.Update();
                    // Cache learned ability IDs from active unit for ability list tracking
                    CacheLearnedAbilities(response.Battle);
                    // Populate map info on battle state from MapLoader
                    if (response.Battle != null && _mapLoader != null)
                    {
                        response.Battle.MapId = _mapLoader.CurrentMapNumber;
                        response.Battle.MapName = MapLoader.GetMapName(_mapLoader.CurrentMapNumber)
                            ?? response.Screen?.LocationName;
                    }
                    if (response.Screen != null)
                        response.ValidPaths ??= NavigationPaths.GetPaths(response.Screen);
                    // Record the authoritative screen for the next command's
                    // CharacterStatusLeakGuard filter.
                    if (response.Screen != null)
                        _previousSettledScreen = response.Screen.Name;
                    WriteResponse(response);

                    // Session observability log — one JSONL row per command.
                    // Never throws (see SessionCommandLog docs) so it's safe
                    // on the response path.
                    var latencyMs = (long)(DateTime.UtcNow - commandStartedAt).TotalMilliseconds;
                    SessionLog.Append(
                        commandId: command.Id ?? "",
                        action: command.Action ?? (command.Keys != null && command.Keys.Count == 0 ? "screen" : "keys"),
                        sourceScreen: sourceScreenName,
                        targetScreen: response.Screen?.Name,
                        status: response.Status ?? "unknown",
                        error: response.Error,
                        latencyMs: latencyMs);

                    _lastProcessedCommandId = command.Id;

                    // Rename processed file to prevent re-processing
                    try
                    {
                        var processedPath = Path.Combine(_bridgeDirectory, "command.processed.json");
                        if (File.Exists(processedPath)) File.Delete(processedPath);
                        File.Move(_commandFilePath, processedPath);
                    }
                    catch { /* ignore rename failures */ }
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"[CommandBridge] Error processing command: {ex.Message}");
                    var errorResponse = new CommandResponse
                    {
                        Id = "unknown",
                        Status = "error",
                        Error = ex.Message,
                        ProcessedAt = DateTime.UtcNow.ToString("o")
                    };
                    errorResponse.Screen = DetectScreenSettled();
                    SyncBattleMenuTracker(errorResponse.Screen);
                    HandleBattleLifecycle(errorResponse.Screen);
                    if (errorResponse.Screen != null)
                        errorResponse.ValidPaths = NavigationPaths.GetPaths(errorResponse.Screen);
                    WriteResponse(errorResponse);

                    // Record the error path too — these are exactly the cases
                    // post-hoc review needs to find.
                    SessionLog.Append(
                        commandId: errorResponse.Id ?? "unknown",
                        action: "exception",
                        sourceScreen: ScreenMachine?.LastDetectedScreen
                            ?? ScreenMachine?.CurrentScreen.ToString(),
                        targetScreen: errorResponse.Screen?.Name,
                        status: "error",
                        error: ex.Message,
                        latencyMs: 0);
                }
            }
        }

        private CommandRequest? ReadCommandFile()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (!File.Exists(_commandFilePath)) return null;
                    var json = File.ReadAllText(_commandFilePath);
                    return JsonSerializer.Deserialize<CommandRequest>(json);
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"[CommandBridge] Failed to read command file: {ex.Message}");
                    try
                    {
                        var quarantined = GameBridge.CommandFileQuarantine.Quarantine(_commandFilePath, DateTime.UtcNow);
                        if (quarantined != null)
                            ModLogger.Log($"[CommandBridge] Quarantined malformed command.json → {Path.GetFileName(quarantined)}");
                    }
                    catch (Exception qex)
                    {
                        ModLogger.LogError($"[CommandBridge] Quarantine failed: {qex.Message}");
                    }
                    return null;
                }
            }
            return null;
        }

        private CommandResponse ExecuteCommand(CommandRequest command)
        {
            if (StrictMode)
            {
                // In strict mode, only allow:
                //   1. Infrastructure actions (scan_units, read_address, etc.)
                //   2. Named game actions from fft.sh helpers (path, battle_wait, etc.)
                //   3. No-op state queries (empty keys, no action) — e.g. screen command
                // Block everything else (raw keys, sequence, unknown actions).
                bool isInfra = !string.IsNullOrEmpty(command.Action) && InfrastructureActions.Contains(command.Action);
                bool isGameAction = !string.IsNullOrEmpty(command.Action) && AllowedGameActions.Contains(command.Action);
                bool isNoOp = string.IsNullOrEmpty(command.Action) && (command.Keys == null || command.Keys.Count == 0);
                // Escape key (VK 27) is always allowed — universal cancel/back
                bool isEscape = string.IsNullOrEmpty(command.Action) && command.Keys?.Count == 1 && command.Keys[0].Vk == 0x1B;

                if (!isInfra && !isGameAction && !isNoOp && !isEscape)
                {
                    var screen = DetectScreen();
                    var paths = screen != null ? NavigationPaths.GetPaths(screen) : null;
                    var available = paths != null ? string.Join(", ", paths.Keys) : "none";
                    string reason = string.IsNullOrEmpty(command.Action)
                        ? "Raw key presses are not allowed"
                        : $"Action '{command.Action}' is not allowed";
                    return new CommandResponse
                    {
                        Id = command.Id,
                        Status = "blocked",
                        Error = $"[STRICT MODE] {reason}. Use the fft.sh helper commands: path, battle_wait, battle_attack, move_grid, scan_units, etc. Current screen: {screen?.Name}. ValidPaths: {available}",
                        ProcessedAt = DateTime.UtcNow.ToString("o"),
                        GameWindowFound = true,
                        Screen = screen,
                        ValidPaths = paths
                    };
                }
            }

            // Route action commands (dump_unit, report_state, etc.)
            if (!string.IsNullOrEmpty(command.Action))
                return ExecuteAction(command);

            return ExecuteKeyCommand(command);
        }

        private CommandResponse ExecuteAction(CommandRequest command)
        {
            var response = new CommandResponse
            {
                Id = command.Id,
                ProcessedAt = DateTime.UtcNow.ToString("o"),
                GameWindowFound = true
            };

            try
            {
                switch (command.Action)
                {
                    case "dump_unit":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.DumpUnitToFile(command.Slot);
                        response.Status = "completed";
                        break;

                    case "dump_all":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.DumpAllActiveUnits();
                        response.Status = "completed";
                        break;

                    case "dump_detection_inputs":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        {
                            var raw = Explorer.ReadMultiple(ScreenAddresses);
                            int dP = (int)raw[0], dU = (int)raw[1], dLoc = (int)raw[2];
                            long dS0 = raw[12], dS9 = raw[13];
                            int dBm = (int)raw[16], dMm = (int)raw[15], dPs = (int)raw[14];
                            int dSf = (int)raw[18], dGo = dSf;
                            int dBt = (int)raw[7], dBa = (int)raw[8], dBmv = (int)raw[9];
                            int dEa = (int)raw[5], dEb = (int)raw[6];
                            int dEv = (int)raw[19], dMc = (int)raw[4];
                            int dHover = (int)raw[3];
                            int dLmf = (int)raw[21];
                            int dSti = (int)raw[22];
                            int dIsf = (int)raw[23];
                            int dSsmi = (int)raw[24];
                            int dEncF = (int)raw[28];
                            bool dInBattle = (dS0 == 255 && dS9 == 0xFFFFFFFF)
                                || (dS9 == 0xFFFFFFFF && (dBm == 2 || dBm == 3 || dBm == 4));
                            // Read tab flags for detection
                            int dUtf = 0, dItf = 0;
                            var dUtfR = Explorer.ReadAbsolute((nint)0x140D3A41E, 1);
                            var dItfR = Explorer.ReadAbsolute((nint)0x140D3A38E, 1);
                            if (dUtfR.HasValue) dUtf = (int)dUtfR.Value.value;
                            if (dItfR.HasValue) dItf = (int)dItfR.Value.value;
                            string detected = GameBridge.ScreenDetectionLogic.Detect(
                                dP, dU, dLoc, dS0, dS9, dBm, dMm, dPs, dGo,
                                dBt, dBa, dBmv, dEa, dEb, !dInBattle && IsPartySubScreen(),
                                dEv, submenuFlag: dSf, menuCursor: dMc, hover: dHover,
                                locationMenuFlag: dLmf, insideShopFlag: dIsf,
                                shopSubMenuIndex: dSsmi, shopTypeIndex: dSti,
                                unitsTabFlag: dUtf, inventoryTabFlag: dItf,
                                encounterFlag: dEncF);
                            var snapshot = new Dictionary<string, object>
                            {
                                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                                ["detected"] = detected,
                                ["inputs"] = new Dictionary<string, object>
                                {
                                    ["party"] = dP,
                                    ["ui"] = dU,
                                    ["rawLocation"] = dLoc,
                                    ["slot0"] = $"0x{dS0:X8}",
                                    ["slot9"] = $"0x{dS9:X8}",
                                    ["battleMode"] = dBm,
                                    ["moveMode"] = dMm,
                                    ["paused"] = dPs,
                                    ["gameOverFlag"] = dGo,
                                    ["battleTeam"] = dBt,
                                    ["battleActed"] = dBa,
                                    ["battleMoved"] = dBmv,
                                    ["encA"] = dEa,
                                    ["encB"] = dEb,
                                    ["isPartySubScreen"] = !dInBattle && IsPartySubScreen(),
                                    ["eventId"] = dEv,
                                    ["submenuFlag"] = dSf,
                                    ["menuCursor"] = dMc,
                                    ["hover"] = dHover,
                                    ["locationMenuFlag"] = dLmf,
                                    ["shopTypeIndex"] = dSti,
                                    ["insideShopFlag"] = dIsf,
                                    ["shopSubMenuIndex"] = dSsmi,
                                    ["encounterFlag"] = dEncF
                                }
                            };
                            response.Info = System.Text.Json.JsonSerializer.Serialize(snapshot,
                                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                            response.Status = "completed";
                        }
                        break;

                    case "report_state":
                        if (StateReporter == null) { response.Status = "failed"; response.Error = "State reporter not initialized"; break; }
                        StateReporter.ReportNow();
                        response.Status = "completed";
                        break;

                    case "render_lifetime_summary":
                        if (StatTracker == null) { response.Status = "failed"; response.Error = "Stat tracker not initialized"; break; }
                        response.Info = StatTracker.RenderLifetimeSummary();
                        response.Status = "completed";
                        break;

                    case "render_battle_summary":
                        if (StatTracker == null) { response.Status = "failed"; response.Error = "Stat tracker not initialized"; break; }
                        response.Info = StatTracker.RenderBattleSummary();
                        response.Status = "completed";
                        break;

                    case "session_stats":
                        response.Info = RenderSessionStats();
                        response.Status = "completed";
                        break;

                    case "search_memory":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.SearchMemoryForUInt16((ushort)command.SearchValue, command.SearchLabel ?? "search");
                        response.Status = "completed";
                        break;

                    case "search_near":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.SearchNearAddress(Explorer.Scanner.UnitDataBase, 0x200000, (ushort)command.SearchValue, command.SearchLabel ?? "near");
                        response.Status = "completed";
                        break;

                    case "snapshot":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.TakeSnapshot(command.SearchLabel ?? "default");
                        response.Status = "completed";
                        break;

                    case "read_dialogue":
                        if (ScriptLookup == null) { response.Status = "failed"; response.Error = "Script lookup not initialized"; break; }
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        {
                            var evtRead = Explorer.ReadAbsolute((nint)0x14077CA94, 2);
                            int evtId = evtRead.HasValue ? (int)evtRead.Value.value : 0;
                            var script = ScriptLookup.GetFormattedScript(evtId);
                            if (script != null)
                            {
                                response.Dialogue = script;
                                response.Status = "completed";
                            }
                            else
                            {
                                response.Status = "failed";
                                response.Error = $"No script found for eventId={evtId}";
                            }
                        }
                        break;

                    case "get_rumor":
                        if (RumorLookup == null || RumorLookup.Count == 0)
                        {
                            response.Status = "failed";
                            response.Error = "Rumor corpus not loaded (world_wldmes.bin missing from Data/?)";
                            break;
                        }
                        {
                            var res = RumorResolver.Resolve(RumorLookup,
                                command.SearchLabel, command.LocationId, command.UnitIndex);
                            if (!res.Ok)
                            {
                                response.Status = "failed";
                                response.Error = res.Error;
                                break;
                            }
                            var rumor = res.Rumor!;
                            response.Dialogue = $"[corpus #{rumor.Index} @0x{rumor.Offset:X}]\n{rumor.Body}";
                            response.Status = "completed";
                        }
                        break;

                    case "list_rumors":
                        if (RumorLookup == null || RumorLookup.Count == 0)
                        {
                            response.Status = "failed";
                            response.Error = "Rumor corpus not loaded";
                            break;
                        }
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine($"Rumor corpus: {RumorLookup.Count} entries from {RumorLookup.SourcePath}");
                            foreach (var r in RumorLookup.All)
                            {
                                // Use FirstSentence so the preview is title-matchable,
                                // not a mid-sentence truncation.
                                var preview = FFTColorCustomizer.GameBridge.RumorLookup.FirstSentence(r.Body);
                                sb.AppendLine($"  [{r.Index:D2}] @0x{r.Offset:X6}: {preview.Replace('\n', ' ')}");
                            }
                            response.Dialogue = sb.ToString();
                            response.Status = "completed";
                        }
                        break;

                    case "mark_blocked":
                        if (command.LocationId >= 0 && command.UnitIndex >= 0)
                        {
                            MarkTileBlocked(command.LocationId, command.UnitIndex);
                            response.Status = "completed";
                        }
                        else
                        {
                            response.Status = "failed";
                            response.Error = "locationId (gridX) and unitIndex (gridY) required";
                        }
                        break;

                    case "set_map":
                        if (command.LocationId >= 0)
                        {
                            EnsureMapLoader();
                            var map = _mapLoader?.LoadMap(command.LocationId);
                            if (map != null)
                            {
                                response.Status = "completed";
                                response.Error = $"Loaded MAP{command.LocationId:D3}: {map.Width}x{map.Height}";
                                ClearBlockedTiles();
                                _battleMapAutoLoaded = true;
                                // Cache this as the random encounter map for the current location
                                if (_lastWorldMapLocation >= 0)
                                    SaveRandomEncounterMap(_lastWorldMapLocation, command.LocationId);
                            }
                            else
                            {
                                response.Status = "failed";
                                response.Error = $"Failed to load MAP{command.LocationId:D3}";
                            }
                        }
                        else
                        {
                            response.Status = "failed";
                            response.Error = "locationId (map number) required, e.g. 74 for MAP074";
                        }
                        break;

                    case "set_strict":
                        StrictMode = command.LocationId != 0; // locationId=1 → on, 0 → off
                        response.Status = "completed";
                        response.Error = $"Strict mode: {(StrictMode ? "ON — game actions must use validPaths" : "OFF — all actions allowed")}";
                        break;

                    case "scan_move":
                    case "auto_move":
                        // No caching — scan is ~15ms (pure memory reads), always fresh.
                        {
                            var currentScreen = DetectScreen();
                            if (currentScreen != null && !BattleTurnTracker.CanScan(currentScreen.Name))
                            {
                                response.Status = "blocked";
                                response.Error = $"Cannot scan during {currentScreen.Name} — wait for BattleMyTurn";
                                response.Screen = currentScreen;
                                break;
                            }
                        }
                        var scanResult = ExecuteNavAction(command);
                        if (scanResult.Status == "completed")
                            CacheLearnedAbilities(scanResult.Battle);
                        if (!command.Verbose && scanResult.Status == "completed")
                            CompactAbilities(scanResult);
                        return scanResult;


                    case "heap_snapshot":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.TakeHeapSnapshot(command.SearchLabel ?? "default");
                        response.Status = "completed";
                        break;

                    case "diff":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        Explorer.DiffSnapshots(command.FromLabel ?? "before", command.ToLabel ?? "after", command.SearchLabel ?? "result");
                        response.Status = "completed";
                        break;

                    case "cursor_walk":
                        {
                            var curScr = DetectScreen();
                            if (curScr == null || curScr.Name != "BattleMoving")
                            {
                                response.Status = "failed";
                                response.Error = $"cursor_walk requires BattleMoving; current={curScr?.Name ?? "Unknown"}";
                                break;
                            }
                            try
                            {
                                var report = RunCursorWalkDiagnostic();
                                response.Info = report;
                                response.Status = report != null && report.StartsWith("Error") ? "failed" : "completed";
                                if (report != null && report.StartsWith("Error")) response.Error = report;
                            }
                            catch (Exception ex)
                            {
                                response.Status = "error";
                                response.Error = $"cursor_walk exception: {ex.Message}";
                                ModLogger.LogError($"[cursor_walk] EXCEPTION: {ex}");
                            }
                        }
                        break;

                    case "dry_run_nav":
                    {
                        // Plan the key sequence for a NavigateToCharacterStatus
                        // call without firing any keys. Used as a pre-flight
                        // safety check when debugging chain-nav crashes: log
                        // the plan, compare against expected, validate BEFORE
                        // committing to a live run. Addresses session 24's
                        // "two prior attempts crashed the game" footgun.
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        string targetName = command.To ?? "";
                        if (string.IsNullOrEmpty(targetName))
                        { response.Status = "failed"; response.Error = "unitName required"; break; }
                        if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                        if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);
                        var _slots = _rosterReader.ReadAll();
                        RosterReader.RosterSlot? tgt = null;
                        foreach (var sx in _slots)
                            if (sx.Name != null && sx.Name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                            { tgt = sx; break; }
                        if (tgt == null)
                        { response.Status = "failed"; response.Error = $"Unit '{targetName}' not in roster"; break; }
                        var curScreen = DetectScreen();
                        string curName = curScreen?.Name ?? "Unknown";
                        var plan = GameBridge.NavigationPlanner.PlanNavigateToCharacterStatus(
                            curName, tgt.DisplayOrder, _slots.Count);
                        response.Info = $"dry_run_nav {targetName}: currentScreen={curName}, displayOrder={tgt.DisplayOrder}, rosterCount={_slots.Count}\nplan: {plan.Render()}";
                        ModLogger.Log($"[dry_run_nav] {response.Info}");
                        response.Status = plan.Ok ? "completed" : "failed";
                        if (!plan.Ok) response.Error = plan.Error;
                        break;
                    }

                    case "dump_unit_struct":
                    {
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        // Searches the UE4 heap for a unit struct matching the given
                        // HP+MaxHP pair (both u16, little-endian). Dumps 256 bytes from
                        // struct base (= match address - 0x10). Used for finding Move/Jump
                        // offsets by correlating known values against byte positions.
                        //
                        // Arguments: pattern = "HHMM" where HH is HP (hex u16 little-endian)
                        //            and MM is MaxHP (hex u16 little-endian).
                        // Example: Kenrick HP=586 MaxHP=586 → pattern "4A024A02".
                        if (string.IsNullOrEmpty(command.Pattern))
                        {
                            response.Status = "failed";
                            response.Error = "Pattern required (HP+MaxHP as hex, e.g. '4A024A02')";
                            break;
                        }
                        try
                        {
                            var hexClean = command.Pattern.Replace(" ", "").Replace("-", "");
                            var patternBytes = new byte[hexClean.Length / 2];
                            for (int i = 0; i < patternBytes.Length; i++)
                                patternBytes[i] = Convert.ToByte(hexClean.Substring(i * 2, 2), 16);

                            var matches = Explorer.SearchBytesInAllMemory(
                                patternBytes, maxResults: 8, minAddr: 0x4000000000L, maxAddr: 0x4200000000L);

                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine($"dump_unit_struct pattern={command.Pattern} → {matches.Count} matches in heap range");
                            foreach (var m in matches)
                            {
                                long hpAddr = (long)m.address;
                                long baseAddr = hpAddr - 0x10;
                                var bytes = Explorer.Scanner.ReadBytes((nint)baseAddr, 256);
                                if (bytes.Length == 0) continue;
                                // Skip empty / zero structs
                                int nonzero = 0;
                                for (int i = 0; i < bytes.Length; i++) if (bytes[i] != 0) nonzero++;
                                if (nonzero < 8) continue;
                                sb.AppendLine();
                                sb.AppendLine($"=== struct base 0x{baseAddr:X} (hp at +0x10 = 0x{hpAddr:X}) ===");
                                // 16 bytes per line
                                for (int row = 0; row < 256; row += 16)
                                {
                                    var segHex = new System.Text.StringBuilder();
                                    for (int c = 0; c < 16; c++) segHex.Append($"{bytes[row + c]:X2} ");
                                    sb.AppendLine($"  +0x{row:X2}: {segHex}");
                                }
                            }
                            var outPath = System.IO.Path.Combine(_bridgeDirectory, "dump_unit_struct.txt");
                            System.IO.File.WriteAllText(outPath, sb.ToString());
                            response.Status = "completed";
                            response.Info = $"dump_unit_struct: {matches.Count} matches → claude_bridge/dump_unit_struct.txt";
                        }
                        catch (Exception ex)
                        {
                            response.Status = "failed";
                            response.Error = $"dump_unit_struct failed: {ex.Message}";
                        }
                        break;
                    }

                    case "find_toggle":
                    {
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        // Uses 3 snapshots: baseline → advanced → back. Returns
                        // addresses that went a→b→a with |b-a|==expectedDelta.
                        // Useful for ad-hoc cursor byte hunts (shop rows, picker
                        // cursors, etc.) without baking a dedicated resolver.
                        var baseLabel = command.FromLabel ?? "base";
                        var advLabel = command.ToLabel ?? "adv";
                        var backLabel = command.SearchLabel ?? "back";
                        int delta = command.SearchValue > 0 ? command.SearchValue : 1;
                        var tc = Explorer.FindToggleCandidates(baseLabel, advLabel, backLabel, maxResults: 64, expectedDelta: delta);
                        response.Info = $"find_toggle({baseLabel},{advLabel},{backLabel},d={delta}): {tc.Count} candidates";
                        var sb = new System.Text.StringBuilder();
                        foreach (var a in tc) sb.AppendLine($"  0x{(long)a:X}");
                        var outPath = System.IO.Path.Combine(_bridgeDirectory, $"find_toggle_{command.SearchLabel ?? "result"}.txt");
                        System.IO.File.WriteAllText(outPath, $"{response.Info}\n\n{sb}");
                        response.Status = "completed";
                        break;
                    }

                    case "find_monotonic":
                    {
                        // Find heap bytes whose values across N snapshots match
                        // an expected sequence (e.g. [0,1,2,3,4]). Way more
                        // selective than find_toggle for cursor-byte hunts —
                        // ~4-billion-to-1 odds against random noise on 5
                        // snapshots vs ~2,000-to-1 for a 3-snap toggle.
                        // Inputs:
                        //   pattern = comma-sep snapshot labels  ("s0,s1,s2,s3,s4")
                        //   to      = comma-sep expected u8 vals ("0,1,2,3,4")
                        //   searchLabel = output file label
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Pattern) || string.IsNullOrEmpty(command.To))
                        {
                            response.Status = "failed";
                            response.Error = "find_monotonic: pattern (snapshot labels CSV) and to (expected values CSV) required";
                            break;
                        }
                        var labels = command.Pattern.Split(',').Select(s => s.Trim()).ToArray();
                        byte[] vals;
                        try
                        {
                            vals = command.To.Split(',').Select(s => byte.Parse(s.Trim())).ToArray();
                        }
                        catch (Exception ex)
                        {
                            response.Status = "failed";
                            response.Error = $"find_monotonic: bad expected-values CSV: {ex.Message}";
                            break;
                        }
                        if (labels.Length != vals.Length)
                        {
                            response.Status = "failed";
                            response.Error = $"find_monotonic: label count ({labels.Length}) != value count ({vals.Length})";
                            break;
                        }
                        var mc = Explorer.FindMonotonicByteCandidates(labels, vals, maxResults: 64);
                        response.Info = $"find_monotonic({string.Join(",", labels)}=[{string.Join(",", vals)}]): {mc.Count} candidates";
                        var msb = new System.Text.StringBuilder();
                        foreach (var a in mc) msb.AppendLine($"  0x{(long)a:X}");
                        var outPath = System.IO.Path.Combine(_bridgeDirectory, $"find_monotonic_{command.SearchLabel ?? "result"}.txt");
                        System.IO.File.WriteAllText(outPath, $"{response.Info}\n\n{msb}");
                        response.Status = "completed";
                        break;
                    }

                    case "resolve_picker_cursor":
                    {
                        int cCount;
                        var info = ResolvePickerCursor(out cCount);
                        response.Status = info != null ? "completed" : "failed";
                        if (info == null) response.Error = "Memory explorer not initialized or game window not found";
                        response.Info = info;
                        ModLogger.Log($"[CommandBridge] {response.Info}");
                        break;
                    }

                    case "resolve_job_cursor":
                    {
                        int cCount;
                        var info = ResolveJobCursor(out cCount);
                        response.Status = info != null ? "completed" : "failed";
                        if (info == null) response.Error = "Memory explorer not initialized or game window not found";
                        response.Info = info;
                        ModLogger.Log($"[CommandBridge] {response.Info}");
                        break;
                    }

                    case "resolve_party_menu_cursor":
                    {
                        int cCount;
                        var info = ResolvePartyMenuCursor(out cCount);
                        response.Status = info != null ? "completed" : "failed";
                        if (info == null) response.Error = "Memory explorer not initialized or game window not found";
                        response.Info = info;
                        ModLogger.Log($"[CommandBridge] {response.Info}");
                        break;
                    }

                    case "resolve_equip_picker_cursor":
                    {
                        int cCount;
                        var info = ResolveEquipPickerCursor(out cCount);
                        response.Status = info != null ? "completed" : "failed";
                        if (info == null) response.Error = "Memory explorer not initialized or game window not found";
                        response.Info = info;
                        ModLogger.Log($"[CommandBridge] {response.Info}");
                        break;
                    }

                    case "resolve_eqa_row":
                    {
                        var info = ResolveEqaRow();
                        response.Status = info != null && info.StartsWith("Resolved") ? "completed" : "failed";
                        if (info == null)
                        {
                            response.Error = "Memory explorer not initialized or game window not found";
                        }
                        else if (!info.StartsWith("Resolved"))
                        {
                            response.Error = info;
                        }
                        response.Info = info;
                        ModLogger.Log($"[CommandBridge] {response.Info}");
                        break;
                    }

                    case "remove_equipment_at_cursor":
                    {
                        var info = RemoveEquipmentAtCursor();
                        response.Status = info != null && info.StartsWith("Removed") ? "completed" : "failed";
                        if (info == null)
                        {
                            response.Error = "Memory explorer not initialized or game window not found";
                        }
                        else if (!info.StartsWith("Removed"))
                        {
                            response.Error = info;
                        }
                        response.Info = info;
                        ModLogger.Log($"[CommandBridge] {response.Info}");
                        break;
                    }

                    case "read_address":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Address)) { response.Status = "failed"; response.Error = "Address required"; break; }
                        var addr = Convert.ToInt64(command.Address, 16);
                        var size = Math.Clamp(command.ReadSize, 1, 4);
                        var result2 = Explorer.ReadAbsolute((nint)addr, size);
                        if (result2 == null) { response.Status = "failed"; response.Error = $"Failed to read {size} bytes at 0x{addr:X}"; break; }
                        response.ReadResult = new ReadResult
                        {
                            Address = $"0x{addr:X}",
                            Size = size,
                            Value = result2.Value.value,
                            Hex = $"0x{result2.Value.value:X}",
                            RawBytes = BitConverter.ToString(result2.Value.raw).Replace("-", " ")
                        };
                        response.Status = "completed";
                        break;

                    case "write_address":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Address)) { response.Status = "failed"; response.Error = "Address required"; break; }
                        var writeAddr = Convert.ToInt64(command.Address, 16);
                        var writeVal = (byte)command.ReadSize; // reuse ReadSize field for the value to write
                        Explorer.Scanner.WriteByte((nint)writeAddr, writeVal);
                        response.Status = "completed";
                        break;

                    case "get_flag":
                    {
                        var name = command.SearchLabel ?? "";
                        if (string.IsNullOrEmpty(name)) { response.Status = "failed"; response.Error = "searchLabel required (flag name)"; break; }
                        var val = StateFlags.Get(name);
                        response.Status = "completed";
                        response.Info = val.HasValue ? $"{name}={val.Value}" : $"{name}=(unset)";
                        response.ReadResult = new ReadResult
                        {
                            Address = name,
                            Value = val ?? 0,
                            Size = val.HasValue ? 1 : 0,
                            Hex = val.HasValue ? val.Value.ToString() : "null",
                        };
                        break;
                    }

                    case "set_flag":
                    {
                        var name = command.SearchLabel ?? "";
                        if (string.IsNullOrEmpty(name)) { response.Status = "failed"; response.Error = "searchLabel required (flag name)"; break; }
                        int val = command.SearchValue;
                        StateFlags.Set(name, val);
                        response.Status = "completed";
                        response.Info = $"Set {name}={val}";
                        break;
                    }

                    case "list_flags":
                    {
                        var snap = StateFlags.Snapshot();
                        response.Status = "completed";
                        response.Info = snap.Count == 0
                            ? "(no flags set)"
                            : string.Join(", ", snap.Select(kv => $"{kv.Key}={kv.Value}"));
                        break;
                    }

                    case "read_block":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Address)) { response.Status = "failed"; response.Error = "Address required"; break; }
                        var blockAddr = Convert.ToInt64(command.Address, 16);
                        var blockSize = Math.Clamp(command.BlockSize, 1, 4096);
                        var blockHex = Explorer.ReadBlock((nint)blockAddr, blockSize);
                        if (blockHex == null) { response.Status = "failed"; response.Error = $"Failed to read {blockSize} bytes at 0x{blockAddr:X}"; break; }
                        response.BlockData = blockHex;
                        response.Status = "completed";
                        break;

                    case "memory_diff":
                        // Memory hunt helper: caller passes a previous snapshot
                        // hex string in `pattern`. We read current memory at
                        // address/blockSize, diff against the snapshot, return
                        // formatted "0xNN: XX -> YY" lines in BlockData. Pure
                        // diff via MemoryDiffCalculator.
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Address)) { response.Status = "failed"; response.Error = "Address required"; break; }
                        try
                        {
                            var diffAddr = Convert.ToInt64(command.Address.Replace("0x", ""), 16);
                            var diffSize = Math.Clamp(command.BlockSize, 1, 4096);
                            var beforeBytes = GameBridge.MemoryDiffCalculator.ParseHex(command.Pattern);
                            if (beforeBytes.Length != diffSize)
                            {
                                response.Status = "failed";
                                response.Error = $"memory_diff: pattern length ({beforeBytes.Length} bytes) != blockSize ({diffSize}). Pass the prior snapshot in 'pattern' as space-separated hex.";
                                break;
                            }
                            var afterBytes = Explorer.Scanner.ReadBytes((nint)diffAddr, diffSize);
                            if (afterBytes == null || afterBytes.Length != diffSize)
                            {
                                response.Status = "failed";
                                response.Error = $"memory_diff: failed to read {diffSize} bytes at 0x{diffAddr:X}";
                                break;
                            }
                            var diffs = GameBridge.MemoryDiffCalculator.Diff(beforeBytes, afterBytes);
                            response.BlockData = GameBridge.MemoryDiffCalculator.FormatDiffs(diffs);
                            response.Status = "completed";
                            response.Info = $"memory_diff: {diffs.Count} byte(s) changed of {diffSize}.";
                        }
                        catch (Exception ex) { response.Status = "failed"; response.Error = $"memory_diff: {ex.Message}"; }
                        break;

                    case "read_bytes":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        try
                        {
                            long readAddr = Convert.ToInt64(command.Pattern, 16);
                            int readSize = command.SearchValue > 0 ? command.SearchValue : 256;
                            if (readSize > 1024) readSize = 1024;
                            var readData = Explorer.Scanner.ReadBytes((nint)readAddr, readSize);
                            response.Status = "completed";
                            response.ReadResult = new ReadResult
                            {
                                Address = $"0x{readAddr:X}",
                                Size = readData.Length,
                                Hex = BitConverter.ToString(readData).Replace("-", " ")
                            };
                        }
                        catch (Exception ex)
                        {
                            response.Status = "failed";
                            response.Error = $"Read failed: {ex.Message}";
                        }
                        break;

                    case "search_bytes":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Pattern)) { response.Status = "failed"; response.Error = "Pattern required (hex string, e.g. '080B00')"; break; }
                        try
                        {
                            // Parse hex string to byte array
                            var hexClean = command.Pattern.Replace(" ", "").Replace("-", "");
                            var patternBytes = new byte[hexClean.Length / 2];
                            for (int i = 0; i < patternBytes.Length; i++)
                                patternBytes[i] = Convert.ToByte(hexClean.Substring(i * 2, 2), 16);

                            // Session 47: honor optional minAddr/maxAddr range
                            // params. Defaults preserve prior behavior (full
                            // memory scan) when callers omit both. Narrow
                            // ranges unblock heap-targeted searches — the
                            // default 100-match cap previously filled up on
                            // main-module hits before reaching 0x4000000000+.
                            var plan = SearchBytesPlan.From(command);
                            var matches = (command.MinAddr != null || command.MaxAddr != null || command.BroadSearch)
                                ? Explorer.SearchBytesInAllMemory(patternBytes, 100, plan.MinAddr, plan.MaxAddr, plan.BroadSearch)
                                : Explorer.SearchBytesInAllMemory(patternBytes, 100);

                            // Write results to file
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine($"Byte pattern search: {command.Pattern} ({patternBytes.Length} bytes)");
                            sb.AppendLine($"Searched at: {DateTime.UtcNow:O}");
                            sb.AppendLine($"Found {matches.Count} matches");
                            sb.AppendLine();
                            var unitBase = Explorer.Scanner.UnitDataBase;
                            foreach (var (matchAddr, ctx) in matches)
                            {
                                long dist = Math.Abs((long)matchAddr - (long)unitBase);
                                string proximity = dist < 0x200000 ? " ** NEAR **" : "";
                                sb.AppendLine($"  0x{matchAddr:X}{proximity}");
                                sb.AppendLine($"    {ctx}");
                            }
                            var searchPath = System.IO.Path.Combine(_bridgeDirectory, $"search_bytes_{command.SearchLabel ?? "result"}.txt");
                            System.IO.File.WriteAllText(searchPath, sb.ToString());

                            // Also put summary in response
                            response.ReadResult = new ReadResult
                            {
                                Address = command.Pattern,
                                Size = patternBytes.Length,
                                Value = matches.Count,
                                Hex = $"{matches.Count} matches",
                                RawBytes = matches.Count > 0 ? $"0x{matches[0].address:X}" : "none"
                            };
                            response.Status = "completed";
                        }
                        catch (Exception ex)
                        {
                            response.Status = "error";
                            response.Error = $"Pattern parse error: {ex.Message}";
                        }
                        break;

                    case "probe_status":
                        // Find a unit by HP pattern and dump 128 bytes before stat pattern for status investigation
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        try
                        {
                            int probeHp = command.SearchValue > 0 ? command.SearchValue : 0;
                            if (probeHp <= 0) { response.Status = "failed"; response.Error = "Provide searchValue = unit's maxHP"; break; }
                            byte hpLo = (byte)(probeHp & 0xFF);
                            byte hpHi = (byte)(probeHp >> 8);
                            // Search for MaxHP MaxHP pattern (HP == MaxHP for full health units)
                            var probePattern = new byte[] { hpLo, hpHi, hpLo, hpHi };
                            var probeMatches = Explorer.SearchBytesInAllMemory(probePattern, 10);
                            if (probeMatches.Count == 0) { response.Status = "failed"; response.Error = $"No match for HP={probeHp}"; break; }

                            // For each match, verify it's a real unit struct by checking exp/level at -8
                            foreach (var (probeAddr, _) in probeMatches)
                            {
                                nint statBase = probeAddr - 8; // stat pattern starts 8 bytes before HP
                                var verifyBytes = Explorer.Scanner.ReadBytes(statBase, 8);
                                if (verifyBytes.Length < 8) continue;
                                byte expByte = verifyBytes[0];
                                byte levelByte = verifyBytes[1];
                                if (levelByte < 1 || levelByte > 99 || expByte > 99) continue;

                                // Read 128 bytes BEFORE stat pattern + 128 bytes after = 256 total
                                nint readStart = statBase - 128;
                                var dumpBytes = Explorer.Scanner.ReadBytes(readStart, 384);
                                if (dumpBytes.Length == 0) continue;

                                var hexStr = BitConverter.ToString(dumpBytes).Replace("-", "");
                                response.Status = "completed";
                                response.Error = $"addr=0x{statBase:X} lv={levelByte} exp={expByte} | pre128+stat+post256: {hexStr}";
                                break;
                            }
                            if (response.Status != "completed")
                            {
                                response.Status = "failed";
                                response.Error = $"Found {probeMatches.Count} HP matches but none verified as unit struct";
                            }
                        }
                        catch (Exception ex)
                        {
                            response.Status = "error";
                            response.Error = $"Probe error: {ex.Message}";
                        }
                        break;

                    case "search_all":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Pattern)) { response.Status = "failed"; response.Error = "Pattern required (hex string, e.g. '080B00')"; break; }
                        try
                        {
                            var hexAll = command.Pattern.Replace(" ", "").Replace("-", "");
                            var patternAll = new byte[hexAll.Length / 2];
                            for (int i = 0; i < patternAll.Length; i++)
                                patternAll[i] = Convert.ToByte(hexAll.Substring(i * 2, 2), 16);

                            var allMatches = Explorer.SearchBytesAllRegions(patternAll, 100);

                            var sbAll = new System.Text.StringBuilder();
                            sbAll.AppendLine($"Byte pattern search (ALL regions): {command.Pattern} ({patternAll.Length} bytes)");
                            sbAll.AppendLine($"Searched at: {DateTime.UtcNow:O}");
                            sbAll.AppendLine($"Found {allMatches.Count} matches");
                            sbAll.AppendLine();
                            var unitBaseAll = Explorer.Scanner.UnitDataBase;
                            foreach (var (matchAddr, ctx) in allMatches)
                            {
                                long dist = Math.Abs((long)matchAddr - (long)unitBaseAll);
                                string proximity = dist < 0x200000 ? " ** NEAR **" : "";
                                sbAll.AppendLine($"  0x{matchAddr:X}{proximity}");
                                sbAll.AppendLine($"    {ctx}");
                            }
                            var searchAllPath = System.IO.Path.Combine(_bridgeDirectory, $"search_all_{command.SearchLabel ?? "result"}.txt");
                            System.IO.File.WriteAllText(searchAllPath, sbAll.ToString());

                            response.ReadResult = new ReadResult
                            {
                                Address = command.Pattern,
                                Size = patternAll.Length,
                                Value = allMatches.Count,
                                Hex = $"{allMatches.Count} matches (all regions)",
                                RawBytes = allMatches.Count > 0 ? $"0x{allMatches[0].address:X}" : "none"
                            };
                            response.Status = "completed";
                        }
                        catch (Exception ex)
                        {
                            response.Status = "error";
                            response.Error = $"Pattern parse error: {ex.Message}";
                        }
                        break;

                    case "scrape_shop_items":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        try
                        {
                            var items = GameBridge.ShopItemScraper.ScrapeVisibleItems(Explorer);
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine($"Shop item scrape: {items.Count} items found");
                            foreach (var item in items)
                                sb.AppendLine($"  0x{item.Address:X}  {item.Name}");
                            response.Info = sb.ToString();
                            response.Status = "completed";
                        }
                        catch (Exception ex) { response.Status = "error"; response.Error = ex.Message; }
                        break;

                    case "shop_stock":
                        // Session 54: decode the current shop's stock
                        // from the static game-data bitmap table. Two
                        // calling modes:
                        //
                        //   Auto (no pattern): read location byte
                        //   (0x14077D208) + use LocationId/UnitIndex
                        //   override for chapter, look up expected
                        //   bitmap in ShopBitmapRegistry, decode.
                        //
                        //   Manual (pattern supplied): caller provides
                        //   the 8-byte bitmap hex directly. Used for
                        //   bootstrapping new shops that aren't in
                        //   the registry yet.
                        //
                        // Category defaults to Weapons; non-weapons
                        // categories don't use this bitmap encoding in
                        // the static table (shields/helms/body/etc.
                        // are TBD — see project_shop_stock_SHIPPED.md).
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        try
                        {
                            string catRaw = command.Description ?? "weapons";
                            if (!Enum.TryParse<GameBridge.ShopStockDecoder.Category>(catRaw, ignoreCase: true, out var cat))
                                cat = GameBridge.ShopStockDecoder.Category.Weapons;

                            byte[]? bmp = null;
                            int expectedCount = (int)command.SearchValue;
                            string source = "manual";
                            int priceLocation = -1;
                            int priceChapter = -1;

                            if (!string.IsNullOrEmpty(command.Pattern))
                            {
                                var hexClean = command.Pattern.Replace(" ", "").Replace("-", "");
                                if (hexClean.Length != 16)
                                {
                                    response.Status = "failed";
                                    response.Error = $"shop_stock pattern must be exactly 16 hex chars (8 bytes); got {hexClean.Length}";
                                    break;
                                }
                                bmp = new byte[8];
                                for (int i = 0; i < 8; i++)
                                    bmp[i] = Convert.ToByte(hexClean.Substring(i * 2, 2), 16);

                                // Still propagate location/chapter when
                                // supplied via command args so manual
                                // mode also benefits from per-chapter
                                // price overrides.
                                if (command.LocationId >= 0) priceLocation = command.LocationId;
                                if (command.UnitIndex > 0) priceChapter = command.UnitIndex;
                            }
                            else
                            {
                                // Auto mode: read location from memory
                                // unless caller overrode via LocationId.
                                // Chapter defaults to 1 (current save
                                // context); callers can override via
                                // UnitIndex.
                                priceLocation = command.LocationId >= 0
                                    ? command.LocationId
                                    : Explorer.Scanner.ReadByte((nint)0x14077D208L);
                                priceChapter = command.UnitIndex > 0 ? command.UnitIndex : 1;

                                // Registry lookup: try the caller-
                                // specified category first then
                                // fall through ALL known categories.
                                // Weapons-vs-Daggers swap is common
                                // (Gariland etc. sell daggers, not
                                // staves). For explicit non-weapon
                                // categories (Helms/Shields/...) we
                                // still check all so auto-mode
                                // "just works" when a shop lacks
                                // that specific category but has
                                // others registered.
                                var allCategories = new[] {
                                    cat,   // Try caller's choice first
                                    GameBridge.ShopStockDecoder.Category.Weapons,
                                    GameBridge.ShopStockDecoder.Category.Daggers,
                                    GameBridge.ShopStockDecoder.Category.Shields,
                                    GameBridge.ShopStockDecoder.Category.Helms,
                                    GameBridge.ShopStockDecoder.Category.Body,
                                    GameBridge.ShopStockDecoder.Category.Accessories,
                                    GameBridge.ShopStockDecoder.Category.Consumables
                                };
                                var seen = new HashSet<GameBridge.ShopStockDecoder.Category>();
                                var categoriesToTry = new List<GameBridge.ShopStockDecoder.Category>();
                                foreach (var c in allCategories)
                                {
                                    if (seen.Add(c)) categoriesToTry.Add(c);
                                }

                                foreach (var tryCat in categoriesToTry)
                                {
                                    var tryBmp = GameBridge.ShopBitmapRegistry.Lookup(priceLocation, priceChapter, tryCat);
                                    if (tryBmp != null)
                                    {
                                        bmp = tryBmp;
                                        cat = tryCat;
                                        break;
                                    }
                                }
                                if (bmp == null)
                                {
                                    response.Status = "failed";
                                    response.Error = $"shop_stock: no record registered for (location={priceLocation}, chapter={priceChapter}) in any category. Supply pattern=<hex> or add to ShopBitmapRegistry.";
                                    break;
                                }
                                source = $"auto(loc={priceLocation},ch={priceChapter})";
                            }

                            if (expectedCount == 0)
                            {
                                // Derive from bitmap/id-array if caller
                                // didn't specify. For id-array format,
                                // count non-zero bytes; for bitmap,
                                // count set bits.
                                if (GameBridge.ShopStockDecoder.FormatForCategory(cat) == GameBridge.ShopStockDecoder.RecordFormat.IdArray)
                                {
                                    foreach (var b in bmp)
                                        if (b != 0) expectedCount++;
                                }
                                else
                                {
                                    foreach (var b in bmp)
                                        for (int bit = 0; bit < 8; bit++)
                                            if ((b & (1 << bit)) != 0) expectedCount++;
                                }
                            }

                            var decoder = new GameBridge.ShopStockDecoder(Explorer);
                            long recAddr;
                            if (GameBridge.ShopStockDecoder.FormatForCategory(cat) == GameBridge.ShopStockDecoder.RecordFormat.IdArray)
                            {
                                // For id-array, pass the non-zero prefix
                                // as the "expected ids" pattern.
                                int nz = 0;
                                while (nz < bmp.Length && bmp[nz] != 0) nz++;
                                var expectedIds = new byte[nz];
                                Array.Copy(bmp, 0, expectedIds, 0, nz);
                                recAddr = decoder.LocateIdArrayRecord(expectedIds);
                            }
                            else
                            {
                                recAddr = decoder.LocateBitmapRecord(bmp, expectedCount);
                            }
                            if (recAddr == 0)
                            {
                                var bmpHex = BitConverter.ToString(bmp).Replace("-", "");
                                response.Status = "failed";
                                response.Error = $"Record not found for pattern {bmpHex} count={expectedCount} format={GameBridge.ShopStockDecoder.FormatForCategory(cat)} (source={source})";
                                break;
                            }
                            // Pass expectedCount through so the decoder
                            // rejects false-positive locates that decode
                            // to wrong item counts (Lesalia/Warjilis
                            // Consumables phantoms — see DecodeStockAt
                            // session 55 fix). Mismatches return empty
                            // rather than partial wrong data.
                            var stock = decoder.DecodeStockAt(recAddr, cat, priceLocation, priceChapter, expectedCount);
                            if (stock.Count == 0)
                            {
                                response.Status = "failed";
                                response.Error = $"Record located at 0x{recAddr:X} but decode returned no items matching expected count {expectedCount} (false-positive locate).";
                                break;
                            }

                            // Seed the resolver's cache so future
                            // screen.stockItems calls find this
                            // category without re-running the
                            // expensive broad-search. The screen
                            // path uses narrow-only locates and
                            // can't reach memory-mapped records on
                            // its own; the dedicated shop_stock
                            // action's broad-search is the
                            // intended seeding mechanism.
                            if (priceLocation >= 0 && priceChapter >= 0)
                                GameBridge.ShopStockResolver.SeedCache(priceLocation, priceChapter, cat, recAddr);

                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine($"shop_stock {cat} [{source}] — record @ 0x{recAddr:X}, {stock.Count} items:");
                            foreach (var it in stock)
                                sb.AppendLine($"  id={it.Id,3} {it.Name,-20} type={it.Type,-12} price={(it.BuyPrice?.ToString() ?? "?")}");
                            response.Info = sb.ToString();
                            response.ShopStock = stock;
                            response.Status = "completed";
                        }
                        catch (Exception ex) { response.Status = "error"; response.Error = ex.Message; }
                        break;

                    case "seed_shop_stock":
                        // Session 55: one-shot helper to populate the
                        // resolver cache for ALL registered categories
                        // at the current shop. Default mode walks the
                        // ShopBitmapRegistry. Pass description="auto"
                        // to use experimental LiveShopScanner instead
                        // (heuristic active-widget scan; currently too
                        // noisy for general use, kept opt-in while
                        // investigation continues).
                        //
                        // Use case: player enters a shop in a new save
                        // / chapter / location. Run seed_shop_stock
                        // once up front; from then on
                        // screen.stockItems is populated and stable.
                        //
                        // Why it exists: the screen-assembly path
                        // intentionally does ZERO AoB scans (would
                        // crash the game under repeated polling).
                        // Seeding has to happen explicitly.
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        try
                        {
                            int seedLocation = command.LocationId >= 0
                                ? command.LocationId
                                : Explorer.Scanner.ReadByte((nint)0x14077D208L);
                            int seedChapter = command.UnitIndex > 0 ? command.UnitIndex : 1;
                            bool useLiveScanner = string.Equals(command.Description, "auto", StringComparison.OrdinalIgnoreCase);

                            var decoder = new GameBridge.ShopStockDecoder(Explorer);
                            var sb = new System.Text.StringBuilder();
                            int seeded = 0, failed = 0;
                            string mode = useLiveScanner ? "live-widget scan" : "registry lookup";
                            sb.AppendLine($"seed_shop_stock loc={seedLocation} ch={seedChapter} ({mode}):");

                            if (useLiveScanner)
                            {
                                var found = GameBridge.LiveShopScanner.ScanAll(Explorer);
                                foreach (var rec in found)
                                {
                                    var decoded = decoder.DecodeStockAt(
                                        rec.Address, rec.Category,
                                        seedLocation, seedChapter, rec.ItemCount);
                                    if (decoded.Count == rec.ItemCount)
                                    {
                                        GameBridge.ShopStockResolver.SeedCache(seedLocation, seedChapter, rec.Category, rec.Address);
                                        sb.AppendLine($"  {rec.Category}: SEEDED @ 0x{rec.Address:X} ({decoded.Count} items)");
                                        seeded++;
                                    }
                                    else
                                    {
                                        sb.AppendLine($"  {rec.Category}: STALE @ 0x{rec.Address:X} (got {decoded.Count} on decode, scanner said {rec.ItemCount})");
                                        failed++;
                                    }
                                }
                            }
                            else
                            {
                                foreach (var cat in GameBridge.ShopBitmapRegistry.RegisteredCategoriesFor(seedLocation, seedChapter))
                                {
                                    var bmp = GameBridge.ShopBitmapRegistry.Lookup(seedLocation, seedChapter, cat);
                                    if (bmp == null) continue;

                                    int expCount = GameBridge.ShopStockDecoder.FormatForCategory(cat)
                                        == GameBridge.ShopStockDecoder.RecordFormat.IdArray
                                            ? GameBridge.ShopStockResolver.CountIdArrayIds(bmp)
                                            : GameBridge.ShopStockResolver.CountBits(bmp);

                                    long recAddr;
                                    if (GameBridge.ShopStockDecoder.FormatForCategory(cat) == GameBridge.ShopStockDecoder.RecordFormat.IdArray)
                                    {
                                        int nz = GameBridge.ShopStockResolver.CountIdArrayIds(bmp);
                                        var ids = new byte[nz];
                                        Array.Copy(bmp, 0, ids, 0, nz);
                                        recAddr = decoder.LocateIdArrayRecord(ids);
                                    }
                                    else
                                    {
                                        recAddr = decoder.LocateBitmapRecord(bmp, expCount);
                                    }

                                    if (recAddr == 0)
                                    {
                                        sb.AppendLine($"  {cat}: NOT FOUND");
                                        failed++;
                                        continue;
                                    }

                                    var decoded = decoder.DecodeStockAt(recAddr, cat, seedLocation, seedChapter, expCount);
                                    if (decoded.Count == expCount)
                                    {
                                        GameBridge.ShopStockResolver.SeedCache(seedLocation, seedChapter, cat, recAddr);
                                        sb.AppendLine($"  {cat}: SEEDED @ 0x{recAddr:X} ({decoded.Count} items)");
                                        seeded++;
                                    }
                                    else
                                    {
                                        sb.AppendLine($"  {cat}: VALIDATION FAILED (got {decoded.Count}, expected {expCount})");
                                        failed++;
                                    }
                                }
                            }

                            sb.AppendLine($"--- {seeded} seeded, {failed} failed");
                            response.Info = sb.ToString();
                            response.Status = "completed";
                        }
                        catch (Exception ex) { response.Status = "error"; response.Error = ex.Message; }
                        break;

                    case "cheat_mode_buff":
                        // Session 47 pt 5: TODO §0 dev tool. Buff Ramza (or a
                        // named target) to near-invincible for one battle so
                        // state-collection playthroughs don't get bottlenecked
                        // by fresh-game stats. Writes HP/MaxHP/PA/affinity.
                        // Deliberately NOT touching level / exp / brave / faith
                        // — see BuffPlanner comments.
                        //
                        // Session 49: when command.Pattern == "all", buffs every
                        // player-side (team=0) battle slot, not just the first.
                        // Useful for multi-party story battles.
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        try
                        {
                            const long BattleArrayBase = 0x140893C00L;
                            const int ArrayStride = 0x200;
                            bool buffAll = string.Equals(command.Pattern, "all", StringComparison.OrdinalIgnoreCase);

                            // Collect player slot positions from latest scan
                            // to distinguish team=0 (player) from team≠0 slots.
                            // Without a team byte in the battle array we use
                            // roster-match position as the player filter.
                            var playerPositions = new HashSet<(int, int)>(
                                _navActions?.GetPlayerSlotPositions() ?? new List<(int x, int y)>());

                            var targetSlots = new List<long>();

                            // Scan forward slots (player side). For buff-single
                            // mode, stop at first valid slot. For buff-all,
                            // collect every player-team slot.
                            for (int s = 0; s < 10; s++)
                            {
                                long slotBase = BattleArrayBase + (long)s * ArrayStride;
                                var inBattleRead = Explorer.ReadAbsolute((nint)(slotBase + 0x12), 2);
                                if (!inBattleRead.HasValue) continue;
                                if ((int)inBattleRead.Value.value == 0) continue;

                                var lvlRead = Explorer.ReadAbsolute((nint)(slotBase + 0x0D), 1);
                                if (!lvlRead.HasValue) continue;
                                int lvl = (int)lvlRead.Value.value;
                                if (lvl < 1 || lvl > 99) continue;

                                var hpRead = Explorer.ReadAbsolute((nint)(slotBase + 0x16), 2);
                                if (!hpRead.HasValue) continue;
                                int maxHp = (int)hpRead.Value.value;
                                if (maxHp <= 0 || maxHp >= 2000) continue;

                                if (buffAll)
                                {
                                    // Only buff if this slot matches a scanned
                                    // player position. Without the match guard
                                    // we could buff a team-2 guest ally we
                                    // don't want (e.g. dismissable Agrias).
                                    var gx = Explorer.ReadAbsolute((nint)(slotBase + 0x33), 1);
                                    var gy = Explorer.ReadAbsolute((nint)(slotBase + 0x34), 1);
                                    if (!gx.HasValue || !gy.HasValue) continue;
                                    var pos = ((int)gx.Value.value, (int)gy.Value.value);
                                    if (!playerPositions.Contains(pos)) continue;
                                    targetSlots.Add(slotBase);
                                }
                                else
                                {
                                    // Single-buff mode: first valid slot wins.
                                    targetSlots.Add(slotBase);
                                    break;
                                }
                            }

                            if (targetSlots.Count == 0)
                            {
                                response.Status = "failed";
                                response.Error = buffAll
                                    ? "cheat_mode_buff all: no roster-matched player slots. Run scan_move first or be in a battle."
                                    : "cheat_mode_buff: no active player-side battle slot found. Must be in a battle.";
                                break;
                            }

                            int hpValue = command.SearchValue > 0 ? command.SearchValue : 999;
                            int totalWritten = 0;
                            foreach (var slot in targetSlots)
                            {
                                var plan = GameBridge.BuffPlanner.PlanInvincibilityWrites(slot, hpValue);
                                foreach (var op in plan)
                                {
                                    for (int i = 0; i < op.Bytes.Length; i++)
                                    {
                                        Explorer.Scanner.WriteByte((nint)(op.Address + i), op.Bytes[i]);
                                        totalWritten++;
                                    }
                                }
                                ModLogger.Log($"[CheatMode] Buffed battle slot 0x{slot:X}: HP={hpValue}, PA=255, Absorb=0xFF");
                            }

                            response.Info = targetSlots.Count == 1
                                ? $"Buffed slot at 0x{targetSlots[0]:X} ({totalWritten} bytes written, HP={hpValue} PA=255 Absorb=All)"
                                : $"Buffed {targetSlots.Count} player slots ({totalWritten} bytes written, HP={hpValue} PA=255 Absorb=All)";
                            response.Status = "completed";
                        }
                        catch (Exception ex)
                        {
                            response.Status = "error";
                            response.Error = ex.Message;
                        }
                        break;

                    case "cheat_kill_enemies":
                        // Session 49 rewrite: discovers the master HP table at
                        // runtime (base shifts per-battle, ~0x14184xxxx, stride
                        // 0x200), walks it to build per-slot (Hp, MaxHp) records
                        // tagged with player vs enemy, hands to KillEnemiesPlanner
                        // which emits HP=0 + dead-bit (+0x31 |= 0x20) writes.
                        //
                        // Session 48 version hammered the BATTLE ARRAY at
                        // 0x140893C00 — that's a mirror, the game re-derived HP
                        // each frame, writes reverted (feedback_hp_is_derived.md).
                        // This version writes the MASTER directly; no hammering.
                        //
                        // Player vs enemy: match slots to scanned-unit HP
                        // fingerprints (team 0 entries from latest scan). The
                        // master table has no team byte, so HP+MaxHP pairs are
                        // the discriminator. See memory/project_master_hp_store.md.
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        try
                        {
                            // --- Collect player fingerprints from last scan
                            var playerFingerprints = _navActions?.GetPlayerHpFingerprints()
                                ?? new List<(int Hp, int MaxHp)>();
                            if (playerFingerprints.Count == 0)
                            {
                                response.Status = "failed";
                                response.Error = "cheat_kill_enemies: no scanned player units. Run scan_move first or be in a battle.";
                                break;
                            }

                            // --- Find the master HP table via anchor search
                            // Use broadSearch=true to hit main-module read-only
                            // pages. For each player fingerprint, search for
                            // the 4-byte (HP u16, MaxHp u16) pattern. Each hit
                            // is a candidate slot; walk backward at stride
                            // 0x200 (allowing interior empty slots) to find
                            // the table base. Session 49: table stride is
                            // 0x200 but the base alignment within the
                            // 0x141800000 region is arbitrary (observed
                            // +0xC0..+0xD8C0 offsets).
                            const long SearchMin = 0x141800000L;
                            const long SearchMax = 0x141900000L;
                            const int SlotStride = 0x200;
                            const int MaxBackwardSlots = 40;
                            const int MaxForwardSlots = 40;

                            long? tableBase = null;
                            int tableSlotCount = 0;

                            foreach (var (hp, maxHp) in playerFingerprints)
                            {
                                byte[] pattern = new byte[]
                                {
                                    (byte)(hp & 0xFF),    (byte)((hp >> 8) & 0xFF),
                                    (byte)(maxHp & 0xFF), (byte)((maxHp >> 8) & 0xFF),
                                };
                                var matches = Explorer.SearchBytesInAllMemory(
                                    pattern, 16, SearchMin, SearchMax, broadSearch: true);
                                foreach (var (matchAddr, _) in matches)
                                {
                                    long anchor = (long)matchAddr;
                                    // Walk backward 40 slots, never breaking
                                    // on empty (interior gaps are allowed).
                                    long lowest = anchor;
                                    int lowestValidIdx = 0;
                                    for (int back = 1; back <= MaxBackwardSlots; back++)
                                    {
                                        long prev = anchor - back * SlotStride;
                                        if (prev < SearchMin) break;
                                        var ph = Explorer.ReadAbsolute((nint)prev, 2);
                                        var pm = Explorer.ReadAbsolute((nint)(prev + 2), 2);
                                        if (!ph.HasValue || !pm.HasValue) break;
                                        int pMax = (int)pm.Value.value;
                                        int pHp = (int)ph.Value.value;
                                        // Valid slot: MaxHp ∈ [1, 2000] and HP ≤ MaxHp.
                                        // Empty slot (MaxHp==0, HP==0) is allowed interior.
                                        bool validSlot = pMax >= 1 && pMax <= 2000 && pHp <= pMax;
                                        bool emptySlot = pMax == 0 && pHp == 0;
                                        if (!validSlot && !emptySlot) break;
                                        if (validSlot)
                                        {
                                            lowest = prev;
                                            lowestValidIdx = back;
                                        }
                                    }

                                    // From lowest valid slot, count forward
                                    // coverage (valid + empty interior).
                                    int coverage = 0;
                                    for (int fwd = 0; fwd <= MaxForwardSlots; fwd++)
                                    {
                                        long slotAddr = lowest + fwd * SlotStride;
                                        if (slotAddr >= SearchMax) break;
                                        var h = Explorer.ReadAbsolute((nint)slotAddr, 2);
                                        var m = Explorer.ReadAbsolute((nint)(slotAddr + 2), 2);
                                        if (!h.HasValue || !m.HasValue) break;
                                        int mv = (int)m.Value.value;
                                        int hv = (int)h.Value.value;
                                        bool valid = mv >= 1 && mv <= 2000 && hv <= mv;
                                        bool empty = mv == 0 && hv == 0;
                                        if (!valid && !empty) break;
                                        coverage = fwd + 1;
                                    }

                                    if (coverage > tableSlotCount)
                                    {
                                        tableBase = lowest;
                                        tableSlotCount = coverage;
                                    }
                                }
                            }

                            ModLogger.Log($"[CheatKill] anchor search: fpCount={playerFingerprints.Count} tableBase={(tableBase.HasValue ? "0x" + tableBase.Value.ToString("X") : "null")} coverage={tableSlotCount}");

                            if (tableBase == null || tableSlotCount < 2)
                            {
                                response.Status = "failed";
                                response.Error = "cheat_kill_enemies: master HP table not found. Run scan_move on BattleMyTurn first.";
                                break;
                            }

                            // --- Build battle-array (HP, MaxHp) → slot map.
                            // Session 49: undead with Reraise auto-revive on
                            // turn rollover, defeating a plain HP=0 + dead-bit.
                            // To also clear the Reraise status bit (+0x47 bit
                            // 0x20 in battle-array) we need the battle-array
                            // slot for each master slot. Match by
                            // (HP, MaxHp) fingerprint — same mechanism as the
                            // master-table discovery.
                            //
                            // Battle array base is constant 0x140893C00 per
                            // project_buff_ramza_offsets.md. Scan the same
                            // slot range the scan-array walker uses.
                            const long BattleArrayBaseConst = 0x140893C00L;
                            const int BattleArrayStrideConst = 0x200;
                            const int BattleSlotsBack = 20;
                            const int BattleSlotsForward = 10;
                            var battleArrayByFp = new Dictionary<(int, int), long>();
                            for (int s = 0; s < BattleSlotsBack + BattleSlotsForward; s++)
                            {
                                long baSlot = BattleArrayBaseConst
                                    + (long)(s - BattleSlotsBack + 1) * BattleArrayStrideConst;
                                var bhR = Explorer.ReadAbsolute((nint)(baSlot + 0x14), 2);
                                var bmR = Explorer.ReadAbsolute((nint)(baSlot + 0x16), 2);
                                if (!bhR.HasValue || !bmR.HasValue) continue;
                                int bh = (int)bhR.Value.value;
                                int bm = (int)bmR.Value.value;
                                if (bm <= 0 || bm > 2000) continue;
                                if (bh > bm) continue;
                                // First match wins (session-49 obs: duplicate
                                // slots in both tables; first is typically live).
                                if (!battleArrayByFp.ContainsKey((bh, bm)))
                                    battleArrayByFp[(bh, bm)] = baSlot;
                            }

                            // --- Walk master table, build per-slot records
                            var fpSet = new HashSet<(int, int)>(playerFingerprints);
                            var slots = new List<GameBridge.KillEnemySlot>();
                            for (int i = 0; i < tableSlotCount; i++)
                            {
                                long slotAddr = tableBase.Value + i * SlotStride;
                                var h = Explorer.ReadAbsolute((nint)slotAddr, 2);
                                var m = Explorer.ReadAbsolute((nint)(slotAddr + 2), 2);
                                if (!h.HasValue || !m.HasValue) continue;
                                int hv = (int)h.Value.value;
                                int mv = (int)m.Value.value;
                                bool isPlayer = fpSet.Contains((hv, mv));

                                // Look up matching battle-array slot for
                                // status-bit access. If found, also read the
                                // current status byte 2 so the planner knows
                                // whether Reraise is set.
                                long baSlotBase = 0;
                                byte statusByte2 = 0;
                                if (battleArrayByFp.TryGetValue((hv, mv), out var ba))
                                {
                                    baSlotBase = ba;
                                    var sb = Explorer.ReadAbsolute((nint)(ba + 0x47), 1);
                                    if (sb.HasValue) statusByte2 = (byte)sb.Value.value;
                                }

                                slots.Add(new GameBridge.KillEnemySlot
                                {
                                    SlotBase = slotAddr,
                                    Hp = hv,
                                    MaxHp = mv,
                                    IsPlayer = isPlayer,
                                    BattleArraySlotBase = baSlotBase,
                                    CurrentStatusByte2 = statusByte2,
                                });
                            }

                            // --- Plan + dispatch
                            var plan = GameBridge.KillEnemiesPlanner.Plan(slots);
                            foreach (var op in plan)
                            {
                                for (int i = 0; i < op.Bytes.Length; i++)
                                    Explorer.Scanner.WriteByte((nint)(op.Address + i), op.Bytes[i]);
                            }

                            int enemiesKilled = plan.Count / 2;
                            int playerSkipped = slots.Count(s => s.IsPlayer);
                            ModLogger.Log($"[CheatKill] masterHP table base=0x{tableBase.Value:X} slots={tableSlotCount} killed={enemiesKilled} players={playerSkipped}");

                            response.Info = $"Killed {enemiesKilled} enemy slot(s) via master HP table at 0x{tableBase.Value:X} (stride 0x200, {tableSlotCount} slots scanned, {playerSkipped} player slot(s) skipped). End your turn to trigger victory.";
                            response.Status = "completed";
                        }
                        catch (Exception ex)
                        {
                            response.Status = "error";
                            response.Error = ex.Message;
                        }
                        break;

                    case "cheat_kill_one":
                        // Session 51 dev tool: cheat-KO a single named party
                        // member. Mirrors cheat_kill_enemies but targets ONE
                        // player slot by name instead of every enemy. Use
                        // case: test revive_all end-to-end by KO'ing one
                        // party member, then running revive_all, then
                        // confirming they stand up.
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        try
                        {
                            var targetName = command.To;
                            if (string.IsNullOrWhiteSpace(targetName))
                            {
                                response.Status = "failed";
                                response.Error = "cheat_kill_one: target name required (pass via 'to' field).";
                                break;
                            }
                            var targetFp = _navActions?.GetPlayerFingerprintByName(targetName);
                            if (!targetFp.HasValue)
                            {
                                response.Status = "failed";
                                response.Error = $"cheat_kill_one: no scanned player named '{targetName}'. Run scan_move first.";
                                break;
                            }
                            if (targetFp.Value.Hp == 0)
                            {
                                response.Status = "failed";
                                response.Error = $"cheat_kill_one: '{targetName}' is already KO'd (Hp=0).";
                                break;
                            }

                            // Anchor search for THIS unit's fingerprint in the
                            // master HP table, then write HP=0 + dead-bit.
                            // Single slot, so no table-base walk needed — just
                            // the first matching address.
                            const long KillOneSearchMin = 0x141800000L;
                            const long KillOneSearchMax = 0x141900000L;
                            byte[] pattern = new byte[]
                            {
                                (byte)(targetFp.Value.Hp & 0xFF),    (byte)((targetFp.Value.Hp >> 8) & 0xFF),
                                (byte)(targetFp.Value.MaxHp & 0xFF), (byte)((targetFp.Value.MaxHp >> 8) & 0xFF),
                            };
                            var matches = Explorer.SearchBytesInAllMemory(
                                pattern, 16, KillOneSearchMin, KillOneSearchMax, broadSearch: true);
                            if (matches.Count == 0)
                            {
                                response.Status = "failed";
                                response.Error = $"cheat_kill_one: fingerprint {targetFp.Value.Hp}/{targetFp.Value.MaxHp} for '{targetName}' not found in master HP table.";
                                break;
                            }

                            // First match = master slot (duplicates exist at
                            // slot[16]/[20] per project_master_hp_dup_player.md
                            // but the first hit is typically the live slot).
                            long slotAddr = (long)matches[0].address;
                            // Write HP=0 (u16 at +0x00).
                            Explorer.Scanner.WriteByte((nint)slotAddr, 0x00);
                            Explorer.Scanner.WriteByte((nint)(slotAddr + 1), 0x00);
                            // Set dead-bit: +0x31 bit 0x20.
                            var statusRead = Explorer.ReadAbsolute((nint)(slotAddr + 0x31), 1);
                            byte statusByte = statusRead.HasValue
                                ? (byte)((int)statusRead.Value.value | 0x20)
                                : (byte)0x20;
                            Explorer.Scanner.WriteByte((nint)(slotAddr + 0x31), statusByte);

                            ModLogger.Log($"[CheatKillOne] KO'd '{targetName}' fp={targetFp.Value.Hp}/{targetFp.Value.MaxHp} at 0x{slotAddr:X}");
                            response.Info = $"KO'd '{targetName}' (fp {targetFp.Value.Hp}/{targetFp.Value.MaxHp}) via master HP slot at 0x{slotAddr:X}. End the current turn to see the KO fire.";
                            response.Status = "completed";
                        }
                        catch (Exception ex)
                        {
                            response.Status = "error";
                            response.Error = ex.Message;
                        }
                        break;

                    case "cheat_revive_allies":
                        // Session 49 dev tool — the reverse of cheat_kill_enemies.
                        // Finds dead player-team slots in the master HP table
                        // (HP=0, MaxHp>0, fingerprint-matched to scanned player)
                        // and writes HP=MaxHp + clears the dead-bit. Used to
                        // recover from accidental ally wipes during testing.
                        // Reuses the same table discovery as cheat_kill_enemies.
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        try
                        {
                            var playerFps = _navActions?.GetPlayerHpFingerprints()
                                ?? new List<(int Hp, int MaxHp)>();
                            if (playerFps.Count == 0)
                            {
                                response.Status = "failed";
                                response.Error = "cheat_revive_allies: no scanned player units. Run scan_move first.";
                                break;
                            }

                            // Session 49: table-base discovery. Same algorithm
                            // as cheat_kill_enemies. Anchor-search for any
                            // player fingerprint, walk back to find table base.
                            const long ReviveSearchMin = 0x141800000L;
                            const long ReviveSearchMax = 0x141900000L;
                            const int ReviveStride = 0x200;
                            const int ReviveMaxBack = 40;
                            const int ReviveMaxFwd = 40;

                            long? revBase = null;
                            int revCount = 0;
                            foreach (var (hp, maxHp) in playerFps)
                            {
                                // Important: dead-player fingerprint is
                                // (0, maxHp). Use the MaxHp for anchor search
                                // — live fingerprint OR dead-fingerprint both
                                // have MaxHp as a 2-byte match.
                                byte[] pattern = new byte[]
                                {
                                    (byte)(hp & 0xFF),    (byte)((hp >> 8) & 0xFF),
                                    (byte)(maxHp & 0xFF), (byte)((maxHp >> 8) & 0xFF),
                                };
                                var matches = Explorer.SearchBytesInAllMemory(
                                    pattern, 16, ReviveSearchMin, ReviveSearchMax, broadSearch: true);
                                foreach (var (matchAddr, _) in matches)
                                {
                                    long anchor = (long)matchAddr;
                                    long low = anchor;
                                    for (int back = 1; back <= ReviveMaxBack; back++)
                                    {
                                        long prev = anchor - back * ReviveStride;
                                        if (prev < ReviveSearchMin) break;
                                        var ph = Explorer.ReadAbsolute((nint)prev, 2);
                                        var pm = Explorer.ReadAbsolute((nint)(prev + 2), 2);
                                        if (!ph.HasValue || !pm.HasValue) break;
                                        int pMax = (int)pm.Value.value;
                                        int pHp = (int)ph.Value.value;
                                        bool valid = pMax >= 1 && pMax <= 2000 && pHp <= pMax;
                                        bool empty = pMax == 0 && pHp == 0;
                                        if (!valid && !empty) break;
                                        if (valid) low = prev;
                                    }
                                    int cover = 0;
                                    for (int fwd = 0; fwd <= ReviveMaxFwd; fwd++)
                                    {
                                        long a = low + fwd * ReviveStride;
                                        if (a >= ReviveSearchMax) break;
                                        var h2 = Explorer.ReadAbsolute((nint)a, 2);
                                        var m2 = Explorer.ReadAbsolute((nint)(a + 2), 2);
                                        if (!h2.HasValue || !m2.HasValue) break;
                                        int mv = (int)m2.Value.value;
                                        int hv = (int)h2.Value.value;
                                        bool v = mv >= 1 && mv <= 2000 && hv <= mv;
                                        bool e = mv == 0 && hv == 0;
                                        if (!v && !e) break;
                                        cover = fwd + 1;
                                    }
                                    if (cover > revCount) { revBase = low; revCount = cover; }
                                }
                            }

                            if (revBase == null || revCount < 2)
                            {
                                response.Status = "failed";
                                response.Error = "cheat_revive_allies: master HP table not found.";
                                break;
                            }

                            // For revive, player fingerprints from scan include
                            // LIVE and DEAD players. A dead player's fingerprint
                            // is (0, MaxHp) — which matches any dead slot with
                            // the same MaxHp. To detect a slot as "player",
                            // match by MaxHp only when HP=0. Build a MaxHp-only
                            // player-match set so dead slots match.
                            var playerMaxHpSet = new HashSet<int>();
                            foreach (var (_, mh) in playerFps) playerMaxHpSet.Add(mh);

                            var revSlots = new List<GameBridge.KillEnemySlot>();
                            for (int i = 0; i < revCount; i++)
                            {
                                long sa = revBase.Value + i * ReviveStride;
                                var h = Explorer.ReadAbsolute((nint)sa, 2);
                                var m = Explorer.ReadAbsolute((nint)(sa + 2), 2);
                                if (!h.HasValue || !m.HasValue) continue;
                                int hv = (int)h.Value.value;
                                int mv = (int)m.Value.value;
                                bool isPlayer = playerMaxHpSet.Contains(mv);
                                revSlots.Add(new GameBridge.KillEnemySlot
                                {
                                    SlotBase = sa,
                                    Hp = hv,
                                    MaxHp = mv,
                                    IsPlayer = isPlayer,
                                });
                            }

                            var revPlan = GameBridge.KillEnemiesPlanner.PlanReviveAllies(revSlots);
                            foreach (var op in revPlan)
                            {
                                for (int i = 0; i < op.Bytes.Length; i++)
                                    Explorer.Scanner.WriteByte((nint)(op.Address + i), op.Bytes[i]);
                            }

                            int revived = revPlan.Count / 2;
                            ModLogger.Log($"[CheatRevive] masterHP table base=0x{revBase.Value:X} slots={revCount} revived={revived}");
                            response.Info = revived == 0
                                ? "No dead player slots to revive."
                                : $"Revived {revived} player slot(s) via master HP table at 0x{revBase.Value:X}.";
                            response.Status = "completed";
                        }
                        catch (Exception ex)
                        {
                            response.Status = "error";
                            response.Error = ex.Message;
                        }
                        break;

                    case "scan_snapshot":
                        // Session 51: cache the latest scan's unit list under
                        // a named label for enemy-turn play-by-play reporting.
                        // Pair with scan_diff. See memory/project_enemy_turn_report_design.md.
                        if (_navActions == null) { response.Status = "failed"; response.Error = "Nav actions not initialized"; break; }
                        try
                        {
                            var label = command.To;
                            if (string.IsNullOrWhiteSpace(label))
                            {
                                response.Status = "failed";
                                response.Error = "scan_snapshot: label required (pass via 'to' field).";
                                break;
                            }
                            var ok = _navActions.SaveNamedSnapshot(label);
                            if (!ok)
                            {
                                response.Status = "failed";
                                response.Error = "scan_snapshot: no scan data available. Run scan_move first.";
                                break;
                            }
                            response.Status = "completed";
                            response.Info = $"Snapshot '{label}' saved ({_navActions.NamedSnapshotCount} total in cache).";
                        }
                        catch (Exception ex) { response.Status = "error"; response.Error = ex.Message; }
                        break;

                    case "scan_diff":
                        // Session 51: diff two named snapshots, emit change
                        // events. Uses pattern/to as the two labels.
                        if (_navActions == null) { response.Status = "failed"; response.Error = "Nav actions not initialized"; break; }
                        try
                        {
                            var fromLabel = command.Pattern ?? "";
                            var toLabel = command.To ?? "";
                            if (string.IsNullOrWhiteSpace(fromLabel) || string.IsNullOrWhiteSpace(toLabel))
                            {
                                response.Status = "failed";
                                response.Error = "scan_diff: pass 'pattern' (from label) and 'to' (to label).";
                                break;
                            }
                            var before = _navActions.GetNamedSnapshot(fromLabel);
                            var after = _navActions.GetNamedSnapshot(toLabel);
                            if (before == null) { response.Status = "failed"; response.Error = $"scan_diff: no snapshot named '{fromLabel}'."; break; }
                            if (after == null) { response.Status = "failed"; response.Error = $"scan_diff: no snapshot named '{toLabel}'."; break; }

                            var events = GameBridge.UnitScanDiff.Compare(before, after);
                            var lines = new List<string>();
                            var dtos = new List<ScanChangeEventDto>();
                            foreach (var e in events)
                            {
                                lines.Add(GameBridge.UnitScanDiff.RenderEvent(e));
                                dtos.Add(new ScanChangeEventDto
                                {
                                    Label = e.Label,
                                    Team = e.Team,
                                    Kind = e.Kind,
                                    OldX = e.OldXY.HasValue ? (int?)e.OldXY.Value.x : null,
                                    OldY = e.OldXY.HasValue ? (int?)e.OldXY.Value.y : null,
                                    NewX = e.NewXY.HasValue ? (int?)e.NewXY.Value.x : null,
                                    NewY = e.NewXY.HasValue ? (int?)e.NewXY.Value.y : null,
                                    OldHp = e.OldHp,
                                    NewHp = e.NewHp,
                                    StatusesGained = e.StatusesGained,
                                    StatusesLost = e.StatusesLost,
                                });
                            }
                            response.ChangeEvents = dtos;
                            response.Status = "completed";
                            response.Info = events.Count == 0
                                ? $"No changes between '{fromLabel}' and '{toLabel}'."
                                : string.Join("\n", lines);
                        }
                        catch (Exception ex) { response.Status = "error"; response.Error = ex.Message; }
                        break;

                    case "write_byte":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (string.IsNullOrEmpty(command.Address)) { response.Status = "failed"; response.Error = "Address required"; break; }
                        try
                        {
                            nint wAddr = (nint)Convert.ToInt64(command.Address.Replace("0x", ""), 16);
                            byte wVal = (byte)command.SearchValue;
                            Explorer.Scanner.WriteByte(wAddr, wVal);
                            // Read back to confirm
                            byte readBack = Explorer.Scanner.ReadByte(wAddr);
                            response.ReadResult = new ReadResult { Address = command.Address, Size = 1, Value = readBack, Hex = $"0x{readBack:X2}" };
                            response.Status = "completed";
                        }
                        catch (Exception ex) { response.Status = "error"; response.Error = ex.Message; }
                        break;

                    case "batch_read":
                        if (Explorer == null) { response.Status = "failed"; response.Error = "Memory explorer not initialized"; break; }
                        if (command.Addresses == null || command.Addresses.Count == 0) { response.Status = "failed"; response.Error = "Addresses required"; break; }
                        var batchReads = new (nint address, int size)[command.Addresses.Count];
                        for (int i = 0; i < command.Addresses.Count; i++)
                        {
                            batchReads[i] = ((nint)Convert.ToInt64(command.Addresses[i].Addr, 16), Math.Clamp(command.Addresses[i].Size, 1, 4));
                        }
                        var batchValues = Explorer.ReadMultiple(batchReads);
                        response.Reads = new List<BatchReadResult>();
                        for (int i = 0; i < batchValues.Length; i++)
                        {
                            response.Reads.Add(new BatchReadResult
                            {
                                Label = command.Addresses[i].Label,
                                Addr = command.Addresses[i].Addr,
                                Val = batchValues[i],
                                Hex = $"0x{batchValues[i]:X}"
                            });
                        }
                        response.Status = "completed";
                        break;

                    case "sequence":
                        return ExecuteSequence(command);

                    case "execute_action":
                    case "path": // legacy alias
                        return ExecuteValidPath(command);

                    case "execute_turn":
                        return ExecuteTurn(command);

                    case "battle_wait":
                        // Auto-scan before wait (battle_wait needs unit data for facing)
                        try
                        {
                            var scanCmd = new CommandRequest { Id = command.Id, Action = "scan_move" };
                            var scanRes = ExecuteNavAction(scanCmd);
                            if (scanRes.Status == "completed")
                            {
                                _turnTracker.MarkScanned();
                                // Credit the turn to the active unit's career.
                                var actor = scanRes.Battle?.Units?
                                    .FirstOrDefault(u => u.IsActive)?.Name;
                                if (actor != null)
                                    StatTracker?.OnTurnTaken(actor);
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogError($"[CommandBridge] Pre-wait scan failed: {ex.Message}");
                        }
                        // Snapshot the OUTGOING active unit BEFORE the cache
                        // clear so the post-wait auto-scan can be diff'd
                        // against it for the multi-unit hand-off banner.
                        var preWaitIdentity = SnapshotActiveUnitIdentity();
                        // Pre-wait UnitScanDiff snapshot for the "while
                        // you waited" recap. Diffs against post-wait scan
                        // to surface enemy-turn casualties (KOs, big
                        // damage, status changes) the agent missed by
                        // not watching the streaming narrator. Live-
                        // flagged 2026-04-25 playtest: 3 of 4 allies
                        // KO'd between agent's turns and they had to
                        // notice DEAD tags by reading the unit list.
                        var preWaitScanSnap = _navActions?.CaptureCurrentUnitSnapshotPublic();
                        _turnTracker.ResetForNewTurn();
                        _battleMenuTracker.OnNewTurn();
                        _movedThisTurn = false;
                        _actedThisTurn = false;
                        _postMoveX = -1;
                        _postMoveY = -1;
                        _waitConfirmPending = false;
                        _lastAbilityName = null;
                        _cachedPrimarySkillset = null;
                        _cachedSecondarySkillset = null;
                        _cachedSupportAbility = null;
                        _cachedLearnedAbilityNames = null;
                        ClearActiveUnitCache();
                        var waitResp = ExecuteNavActionWithAutoScan(command);
                        // Wait nav action returns with Screen often unpopulated
                        // (the outer ProcessCommand wrapper at line 1607 fills
                        // it via DetectScreenSettled AFTER we return). Detect
                        // here so the settle conditional can fire on a
                        // genuine BattleMyTurn return.
                        if (waitResp.Status == "completed" && waitResp.Screen == null)
                            waitResp.Screen = DetectScreen();
                        // Auto-scan inside ExecuteNavActionWithAutoScan races
                        // the active-unit transition: the wait nav returns
                        // when Ctrl is released and BattleMyTurn is detected,
                        // but the static battle array's [ACTIVE] flag can
                        // lag by ~200ms — the scan still reports the prior
                        // unit as active. Settle and re-scan so the
                        // hand-off banner + screen header reflect the
                        // truly-current active unit.
                        // Skip the 250ms settle when the auto-scan already
                        // resolved a different active unit — the static
                        // [ACTIVE] byte clearly transitioned, no race to
                        // defeat. Saves ~265ms (settle + extra scan) per
                        // wait when the hand-off is already detected.
                        bool needsSettle = waitResp.Status == "completed"
                            && waitResp.Screen?.Name == "BattleMyTurn"
                            && (_cachedActiveUnitName == null
                                || (preWaitIdentity?.Name != null && _cachedActiveUnitName == preWaitIdentity.Name));
                        if (needsSettle)
                        {
                            Thread.Sleep(250);
                            try
                            {
                                var settleScan = new CommandRequest { Id = command.Id, Action = "scan_move" };
                                var settleResp = ExecuteNavAction(settleScan);
                                if (settleResp.Status == "completed")
                                {
                                    CacheLearnedAbilities(settleResp.Battle);
                                    waitResp.Battle = settleResp.Battle;
                                    waitResp.ValidPaths = settleResp.ValidPaths;
                                    waitResp.Screen = settleResp.Screen ?? waitResp.Screen;
                                }
                            }
                            catch (Exception ex)
                            {
                                ModLogger.LogError($"[CommandBridge] Post-wait settle scan failed: {ex.Message}");
                            }
                        }
                        // While-you-waited recap: diff pre vs post units
                        // and surface action effects (HP delta, status,
                        // KO) at the head of the response. Mirrors the
                        // execute_turn outcome recap for callers using
                        // standalone battle_wait. Prepended BEFORE
                        // TURN HANDOFF so the read order is "what
                        // happened during the enemy turns" → "who's up
                        // next."
                        try
                        {
                            var postWaitScanSnap = _navActions?.CaptureCurrentUnitSnapshotPublic();
                            if (preWaitScanSnap != null && postWaitScanSnap != null)
                            {
                                var diffEvents = GameBridge.UnitScanDiff.Compare(preWaitScanSnap, postWaitScanSnap);
                                var recap = GameBridge.OutcomeRecapRenderer.Render(diffEvents);
                                if (!string.IsNullOrEmpty(recap))
                                {
                                    waitResp.Info = string.IsNullOrEmpty(waitResp.Info)
                                        ? recap
                                        : recap + " | " + waitResp.Info;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogError($"[BattleWait] While-you-waited recap render failed: {ex.Message}");
                        }
                        PrependHandoffBanner(waitResp,
                            GameBridge.TurnHandoffBannerClassifier.BuildBanner(
                                preWaitIdentity, SnapshotActiveUnitIdentity()));
                        return waitResp;

                    case "battle_flee":
                        _battleMenuTracker.ReturnToMyTurn();
                        return ExecuteNavAction(command);

                    case "battle_attack":
                        goto case "battle_ability";
                    case "battle_ability":
                        // Scan before cast only if we haven't already scanned
                        // this turn. ~170ms saved per turn when the caller
                        // has already run `screen` (which auto-scans on new
                        // turns). Post-move re-scan still happens in the
                        // battle_move case — so _lastScannedUnits is fresh
                        // after a move+act bundle.
                        //
                        // When freshScan is null the range-validation block
                        // below skips gracefully (the `freshScan?.Battle?`
                        // null-check short-circuits). The cast proceeds and
                        // the game itself rejects or misses if out-of-range.
                        CommandResponse? freshScan = null;
                        if (!_turnTracker.WasScannedThisTurn)
                        {
                            var autoScanCmd = new CommandRequest { Id = command.Id, Action = "scan_move" };
                            freshScan = ExecuteNavAction(autoScanCmd);
                            if (freshScan.Status == "completed")
                            {
                                CacheLearnedAbilities(freshScan.Battle);
                                _turnTracker.MarkScanned();
                            }

                            // S58: first-scan null/null retry. TODO §1 Tier 3
                            // "battle_ability first-scan null/null for secondary
                            // skillset" — the initial scan occasionally returns
                            // null/null before the roster match settles. Retry
                            // once so navigation doesn't bail with "unknown
                            // skillset". Auto-scan catches later misses anyway;
                            // this closes the first-scan window.
                            if (_cachedPrimarySkillset == null
                                && _cachedSecondarySkillset == null
                                && freshScan?.Battle?.Units?.Any(u => u.IsActive) == true)
                            {
                                ModLogger.Log("[CommandBridge] Pre-cast scan returned null/null skillsets — retry once");
                                var retryCmd = new CommandRequest { Id = command.Id + "#retry", Action = "scan_move" };
                                var retryScan = ExecuteNavAction(retryCmd);
                                if (retryScan.Status == "completed")
                                {
                                    CacheLearnedAbilities(retryScan.Battle);
                                    freshScan = retryScan;
                                }
                            }
                        }
                        // Validate target is within ability's horizontal range from caster.
                        // After move, use confirmed post-move position instead of stale static array.
                        string? abilityToValidate = command.Action == "battle_attack" ? "Attack" : command.Description;
                        if (abilityToValidate != null
                            && command.LocationId >= 0 && command.UnitIndex >= 0
                            && freshScan?.Battle?.Units != null)
                        {
                            var activeUnit = freshScan.Battle.Units
                                .FirstOrDefault(u => u.IsActive);
                            var matchingAbility = activeUnit?.Abilities?
                                .FirstOrDefault(a => a.Name.Equals(abilityToValidate, StringComparison.OrdinalIgnoreCase));
                            if (matchingAbility != null && activeUnit != null
                                && int.TryParse(matchingAbility.HRange, out int hr) && hr > 0)
                            {
                                int casterX = _movedThisTurn && _postMoveX >= 0 ? _postMoveX : activeUnit.X;
                                int casterY = _movedThisTurn && _postMoveY >= 0 ? _postMoveY : activeUnit.Y;
                                int dist = Math.Abs(command.LocationId - casterX) + Math.Abs(command.UnitIndex - casterY);
                                if (dist > hr)
                                {
                                    return new CommandResponse { Id = command.Id, Status = "failed",
                                        Error = $"Target ({command.LocationId},{command.UnitIndex}) is {dist} tiles away from ({casterX},{casterY}) but '{abilityToValidate}' has range {hr}.",
                                        ProcessedAt = DateTime.UtcNow.ToString("o"), GameWindowFound = true,
                                        Screen = DetectScreenSettled() };
                                }
                            }
                        }
                        // Set ability name only after all validation passes
                        _lastAbilityName = command.Action == "battle_attack"
                            ? "Attack" : command.Description;
                        _battleMenuTracker.ReturnToMyTurn();
                        // 2026-04-25: previously set _actedThisTurn=true here
                        // (commit 5f71fa4 — "commit-to-act time"). That
                        // interacts BADLY with the BattleActedMovedOverride
                        // shipped same session: the override exposes
                        // _actedThisTurn as screen.BattleActed=1, which the
                        // BattleAttack guard at NavigationActions.cs:980
                        // consults via WaitForTurnState's screen read —
                        // self-rejecting EVERY action with "You've already
                        // acted this turn" on a fresh turn. The mid-flight
                        // failure handler below + the success handler at
                        // line ~3881 cover the original concerns (timeout/
                        // abort); pre-flight set was redundant.
                        // S58: snapshot active unit's HP before the action
                        // so we can classify post-action counter-KO.
                        var preActionState = _navActions?.ReadPostActionState();
                        var actionResult = ExecuteNavAction(command);

                        // Auto-recover from "Failed to enter targeting mode"
                        // via a single retry. Root cause: BattleMoving stale
                        // read when the previous Move-mode state hasn't fully
                        // cleared. The nav already called EscapeToMyTurn on
                        // the first failure, so we're back in action-menu
                        // state — one fresh attempt usually succeeds.
                        // Live-observed 2026-04-25 Siedge Weald: repeated
                        // manually-issued battle_ability calls both failed
                        // with "current: BattleMoving". Auto-retry avoids
                        // the ask-user-to-try-again cycle.
                        if (actionResult.Status != "completed"
                            && actionResult.Error != null
                            && actionResult.Error.Contains("Failed to enter targeting mode"))
                        {
                            ModLogger.Log($"[CommandBridge] battle_ability targeting failed once — auto-retry: {actionResult.Error}");
                            _battleMenuTracker.ReturnToMyTurn();
                            actionResult = ExecuteNavAction(command);
                        }

                        if (actionResult.Status != "completed")
                        {
                            _lastAbilityName = null; // clear on failure

                            // Mid-flight failure heuristic: if the failure
                            // happened AFTER the action keys were sent but
                            // before the bridge could confirm completion,
                            // the game may still have registered the action.
                            // Set _actedThisTurn so downstream EffectiveMenuCursor
                            // doesn't show "ui=Abilities" when the cursor
                            // is visually on Move.
                            //
                            // Pre-action rejections (range validation, Act
                            // already used, wrong screen) don't match these
                            // strings and correctly leave the flag false.
                            //
                            // Live-observed 2026-04-25 Siedge Weald: user
                            // saw `ui=Abilities` after an execute_turn that
                            // appeared to land the attack but returned
                            // non-completed status due to mid-flight timing.
                            var err = actionResult.Error ?? "";
                            if (err.Contains("Failed to enter targeting mode")
                                || err.Contains("Navigation miss")
                                || err.Contains("timeout"))
                            {
                                _actedThisTurn = true;
                                ModLogger.Log($"[CommandBridge] battle_ability mid-flight failure — setting _actedThisTurn=true: {err}");
                            }
                        }
                        else
                        {
                            // Mark that we acted this turn so EffectiveMenuCursor
                            // can correct post-action stale reads even when the
                            // battleActed byte transiently reads 0.
                            _actedThisTurn = true;
                            // ??= so an explicit PostAction set by NavigationActions
                            // (battle_attack / battle_ability use the caster's
                            // start position to avoid the cursor-on-target
                            // mixing target-coords with caster-HP) wins.
                            actionResult.PostAction ??= _navActions?.ReadPostActionState();
                            // S58: detect Counter-KO (active unit died from the
                            // reaction to their own attack). Surface in the
                            // Info string so callers don't blindly call
                            // battle_wait on a dead unit.
                            if (GameBridge.CounterAttackKoClassifier.IsActiveUnitKod(
                                    preActionState, actionResult.PostAction))
                            {
                                actionResult.Info = (actionResult.Info != null ? actionResult.Info + " | " : "")
                                    + "[counter-KO] active unit died from reaction — do not battle_wait";
                                ModLogger.Log($"[CommandBridge] Counter-KO detected: pre HP={preActionState?.Hp} → post HP={actionResult.PostAction?.Hp}");
                            }
                        }
                        return actionResult;

                    case "battle_move":
                    case "move_grid": // legacy alias
                        // Pre-move scan only if we don't already have a fresh
                        // turn scan. battle_move needs ValidMoveTiles populated
                        // for the MoveValidator check — but that's already
                        // populated by the turn's first scan. Skip redundancy.
                        if (!_turnTracker.WasScannedThisTurn)
                        {
                            var moveScanCmd = new CommandRequest { Id = command.Id, Action = "scan_move" };
                            var moveScanRes = ExecuteNavAction(moveScanCmd);
                            if (moveScanRes.Status == "completed")
                            {
                                CacheLearnedAbilities(moveScanRes.Battle);
                                _turnTracker.MarkScanned();
                            }
                        }
                        _battleMenuTracker.ReturnToMyTurn();
                        var moveResult = ExecuteNavAction(command);
                        if (moveResult.Status == "completed")
                        {
                            _movedThisTurn = true;
                            moveResult.PostAction = _navActions?.ReadPostActionState();
                            if (moveResult.PostAction != null)
                            {
                                _postMoveX = moveResult.PostAction.X;
                                _postMoveY = moveResult.PostAction.Y;
                            }
                            // Re-scan after move so positions are fresh for battle_attack range validation
                            try
                            {
                                var postMoveScan = new CommandRequest { Id = command.Id, Action = "scan_move" };
                                var postMoveRes = ExecuteNavAction(postMoveScan);
                                if (postMoveRes.Status == "completed")
                                    CacheLearnedAbilities(postMoveRes.Battle);
                            }
                            catch { }
                        }
                        return moveResult;

                    case "world_travel_to":
                    case "travel_to": // legacy alias
                    case "navigate": // legacy alias
                    case "confirm_attack":
                    case "move_to":
                    case "scan_units":
                    case "test_c_hold":
                    case "get_arrows":
                    case "save":
                    case "load":
                    case "battle_retry":
                    case "battle_retry_formation":
                    case "buy":
                    case "sell":
                    case "change_job":
                    case "open_eqa":
                    case "open_job_selection":
                    case "open_character_status":
                    case "swap_unit_to":
                    case "auto_place_units":
                        return ExecuteNavActionWithAutoScan(command);

                    case "advance_dialogue":
                        // Bump the dialogue box counter BEFORE dispatching, so
                        // the post-advance screen response reflects the new
                        // line. Read eventId directly from memory so the tracker
                        // can reset on scene change without waiting for the
                        // next DetectScreen pass.
                        if (Explorer != null)
                        {
                            var evtRead = Explorer.ReadAbsolute((nint)0x14077CA94, 2);
                            int evt = evtRead.HasValue ? (int)evtRead.Value.value : 0;
                            if (evt >= 1 && evt < 400)
                                _dialogueTracker.Advance(evt);
                        }
                        return ExecuteNavActionWithAutoScan(command);

                    case "set_screen":
                        if (ScreenMachine == null) { response.Status = "failed"; response.Error = "Screen state machine not initialized"; break; }
                        var screenName = command.SearchLabel ?? "unknown";
                        if (Enum.TryParse<GameScreen>(screenName, ignoreCase: true, out var screen))
                        {
                            ScreenMachine.SetScreen(screen);
                            response.Status = "completed";
                        }
                        else
                        {
                            response.Status = "failed";
                            response.Error = $"Unknown screen: {screenName}. Valid: {string.Join(", ", Enum.GetNames<GameScreen>())}";
                        }
                        break;

                    case "reset_state_machine":
                    {
                        // Infra action: hard-reset the screen state
                        // machine to WorldMap + clear every auto-resolve
                        // latch so the next screen calls re-run fresh.
                        // Previously used by fft_resync (removed session
                        // 46); still callable manually as a debug aid.
                        // Use after an escape-storm has
                        // confirmed the game is ACTUALLY on WorldMap and
                        // only the SM is stale. Does NOT fire any keys —
                        // caller is responsible for escaping to WorldMap
                        // first.
                        var cleared = new List<string>();
                        if (ScreenMachine != null)
                        {
                            ScreenMachine.SetScreen(GameScreen.WorldMap);
                            cleared.Add("SM→WorldMap");
                        }
                        if (_resolvedPickerCursorAddr != 0)
                        {
                            _resolvedPickerCursorAddr = 0L;
                            cleared.Add("pickerCursor");
                        }
                        if (_resolvedJobCursorAddr != 0 || _jobCursorResolveAttempted)
                        {
                            _resolvedJobCursorAddr = 0L;
                            _jobCursorResolveAttempted = false;
                            cleared.Add("jobCursor");
                        }
                        if (_resolvedPartyMenuCursorAddr != 0)
                        {
                            _resolvedPartyMenuCursorAddr = 0L;
                            cleared.Add("partyMenuCursor");
                        }
                        if (_resolvedEquipPickerCursorAddr != 0 || _equipPickerCursorResolveAttempted)
                        {
                            _resolvedEquipPickerCursorAddr = 0L;
                            _equipPickerCursorResolveAttempted = false;
                            cleared.Add("equipPickerCursor");
                        }
                        if (_resolvedEqaColumnCursorAddr != 0 || _eqaColumnCursorResolveAttempted)
                        {
                            _resolvedEqaColumnCursorAddr = 0L;
                            _eqaColumnCursorResolveAttempted = false;
                            cleared.Add("eqaColumnCursor");
                        }
                        if (_eqaRowAutoResolveAttempted)
                        {
                            _eqaRowAutoResolveAttempted = false;
                            cleared.Add("eqaRowAutoLatch");
                        }
                        response.Status = "completed";
                        response.Info = cleared.Count == 0
                            ? "reset_state_machine: nothing to clear (already fresh)"
                            : $"reset_state_machine cleared: {string.Join(", ", cleared)}";
                        break;
                    }

                    case "hold_key":
                        // Holds a key down for a specified duration, then releases.
                        // Used for game mechanics that require a real held press — e.g.
                        // hold-B-3s → DismissUnit confirmation on CharacterStatus.
                        // Expects command.SearchValue = VK code, command.ReadSize = ms
                        // (reusing existing fields to avoid bloating the command model).
                        {
                            IntPtr gameWindow = Process.GetCurrentProcess().MainWindowHandle;
                            if (gameWindow == IntPtr.Zero)
                            {
                                response.Status = "failed";
                                response.Error = "Game window not found";
                                break;
                            }
                            int vk = command.SearchValue;
                            int holdMs = command.ReadSize > 0 ? command.ReadSize : 3500;
                            if (vk <= 0)
                            {
                                response.Status = "failed";
                                response.Error = "hold_key requires searchValue=<vk code>";
                                break;
                            }
                            bool down = _inputSimulator.SendKeyDownToWindow(gameWindow, vk);
                            Thread.Sleep(holdMs);
                            bool up = _inputSimulator.SendKeyUpToWindow(gameWindow, vk);
                            if (down && up)
                            {
                                response.Status = "completed";
                                // Notify state machine — particularly relevant for
                                // VK_B on CharacterStatus which should trigger DismissUnit.
                                if (vk == 0x42 /* VK_B */ && holdMs >= 3000
                                    && ScreenMachine?.CurrentScreen == GameScreen.CharacterStatus)
                                {
                                    ScreenMachine.SetScreen(GameScreen.DismissUnit);
                                }

                                // Session 48: optional follow-up key tap. Used by
                                // BattleSequence Flee: hold-B opens the Yes/No
                                // modal with Yes preselected; we immediately tap
                                // Enter so the caller never sees the intermediate
                                // confirmation as a state.
                                if (command.FollowUpVk > 0)
                                {
                                    int followDelay = command.FollowUpDelayMs > 0 ? command.FollowUpDelayMs : 300;
                                    Thread.Sleep(followDelay);
                                    _inputSimulator.SendKeyPressToWindow(gameWindow, command.FollowUpVk);
                                }
                            }
                            else
                            {
                                response.Status = "failed";
                                response.Error = $"hold_key send failed (down={down} up={up})";
                            }
                        }
                        break;

                    default:
                        response.Status = "failed";
                        response.Error = $"Unknown action: {command.Action}";
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Status = "error";
                response.Error = ex.Message;
            }

            return response;
        }

        /// <summary>
        /// Session 47: `execute_turn` bundled action. Converts a TurnPlan
        /// (move target, ability + target, wait direction) into the existing
        /// battle_move / battle_ability / battle_wait primitives and dispatches
        /// them in order. Aborts at the first non-completed sub-step.
        /// </summary>
        private CommandResponse ExecuteTurn(CommandRequest command)
        {
            // Stand-still normalization: if the move target is the caster's
            // current tile, drop the move so the bundle dispatches just
            // the ability + wait. The BFS-emitted move list excludes the
            // origin by design, so without this the call would otherwise
            // fail with "Tile (X,Y) is not in the valid move range."
            // Live-flagged 2026-04-25 playtest.
            EnsureNavActions();
            var standStillReadout = _navActions?.ReadPostActionState();
            var (normMoveX, normMoveY) = GameBridge.StandStillNormalizer.NormalizeSameTile(
                command.MoveX, command.MoveY,
                standStillReadout?.X, standStillReadout?.Y);

            var plan = new GameBridge.TurnPlan
            {
                MoveX = normMoveX,
                MoveY = normMoveY,
                AbilityName = command.AbilityName,
                TargetX = command.TargetX,
                TargetY = command.TargetY,
                Direction = command.Pattern, // reuse existing direction field convention
                SkipWait = command.SkipWait,
            };

            // Pre-flight: reject plans that attempt a move after move is
            // consumed or an ability after act is consumed. Without this
            // the bundle dispatches the first sub-step which then fails
            // with a misleading "Not in Move mode" (live-repro 2026-04-24).
            // Mirror the canonical message from battle_attack/battle_ability
            // entry-reset check (commit 8cf9197).
            // EnsureNavActions already called above for stand-still readout.
            var preflightScreen = DetectScreen();
            if (preflightScreen != null
                && (preflightScreen.Name == "BattleMyTurn" || preflightScreen.Name == "BattleActing"))
            {
                bool preHasMoved = preflightScreen.BattleMoved == 1 || _movedThisTurn;
                bool preHasActed = preflightScreen.BattleActed == 1 || _actedThisTurn;
                string? preflightError = GameBridge.ExecuteTurnPreflightValidator.Validate(
                    plan, preHasMoved, preHasActed);
                if (preflightError != null)
                {
                    return new CommandResponse
                    {
                        Id = command.Id,
                        Status = "failed",
                        Error = preflightError,
                        ProcessedAt = System.DateTime.UtcNow.ToString("o"),
                        GameWindowFound = true,
                        Screen = preflightScreen,
                    };
                }
            }

            // Defensive menu-cursor reset before the first sub-step. The
            // standalone battle_move helper handles ui=Abilities / ui=Wait
            // start states by navigating the menu cursor to Move (slot 0)
            // before pressing Enter. Live-flagged playtest #3 2026-04-25:
            // execute_turn's first move sub-step reportedly failed "Not in
            // Move mode" from ui=Abilities while a standalone battle_move
            // succeeded from the same state. Force the cursor byte to 0
            // here so the sub-dispatch starts from a known menu position.
            if (preflightScreen != null
                && preflightScreen.Name == "BattleMyTurn"
                && preflightScreen.MenuCursor != 0
                && Explorer != null)
            {
                Explorer.Scanner.WriteByte((nint)0x1407FC620, 0);
                _battleMenuTracker.ReturnToMyTurn();
                ModLogger.Log($"[ExecuteTurn] Pre-flight cursor reset: was {preflightScreen.MenuCursor} → 0 (Move)");
            }

            // S58: seed the accumulator with the pre-bundle snapshot so HP
            // delta / move delta / killed units can be aggregated across
            // every sub-step. Pre-bundle scan is cheap if already cached.
            var accumulator = new GameBridge.ExecuteTurnResultAccumulator();
            var initialPost = _navActions?.ReadPostActionState();
            accumulator.Seed(initialPost);
            var preBundleUnits = SnapshotUnitsForKillDiff();
            // Pre-bundle UnitScanDiff snapshot for the OUTCOME recap.
            // Mirrors what battle_wait's narrator does — diff pre vs
            // post for HP/status/KO events, then render via
            // OutcomeRecapRenderer to surface what the agent's action
            // actually did (Hasteja → +Haste on Wilham, etc).
            var preBundleScanSnap = _navActions?.CaptureCurrentUnitSnapshotPublic();
            // Capture the OUTGOING active-unit identity. The bundle's
            // battle_wait sub-step clears the cache and the post-wait
            // auto-scan repopulates it for the NEW unit (multi-unit
            // party play). Diff at the end to emit a hand-off banner so
            // the caller doesn't keep issuing commands meant for the
            // prior unit (live-flagged 2026-04-25 playtest).
            var preBundleIdentity = SnapshotActiveUnitIdentity();

            CommandResponse? last = null;
            // Aggregate each sub-step's Info so the bundled response carries
            // hit/miss/damage/KO outcomes — without this, the final `last`
            // response is whatever battle_wait set (typically empty), and
            // the caller sees "[BattleMyTurn] ... t=20247ms[execute_turn]!!"
            // with no clue what their attack did. Live-flagged 2026-04-25
            // playtest: Ramza died on an attack and the agent had to grep
            // mod logs to figure out why.
            var stepInfos = new List<string>();
            int stepIndex = 0;
            foreach (var step in plan.ToSteps())
            {
                var sub = new CommandRequest
                {
                    Id = command.Id + "#" + stepIndex,
                    Action = step.Action,
                };
                switch (step.Action)
                {
                    case "battle_move":
                        sub.LocationId = step.X;
                        sub.UnitIndex = step.Y;
                        break;
                    case "battle_ability":
                        sub.Description = step.AbilityName;
                        if (step.HasTarget)
                        {
                            sub.LocationId = step.X;
                            sub.UnitIndex = step.Y;
                        }
                        break;
                    case "battle_wait":
                        if (!string.IsNullOrEmpty(step.Action))
                            sub.Pattern = step.Direction;
                        break;
                }

                // Recurse through the main dispatch so each sub-action runs
                // through its normal pipeline (scan, validation, retry). This
                // keeps execute_turn a thin orchestrator, not a parallel code
                // path that could drift from the primitives.
                last = ExecuteAction(sub);
                if (last == null) break;
                if (!string.IsNullOrWhiteSpace(last.Info))
                    stepInfos.Add(last.Info);
                accumulator.RecordStep(step.Action, last.PostAction);

                // S58: if the sub-action's resulting screen indicates the
                // turn ended or the battle ended, stop with a clear message
                // rather than burning subsequent sub-steps on a non-battle
                // state. Dialogue interrupts are NOT abort-worthy — caller
                // can advance past them, so we leave status "completed" and
                // continue.
                var interruption = GameBridge.TurnInterruptionClassifier.Classify(last.Screen?.Name);
                if (GameBridge.TurnInterruptionClassifier.ShouldAbortTurn(interruption))
                {
                    // BattleVictory / GameOver / WorldMap can flicker transiently
                    // mid-turn (the screen detector has known-overrides that
                    // briefly surface terminal states). Don't bail on the first
                    // sighting — re-check after a settle to see if it sticks.
                    // Live-flagged playtest #4 2026-04-25: a 1-2s spurious
                    // BattleVictory state aborted execute_turn's wait step
                    // mid-X-Potion. The flicker is acknowledged in Commands.md
                    // as a known gotcha; the helper should retry through it.
                    Thread.Sleep(800);
                    var recheck = DetectScreen();
                    var recheckInterruption = GameBridge.TurnInterruptionClassifier.Classify(recheck?.Name);
                    if (!GameBridge.TurnInterruptionClassifier.ShouldAbortTurn(recheckInterruption))
                    {
                        ModLogger.Log($"[ExecuteTurn] Transient interruption resolved: {last.Screen?.Name} → {recheck?.Name}; continuing");
                        last.Screen = recheck;
                        // fall through to continue the loop
                    }
                    else
                    {
                        last.Info = (last.Info != null ? last.Info + " | " : "")
                            + $"[turn-interrupt] step '{step.Action}' landed on {last.Screen?.Name} ({interruption}) — aborting execute_turn bundle";
                        break;
                    }
                }

                if (last.Status != "completed") break;
                stepIndex++;
            }

            if (last == null)
            {
                return new CommandResponse
                {
                    Id = command.Id,
                    Status = "failed",
                    Error = "execute_turn: no steps emitted",
                    ProcessedAt = System.DateTime.UtcNow.ToString("o"),
                    GameWindowFound = true,
                };
            }

            // S58: compute post-bundle scan-diff for killed-unit aggregation.
            // Only attempt when pre-bundle scan succeeded — otherwise we
            // can't tell new kills apart from unit-set churn.
            var postBundleUnits = SnapshotUnitsForKillDiff();
            if (preBundleUnits != null && postBundleUnits != null)
                accumulator.RecordScanDiff(preBundleUnits, postBundleUnits);

            last.Id = command.Id;
            last.TurnSummary = BuildTurnSummary(accumulator);
            // Concatenate sub-step Info so the bundled response surfaces
            // every outcome — the original last.Info is the FINAL step's
            // Info (often empty after battle_wait). Joined with " | " so
            // grep-friendly and fits one line in the compact formatter.
            if (stepInfos.Count > 0)
                last.Info = string.Join(" | ", stepInfos);
            // Backfill PostAction from the accumulator if the final sub-step
            // didn't populate one (battle_wait typically doesn't). Without
            // this, the formatter's `→ (X,Y) HP=H/MH` trailer is missing
            // entirely and the caller has to follow up with `screen` to
            // see their unit's HP. Live-flagged playtest #3 2026-04-25.
            if (last.PostAction == null && accumulator.FinalPostAction != null)
                last.PostAction = accumulator.FinalPostAction;
            // Last-resort fallback: read fresh post-state if neither the
            // sub-step nor accumulator captured one (e.g. all sub-steps
            // failed early and accumulator stayed empty). Keeps the
            // trailer present even on degenerate paths.
            if (last.PostAction == null)
                last.PostAction = _navActions?.ReadPostActionState();
            // Outcome recap: diff pre vs post unit scans and surface
            // action effects (HP delta, status, KO) at the head of the
            // response. Without this, agent has to mentally HP-diff
            // prior `screen` output to verify their action landed —
            // live-flagged 2026-04-25 playtest. Prepended BEFORE the
            // hand-off banner so the recap reads top-down: "what just
            // happened" → "who's up next".
            try
            {
                var postBundleScanSnap = _navActions?.CaptureCurrentUnitSnapshotPublic();
                if (preBundleScanSnap != null && postBundleScanSnap != null)
                {
                    var diffEvents = GameBridge.UnitScanDiff.Compare(preBundleScanSnap, postBundleScanSnap);
                    var recap = GameBridge.OutcomeRecapRenderer.Render(diffEvents);
                    if (!string.IsNullOrEmpty(recap))
                    {
                        last.Info = string.IsNullOrEmpty(last.Info)
                            ? recap
                            : recap + " | " + last.Info;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[ExecuteTurn] Outcome recap render failed: {ex.Message}");
            }

            // Multi-unit party hand-off: the wait sub-step's auto-scan
            // already repopulated the active-unit cache for the new
            // unit. Compare against the pre-bundle snapshot and prepend
            // a loud `=== TURN HANDOFF: A → B ===` banner when they
            // differ. Pure helper short-circuits when either side is
            // null, so partial bundles (no wait, transient empty scan)
            // emit nothing instead of misleading "→ null" text.
            PrependHandoffBanner(last,
                GameBridge.TurnHandoffBannerClassifier.BuildBanner(
                    preBundleIdentity, SnapshotActiveUnitIdentity()));
            return last;
        }

        /// <summary>
        /// S58: snapshot current battle roster for execute_turn kill-diff.
        /// Reads the last-scanned units through NavigationActions' cache.
        /// Returns null when no scan data is available — caller treats as
        /// "cannot diff" and skips kill-attribution.
        /// </summary>
        private List<GameBridge.UnitSnapshot>? SnapshotUnitsForKillDiff()
        {
            var cached = _navActions?.LastScannedUnitSnapshots();
            if (cached == null || cached.Count == 0) return null;
            return cached;
        }

        private static TurnSummary BuildTurnSummary(GameBridge.ExecuteTurnResultAccumulator acc)
        {
            var summary = new TurnSummary
            {
                HpDelta = acc.HpDelta,
                PreMoveX = acc.PreMoveX,
                PreMoveY = acc.PreMoveY,
                PostMoveX = acc.PostMoveX,
                PostMoveY = acc.PostMoveY,
            };
            foreach (var killed in acc.KilledUnits)
            {
                summary.KilledUnits.Add(new KilledUnitSummary
                {
                    Name = killed.Name,
                    JobName = killed.JobName,
                    Team = killed.Team,
                });
            }
            return summary;
        }

        private CommandResponse ExecuteValidPath(CommandRequest command)
        {
            var response = new CommandResponse
            {
                Id = command.Id,
                ProcessedAt = DateTime.UtcNow.ToString("o"),
                GameWindowFound = true
            };

            var pathName = command.To;
            if (string.IsNullOrEmpty(pathName))
            {
                response.Status = "failed";
                response.Error = "Missing 'to' field — specify the validPath name (e.g. \"Flee\", \"PartyMenu\")";
                return response;
            }

            // Detect current screen and look up the path
            var screen = DetectScreen();
            if (screen == null)
            {
                response.Status = "failed";
                response.Error = "Could not detect current screen";
                return response;
            }

            // Apply SM override for detection-ambiguous screens (SaveSlotPicker
            // vs TravelList). Without this, execute_action looks up paths on
            // the wrong screen name after SM transitioned but detection stayed
            // on the collision fingerprint.
            if (ScreenMachine != null)
            {
                var resolved = ScreenDetectionLogic.ResolveAmbiguousScreen(
                    ScreenMachine.CurrentScreen, screen.Name);
                if (resolved != screen.Name)
                {
                    ModLogger.Log($"[SM-Override] execute_action: detection={screen.Name} → {resolved} (SM={ScreenMachine.CurrentScreen}).");
                    screen.Name = resolved;
                }
            }

            var paths = NavigationPaths.GetPaths(screen);
            if (paths == null || !paths.TryGetValue(pathName, out var path))
            {
                // Session 47: richer fail-loud — Name — Desc for each
                // available action, aliases coalesced. Helps the user
                // see the purpose of each action, not just names.
                var available = NavigationPathsDescription
                    .FormatAvailableActions(screen.Name);
                response.Status = "failed";
                response.Error = $"No path '{pathName}' on screen '{screen.Name}'. Available: {available}";
                return response;
            }

            // Note (session 47): the previous explicit `_dialogueTracker.Advance`
            // for `Advance` validPath was removed. ExecuteKeyCommand now bumps
            // via DialogueTrackerKeyHook on any raw Enter landing on a
            // dialogue screen, so the Advance validPath (which dispatches
            // Enter through ExecuteKeyCommand) gets its bump there. Keeping
            // the explicit bump here would double-count.

            // If the path specifies a high-level action, delegate.
            // battle_wait needs special handling (confirmation, pre-scan, turn reset)
            // that only exists in the main command switch — call ExecuteNavActionWithAutoScan
            // which handles the full wait cycle including facing and turn polling.
            if (!string.IsNullOrEmpty(path.Action))
            {
                command.Action = path.Action;
                if (path.LocationId != 0) command.LocationId = path.LocationId;
                // Session 48: hold_key paths pass vk via searchValue, durationMs
                // via readSize — the same fields hold_key's case-statement reads.
                if (path.Action == "hold_key")
                {
                    if (path.Vk != 0) command.SearchValue = path.Vk;
                    if (path.DurationMs != 0) command.ReadSize = path.DurationMs;
                    if (path.FollowUpVk != 0) command.FollowUpVk = path.FollowUpVk;
                    if (path.FollowUpDelayMs != 0) command.FollowUpDelayMs = path.FollowUpDelayMs;
                    // hold_key lives in the main ProcessCommand switch, not
                    // NavigationActions. Route through ExecuteAction so it
                    // actually dispatches instead of falling to "unknown nav
                    // action".
                    return ExecuteAction(command);
                }

                if (path.Action == "battle_wait")
                {
                    // Pre-scan for facing data
                    try
                    {
                        var scanCmd = new CommandRequest { Id = command.Id, Action = "scan_move" };
                        ExecuteNavAction(scanCmd);
                    }
                    catch { }
                    // Snapshot OUTGOING identity before the cache clear so
                    // the post-wait auto-scan can be diff'd for the
                    // multi-unit hand-off banner. Mirrors the direct
                    // `case "battle_wait"` site.
                    var preWaitIdentity = SnapshotActiveUnitIdentity();
                    _turnTracker.ResetForNewTurn();
                    _battleMenuTracker.OnNewTurn();
                    _movedThisTurn = false;
                    _actedThisTurn = false;
                    _waitConfirmPending = false;
                    _lastAbilityName = null;
                    _cachedPrimarySkillset = null;
                    _cachedSecondarySkillset = null;
                    _cachedSupportAbility = null;
                    _cachedLearnedAbilityNames = null;
                    // Parity with the direct `case "battle_wait"` site —
                    // the prior unit's identity must clear so the
                    // post-wait scan_move repopulates with the NEW
                    // unit's name/job/pos/HP. Without this clear the
                    // hand-off banner can't fire (cache compares as
                    // unchanged) AND the screen-line trailer keeps
                    // showing the prior unit during the brief window
                    // before the auto-scan completes.
                    ClearActiveUnitCache();
                    var waitResp = ExecuteNavActionWithAutoScan(command);
                    // Wait nav often returns with Screen=null; outer wrapper
                    // populates later. Detect here so the settle conditional
                    // can fire — see direct `case "battle_wait":` for full
                    // context.
                    if (waitResp.Status == "completed" && waitResp.Screen == null)
                        waitResp.Screen = DetectScreen();
                    // Same settle race as `case "battle_wait":` above —
                    // skip when auto-scan already resolved a different
                    // active unit (no race to defeat).
                    bool needsSettle = waitResp.Status == "completed"
                        && waitResp.Screen?.Name == "BattleMyTurn"
                        && (_cachedActiveUnitName == null
                            || (preWaitIdentity?.Name != null && _cachedActiveUnitName == preWaitIdentity.Name));
                    if (needsSettle)
                    {
                        Thread.Sleep(250);
                        try
                        {
                            var settleScan = new CommandRequest { Id = command.Id, Action = "scan_move" };
                            var settleResp = ExecuteNavAction(settleScan);
                            if (settleResp.Status == "completed")
                            {
                                CacheLearnedAbilities(settleResp.Battle);
                                waitResp.Battle = settleResp.Battle;
                                waitResp.ValidPaths = settleResp.ValidPaths;
                                waitResp.Screen = settleResp.Screen ?? waitResp.Screen;
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogError($"[CommandBridge] Post-wait settle scan failed: {ex.Message}");
                        }
                    }
                    PrependHandoffBanner(waitResp,
                        GameBridge.TurnHandoffBannerClassifier.BuildBanner(
                            preWaitIdentity, SnapshotActiveUnitIdentity()));
                    return waitResp;
                }

                return ExecuteNavAction(command);
            }

            // Otherwise execute as a key command with the path's wait conditions
            if (path.Keys == null || path.Keys.Length == 0)
            {
                response.Status = "failed";
                response.Error = $"Path '{pathName}' has no keys or action";
                return response;
            }

            // Convert PathEntry keys to CommandRequest keys and execute
            command.Keys = new System.Collections.Generic.List<KeyCommand>();
            foreach (var k in path.Keys)
                command.Keys.Add(new KeyCommand { Vk = k.Vk, Name = k.Name });
            command.WaitForScreen = path.WaitForScreen;
            command.WaitUntilScreenNot = path.WaitUntilScreenNot;
            if (path.WaitTimeoutMs > 0) command.WaitTimeoutMs = path.WaitTimeoutMs;
            // Propagate the path's inter-key delay (e.g. 800ms for
            // ReturnToWorldMap) so long Escape chains don't race the
            // game's close animations.
            if (path.DelayBetweenMs > 0) command.DelayBetweenMs = path.DelayBetweenMs;
            command.Action = null; // Clear action so ExecuteKeyCommand runs

            return ExecuteKeyCommand(command);
        }

        private void EnsureNavActions()
        {
            if (_navActions == null && Explorer != null)
            {
                _navActions = new NavigationActions(_inputSimulator, Explorer, DetectScreen);
                _navActions.BattleTracker = BattleTracker;
                _navActions.GetAbilitiesSubmenuItems = GetAbilitiesSubmenuItems;
                _navActions.GetAbilityListForSkillset = GetAbilityListForSkillset;
            }
            EnsureMapLoader();
            if (_navActions != null)
            {
                _navActions._mapLoader = _mapLoader;
                // Wire the SM so compound nav helpers (open_eqa, etc.) keep
                // it in sync as they drive the UI. Reassigned each call —
                // ScreenMachine is owned by CommandWatcher, safe to repoint.
                _navActions.ScreenMachine = ScreenMachine;
                // S58: wire stat tracker so battle_attack / battle_ability /
                // battle_move can record damage / kills / moves / abilities.
                _navActions.StatTracker = StatTracker;
            }
        }

        private CommandResponse ExecuteNavAction(CommandRequest command)
        {
            if (Explorer == null)
                return new CommandResponse { Id = command.Id, Status = "failed", Error = "Memory explorer not initialized", ProcessedAt = DateTime.UtcNow.ToString("o") };

            EnsureNavActions();
            return _navActions!.Execute(command);
        }

        /// <summary>
        /// Execute a nav action, then auto-scan if the result lands on BattleMyTurn for a player unit.
        /// This ensures auto-scan fires regardless of which action caused the turn transition.
        /// </summary>
        private CommandResponse ExecuteNavActionWithAutoScan(CommandRequest command)
        {
            // Invalidate roster-name cache on events that reshuffle the roster:
            // - load: slot contents change wholesale when the save restores.
            // - hire/dismiss/rename (future): mutate the live slots; wire when those land.
            // Skipping "save" — save doesn't change the in-memory slots, only writes
            // them out; cache stays valid.
            if (command?.Action == "load" && _rosterNameTable != null)
            {
                _rosterNameTable.Invalidate();
            }

            var response = ExecuteNavAction(command);

            // Skip auto-scan for transition-commit actions (e.g.
            // auto_place_units) where the static battle array may not
            // yet reflect the new state — S57 surfaced "No ally found
            // in scan" emitted as [auto-scan] prefix on the response.
            bool commandAllowsAutoScan = AutoScanCommandClassifier.ShouldAutoScanAfter(command?.Action);

            if (commandAllowsAutoScan
                && response.Screen != null
                && _turnTracker.ShouldAutoScan(response.Screen.Name, response.Screen.BattleTeam, response.Screen.BattleUnitId, response.Screen.BattleUnitHp))
            {
                try
                {
                    var scanCommand = new CommandRequest { Id = response.Id, Action = "scan_move" };
                    var scanResponse = ExecuteNavAction(scanCommand);
                    response.Battle = scanResponse.Battle;
                    response.ValidPaths = scanResponse.ValidPaths;
                    response.Screen = scanResponse.Screen ?? response.Screen;
                    response.Info = scanResponse.Info;
                    if (scanResponse.Error != null)
                        response.Error = (response.Error != null ? response.Error + " | " : "") + "[auto-scan] " + scanResponse.Error;
                    // Populate the active-unit identity cache so the multi-
                    // unit hand-off banner can detect a unit change after
                    // battle_wait / execute_turn. The explicit `case "scan_move"`
                    // path does this, but ExecuteNavAction here bypasses
                    // that branch and only the Battle payload comes back —
                    // without this call, _cachedActiveUnitName stays null
                    // and TurnHandoffBannerClassifier.BuildBanner returns null.
                    if (scanResponse.Status == "completed")
                        CacheLearnedAbilities(scanResponse.Battle);
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"[CommandBridge] Auto-scan failed: {ex.Message}");
                }
                _turnTracker.MarkScanned();
            }

            return response;
        }

        private CommandResponse ExecuteKeyCommand(CommandRequest command)
        {
            var response = new CommandResponse
            {
                Id = command.Id,
                ProcessedAt = DateTime.UtcNow.ToString("o"),
                KeyResults = new List<KeyResult>()
            };

            IntPtr gameWindow = Process.GetCurrentProcess().MainWindowHandle;
            response.GameWindowFound = gameWindow != IntPtr.Zero;

            if (!response.GameWindowFound)
            {
                response.Status = "failed";
                response.Error = "Could not find game window handle";
                return response;
            }

            // Capture SM state before keys for change detection
            var _smScreenBeforeKeys = ScreenMachine?.CurrentScreen ?? GameScreen.Unknown;

            int successCount = 0;
            var keySw = System.Diagnostics.Stopwatch.StartNew();
            long lastKeyMs = 0;
            for (int i = 0; i < command.Keys.Count; i++)
            {
                var key = command.Keys[i];
                long nowMs = keySw.ElapsedMilliseconds;
                long sinceLast = i == 0 ? 0 : nowMs - lastKeyMs;
                // Mark this key as bridge-sent BEFORE dispatching, so the
                // user-input monitor's poller skips it when it sees the
                // rising edge on the key state (de-dup window ~150ms).
                UserInputMonitor?.MarkBridgeSent(key.Vk);
                bool success = _inputSimulator.SendKeyPressToWindow(gameWindow, key.Vk);
                lastKeyMs = nowMs;

                response.KeyResults.Add(new KeyResult { Vk = key.Vk, Success = success });
                if (success) successCount++;

                ModLogger.LogDebug($"[CommandBridge] Key {key.Name ?? key.Vk.ToString()} (0x{key.Vk:X2}) [i={i}, +{sinceLast}ms]: {(success ? "OK" : "FAIL")}");

                if (success)
                {
                    ScreenMachine?.OnKeyPressed(key.Vk);
                    ScreenMachine?.OnKeyPressedForDetectedScreen(key.Vk);
                    if (_battleMenuTracker.InSubmenu)
                        _battleMenuTracker.OnKeyPressed(key.Vk);
                    InvalidateJobCursorOnRowCross(key.Vk);
                    InvalidatePartyMenuCursorOnMove(key.Vk);
                    InvalidateEquipPickerCursorOnMove(key.Vk);
                    InvalidateEqaColumnCursorOnMove(key.Vk);

                    // Session 47: raw Enter on a dialogue screen advances
                    // the box counter. advance_dialogue and
                    // `execute_action Advance` both bump explicitly on
                    // other paths — advance_dialogue dispatches via
                    // NavigationActions.SendKey (NOT this method), so
                    // no double-bump risk there. The `Advance` validPath
                    // used to bump at ExecuteValidPath:2898 then fall
                    // through here; that explicit bump is now redundant
                    // and has been removed.
                    if (Explorer != null
                        && (key.Vk == 0x0D) // VK_ENTER
                        && ScreenMachine?.LastDetectedScreen != null)
                    {
                        var evtRead = Explorer.ReadAbsolute((nint)0x14077CA94, 2);
                        int evt = evtRead.HasValue ? (int)evtRead.Value.value : 0;
                        GameBridge.DialogueTrackerKeyHook.HandleKeyPress(
                            _dialogueTracker, key.Vk,
                            ScreenMachine.LastDetectedScreen, evt);
                    }
                }

                if (key.HoldMs > 0)
                    Thread.Sleep(key.HoldMs);

                if (i < command.Keys.Count - 1 && command.DelayBetweenMs > 0)
                    Thread.Sleep(command.DelayBetweenMs);
            }

            response.KeysProcessed = successCount;
            response.Status = successCount == command.Keys.Count ? "completed"
                            : successCount > 0 ? "partial"
                            : "failed";

            // If keys succeeded, return SM state directly instead of running
            // detection. The SM processes each key synchronously and knows
            // the correct screen. Detection reads stale memory bytes that
            // take 200-500ms+ to settle after transitions, causing every
            // "first read is wrong" bug. Detection still runs on
            // observational `screen` reads (no keys) as a correction layer.
            if (response.Status != "failed")
            {
                // Wait conditions still use detection (they poll over time)
                if (command.WaitForScreen != null || command.WaitUntilScreenNot != null
                    || (command.WaitForChange != null && command.WaitForChange.Count > 0))
                {
                    var waitResult = WaitForCondition(
                        command.WaitForScreen, command.WaitUntilScreenNot,
                        command.WaitForChange, command.WaitTimeoutMs);
                    if (waitResult.screen != null)
                        response.Screen = waitResult.screen;
                    if (waitResult.timedOut)
                    {
                        response.Status = "completed_timeout";
                        ModLogger.Log($"[CommandBridge] Command {command.Id} keys OK but wait timed out after {command.WaitTimeoutMs}ms");
                    }

                    // Session 48: when a path transitions INTO WorldMap, auto-
                    // tap C so the cursor snaps to the player's current node.
                    // Many flows (battle_flee, Exit, Leave, auto-return) leave
                    // the cursor a few tiles off the player, breaking the next
                    // world_travel_to by making it target the wrong node.
                    // Skip if the wait timed out — if we didn't actually reach
                    // WorldMap, tapping C would fire in the wrong screen.
                    if (!waitResult.timedOut
                        && string.Equals(command.WaitForScreen, "WorldMap", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            IntPtr gw = Process.GetCurrentProcess().MainWindowHandle;
                            if (gw != IntPtr.Zero)
                                _inputSimulator.SendKeyPressToWindow(gw, 0x43); // VK_C
                        }
                        catch { /* best-effort — never block the response */ }
                    }
                }

                // Build screen from SM state — no memory reads, no stale bytes.
                // Only trust SM for party-tree transitions where the SM has
                // full coverage (PartyMenu ↔ CharacterStatus ↔ EqA ↔ pickers
                // ↔ JobSelection). For everything else (WorldMap, LocationMenu,
                // shops, battle), fall back to detection — the SM doesn't
                // model those transitions and will be confidently wrong.
                bool smInPartyTreeNow = ScreenMachine?.CurrentScreen is
                    GameScreen.PartyMenuUnits or GameScreen.CharacterStatus or
                    GameScreen.EquipmentScreen or GameScreen.EquipmentItemList or
                    GameScreen.JobScreen or GameScreen.JobActionMenu or
                    GameScreen.JobChangeConfirmation or
                    GameScreen.SecondaryAbilities or GameScreen.ReactionAbilities or
                    GameScreen.SupportAbilities or GameScreen.MovementAbilities or
                    GameScreen.CombatSets or GameScreen.CharacterDialog or
                    GameScreen.DismissUnit or
                    GameScreen.ChronicleEncyclopedia or GameScreen.ChronicleStateOfRealm or
                    GameScreen.ChronicleEvents or GameScreen.ChronicleAuracite or
                    GameScreen.ChronicleReadingMaterials or GameScreen.ChronicleCollection or
                    GameScreen.ChronicleErrands or GameScreen.ChronicleStratagems or
                    GameScreen.ChronicleLessons or GameScreen.ChronicleAkademicReport or
                    GameScreen.OptionsSettings;
                bool smWasInPartyTree = _smScreenBeforeKeys is
                    GameScreen.PartyMenuUnits or GameScreen.CharacterStatus or
                    GameScreen.EquipmentScreen or GameScreen.EquipmentItemList or
                    GameScreen.JobScreen or GameScreen.JobActionMenu or
                    GameScreen.JobChangeConfirmation or
                    GameScreen.SecondaryAbilities or GameScreen.ReactionAbilities or
                    GameScreen.SupportAbilities or GameScreen.MovementAbilities or
                    GameScreen.CombatSets or GameScreen.CharacterDialog or
                    GameScreen.DismissUnit;
                if (response.Screen == null && ScreenMachine != null
                    && successCount > 0
                    && smInPartyTreeNow)
                {
                    // SM says party tree — but verify with detection first.
                    // If detection says we left the party tree AND menuDepth==0
                    // (definitively outer screen), trust detection and sync
                    // the SM. When menuDepth > 0, we're in a nested panel —
                    // trust the SM because detection can be confused by
                    // stale ui=1/unitsTabFlag=0 combinations on EqA/pickers
                    // that make detection return TravelList spuriously.
                    var detCheck = DetectScreen();
                    bool detectionSaysPartyTree = detCheck != null && (
                        detCheck.Name == "PartyMenuUnits" ||
                        detCheck.Name == "PartyMenuInventory" ||
                        detCheck.Name == "PartyMenuChronicle" ||
                        detCheck.Name == "PartyMenuOptions" ||
                        detCheck.Name == "PartySubScreen");
                    // Skip the drift check when the SM just transitioned
                    // during this command — menuDepth may lag 50-200ms
                    // before flipping from 0 to 2 after entering a nested
                    // panel. If SM.CurrentScreen != _smScreenBeforeKeys,
                    // the SM just updated itself; trust it through the
                    // animation lag. Next detection cycle re-checks.
                    bool smJustTransitioned = ScreenMachine.CurrentScreen != _smScreenBeforeKeys;
                    if (detCheck != null && !detectionSaysPartyTree
                        && detCheck.MenuDepth == 0
                        && !smJustTransitioned)
                    {
                        // Detection says we're NOT in the party tree AND
                        // memory confirms outer screen — SM is stale.
                        ModLogger.Log($"[SM-Drift] SM={ScreenMachine.CurrentScreen} but detection={detCheck.Name} + menuDepth=0. Trusting detection.");
                        response.Screen = detCheck;
                        var detectedGs = detCheck.Name switch
                        {
                            "WorldMap" => GameScreen.WorldMap,
                            "TravelList" => GameScreen.TravelList,
                            "LocationMenu" => GameScreen.LocationMenu,
                            "Tavern" => GameScreen.Tavern,
                            _ => (GameScreen?)null
                        };
                        if (detectedGs.HasValue)
                            ScreenMachine.SetScreen(detectedGs.Value);
                    }
                    else
                    {
                        // Detection agrees we're in the party tree — use SM
                        // for the precise sub-screen disambiguation.
                        response.Screen = BuildScreenFromSM();
                    }
                }
                // Fallback to detection if SM isn't available or didn't
                // model the transition.
                if (response.Screen == null)
                {
                    response.Screen = DetectScreen();
                    // Mirror detected name into SM for all screens (see
                    // query-path note above — the enum-sync table below
                    // only covers 4).
                    if (response.Screen != null && ScreenMachine != null)
                        ScreenMachine.ObserveDetectedScreen(response.Screen.Name);

                    // Override detection-ambiguous names where the SM has a
                    // stronger signal. If we override here, also skip the
                    // SM-sync below — syncing SM to the stale detection
                    // result (e.g. TravelList when SM correctly says
                    // SaveSlotPicker) would undo the valid SM state.
                    bool overrode = false;
                    if (response.Screen != null && ScreenMachine != null)
                    {
                        var resolved = ScreenDetectionLogic.ResolveAmbiguousScreen(
                            ScreenMachine.CurrentScreen, response.Screen.Name,
                            ScreenMachine.KeysSinceLastSetScreen,
                            ScreenMachine.LastSetScreenFromKey);
                        if (resolved != response.Screen.Name)
                        {
                            ModLogger.Log($"[SM-Override] Detection={response.Screen.Name} → {resolved} (SM={ScreenMachine.CurrentScreen}).");
                            response.Screen.Name = resolved;
                            overrode = true;
                        }
                    }
                    // Sync SM to detection result so the SM doesn't carry
                    // a stale state into the next key press. This handles
                    // transitions the SM doesn't model (WorldMap↔LocationMenu,
                    // shop screens, etc.).
                    if (!overrode && response.Screen != null && ScreenMachine != null)
                    {
                        var detectedGs = response.Screen.Name switch
                        {
                            "WorldMap" => GameScreen.WorldMap,
                            "TravelList" => GameScreen.TravelList,
                            "LocationMenu" => GameScreen.LocationMenu,
                            "Tavern" => GameScreen.Tavern,
                            _ => (GameScreen?)null
                        };
                        if (detectedGs.HasValue && ScreenMachine.CurrentScreen != detectedGs.Value)
                        {
                            ModLogger.Log($"[SM-Sync] Detection={response.Screen.Name}, SM={ScreenMachine.CurrentScreen}. Syncing SM to {detectedGs.Value}.");
                            ScreenMachine.SetScreen(detectedGs.Value);
                        }
                    }
                }
            }

            ModLogger.Log($"[CommandBridge] Command {command.Id} finished: {response.Status} ({successCount}/{command.Keys.Count} keys)");
            return response;
        }

        /// <summary>
        /// Polls game state at ~5ms intervals until the requested condition is met or timeout.
        /// Returns the final detected screen and whether the wait timed out.
        /// Call TakePreSnapshot before sending keys if using waitForChange.
        /// </summary>
        private (DetectedScreen? screen, bool timedOut) WaitForCondition(
            string? waitForScreen, string? waitUntilScreenNot,
            List<string>? waitForChange, int waitTimeoutMs)
        {
            bool hasWait = waitForScreen != null
                        || waitUntilScreenNot != null
                        || (waitForChange != null && waitForChange.Count > 0);

            if (!hasWait || Explorer == null)
                return (null, false);

            int timeoutMs = Math.Clamp(waitTimeoutMs, 50, 10000);
            var sw = Stopwatch.StartNew();

            // Snapshot pre-wait values for WaitForChange
            long[]? preValues = null;
            (nint address, int size)[]? changeAddresses = null;
            if (waitForChange != null && waitForChange.Count > 0)
            {
                changeAddresses = new (nint, int)[waitForChange.Count];
                for (int i = 0; i < waitForChange.Count; i++)
                    changeAddresses[i] = ((nint)Convert.ToInt64(waitForChange[i], 16), 1);
                preValues = Explorer.ReadMultiple(changeAddresses);
            }

            DetectedScreen? lastScreen = null;

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(5);
                lastScreen = DetectScreen();
                if (lastScreen == null) continue;

                if (waitForScreen != null)
                {
                    if (string.Equals(lastScreen.Name, waitForScreen, StringComparison.OrdinalIgnoreCase))
                        return (lastScreen, false);
                    continue;
                }

                if (waitUntilScreenNot != null)
                {
                    if (!string.Equals(lastScreen.Name, waitUntilScreenNot, StringComparison.OrdinalIgnoreCase))
                    {
                        // Screen changed — now settle: wait for 10 consecutive matching
                        // reads at 100ms intervals (1 second stable). Animations and
                        // transient states (BattleActing during attacks) can persist
                        // for several hundred milliseconds.
                        string newName = lastScreen.Name;
                        int stableCount = 0;
                        while (sw.ElapsedMilliseconds < timeoutMs)
                        {
                            Thread.Sleep(100);
                            var settled = DetectScreen();
                            if (settled == null) { stableCount = 0; continue; }
                            if (settled.Name == newName)
                            {
                                stableCount++;
                                lastScreen = settled;
                                if (stableCount >= 10)
                                    return (settled, false);
                            }
                            else if (string.Equals(settled.Name, waitUntilScreenNot, StringComparison.OrdinalIgnoreCase))
                            {
                                // Reverted back — was a transient blip, keep waiting
                                break;
                            }
                            else
                            {
                                // Changed to something else — restart settle
                                newName = settled.Name;
                                lastScreen = settled;
                                stableCount = 0;
                            }
                        }
                    }
                    continue;
                }

                if (changeAddresses != null && preValues != null)
                {
                    var currentValues = Explorer.ReadMultiple(changeAddresses);
                    for (int i = 0; i < preValues.Length; i++)
                    {
                        if (currentValues[i] != preValues[i])
                            return (lastScreen, false);
                    }
                }
            }

            return (lastScreen, true);
        }

        private CommandResponse ExecuteSequence(CommandRequest command)
        {
            var response = new CommandResponse
            {
                Id = command.Id,
                ProcessedAt = DateTime.UtcNow.ToString("o"),
                GameWindowFound = true,
                Sequence = new SequenceResult
                {
                    TotalSteps = command.Steps?.Count ?? 0
                }
            };

            IntPtr gameWindow = Process.GetCurrentProcess().MainWindowHandle;
            response.GameWindowFound = gameWindow != IntPtr.Zero;

            if (!response.GameWindowFound)
            {
                response.Status = "failed";
                response.Error = "Could not find game window handle";
                return response;
            }

            if (command.Steps == null || command.Steps.Count == 0)
            {
                response.Status = "completed";
                return response;
            }

            for (int s = 0; s < command.Steps.Count; s++)
            {
                var step = command.Steps[s];
                var stepResult = new StepResult
                {
                    Index = s,
                    Description = step.Description
                };

                // Send keys for this step
                int keysOk = 0;
                for (int k = 0; k < step.Keys.Count; k++)
                {
                    var key = step.Keys[k];
                    UserInputMonitor?.MarkBridgeSent(key.Vk);
                    bool success = _inputSimulator.SendKeyPressToWindow(gameWindow, key.Vk);
                    if (success)
                    {
                        keysOk++;
                        ScreenMachine?.OnKeyPressed(key.Vk);
                        if (_battleMenuTracker.InSubmenu)
                            _battleMenuTracker.OnKeyPressed(key.Vk);
                        InvalidateJobCursorOnRowCross(key.Vk);
                        InvalidatePartyMenuCursorOnMove(key.Vk);
                    InvalidateEquipPickerCursorOnMove(key.Vk);
                    }

                    if (key.HoldMs > 0)
                        Thread.Sleep(key.HoldMs);

                    if (k < step.Keys.Count - 1 && command.DelayBetweenMs > 0)
                        Thread.Sleep(command.DelayBetweenMs);
                }

                stepResult.KeysProcessed = keysOk;

                // Smart wait: use condition-based polling if specified, else fall back to fixed sleep
                bool stepHasWait = step.WaitForScreen != null
                                || step.WaitUntilScreenNot != null
                                || (step.WaitForChange != null && step.WaitForChange.Count > 0);
                if (stepHasWait)
                {
                    var stepWait = WaitForCondition(
                        step.WaitForScreen, step.WaitUntilScreenNot,
                        step.WaitForChange, step.WaitTimeoutMs);
                    if (stepWait.timedOut)
                    {
                        stepResult.Status = "timeout";
                        ModLogger.Log($"[CommandBridge] Sequence step {s} wait timed out");
                    }
                }
                else if (step.WaitMs > 0)
                {
                    Thread.Sleep(step.WaitMs);
                }

                // Read memory address if requested
                if (!string.IsNullOrEmpty(step.ReadAddress) && Explorer != null)
                {
                    var readAddr = Convert.ToInt64(step.ReadAddress, 16);
                    var readSz = Math.Clamp(step.ReadSize, 1, 4);
                    var readVal = Explorer.ReadAbsolute((nint)readAddr, readSz);
                    if (readVal != null)
                    {
                        stepResult.ReadResult = new ReadResult
                        {
                            Address = $"0x{readAddr:X}",
                            Size = readSz,
                            Value = readVal.Value.value,
                            Hex = $"0x{readVal.Value.value:X}",
                            RawBytes = BitConverter.ToString(readVal.Value.raw).Replace("-", " ")
                        };
                    }
                }

                // Check assertions if present
                if (step.Assert != null)
                {
                    var failure = CheckAssertions(step.Assert, s);
                    if (failure != null)
                    {
                        stepResult.Status = "assertion_failed";
                        response.Sequence.StepResults.Add(stepResult);
                        response.Sequence.StepsCompleted = s;
                        response.Sequence.FailedAssertion = failure;
                        response.Status = "assertion_failed";

                        // Embed state at failure point
                        var failState = StateReporter?.GetCurrentState();
                        if (failState != null)
                        {
                            failState.ScreenState = ScreenMachine?.GetScreenState();
                            response.GameState = failState;
                        }

                        ModLogger.Log($"[CommandBridge] Sequence stopped at step {s}: {failure.Field} expected={failure.Expected} actual={failure.Actual}");
                        return response;
                    }
                }

                stepResult.Status = "completed";
                response.Sequence.StepResults.Add(stepResult);
                response.Sequence.StepsCompleted = s + 1;
            }

            response.Status = "completed";

            // Embed final state
            var finalState = StateReporter?.GetCurrentState();
            if (finalState != null)
            {
                finalState.ScreenState = ScreenMachine?.GetScreenState();
                response.GameState = finalState;
            }

            ModLogger.Log($"[CommandBridge] Sequence completed: {response.Sequence.StepsCompleted}/{response.Sequence.TotalSteps} steps");
            return response;
        }

        private AssertionFailure? CheckAssertions(SequenceAssert assert, int stepIndex)
        {
            // Check screen assertion against state machine
            if (assert.Screen != null && ScreenMachine != null)
            {
                var actualScreen = ScreenMachine.CurrentScreen.ToString();
                if (!string.Equals(actualScreen, assert.Screen, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssertionFailure
                    {
                        StepIndex = stepIndex,
                        Field = "screen",
                        Expected = assert.Screen,
                        Actual = actualScreen
                    };
                }
            }

            // Check cursor index from memory
            if (assert.CursorIndex != null && StateReporter != null)
            {
                var state = StateReporter.GetCurrentState();
                var actualCursor = state.UI?.CursorIndex ?? -1;
                if (actualCursor != assert.CursorIndex.Value)
                {
                    return new AssertionFailure
                    {
                        StepIndex = stepIndex,
                        Field = "cursorIndex",
                        Expected = assert.CursorIndex.Value.ToString(),
                        Actual = actualCursor.ToString()
                    };
                }
            }

            // Check tab assertion against state machine
            if (assert.Tab != null && ScreenMachine != null)
            {
                var screenState = ScreenMachine.GetScreenState();
                var actualTab = screenState.Tab ?? "";
                if (!string.Equals(actualTab, assert.Tab, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssertionFailure
                    {
                        StepIndex = stepIndex,
                        Field = "tab",
                        Expected = assert.Tab,
                        Actual = actualTab
                    };
                }
            }

            // Check sidebar index against state machine
            if (assert.SidebarIndex != null && ScreenMachine != null)
            {
                var screenState = ScreenMachine.GetScreenState();
                var actualSidebar = screenState.SidebarIndex ?? -1;
                if (actualSidebar != assert.SidebarIndex.Value)
                {
                    return new AssertionFailure
                    {
                        StepIndex = stepIndex,
                        Field = "sidebarIndex",
                        Expected = assert.SidebarIndex.Value.ToString(),
                        Actual = actualSidebar.ToString()
                    };
                }
            }

            return null;
        }

        private void WriteResponse(CommandResponse response)
        {
            try
            {
                var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_responseFilePath, json);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[CommandBridge] Failed to write response: {ex.Message}");
            }
        }

        // Screen detection address table — indices match the ReadMultiple call below
        private static readonly (nint address, int size)[] ScreenAddresses =
        {
            ((nint)0x140D3A41E, 1),  // 0: partyFlag
            ((nint)0x140D4A264, 1),  // 1: uiFlag
            ((nint)0x14077D208, 1),  // 2: location
            ((nint)0x140787A22, 1),  // 3: hover
            ((nint)0x1407FC620, 1),  // 4: menuCursor (battle action menu: 0=Move,1=Abilities,2=Wait,3=Status,4=AutoBattle)
            ((nint)0x140900824, 1),  // 5: encA
            ((nint)0x140900828, 1),  // 6: encB
            ((nint)0x14077D2A2, 2),  // 7: battleTeam
            ((nint)0x14077CA8C, 1),  // 8: battleAct
            ((nint)0x14077CA9C, 1),  // 9: battleMov
            ((nint)0x14077D2A4, 2),  // 10: battleId
            ((nint)0x14077D2AC, 2),  // 11: battleHp
            ((nint)0x14077CA30, 4),  // 12: unitSlot0
            ((nint)0x14077CA54, 4),  // 13: unitSlot9
            ((nint)0x140C64A5C, 1),  // 14: pauseFlag
            ((nint)0x14077CA5C, 1),  // 15: moveMode (VOLATILE/unused — was 255=selecting tile, replaced by battleMode[16]==2)
            ((nint)0x140900650, 1),  // 16: battleMode (3=action menu, 2=move, 0=game over/cutscene)
            ((nint)0x14077C970, 1),  // 17: cameraRotation (incrementing counter, mod 4 = current rotation 0-3)
            ((nint)0x140D3A10C, 1),  // 18: submenuFlag (1=submenu/mode active, 0=top-level menu; also 1 during game over)
            ((nint)0x14077CA94, 2),  // 19: eventId (event file number during cutscenes, nameId during battle)
            ((nint)0x1411A0FB6, 1),  // 20: storyObjective (yellow diamond location ID on world map)
            ((nint)0x140D43481, 1),  // 21: locationMenuFlag (1=inside a named location's menu like Outfitters/Tavern list, 0=elsewhere)
            ((nint)0x140D435F0, 1),  // 22: shopTypeIndex (0=Outfitter, 1=Tavern, 2=Warriors' Guild, 3=Poachers' Den — index of hovered shop in LocationMenu)
            ((nint)0x141844DD0, 1),  // 23: insideShopFlag (1=inside a shop/service interior after pressing Enter, 0=elsewhere)
            ((nint)0x14184276C, 1),  // 24: shopSubMenuIndex (Outfitter: 0=menu, 1=Buy, 4=Sell, 6=Fitting — other shops unmapped)
            ((nint)0x140D39CD0, 4),  // 25: gil (player's currency, u32 little-endian)
            ((nint)0x141870704, 4),  // 26: shopListCursorIndex (row the player is highlighting inside OutfitterBuy/Sell/Fitting; 0-based)
            ((nint)0x14077CB67, 1),  // 27: menuDepth (0=outer menu (WorldMap/PartyMenu/CharacterStatus), 2=inner panel (EquipmentAndAbilities or an ability picker). Discovered 2026-04-14 session 13 via module-memory snapshot diff; verified stable across repeated reads. Primary use: drift-check the state machine — if state machine thinks we're on EquipmentAndAbilities/picker but menuDepth reads 0, we're actually on CharacterStatus and should snap back.)
            ((nint)0x140D87830, 1),  // 28: encounterFlag (10=encounter dialog active, 0=no encounter. Session 20 diff at TheSiedgeWeald. Replaces unusable encA/encB noise counters.)
        };

        /// <summary>
        /// Polls DetectScreen until two consecutive reads return the same screen name,
        /// ensuring the game UI has settled after a transition. Waits up to 1s.
        /// </summary>
        private DetectedScreen? DetectScreenSettled() => DetectScreenSettled(requireSettle: true);

        /// <summary>
        /// When <paramref name="requireSettle"/> is false, returns a single DetectScreen()
        /// read without the 150ms+ settle loop. Screen-query commands (empty keys list,
        /// no action) can't be mid-transition — nothing just changed — so forcing a
        /// settle is pure wasted latency. Saves ~150-200ms on every `screen` call.
        /// The key-sending and action paths still settle (the default) to let UI
        /// animations finish before we report state.
        ///
        /// Session 46: tried raising the cap from 1000ms to 3000ms for
        /// Fight→Formation transitions but it made every menu nav slow
        /// in the common case (stable reads only need ~150ms, but jitter
        /// paths consumed the full cap). Reverted to 1000ms. Formation
        /// transitions still need a specialized wait in the `Fight`
        /// action handler — TODO follow-up.
        /// </summary>
        private DetectedScreen? DetectScreenSettled(bool requireSettle)
        {
            var first = DetectScreen();
            if (first == null) return null;
            if (!requireSettle) return first;

            var sw = Stopwatch.StartNew();
            string lastName = first.Name;
            DetectedScreen? last = first;
            int stableCount = 1; // first read is the first stable observation

            // Tightened from 50ms→30ms and count>=3→count>=2.
            // DetectScreen is a batch of pointer-direct reads — sub-ms.
            // Two consecutive stable reads at 30ms = 60ms minimum settle
            // (was 150ms). Per-action win on every completed command.
            while (sw.ElapsedMilliseconds < 1000)
            {
                Thread.Sleep(30);
                var current = DetectScreen();
                if (current == null) continue;

                if (current.Name == lastName)
                {
                    stableCount++;
                    last = current;
                    if (stableCount >= 2)
                        return current;
                }
                else
                {
                    lastName = current.Name;
                    last = current;
                    stableCount = 1;
                }
            }

            return last;
        }

        private void EnsureMapLoader()
        {
            if (_mapLoader != null) return;
            // Look for map JSON files in claude_bridge/maps/ directory
            var mapsDir = Path.Combine(_bridgeDirectory, "maps");
            if (!Directory.Exists(mapsDir))
                Directory.CreateDirectory(mapsDir);
            _mapLoader = new MapLoader(mapsDir);
        }

        /// <summary>
        // ===== Movement Tile Validity via BFS + Learn-by-Doing Cache =====
        // BFS gives 100% recall (all valid tiles + ~12 false positives from impassable terrain).
        // When a move fails (tile is tree/obstacle), cache it as blocked for the rest of the battle.
        // This self-corrects: after 1-2 failed moves, the tile list becomes exact.
        private const long AddrTerrainGrid = 0x140C65000;
        private const int TerrainEntrySize = 7;
        private const int TerrainGridCols = 9;
        private const int TerrainGridRows = 8;
        private const int TerrainEntryCount = TerrainGridCols * TerrainGridRows; // 72

        private readonly HashSet<(int, int)> _blockedTiles = new();
        private int _lastWorldMapLocation = -1;
        private bool _battleMapAutoLoaded = false;

        private static readonly Dictionary<int, string> LocationNames = new()
        {
            // Settlements (0-14) — verified in-game 2026-04-06
            {0, "Royal City of Lesalia"}, {1, "Riovanes Castle"}, {2, "Eagrose Castle"},
            {3, "Lionel Castle"}, {4, "Limberry Castle"}, {5, "Zeltennia Castle"},
            {6, "Magick City of Gariland"}, {7, "Walled City of Yardrow"}, {8, "Mining Town of Gollund"},
            {9, "Merchant City of Dorter"}, {10, "Castled City of Zaland"}, {11, "Clockwork City of Goug"},
            {12, "Port City of Warjilis"}, {13, "Free City of Bervenia"}, {14, "Trade City of Sal Ghidos"},
            // Miscellaneous (15-23) — verified in-game 2026-04-06
            {15, "Ziekden Fortress"}, {16, "Mullonde"}, {17, "Brigands' Den"},
            {18, "Orbonne Monastery"}, {19, "Golgollada Gallows"}, {20, "unused"},
            {21, "Fort Besselat"}, {22, "Midlight's Deep"}, {23, "Nelveska Temple"},
            // Battlegrounds (24-42) — verified in-game 2026-04-06
            {24, "Mandalia Plain"}, {25, "Fovoham Windflats"}, {26, "The Siedge Weald"},
            {27, "Mount Bervenia"}, {28, "Zeklaus Desert"}, {29, "Lenalian Plateau"},
            {30, "Tchigolith Fenlands"}, {31, "The Yuguewood"}, {32, "Araguay Woods"},
            {33, "Grogh Heights"}, {34, "Beddha Sandwaste"}, {35, "Zeirchele Falls"},
            {36, "Dorvauldar Marsh"}, {37, "Balias Tor"}, {38, "Dugeura Pass"},
            {39, "Balias Swale"}, {40, "Finnath Creek"}, {41, "Lake Poescas"},
            {42, "Mount Germinas"},
        };

        /// <summary>
        /// Syncs the battle menu tracker with the settled screen state.
        /// Called AFTER DetectScreenSettled so we only react to stable screen transitions,
        /// not intermediate flickers during settling.
        /// </summary>
        private void SyncBattleMenuTracker(DetectedScreen? screen)
        {
            if (screen == null) return;

            if (screen.Name == "BattleAbilities" && !_battleMenuTracker.InSubmenu)
            {
                _battleMenuTracker.EnterAbilitiesSubmenu(GetAbilitiesSubmenuItems());
                screen.UI = _battleMenuTracker.CurrentItem;
            }
            else if (screen.Name == "BattleAbilities" && _battleMenuTracker.InSubmenu)
            {
                // Refresh items list — covers the case where the
                // Support-ability cache became known on a later scan
                // and grew the submenu (e.g. Reequip / Evasive Stance
                // adding a 4th row). Without this the tracker keeps
                // the stale row count from when the submenu was first
                // entered.
                _battleMenuTracker.RefreshSubmenuItems(GetAbilitiesSubmenuItems());
                if (_battleMenuTracker.InAbilityList)
                {
                    // Level 3: inside an ability list (e.g. Mettle → Focus/Rush/Shout)
                    var skillsetName = _battleMenuTracker.SelectedItem;
                    if (skillsetName != null)
                        screen.Name = GameBridge.ScreenDetectionLogic.GetAbilityScreenName(skillsetName);
                    screen.UI = _battleMenuTracker.CurrentAbility;
                }
                else if (_battleMenuTracker.SelectedItem != null)
                {
                    // An ability submenu item was selected — enter the ability list
                    var skillsetName = _battleMenuTracker.SelectedItem;
                    if (skillsetName != "Attack") // Attack goes to targeting, not a list
                    {
                        var abilityNames = GetAbilityListForSkillset(skillsetName);
                        if (abilityNames.Length > 0)
                        {
                            _battleMenuTracker.EnterAbilityList(abilityNames);
                            screen.Name = GameBridge.ScreenDetectionLogic.GetAbilityScreenName(skillsetName);
                            screen.UI = _battleMenuTracker.CurrentAbility;
                        }
                    }
                    else
                    {
                        screen.Name = GameBridge.ScreenDetectionLogic.GetAbilityScreenName(skillsetName);
                        screen.UI = skillsetName;
                    }
                }
                else
                {
                    screen.UI = _battleMenuTracker.CurrentItem;
                }
            }
            else if (screen.Name != null)
            {
                _battleMenuTracker.SyncForScreen(screen.Name);
            }
        }

        private void ClearActiveUnitCache()
        {
            _cachedActiveUnitName = null;
            _cachedActiveUnitJob = null;
            _cachedActiveUnitX = -1;
            _cachedActiveUnitY = -1;
            _cachedActiveUnitHp = 0;
            _cachedActiveUnitMaxHp = 0;
            _cachedActiveUnitWeaponTag = null;
        }

        /// <summary>
        /// Snapshot the current active-unit identity for hand-off detection.
        /// Returns null when the cache is empty so the banner classifier
        /// can short-circuit comparisons.
        /// </summary>
        private GameBridge.TurnHandoffBannerClassifier.UnitIdentity? SnapshotActiveUnitIdentity()
            => _cachedActiveUnitName == null
                ? null
                : new GameBridge.TurnHandoffBannerClassifier.UnitIdentity(
                    _cachedActiveUnitName, _cachedActiveUnitJob,
                    _cachedActiveUnitX, _cachedActiveUnitY,
                    _cachedActiveUnitHp, _cachedActiveUnitMaxHp);

        /// <summary>
        /// Prepend the loud hand-off banner to a response's Info field so
        /// the compact one-line trailer surfaces it. Idempotent: skips when
        /// the same banner is already in Info (sub-step like battle_wait
        /// may have already prepended it). Delegates to the pure helper
        /// `HandoffBannerJoiner.PrependIfAbsent`.
        /// </summary>
        private static void PrependHandoffBanner(CommandResponse response, string? banner)
        {
            response.Info = GameBridge.HandoffBannerJoiner.PrependIfAbsent(response.Info, banner);
        }

        /// <summary>
        /// Render a per-action-type latency summary of the current session.
        /// Parses the JSONL session log and delegates stats math to the
        /// pure SessionStatsCalculator. Returns a pre-formatted string
        /// suitable for display.
        /// </summary>
        private string RenderSessionStats()
        {
            var logPath = _sessionLog?.LogPath;
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                return "(no session log yet — run a few commands first)";

            var rows = new List<GameBridge.SessionStatRow>();
            try
            {
                foreach (var line in File.ReadAllLines(logPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        var row = new GameBridge.SessionStatRow();
                        if (root.TryGetProperty("action", out var a)) row.Action = a.GetString() ?? "";
                        if (root.TryGetProperty("status", out var s)) row.Status = s.GetString() ?? "";
                        if (root.TryGetProperty("latencyMs", out var lm) && lm.ValueKind == System.Text.Json.JsonValueKind.Number)
                            row.LatencyMs = lm.GetInt32();
                        rows.Add(row);
                    }
                    catch { /* skip malformed row */ }
                }
            }
            catch (Exception ex)
            {
                return $"(session log read failed: {ex.Message})";
            }

            var report = GameBridge.SessionStatsCalculator.Compute(rows);
            var sb = new System.Text.StringBuilder();
            var fileName = System.IO.Path.GetFileName(logPath);
            sb.AppendLine($"─ Session: {fileName} ({report.TotalRows} rows) ─");
            sb.AppendLine(string.Format("{0,-22} {1,6}  {2,8}  {3,8}  {4,8}  {5,6}",
                "action", "count", "median", "p95", "max", "failed"));
            foreach (var e in report.Actions)
            {
                sb.AppendLine(string.Format("{0,-22} {1,6}  {2,6}ms  {3,6}ms  {4,6}ms  {5,6}",
                    Truncate(e.Action, 22), e.Count, e.MedianMs, e.P95Ms, e.MaxMs, e.Failed));
            }
            sb.AppendLine($"total: {report.TotalRows} rows · {report.FailedRows} failed · {report.SlowRows} slow (>=2000ms)");
            return sb.ToString().TrimEnd();
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);

        /// <summary>
        /// Drives BattleStatTracker lifecycle events (StartBattle /
        /// EndBattle) from screen-transition edges. Called after
        /// DetectScreenSettled so we only act on stable transitions.
        /// </summary>
        private void HandleBattleLifecycle(DetectedScreen? screen)
        {
            if (StatTracker == null || screen == null) return;

            var ev = GameBridge.BattleLifecycleClassifier.Classify(
                _lastClassifiedScreen, screen.Name);

            // S59: post-Victory loss surfacing. If we're transitioning
            // from BattleVictory into BattleDesertion (unit crystallized)
            // or a GameOver flicker, the Won result stands — but the
            // battle summary should note that a unit was lost after
            // the banner instead of silently swallowing the event.
            if (_lastClassifiedScreen == "BattleVictory")
            {
                if (screen.Name == "BattleDesertion")
                    StatTracker.NotePostVictoryLoss("A unit was lost after victory (desertion / crystallization)");
                else if (screen.Name == "GameOver")
                    StatTracker.NotePostVictoryLoss("Post-victory GameOver flicker — battle result stands as a win");
            }

            switch (ev)
            {
                case GameBridge.BattleLifecycleEvent.StartBattle:
                    var loc = screen.LocationName ?? "(unknown)";
                    StatTracker.StartBattle(loc);
                    // S58: ReadLiveHp's address cache is battle-scoped — the
                    // readonly-region pages shift across battles. Clear on
                    // start so the first attack re-scans and re-memoizes.
                    _navActions?.LiveHpCache.Clear();
                    // Move/Jump cache also per-battle: heap struct addresses
                    // change per battle, and so can unit rosters.
                    _navActions?.MoveJumpCache.Clear();
                    // S58: active-unit name/job/pos cache is stale across
                    // battles — TODO §1 Tier 3 "Active unit name/job stale
                    // across battles". Reset here so the first screen query
                    // of the new battle re-populates instead of showing
                    // prior battle's frozen data.
                    ClearActiveUnitCache();
                    _cachedLearnedAbilityNames = null;
                    _cachedPrimarySkillset = null;
                    _cachedSecondarySkillset = null;
                    _cachedSupportAbility = null;
                    // Clear turn-consumed flags on battle start. A prior
                    // battle ending mid-turn (flee / GameOver / unexpected
                    // transition) leaves these flags set; without this
                    // reset the first turn of the new battle would pre-flight-
                    // fail execute_turn / mis-correct the cursor via
                    // EffectiveMenuCursor with stale post-action assumptions.
                    _movedThisTurn = false;
                    _actedThisTurn = false;
                    break;

                case GameBridge.BattleLifecycleEvent.EndBattleVictory:
                    StatTracker.EndBattle(won: true);
                    break;

                case GameBridge.BattleLifecycleEvent.EndBattleDefeat:
                    StatTracker.EndBattle(won: false);
                    break;
            }

            _lastClassifiedScreen = screen.Name;
        }

        /// <summary>
        /// Cache learned ability IDs from the active unit's scan results.
        /// </summary>
        private void CacheLearnedAbilities(GameBridge.BattleState? battle)
        {
            if (battle == null) return;

            // Cache abilities and skillsets from the active unit in the Units list
            // (populated by NavigationActions with roster-sourced job/brave/faith)
            var activeUnit = battle.Units?.FirstOrDefault(u => u.IsActive);
            if (activeUnit == null) return;

            if (activeUnit.Abilities != null && activeUnit.Abilities.Count > 0)
            {
                _cachedLearnedAbilityNames = new HashSet<string>(
                    activeUnit.Abilities.Select(a => a.Name));
            }

            // Primary skillset from roster-sourced job name
            if (activeUnit.JobName != null)
            {
                _cachedPrimarySkillset = GetPrimarySkillsetByJobName(activeUnit.JobName);
                if (_cachedPrimarySkillset == null)
                    ModLogger.Log($"[CommandBridge] WARN: No primary skillset for job '{activeUnit.JobName}' — submenu will be missing primary");
            }

            // Secondary skillset from the roster-matched scan data
            // Uses the active unit from Units list (correctly matched by RosterMatcher)
            // instead of BattleTracker.ActiveUnit (which may identify the wrong unit)
            //
            // S59: SecondaryAbility byte can read 0 transiently (live-repro'd on
            // Ramza's second turn in a battle — scan earlier returned idx=6/Items,
            // later scan returned idx=0/null). If we have a non-null cached value
            // AND the abilities list contains entries from a non-primary skillset,
            // infer the secondary from the abilities list instead of blanking.
            // S60 fix: persist _cachedSecondarySkillset across scans via
            // SecondarySkillsetResolver. The SecondaryAbility byte reads 0
            // transiently (live-confirmed S59 on Ramza's second turn); when
            // it does, FilterAbilitiesBySkillsets upstream also strips the
            // secondary's abilities from the abilities[] list, so inference
            // has nothing to work with. Blanking to null on that transient
            // miss broke the `battle_ability "Phoenix Down"` submenu lookup
            // for the rest of the turn. The resolver now preserves the last
            // known good value instead. Cache cleared on turn reset.
            string? byteResolved = activeUnit.SecondaryAbility > 0
                ? GetSkillsetName(activeUnit.SecondaryAbility)
                : null;
            var abilityNames = activeUnit.Abilities?.ConvertAll(a => a.Name);
            string? previousCache = _cachedSecondarySkillset;
            _cachedSecondarySkillset = GameBridge.SecondarySkillsetResolver.Resolve(
                byteResolvedSecondary: byteResolved,
                abilityNames: abilityNames,
                primarySkillset: _cachedPrimarySkillset,
                previousCache: previousCache,
                getSkillsetForAbility: GameBridge.ActionAbilityLookup.GetSkillsetForAbility);

            if (_cachedSecondarySkillset != null
                && _cachedSecondarySkillset != byteResolved
                && _cachedSecondarySkillset != previousCache)
            {
                ModLogger.Log($"[CommandBridge] SecondaryAbility byte read 0 — inferred '{_cachedSecondarySkillset}' from abilities[] list");
            }
            else if (byteResolved == null && _cachedSecondarySkillset != null
                     && _cachedSecondarySkillset == previousCache)
            {
                ModLogger.Log($"[CommandBridge] SecondaryAbility byte read 0 — preserved cached '{_cachedSecondarySkillset}'");
            }
            ModLogger.Log($"[CommandBridge] Skillsets: primary={_cachedPrimarySkillset ?? "null"}, secondary={_cachedSecondarySkillset ?? "null"} (secondaryIdx={activeUnit.SecondaryAbility})");

            // Cache active unit's Support ability for the Abilities-
            // submenu row count. Reequip / Evasive Stance add a battle
            // command row that the bridge needs to know about so cursor
            // labeling and ScrollDown wrap-around match the in-game menu.
            // Preserve prior cache when scan returned no name (transient
            // empty-active-unit reads happen after key presses) — same
            // pattern as SecondarySkillsetResolver. Reset is handled by
            // turn-boundary sites (battle_wait, StartBattle).
            if (activeUnit.Support != null)
            {
                _cachedSupportAbility = activeUnit.Support;
            }

            // Snapshot active unit identity for the compact battle screen line.
            // Preserve-on-null: rapid back-to-back scans (e.g. the post-wait
            // settle re-scan) sometimes get degraded roster-name resolution
            // and surface activeUnit.Name as null — overwriting would nuke
            // the prior good cache and break the multi-unit hand-off banner.
            // Same pattern as `_cachedSupportAbility` above and per
            // `feedback_cache_preserve_on_null_active_unit.md`.
            // Reset is handled at turn-boundary sites (battle_wait,
            // StartBattle, terminal-screen lifecycle).
            if (!string.IsNullOrEmpty(activeUnit.Name))
            {
                _cachedActiveUnitName = activeUnit.Name;
                _cachedActiveUnitJob = activeUnit.JobName;
                _cachedActiveUnitX = activeUnit.X;
                _cachedActiveUnitY = activeUnit.Y;
                _cachedActiveUnitHp = activeUnit.Hp;
                _cachedActiveUnitMaxHp = activeUnit.MaxHp;
                // Weapon banner tag — computed server-side in
                // NavigationActions.CollectUnitPositionsFull via ItemData.ComposeWeaponTag.
                // Null when unarmed / unknown weapon / non-player team.
                _cachedActiveUnitWeaponTag = activeUnit.WeaponTag;
            }
        }

        /// <summary>
        /// Strip empty tiles and verbose fields from ability entries to reduce
        /// token usage in the scan_move response. Only occupied tiles (with an
        /// enemy/ally/self) are kept; empty tiles are summarized as TotalTargets count.
        /// Effect (flavor text) and VRange/AoE/HoE (derivable from metadata) are removed.
        /// </summary>
        private static void CompactAbilities(CommandResponse response)
        {
            var units = response.Battle?.Units;
            if (units == null) return;

            foreach (var unit in units)
            {
                if (unit.Abilities == null) continue;
                foreach (var ability in unit.Abilities)
                {
                    if (ability.ValidTargetTiles != null)
                    {
                        int total = ability.ValidTargetTiles.Count;
                        // Keep only tiles with an occupant
                        var occupied = ability.ValidTargetTiles
                            .Where(t => t.Occupant != null)
                            .ToList();
                        ability.TotalTargets = total;
                        ability.ValidTargetTiles = occupied.Count > 0 ? occupied : null;
                    }
                    // Strip flavor text — Claude knows what Potion does from the name.
                    // Keep addedEffect since it's mechanically useful ("Restores 30 HP").
                    ability.Effect = null!;
                }
                // Server-side: hide enemy-target abilities with no enemies, collapse Aim families
                unit.Abilities = AbilityCompactor.Compact(unit.Abilities);
            }
        }

        private void CacheSecondaryFromRoster(int level, int brave, int faith)
        {
            const long AddrRosterBase = 0x1411A18D0;
            const int RosterStride = 0x258;
            const int RosterMaxSlots = 20;

            if (Explorer == null) return;

            try
            {
                var reads = new (nint, int)[RosterMaxSlots * 4];
                for (int s = 0; s < RosterMaxSlots; s++)
                {
                    long addr = AddrRosterBase + s * RosterStride;
                    reads[s * 4] = ((nint)(addr + 0x1D), 1); // level
                    reads[s * 4 + 1] = ((nint)(addr + 0x1E), 1); // brave
                    reads[s * 4 + 2] = ((nint)(addr + 0x1F), 1); // faith
                    reads[s * 4 + 3] = ((nint)(addr + 0x07), 1); // secondary index
                }
                var vals = Explorer.ReadMultiple(reads);

                for (int s = 0; s < RosterMaxSlots; s++)
                {
                    if ((int)vals[s * 4] == level && (int)vals[s * 4 + 1] == brave && (int)vals[s * 4 + 2] == faith)
                    {
                        int rSecondary = (int)vals[s * 4 + 3];
                        _cachedSecondarySkillset = rSecondary > 0 ? GetSkillsetName(rSecondary) : null;
                        break;
                    }
                }
            }
            catch { /* best effort */ }
        }

        private HashSet<string>? _cachedLearnedAbilityNames;

        /// <summary>
        /// Get the ability names for a skillset, filtered to only learned abilities.
        /// Falls back to full skillset if no cached scan data.
        /// </summary>
        private string[] GetAbilityListForSkillset(string skillsetName)
        {
            // Return the FULL skillset list in its canonical order — the
            // game's in-battle ability submenu shows every ability of the
            // skillset (unlearned ones rendered greyed-out but still
            // occupying their slot). Using a learned-filtered list for
            // navigation causes off-by-N index errors when some entries
            // are unlearned.
            //
            // Aurablast incident (Kenrick / Martial Arts): learned list
            // was [Cyclone, Purification, Chakra, Revive] (4 entries),
            // so "Chakra" resolved to index 2 and Down×2 landed on
            // Aurablast — which sits at index 2 in the game's actual
            // 8-entry display.
            //
            // The per-unit ability[] array in scan_move output (populated
            // by NavigationActions.FilterAbilitiesBySkillsets) still shows
            // only LEARNED abilities — that's the decision-aid for "what
            // can this unit cast?" and is correct. This method is specifically
            // the NAV list: what the game's cursor traverses.
            var allAbilities = GameBridge.ActionAbilityLookup.GetSkillsetAbilities(skillsetName);
            return allAbilities?.Select(a => a.Name).ToArray() ?? System.Array.Empty<string>();
        }

        private static string? GetLocationName(int locationId)
        {
            return LocationNames.TryGetValue(locationId, out var name) ? name : null;
        }

        /// <summary>
        /// Base address of the location-unlock array (session 16, 2026-04-15).
        /// One byte per location id, indexed 0..N. 0x01 = unlocked/revealed,
        /// 0x00 = locked/unrevealed. Live-verified in an endgame save: most
        /// bytes = 0x01, two bytes = 0x00 (locked story triggers).
        /// </summary>
        private const long LocationUnlockArrayAddr = 0x1411A10B0;

        /// <summary>
        /// Read the unlock flag for a given location ID. Returns <c>true</c>
        /// when the location is available for travel, <c>false</c> when the
        /// game hasn't unlocked it yet. Returns <c>null</c> if memory access
        /// fails (Explorer not initialized or out-of-range read).
        /// </summary>
        internal bool? IsLocationUnlocked(int locationId)
        {
            if (Explorer == null || locationId < 0 || locationId > 60) return null;
            var r = Explorer.ReadAbsolute((nint)(LocationUnlockArrayAddr + locationId), 1);
            if (!r.HasValue) return null;
            return r.Value.value != 0;
        }

        /// <summary>
        /// Reads the active unit's secondary ability from the roster and returns
        /// the Abilities submenu items (always "Attack" first, then the secondary).
        /// </summary>
        private string[] GetAbilitiesSubmenuItems()
        {
            var items = new List<string> { "Attack" };

            if (_cachedPrimarySkillset != null)
                items.Add(_cachedPrimarySkillset);
            if (_cachedSecondarySkillset != null)
                items.Add(_cachedSecondarySkillset);

            // Reequip / Evasive Stance add a Reequip / Defend command
            // row at the bottom of the submenu. Without this the
            // bridge's cursor labeling wraps Items → Attack instead of
            // Items → Reequip → Attack, and ScrollDown can't reach the
            // command. Live-verified 2026-04-25 with Ramza S:Reequip.
            var supportCmd = GameBridge.SupportAbilityBattleCommand.Resolve(_cachedSupportAbility);
            if (supportCmd != null)
                items.Add(supportCmd);

            return items.ToArray();
        }

        /// <summary>
        /// Maps job name (from scan results) to the job's primary skillset name.
        /// </summary>
        /// <summary>
        /// Orders a set of passive-ability IDs by the game's canonical picker
        /// order (the sequence the player sees in-game), returning display
        /// names. Unlearned abilities are excluded. If <paramref name="pickerOrder"/>
        /// is null (order not yet captured live), falls back to the byte-ID
        /// order of the input set — this matches the legacy behavior and is
        /// non-harmful for list-display but will cause change_*_ability_to
        /// helpers to navigate with the wrong delta. Session 13 2026-04-14:
        /// reaction order is captured; support and movement orders still
        /// pending live walkthroughs.
        /// </summary>
        internal static List<string> OrderByPicker(
            HashSet<byte> learnedIds,
            byte[]? pickerOrder,
            Dictionary<byte, AbilityData.AbilityInfo> dict)
        {
            var result = new List<string>();
            if (pickerOrder != null)
            {
                foreach (var id in pickerOrder)
                {
                    if (learnedIds.Contains(id) && dict.TryGetValue(id, out var info))
                        result.Add(info.Name);
                }
                // Any learned IDs not in pickerOrder (e.g. abilities we
                // haven't observed in the live picker yet) get appended in
                // ID order so the list is still complete.
                foreach (var id in learnedIds)
                {
                    if (System.Array.IndexOf(pickerOrder, id) < 0 && dict.TryGetValue(id, out var info))
                        result.Add(info.Name);
                }
            }
            else
            {
                // No canonical order yet — fall back to ID-sorted.
                var sorted = new List<byte>(learnedIds);
                sorted.Sort();
                foreach (var id in sorted)
                    if (dict.TryGetValue(id, out var info))
                        result.Add(info.Name);
            }
            return result;
        }

        internal static string? GetPrimarySkillsetByJobName(string jobName)
        {
            return jobName switch
            {
                "Squire" => "Fundaments",
                "Chemist" => "Items",
                "Knight" => "Arts of War",
                "Archer" => "Aim",
                "Monk" => "Martial Arts",
                "White Mage" => "White Magicks",
                "Black Mage" => "Black Magicks",
                "Time Mage" => "Time Magicks",
                "Mystic" => "Mystic Arts",
                "Summoner" => "Summon",
                "Thief" => "Steal",
                "Orator" => "Speechcraft",
                "Geomancer" => "Geomancy",
                "Dragoon" => "Jump",
                "Samurai" => "Iaido",
                "Ninja" => "Throw",
                "Arithmetician" => "Arithmeticks",
                "Bard" => "Bardsong",
                "Dancer" => "Dance",
                "Dark Knight" => "Darkness",
                "Onion Knight" => null, // No primary action ability
                // Ramza/story character jobs
                "Gallant Knight" => "Mettle",   // Ramza Ch4
                "Heretic" => "Mettle",         // Ramza Ch4 (legacy)
                "Mettle" => "Mettle",
                // Story-character canonical primary skillsets. Add more as
                // you verify them in-game — unverified entries are omitted
                // so `Primary:` simply shows blank rather than a wrong name.
                "Holy Knight" => "Holy Sword",       // Agrias — verified 2026-04-14
                // Story-character primaries sourced from FFHandsFree/Wiki/
                // StoryCharacters.md (2026-04-15 session 16). Names match
                // the canonical skillsets shown in-game on the Primary row.
                // Not yet live-verified per-class — flag any mismatch.
                "Soldier" => "Limit",                // Cloud
                "Machinist" => "Snipe",              // Mustadio (WotL)
                "Engineer" => "Snipe",               // Mustadio (PSX name, same skillset)
                "Skyseer" => "Sky Mantra",           // Rapha
                "Heaven Knight" => "Sky Mantra",     // Rapha (PSX name)
                "Netherseer" => "Nether Mantra",     // Marach
                "Hell Knight" => "Nether Mantra",    // Marach (PSX name)
                "Divine Knight" => "Unyielding Blade", // Meliadoul
                "Templar" => "Spellblade",           // Beowulf
                "Dragonkin" => "Dragon",             // Reis (human form)
                "Holy Dragon" => "Dragon",           // Reis (dragon form)
                "Steel Giant" => "Work",             // Construct 8
                "Game Hunter" => "Hunting",          // Luso
                "Sky Pirate" => "Sky Pirating",      // Balthier
                "Thunder God" => "Swordplay",        // Orlandeau — Sword Saint's primary is the composite "Swordplay" (Holy + Unyielding + Fell Sword sub-skillsets). Verified live 2026-04-16 on actual EqA panel.
                "Sword Saint" => "Swordplay",        // Orlandeau (alt name)
                "Fell Knight" => "Fell Sword",       // Gaffgarion
                "Arc Knight" => "Holy Sword",        // Zalbag/Delita endgame — placeholder, verify
                "Rune Knight" => "Holy Sword",       // Dycedarg — placeholder, verify
                _ => null
            };
        }

        /// <summary>
        /// Maps secondary ability index (+0x07) to skillset name.
        /// These indices are into the character's personal unlocked ability list.
        /// </summary>
        internal static string? GetSkillsetName(int index)
        {
            return index switch
            {
                3 => "Mettle",
                4 => "Mettle",
                5 => "Fundaments",
                6 => "Items",
                7 => "Arts of War",
                8 => "Aim",
                9 => "Martial Arts",
                10 => "White Magicks",
                11 => "Black Magicks",
                12 => "Time Magicks",
                13 => "Summon",
                14 => "Steal",
                15 => "Speechcraft",
                16 => "Mystic Arts",
                17 => "Geomancy",
                18 => "Jump",
                19 => "Iaido",
                20 => "Throw",
                21 => "Arithmeticks",
                22 => "Bardsong",
                _ => null
            };
        }

        /// <summary>
        /// Build the detail panel for whatever the cursor is hovering on
        /// EquipmentAndAbilities (or a picker screen). Mirrors the game's
        /// right-side info panel so Claude sees the same data a human player
        /// would — WP/evade for weapons, descriptions for abilities, etc.
        ///
        /// `col` and `row` identify the grid cell (col 0 = equipment slot,
        /// col 1 = ability slot; row 0..4 within each column). For picker
        /// screens, pass col=1 and row=the slot type (1 secondary / 2 reaction
        /// / 3 support / 4 movement) to get the right "Type" label.
        /// </summary>
        public static UiDetail? BuildUiDetail(string name, int col, int row)
        {
            // Equipment column — find the item and surface its stats.
            if (col == 0)
            {
                var itemEntry = ItemData.Items.Values.FirstOrDefault(i => i.Name == name);
                if (itemEntry != null)
                {
                    return new UiDetail
                    {
                        Name = itemEntry.Name,
                        Type = PrettyItemType(itemEntry.Type),
                        Wp = itemEntry.WeaponPower,
                        Wev = itemEntry.WeaponEvade,
                        Range = itemEntry.Range,
                        Pev = itemEntry.PhysicalEvade,
                        Mev = itemEntry.MagicEvade,
                        HpBonus = itemEntry.HpBonus,
                        MpBonus = itemEntry.MpBonus,
                        // Extended info-panel fields (TODO §0 2026-04-14).
                        // Populated for top hero items; null elsewhere until
                        // the bulk-populate pass lands.
                        AttributeBonuses = itemEntry.AttributeBonuses,
                        EquipmentEffects = itemEntry.EquipmentEffects,
                        AttackEffects = itemEntry.AttackEffects,
                        Element = itemEntry.Element,
                        CanDualWield = itemEntry.CanDualWield,
                        CanWieldTwoHanded = itemEntry.CanWieldTwoHanded,
                        // ItemData doesn't carry free-form descriptions yet.
                        // Leave null — the UI will show stats + the extended
                        // fields above. If we want lore strings later, they'd
                        // come from the game's NXD item description table.
                    };
                }
                return new UiDetail { Name = name };
            }

            // Ability column. Row determines category:
            //   0 Primary skillset, 1 Secondary skillset, 2 Reaction, 3 Support, 4 Movement
            if (col == 1)
            {
                // Passive lookups — direct name→info via AbilityData dicts.
                var reaction = AbilityData.ReactionAbilities.Values.FirstOrDefault(a => a.Name == name);
                if (reaction != null)
                {
                    var (desc, usage) = SplitUsageCondition(reaction.Description);
                    return new UiDetail { Name = name, Type = "Reaction", Job = reaction.Job, Description = desc, UsageCondition = usage };
                }
                var support = AbilityData.SupportAbilities.Values.FirstOrDefault(a => a.Name == name);
                if (support != null)
                {
                    var (desc, usage) = SplitUsageCondition(support.Description);
                    return new UiDetail { Name = name, Type = "Support", Job = support.Job, Description = desc, UsageCondition = usage };
                }
                var movement = AbilityData.MovementAbilities.Values.FirstOrDefault(a => a.Name == name);
                if (movement != null)
                {
                    var (desc, usage) = SplitUsageCondition(movement.Description);
                    return new UiDetail { Name = name, Type = "Movement", Job = movement.Job, Description = desc, UsageCondition = usage };
                }

                // Skillset (Primary/Secondary slot). Map skillset name → owning job,
                // surface as "Primary skillset" or "Secondary skillset" per row.
                string? ownerJob = SkillsetOwnerJob(name);
                string typeLabel = row == 0 ? "Primary skillset" : "Secondary skillset";
                return new UiDetail { Name = name, Type = typeLabel, Job = ownerJob };
            }

            return new UiDetail { Name = name };
        }

        /// <summary>
        /// Some ability descriptions in AbilityData.cs embed a "Usage condition:"
        /// clause at the tail (matches the game's separate "Usage Conditions"
        /// panel for passives like Mana Shield). Split the clause out so we
        /// can surface it as a dedicated UiDetail.UsageCondition field.
        /// Returns (mainDescription, usageConditionOrNull).
        /// </summary>
        internal static (string? main, string? usage) SplitUsageCondition(string? description)
        {
            if (string.IsNullOrEmpty(description)) return (description, null);
            const string marker = "Usage condition:";
            int idx = description.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return (description, null);
            var main = description.Substring(0, idx).TrimEnd(' ', '.').TrimEnd() + ".";
            var usage = description.Substring(idx + marker.Length).Trim();
            if (usage.Length > 0) usage = char.ToUpper(usage[0]) + usage.Substring(1);
            return (main, usage);
        }

        /// <summary>
        /// Human-readable label for an ItemData subtype string (e.g.
        /// "knightsword" → "Knight's Sword"). Falls back to title-casing
        /// the raw subtype if we don't have a canonical mapping.
        /// </summary>
        private static string PrettyItemType(string rawType) => rawType switch
        {
            "knife" => "Dagger",
            "ninjablade" => "Ninja Blade",
            "sword" => "Sword",
            "knightsword" => "Knight's Sword",
            "katana" => "Katana",
            "axe" => "Axe",
            "rod" => "Rod",
            "staff" => "Staff",
            "flail" => "Flail",
            "gun" => "Gun",
            "crossbow" => "Crossbow",
            "bow" => "Bow",
            "instrument" => "Instrument",
            "book" => "Book",
            "polearm" => "Polearm",
            "pole" => "Pole",
            "bag" => "Bag",
            "cloth" => "Cloth",
            "throwing" => "Throwing Weapon",
            "bomb" => "Bomb",
            "fellsword" => "Fellsword",
            "shield" => "Shield",
            "helmet" => "Helmet",
            "hat" => "Hat",
            "hairadornment" => "Hair Adornment",
            "armor" => "Armor",
            "clothing" => "Clothing",
            "robe" => "Robe",
            "shoes" => "Shoes",
            "armguard" => "Armguard",
            "ring" => "Ring",
            "armlet" => "Armlet",
            "cloak" => "Cloak",
            "perfume" => "Perfume",
            "liprouge" => "Lip Rouge",
            "chemistitem" => "Chemist Item",
            _ => rawType,
        };

        /// <summary>
        /// Inverse of GetPrimarySkillsetByJobName: returns the job that
        /// teaches a given skillset (e.g. "Items" → "Chemist").
        /// </summary>
        private static string? SkillsetOwnerJob(string skillsetName) => skillsetName switch
        {
            "Mettle" => "Squire",
            "Fundaments" => "Squire",
            "Items" => "Chemist",
            "Arts of War" => "Knight",
            "Aim" => "Archer",
            "Martial Arts" => "Monk",
            "White Magicks" => "White Mage",
            "Black Magicks" => "Black Mage",
            "Time Magicks" => "Time Mage",
            "Summon" => "Summoner",
            "Steal" => "Thief",
            "Speechcraft" => "Orator",
            "Mystic Arts" => "Mystic",
            "Geomancy" => "Geomancer",
            "Jump" => "Dragoon",
            "Iaido" => "Samurai",
            "Throw" => "Ninja",
            "Arithmeticks" => "Arithmetician",
            "Bardsong" => "Bard",
            "Dance" => "Dancer",
            "Darkness" => "Dark Knight",
            _ => null,
        };

        private string? _lastLocationPath;

        private string GetLastLocationPath()
        {
            if (_lastLocationPath != null) return _lastLocationPath;
            // Save in bridge directory (claude_bridge/last_location.txt)
            _lastLocationPath = Path.Combine(_bridgeDirectory, "last_location.txt");
            return _lastLocationPath;
        }

        private void SaveLastLocation(int locationId)
        {
            _lastWorldMapLocation = locationId;
            try { File.WriteAllText(GetLastLocationPath(), locationId.ToString()); }
            catch { /* best effort */ }
        }

        private int LoadLastLocation()
        {
            try
            {
                var path = GetLastLocationPath();
                if (File.Exists(path))
                    return int.Parse(File.ReadAllText(path).Trim());
            }
            catch { }
            return -1;
        }

        private void SaveRandomEncounterMap(int locationId, int mapNumber)
        {
            try
            {
                EnsureMapLoader();
                var dir = _mapLoader?.MapDataDir ?? _bridgeDirectory;
                var path = Path.Combine(dir, "..", "random_encounter_maps.json");
                if (!File.Exists(path))
                    path = Path.Combine(dir, "random_encounter_maps.json");

                var lookup = new Dictionary<string, object>();
                if (File.Exists(path))
                {
                    var existing = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(File.ReadAllText(path));
                    if (existing != null)
                        foreach (var kv in existing)
                            if (kv.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                                lookup[kv.Key] = kv.Value.GetInt32();
                            else if (kv.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                                lookup[kv.Key] = kv.Value.GetString()!;
                }
                lookup[locationId.ToString()] = mapNumber;
                var json = System.Text.Json.JsonSerializer.Serialize(lookup, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                ModLogger.Log($"[Map] Saved random encounter map: location {locationId} → MAP{mapNumber:D3}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[Map] Failed to save random encounter map: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark a grid tile as blocked (impassable). Called when a move attempt fails.
        /// Persists for the duration of the battle.
        /// </summary>
        public void MarkTileBlocked(int gridX, int gridY)
        {
            _blockedTiles.Add((gridX, gridY));
            ModLogger.Log($"[Tiles] Marked ({gridX},{gridY}) as blocked. Total blocked: {_blockedTiles.Count}");
        }

        /// <summary>
        /// Clear blocked tile cache (call when entering a new battle).
        /// </summary>
        public void ClearBlockedTiles()
        {
            _blockedTiles.Clear();
            _battleMapAutoLoaded = false;
            _mapLoader?.ClearRejections();
        }

        /// <summary>
        /// Reads cursor grid position and computes valid movement tiles during BattleMoving.
        /// Uses BFS with terrain heights + blocked tile cache for accuracy.
        /// </summary>
        private void PopulateBattleTileData(DetectedScreen screen)
        {
            if (Explorer == null) return;

            try
            {
                // Read cursor position from grid addresses
                var cursorXResult = Explorer.ReadAbsolute((nint)0x140C64A54, 1);
                var cursorYResult = Explorer.ReadAbsolute((nint)0x140C6496C, 1);
                if (cursorXResult != null) screen.CursorX = (int)cursorXResult.Value.value;
                if (cursorYResult != null) screen.CursorY = (int)cursorYResult.Value.value;

                if (screen.Name != "BattleMoving")
                    return;

                // Surface the cursor tile on the compact screen line so Claude can
                // see where the cursor is about to confirm without a separate scan.
                if (cursorXResult != null && cursorYResult != null)
                    screen.UI = GameBridge.BattleCursorFormatter.FormatCursor(screen.CursorX, screen.CursorY);

                // Auto-load map from location ID if not already loaded
                EnsureMapLoader();
                if (_lastWorldMapLocation < 0)
                    _lastWorldMapLocation = LoadLastLocation();
                if (!_battleMapAutoLoaded && _lastWorldMapLocation >= 0 && _mapLoader != null)
                {
                    var autoMap = _mapLoader.LoadMapForLocation(_lastWorldMapLocation);
                    if (autoMap != null)
                    {
                        ModLogger.Log($"[Tiles] Auto-loaded MAP{autoMap.MapNumber:D3} for location {_lastWorldMapLocation}");
                        ClearBlockedTiles();
                    }
                    _battleMapAutoLoaded = true;
                }

                // Read Move/Jump base stats from UI buffer, then add movement ability bonus.
                // UI buffer shows BASE values (e.g., 4), not effective (e.g., 7 with Move+3).
                var moveResult = Explorer.ReadAbsolute((nint)0x1407AC7E4, 1);
                var jumpResult = Explorer.ReadAbsolute((nint)0x1407AC7E6, 1);
                int moveStat = moveResult != null ? (int)moveResult.Value.value : 4;
                int jumpStat = jumpResult != null ? (int)jumpResult.Value.value : 3;

                // Apply movement ability bonus from scan data (name-based, reliable)
                // and equipment bonuses. The UI buffer only has base Move/Jump.
                var activeAlly = _navActions?.GetActiveAlly();
                string? movementAbilityName = activeAlly?.MovementAbility;
                (moveStat, jumpStat) = GameBridge.MovementBfs.ApplyMovementAbility(moveStat, jumpStat, movementAbilityName);

                // Fallback: try BattleTracker ability ID if name not available
                if (movementAbilityName == null)
                {
                    var battleState = BattleTracker?.Update();
                    int movementAbilityId = battleState?.ActiveUnit?.MovementAbility ?? 0;
                    if (movementAbilityId == 0xE6) moveStat += 1;      // Move+1
                    else if (movementAbilityId == 0xE7) moveStat += 2; // Move+2
                    else if (movementAbilityId == 0xE8) moveStat += 3; // Move+3
                    else if (movementAbilityId == 0xEB) jumpStat += 1; // Jump+1
                    else if (movementAbilityId == 0xEC) jumpStat += 2; // Jump+2
                    else if (movementAbilityId == 0xED) jumpStat += 3; // Jump+3
                }

                // Read unit's grid position
                var gxResult = Explorer.ReadAbsolute((nint)0x140C64A54, 1);
                var gyResult = Explorer.ReadAbsolute((nint)0x140C6496C, 1);
                int unitGX = gxResult != null ? (int)gxResult.Value.value : 0;
                int unitGY = gyResult != null ? (int)gyResult.Value.value : 0;

                // === Try JSON map data first (exact terrain) ===
                var mapData = _mapLoader?.CurrentMap;
                if (mapData != null)
                {
                    // Get enemy and ally positions from last scan (if available)
                    var enemyPositions = _navActions?.GetEnemyPositions();
                    var allyPositions = _navActions?.GetAllyPositions();

                    var validTiles = GameBridge.MovementBfs.ComputeValidTiles(
                        mapData, unitGX, unitGY, moveStat, jumpStat, enemyPositions, allyPositions);
                    screen.Tiles = validTiles;
                    ModLogger.Log($"[Tiles] MapBFS (MAP{mapData.MapNumber:D3}): {validTiles.Count} tiles (blocked: {_blockedTiles.Count}, enemies: {enemyPositions?.Count ?? 0}, allies: {allyPositions?.Count ?? 0}). " +
                        $"Unit=({unitGX},{unitGY}), Move={moveStat}, Jump={jumpStat}");
                    // MoveTileCountValidator was wired to 0x142FEA008 but that
                    // byte does not actually encode the game's valid-tile count
                    // (live-verified: byte=11 while 20 blue tiles visible on
                    // Siedge Weald for Lloyd). Disabled until a real count
                    // signal is found. See memory/project_move_bitmap_hunt_s28.md.
                    // LogBfsTileCountMismatch(validTiles.Count, screen);
                    return;
                }

                // === Fallback: memory terrain grid (approximate) ===
                byte[]? terrainData = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var td = Explorer.Scanner.ReadBytes((nint)AddrTerrainGrid, TerrainEntryCount * TerrainEntrySize);
                    if (td.Length >= TerrainEntryCount * TerrainEntrySize)
                    {
                        int unitEntry = unitGY * TerrainGridCols + (unitGX + 1);
                        if (unitEntry >= 0 && unitEntry < TerrainEntryCount)
                        {
                            int mOff = unitEntry * TerrainEntrySize;
                            if (td[mOff + 3] == 0x1F && td[mOff + 4] == 0x1F && td[mOff + 5] == 0x1F)
                            {
                                terrainData = td;
                                break;
                            }
                        }
                    }
                    Thread.Sleep(5);
                }

                if (terrainData != null)
                {
                    var validTiles = ComputeValidTilesBFS(terrainData, unitGX, unitGY, moveStat, jumpStat);
                    screen.Tiles = validTiles;
                    ModLogger.Log($"[Tiles] MemBFS: {validTiles.Count} tiles (blocked cache: {_blockedTiles.Count}). " +
                        $"Unit=({unitGX},{unitGY}), Move={moveStat}, Jump={jumpStat}");
                    // MoveTileCountValidator was wired to 0x142FEA008 but that
                    // byte does not actually encode the game's valid-tile count
                    // (live-verified: byte=11 while 20 blue tiles visible on
                    // Siedge Weald for Lloyd). Disabled until a real count
                    // signal is found. See memory/project_move_bitmap_hunt_s28.md.
                    // LogBfsTileCountMismatch(validTiles.Count, screen);
                    return;
                }

                // === Fallback: original tile path list ===
                var cursorIdx = Explorer.ReadAbsolute((nint)0x140C64E7C, 1);
                int idx = cursorIdx != null ? (int)cursorIdx.Value.value : 0;

                var tileData = Explorer.Scanner.ReadBytes((nint)0x140C66315, 350);
                if (tileData.Length < 7) return;

                var pathTiles = new List<TilePosition>();
                for (int i = 0; i < tileData.Length - 6; i += 7)
                {
                    int x = tileData[i];
                    int y = tileData[i + 1];
                    int flag = tileData[i + 3];

                    if (flag == 0) break;
                    if (x > 30 || y > 30) break;

                    pathTiles.Add(new TilePosition { X = x, Y = y });
                }

                if (idx >= 0 && idx < pathTiles.Count)
                {
                    screen.CursorX = pathTiles[idx].X;
                    screen.CursorY = pathTiles[idx].Y;
                }

                var seen = new HashSet<(int, int)>();
                var uniqueTiles = new List<TilePosition>();
                foreach (var t in pathTiles)
                {
                    if (seen.Add((t.X, t.Y)))
                        uniqueTiles.Add(t);
                }
                screen.Tiles = uniqueTiles;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[CommandBridge] PopulateBattleTileData error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the game's own tileCount byte at 0x142FEA008 (discovered
        /// session 28: toggles between the previous unit's count and the
        /// current active unit's count on Move mode entry/exit) and compares
        /// it against the BFS-computed count. Emits a LOUD WARNING when the
        /// two disagree so we notice when our BFS is wrong before Claude
        /// acts on a bad tile list.
        /// </summary>
        private void LogBfsTileCountMismatch(int bfsCount, DetectedScreen? screen)
        {
            if (Explorer == null) return;
            var read = Explorer.ReadAbsolute((nint)0x142FEA008, 1);
            int? gameCount = read.HasValue ? (int)read.Value.value : (int?)null;
            var warning = GameBridge.MoveTileCountValidator.Compare(bfsCount, gameCount);
            if (warning != null)
            {
                ModLogger.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                ModLogger.Log(warning);
                // Dump rich debug info to help diagnose the mismatch without
                // another round trip: unit position, Move/Jump, active map,
                // and the full BFS tile list with heights.
                try
                {
                    int unitX = screen?.CursorX ?? -1;
                    int unitY = screen?.CursorY ?? -1;
                    var moveRead = Explorer.ReadAbsolute((nint)0x1407AC7E4, 1);
                    var jumpRead = Explorer.ReadAbsolute((nint)0x1407AC7E6, 1);
                    int mv = moveRead.HasValue ? (int)moveRead.Value.value : -1;
                    int jmp = jumpRead.HasValue ? (int)jumpRead.Value.value : -1;
                    string activeAbility = _navActions?.GetActiveAlly()?.MovementAbility ?? "(none)";
                    string activeName = _navActions?.GetActiveAlly()?.Name ?? "(unknown)";
                    int mapNum = _mapLoader?.CurrentMapNumber ?? -1;
                    int loc = _lastWorldMapLocation;
                    ModLogger.Log($"[BFS DEBUG] activeUnit={activeName} pos=({unitX},{unitY}) Move={mv} Jump={jmp} movementAbility={activeAbility}");
                    ModLogger.Log($"[BFS DEBUG] mapNumber=MAP{mapNum:D3} locationId={loc}");
                    if (screen?.Tiles != null)
                    {
                        var tilesStr = string.Join(" ", screen.Tiles.ConvertAll(t => $"({t.X},{t.Y})"));
                        ModLogger.Log($"[BFS DEBUG] bfsTiles ({screen.Tiles.Count}): {tilesStr}");
                    }
                    ModLogger.Log($"[BFS DEBUG] blockedTiles ({_blockedTiles.Count}): {string.Join(" ", _blockedTiles)}");
                }
                catch (Exception ex)
                {
                    ModLogger.Log($"[BFS DEBUG] dump failed: {ex.Message}");
                }
                ModLogger.Log("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                if (screen != null) screen.BfsMismatchWarning = warning;
            }
        }

        /// <summary>
        /// Diagnostic: walks the cursor through each flood-fill candidate tile
        /// from the unit's start position, snapshots memory before + after
        /// each move, and infers "is valid" from whether any render slot
        /// went `04 → 05` in the slot region (cursor-on-valid-tile signal).
        /// Returns a report comparing game-observed tiles vs our BFS result.
        /// Slow (seconds per probe); diagnostic-only, NOT for runtime.
        /// Must be invoked from BattleMoving with cursor on the unit's tile.
        /// </summary>
        public string RunCursorWalkDiagnostic()
        {
            if (Explorer == null) return "Error: memory explorer not initialized";
            IntPtr win = Process.GetCurrentProcess().MainWindowHandle;
            if (win == IntPtr.Zero) return "Error: game window not found";

            const int VK_RIGHT = 0x27, VK_LEFT = 0x25, VK_UP = 0x26, VK_DOWN = 0x28;
            nint SLOT_REGION_MIN = (nint)0x140DDF000L;
            nint SLOT_REGION_MAX = (nint)0x140DE8000L;
            const int KEY_DELAY_MS = 300;
            const int SETTLE_MS = 200;
            const int MAX_KEYS_TOTAL = 400;

            int ReadCursorX()
            {
                var r = Explorer.ReadAbsolute((nint)0x140C64A54, 1);
                return r.HasValue ? (int)r.Value.value : -1;
            }
            int ReadCursorY()
            {
                var r = Explorer.ReadAbsolute((nint)0x140C6496C, 1);
                return r.HasValue ? (int)r.Value.value : -1;
            }

            int startX = ReadCursorX();
            int startY = ReadCursorY();
            if (startX < 0 || startY < 0) return "Error: could not read cursor position";

            // Capture BFS tile list NOW, before we mess with screen state via cursor moves.
            // The cursor-walk sequence causes DetectScreen() at the end to lose the tile list.
            var initialScreen = DetectScreen();
            var bfsTiles = initialScreen?.Tiles?.ConvertAll(t => ((int x, int y))(t.X, t.Y)) ?? new List<(int x, int y)>();

            int keyCount = 0;
            void PressKey(int vk)
            {
                keyCount++;
                _inputSimulator.SendKeyPressToWindow(win, vk);
                Thread.Sleep(KEY_DELAY_MS);
            }

            // Calibrate arrow keys: press each once, read delta, restore.
            (int dx, int dy) CalibrateArrow(int pressVk, int restoreVk)
            {
                int preX = ReadCursorX(), preY = ReadCursorY();
                PressKey(pressVk);
                int postX = ReadCursorX(), postY = ReadCursorY();
                int dx = postX - preX;
                int dy = postY - preY;
                // If cursor didn't move (map edge), return the opposite of restoreVk as a hint — but mostly zero.
                if (dx == 0 && dy == 0)
                {
                    ModLogger.Log($"[cursor_walk] WARNING: key 0x{pressVk:X2} did not move cursor from ({preX},{preY}) — map edge or blocked");
                    return (0, 0);
                }
                PressKey(restoreVk);
                int restX = ReadCursorX(), restY = ReadCursorY();
                if (restX != preX || restY != preY)
                {
                    ModLogger.Log($"[cursor_walk] WARNING: restore didn't return to start: expected ({preX},{preY}) got ({restX},{restY})");
                }
                return (dx, dy);
            }

            // Calibrate all 4 directions by pressing each key once and
            // observing deltas. After each press, read new position. If
            // delta is (0,0), the direction is edge-blocked — no need to
            // restore. If delta is non-zero, we've moved; track cumulative
            // drift and restore all at once at the end by pressing the
            // inverse of each delta.
            (int dx, int dy) ObserveKey(int pressVk)
            {
                int preX = ReadCursorX(), preY = ReadCursorY();
                PressKey(pressVk);
                int postX = ReadCursorX(), postY = ReadCursorY();
                int dx = postX - preX;
                int dy = postY - preY;
                if (dx == 0 && dy == 0)
                {
                    ModLogger.Log($"[cursor_walk] NOTE: key 0x{pressVk:X2} could not move cursor from ({preX},{preY}) — likely map edge; will rely on other directions");
                }
                return (dx, dy);
            }

            // Press each arrow once. Track cumulative cursor position.
            var right = ObserveKey(VK_RIGHT);
            var left = ObserveKey(VK_LEFT);
            var up = ObserveKey(VK_UP);
            var down = ObserveKey(VK_DOWN);
            var cal = new GameBridge.ArrowKeyCalibration(right, left, up, down);
            ModLogger.Log($"[cursor_walk] Calibration: Right={right} Left={left} Up={up} Down={down}");

            // Restore to start using the completed calibration. If any key
            // initially tested as (0,0) (edge-blocked), it may now work from
            // a non-edge position — retry unknown keys.
            void ReCalibrateIfZero()
            {
                if (right.dx == 0 && right.dy == 0) { right = ObserveKey(VK_RIGHT); cal = new GameBridge.ArrowKeyCalibration(right, left, up, down); }
                if (left.dx == 0 && left.dy == 0) { left = ObserveKey(VK_LEFT); cal = new GameBridge.ArrowKeyCalibration(right, left, up, down); }
                if (up.dx == 0 && up.dy == 0) { up = ObserveKey(VK_UP); cal = new GameBridge.ArrowKeyCalibration(right, left, up, down); }
                if (down.dx == 0 && down.dy == 0) { down = ObserveKey(VK_DOWN); cal = new GameBridge.ArrowKeyCalibration(right, left, up, down); }
            }

            int curX0 = ReadCursorX(), curY0 = ReadCursorY();
            if (curX0 != startX || curY0 != startY)
            {
                ModLogger.Log($"[cursor_walk] Post-calibration at ({curX0},{curY0}), navigating back to start ({startX},{startY}).");
                // Retry (0,0) keys now that we're off the edge.
                ReCalibrateIfZero();
                ModLogger.Log($"[cursor_walk] Re-cal: Right={right} Left={left} Up={up} Down={down}");
                curX0 = ReadCursorX(); curY0 = ReadCursorY();
                var recoveryPath = cal.BuildPath(curX0, curY0, startX, startY);
                foreach (var key in recoveryPath)
                {
                    int vk = key switch
                    {
                        GameBridge.ArrowKeyCalibration.Key.Right => VK_RIGHT,
                        GameBridge.ArrowKeyCalibration.Key.Left => VK_LEFT,
                        GameBridge.ArrowKeyCalibration.Key.Up => VK_UP,
                        _ => VK_DOWN,
                    };
                    PressKey(vk);
                }
                int curX1 = ReadCursorX(), curY1 = ReadCursorY();
                if (curX1 != startX || curY1 != startY)
                {
                    return $"Error: calibration drift ({curX1},{curY1}) could not recover to ({startX},{startY}). Working keys: Right={right} Left={left} Up={up} Down={down}. Aborting.";
                }
            }

            // Helper: navigate cursor from current (cx,cy) to (tx,ty) by arrow keys.
            // Verifies landing; if wrong, returns false so caller skips this probe.
            bool NavigateCursor(int tx, int ty)
            {
                int cx = ReadCursorX(), cy = ReadCursorY();
                var path = cal.BuildPath(cx, cy, tx, ty);
                foreach (var key in path)
                {
                    if (keyCount >= MAX_KEYS_TOTAL) return false;
                    int vk = key switch
                    {
                        GameBridge.ArrowKeyCalibration.Key.Right => VK_RIGHT,
                        GameBridge.ArrowKeyCalibration.Key.Left => VK_LEFT,
                        GameBridge.ArrowKeyCalibration.Key.Up => VK_UP,
                        _ => VK_DOWN,
                    };
                    PressKey(vk);
                }
                int fx = ReadCursorX(), fy = ReadCursorY();
                return fx == tx && fy == ty;
            }

            // Probe a tile: navigate, snapshot before nav was not captured here because nav mutates state.
            // Instead: snapshot AT current (pre-probe) location, nav to target, snapshot, diff.
            // Look for any 04→05 transition in slot region → cursor landed on a valid move tile.
            bool IsTileValid(int tx, int ty)
            {
                Explorer.TakeSnapshot("_cw_before");
                Thread.Sleep(SETTLE_MS);
                if (!NavigateCursor(tx, ty))
                {
                    ModLogger.Log($"[cursor_walk] Navigation to ({tx},{ty}) failed (path rejection or key cap)");
                    return false;
                }
                Thread.Sleep(SETTLE_MS);
                Explorer.TakeSnapshot("_cw_after");
                int transitions = Explorer.CountTransitionsInRange(
                    "_cw_before", "_cw_after",
                    SLOT_REGION_MIN, SLOT_REGION_MAX,
                    oldVal: 0x04, newVal: 0x05);
                return transitions > 0;
            }

            // Flood fill from unit's position, using the probe as validity oracle.
            // The start tile is valid by definition.
            var gameTiles = GameBridge.CursorFloodFill.Flood(startX, startY, t =>
            {
                if (t.x == startX && t.y == startY) return true;
                if (keyCount >= MAX_KEYS_TOTAL) return false;
                return IsTileValid(t.x, t.y);
            });

            // Restore cursor to start so we leave state clean
            NavigateCursor(startX, startY);

            var gameTilesList = gameTiles.ToList();
            var result = GameBridge.BfsTileVerifier.Compare(bfsTiles, gameTilesList);
            var report = GameBridge.BfsTileVerifier.FormatReport(result);

            // Extra full listings for debugging
            var gameList = string.Join(" ", gameTilesList.ConvertAll(t => $"({t.x},{t.y})"));
            var bfsList = string.Join(" ", bfsTiles.ConvertAll(t => $"({t.x},{t.y})"));
            ModLogger.Log($"[cursor_walk] keyCount={keyCount} start=({startX},{startY})");
            ModLogger.Log($"[cursor_walk] game valid ({gameTilesList.Count}): {gameList}");
            ModLogger.Log($"[cursor_walk] BFS candidates ({bfsTiles.Count}): {bfsList}");
            ModLogger.Log($"[cursor_walk] {report}");
            return report + $" | keys={keyCount} gameTiles=[{gameList}] bfsTiles=[{bfsList}]";
        }

        /// <summary>
        /// BFS computation of valid movement tiles using terrain height data.
        /// Uses min(b0,b1) as tile height, checks |height_diff| ≤ jump per step.
        /// Excludes tiles in the blocked cache (learned from failed move attempts).
        /// </summary>
        private List<TilePosition> ComputeValidTilesBFS(byte[] terrainData, int unitGX, int unitGY, int moveStat, int jumpStat)
        {
            int GetHeight(int gx, int gy)
            {
                if (gx < -1 || gx > 7 || gy < 0 || gy > 7) return -1;
                int idx = gy * TerrainGridCols + (gx + 1);
                if (idx < 0 || idx >= TerrainEntryCount) return -1;
                int off = idx * TerrainEntrySize;
                int b0 = terrainData[off];
                int b1 = terrainData[off + 1];
                if (terrainData[off + 3] == 0x1F && terrainData[off + 4] == 0x1F && terrainData[off + 5] == 0x1F)
                    return 0;
                return Math.Min(b0, b1);
            }

            var visited = new Dictionary<(int, int), int>();
            var queue = new Queue<(int x, int y, int cost)>();
            queue.Enqueue((unitGX, unitGY, 0));
            visited[(unitGX, unitGY)] = 0;

            int[][] dirs = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1} };

            while (queue.Count > 0)
            {
                var (x, y, cost) = queue.Dequeue();
                if (cost >= moveStat) continue;

                foreach (var d in dirs)
                {
                    int nx = x + d[0], ny = y + d[1];
                    if (nx < 0 || ny < 0 || ny > 7) continue;

                    int nh = GetHeight(nx, ny);
                    if (nh < 0) continue;

                    // Skip tiles known to be blocked (trees, obstacles — learned from failed moves)
                    if (_blockedTiles.Contains((nx, ny))) continue;

                    int ch = GetHeight(x, y);
                    if (Math.Abs(nh - ch) > jumpStat) continue;

                    int newCost = cost + 1;
                    if (!visited.ContainsKey((nx, ny)) || visited[(nx, ny)] > newCost)
                    {
                        visited[(nx, ny)] = newCost;
                        queue.Enqueue((nx, ny, newCost));
                    }
                }
            }

            var allTiles = visited
                .OrderBy(kv => kv.Value)
                .ThenBy(kv => Math.Abs(kv.Key.Item1 - unitGX) + Math.Abs(kv.Key.Item2 - unitGY))
                .Select(kv => new TilePosition { X = kv.Key.Item1, Y = kv.Key.Item2 })
                .ToList();

            return allTiles;
        }

        /// <summary>
        /// BFS using JSON map data for exact terrain heights and passability.
        /// Grid coords = map tile coords (identity mapping).
        /// Height formula: display = height + slope_height / 2.0
        /// Jump check uses display heights. All terrain costs 1 move point.
        /// Enemy-occupied tiles block movement (can't move through or onto them).
        /// </summary>
        private List<TilePosition> ComputeValidTilesFromMap(MapData map, int unitGX, int unitGY, int moveStat, int jumpStat, HashSet<(int, int)>? enemyPositions = null)
        {
            double GetDisplayHeight(int x, int y)
            {
                if (!map.InBounds(x, y)) return -1;
                var t = map.Tiles[x, y];
                return t.Height + t.SlopeHeight / 2.0;
            }

            var visited = new Dictionary<(int, int), int>();
            var queue = new Queue<(int x, int y, int cost)>();

            if (!map.InBounds(unitGX, unitGY)) return new List<TilePosition>();

            queue.Enqueue((unitGX, unitGY, 0));
            visited[(unitGX, unitGY)] = 0;

            int[][] dirs = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1} };

            while (queue.Count > 0)
            {
                var (x, y, cost) = queue.Dequeue();
                if (cost >= moveStat) continue;

                double ch = GetDisplayHeight(x, y);

                foreach (var d in dirs)
                {
                    int nx = x + d[0], ny = y + d[1];

                    if (!map.InBounds(nx, ny)) continue;
                    if (!map.IsWalkable(nx, ny)) continue;
                    if (_blockedTiles.Contains((nx, ny))) continue;
                    if (enemyPositions != null && enemyPositions.Contains((nx, ny))) continue;

                    double nh = GetDisplayHeight(nx, ny);
                    if (nh < 0 || ch < 0) continue;

                    if (Math.Abs(nh - ch) > jumpStat) continue;

                    int tileCost = map.Tiles[nx, ny].MoveCost;
                    int newCost = cost + tileCost;
                    if (newCost > moveStat) continue;
                    if (!visited.ContainsKey((nx, ny)) || visited[(nx, ny)] > newCost)
                    {
                        visited[(nx, ny)] = newCost;
                        queue.Enqueue((nx, ny, newCost));
                    }
                }
            }

            // Exclude the starting tile (unit's own position)
            visited.Remove((unitGX, unitGY));

            return visited
                .OrderBy(kv => kv.Value)
                .ThenBy(kv => Math.Abs(kv.Key.Item1 - unitGX) + Math.Abs(kv.Key.Item2 - unitGY))
                .Select(kv => new TilePosition { X = kv.Key.Item1, Y = kv.Key.Item2 })
                .ToList();
        }

        private bool IsPartySubScreen()
        {
            if (ScreenMachine == null) return false;
            return ScreenMachine.CurrentScreen is
                GameScreen.PartyMenuUnits or
                GameScreen.CharacterStatus or
                GameScreen.EquipmentScreen or
                GameScreen.EquipmentItemList or
                GameScreen.JobScreen or
                GameScreen.JobActionMenu or
                GameScreen.JobChangeConfirmation or
                GameScreen.SecondaryAbilities or
                GameScreen.ReactionAbilities or
                GameScreen.SupportAbilities or
                GameScreen.MovementAbilities or
                GameScreen.CombatSets or
                GameScreen.CharacterDialog or
                GameScreen.DismissUnit;
        }

        /// <summary>
        /// Build a screen response from the state machine's current state,
        /// without reading game memory. Used for key-press responses where
        /// the SM already processed the key and knows the correct screen.
        /// Avoids stale-memory issues that plague detection during transitions.
        /// </summary>
        /// <summary>
        /// Enrich a screen with viewedUnit, sidebar UI, loadout, abilities, and
        /// cursor label — the same data that DetectScreen populates for unit-scoped
        /// screens. Called by both BuildScreenFromSM (key-press responses) and
        /// could replace the inline code in DetectScreen in a future refactor.
        /// </summary>
        private void EnrichUnitScopedScreen(DetectedScreen screen)
        {
            if (ScreenMachine == null || Explorer == null) return;

            // viewedUnit — resolve from SM's saved party cursor
            bool isUnitScoped =
                screen.Name == "CharacterStatus" ||
                screen.Name == "EquipmentAndAbilities" ||
                screen.Name == "JobSelection" ||
                screen.Name == "JobActionMenu" ||
                screen.Name == "JobChangeConfirmation" ||
                screen.Name == "SecondaryAbilities" ||
                screen.Name == "ReactionAbilities" ||
                screen.Name == "SupportAbilities" ||
                screen.Name == "MovementAbilities" ||
                screen.Name == "EquippableWeapons" ||
                screen.Name == "EquippableShields" ||
                screen.Name == "EquippableHeadware" ||
                screen.Name == "EquippableCombatGarb" ||
                screen.Name == "EquippableAccessories" ||
                screen.Name == "CombatSets" ||
                screen.Name == "CharacterDialog" ||
                screen.Name == "DismissUnit";
            if (!isUnitScoped) return;

            if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
            if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);
            var viewedSlot = _rosterReader.GetSlotByDisplayOrder(ScreenMachine.ViewedGridIndex);
            if (viewedSlot?.Name != null)
                screen.ViewedUnit = viewedSlot.Name;

            // CharacterStatus sidebar label
            if (screen.Name == "CharacterStatus")
            {
                screen.UI = ScreenMachine.SidebarIndex switch
                {
                    0 => "Equipment & Abilities",
                    1 => "Job",
                    2 => "Combat Sets",
                    _ => null
                };
            }

            // JobActionMenu cursor label
            if (screen.Name == "JobActionMenu")
            {
                screen.UI = ScreenMachine.JobActionIndex == 0 ? "Learn Abilities" : "Change Job";
            }

            // DismissUnit cursor label
            if (screen.Name == "DismissUnit")
            {
                screen.UI = ScreenMachine.DismissConfirmSelected ? "Confirm" : "Back";
            }

            // EquipmentAndAbilities: loadout + abilities + cursor label
            if (screen.Name == "EquipmentAndAbilities" && viewedSlot != null)
            {
                int slotIdx = viewedSlot.SlotIndex;
                var lo = _rosterReader.ReadLoadout(slotIdx);
                var ab = _rosterReader.ReadEquippedAbilities(slotIdx, viewedSlot.JobName);

                if (lo != null)
                {
                    screen.Loadout = new Loadout
                    {
                        UnitName = viewedSlot.Name,
                        Weapon = lo.WeaponName,
                        LeftHand = lo.LeftHandName,
                        Shield = lo.ShieldName,
                        Helm = lo.HelmName,
                        Body = lo.BodyName,
                        Accessory = lo.AccessoryName,
                    };

                    // Cursor label from equipment column
                    int row = ScreenMachine.CursorRow;
                    int col = ScreenMachine.CursorCol;
                    if (col == 0)
                    {
                        screen.UI = row switch
                        {
                            0 => lo.WeaponName,
                            1 => lo.ShieldName,
                            2 => lo.HelmName,
                            3 => lo.BodyName,
                            4 => lo.AccessoryName,
                            _ => null
                        };
                    }
                }
                if (ab != null)
                {
                    screen.Abilities = new AbilityLoadoutPayload
                    {
                        Primary = ab.Primary,
                        Secondary = ab.Secondary,
                        Reaction = ab.Reaction,
                        Support = ab.Support,
                        Movement = ab.Movement,
                    };

                    // Cursor label from ability column
                    int row = ScreenMachine.CursorRow;
                    int col = ScreenMachine.CursorCol;
                    if (col == 1)
                    {
                        screen.UI = row switch
                        {
                            0 => ab.Primary,
                            1 => ab.Secondary,
                            2 => ab.Reaction,
                            3 => ab.Support,
                            4 => ab.Movement,
                            _ => null
                        };
                    }
                }
            }
        }

        private DetectedScreen? BuildScreenFromSM()
        {
            if (ScreenMachine == null) return null;

            string name = ScreenMachine.CurrentScreen switch
            {
                GameScreen.WorldMap => "WorldMap",
                GameScreen.TravelList => "TravelList",
                GameScreen.LocationMenu => "LocationMenu",
                GameScreen.CharacterStatus => "CharacterStatus",
                GameScreen.EquipmentScreen => "EquipmentAndAbilities",
                GameScreen.EquipmentItemList => ScreenMachine.CurrentEquipmentSlot switch
                {
                    EquipmentSlot.Weapon => "EquippableWeapons",
                    EquipmentSlot.Shield => "EquippableShields",
                    EquipmentSlot.Headware => "EquippableHeadware",
                    EquipmentSlot.CombatGarb => "EquippableCombatGarb",
                    EquipmentSlot.Accessory => "EquippableAccessories",
                    _ => "EquipmentItemList"
                },
                GameScreen.SecondaryAbilities => "SecondaryAbilities",
                GameScreen.ReactionAbilities => "ReactionAbilities",
                GameScreen.SupportAbilities => "SupportAbilities",
                GameScreen.MovementAbilities => "MovementAbilities",
                GameScreen.CombatSets => "CombatSets",
                GameScreen.CharacterDialog => "CharacterDialog",
                GameScreen.DismissUnit => "DismissUnit",
                GameScreen.JobScreen => "JobSelection",
                GameScreen.JobActionMenu => "JobActionMenu",
                GameScreen.JobChangeConfirmation => "JobChangeConfirmation",
                GameScreen.ChronicleEncyclopedia => "ChronicleEncyclopedia",
                GameScreen.ChronicleStateOfRealm => "ChronicleStateOfRealm",
                GameScreen.ChronicleEvents => "ChronicleEvents",
                GameScreen.ChronicleAuracite => "ChronicleAuracite",
                GameScreen.ChronicleReadingMaterials => "ChronicleReadingMaterials",
                GameScreen.ChronicleCollection => "ChronicleCollection",
                GameScreen.ChronicleErrands => "ChronicleErrands",
                GameScreen.ChronicleStratagems => "ChronicleStratagems",
                GameScreen.ChronicleLessons => "ChronicleLessons",
                GameScreen.ChronicleAkademicReport => "ChronicleAkademicReport",
                GameScreen.OptionsSettings => "OptionsSettings",
                GameScreen.SaveSlotPicker => "SaveSlotPicker",
                GameScreen.Tavern => "Tavern",
                GameScreen.TavernRumors => "TavernRumors",
                GameScreen.TavernErrands => "TavernErrands",
                GameScreen.PartyMenuUnits => ScreenMachine.Tab switch
                {
                    PartyTab.Units => "PartyMenuUnits",
                    PartyTab.Inventory => "PartyMenuInventory",
                    PartyTab.Chronicle => "PartyMenuChronicle",
                    PartyTab.Options => "PartyMenuOptions",
                    _ => "PartyMenuUnits"
                },
                _ => "Unknown"
            };

            var screen = new DetectedScreen { Name = name };

            // Enrich unit-scoped screens with viewedUnit + loadout + abilities
            // so execute_action responses show the same data as `screen`.
            EnrichUnitScopedScreen(screen);

            ModLogger.Log($"[BuildScreenFromSM] {name} (SM={ScreenMachine.CurrentScreen}, Tab={ScreenMachine.Tab})");
            return screen;
        }

        private DetectedScreen? DetectScreen()
        {
            if (Explorer == null) return null;

            try
            {
                var v = Explorer.ReadMultiple(ScreenAddresses);

                var screen = new DetectedScreen
                {
                    Location = (int)v[2],
                    Hover = (int)v[3],
                    MenuCursor = (int)v[4],
                    UiPresent = (int)v[1],
                    BattleTeam = (int)v[7],
                    BattleActed = (int)v[8],
                    BattleMoved = (int)v[9],
                    BattleUnitId = (int)v[10],
                    BattleUnitHp = (int)v[11],
                    CameraRotation = (int)(v[17] - 1 + 4) % 4,
                    MenuDepth = (int)v[27],
                    // UI is left null here. The action-menu cursor mapping
                    // (Move/Abilities/Wait/Status/AutoBattle) only makes
                    // sense in battle — outside battle the cursor sits at
                    // index 0 by default and was leaking "Move" to every
                    // screen (WorldMap, PartyMenu, LocationMenu, ...). The
                    // BattleMyTurn / BattleActing block below sets UI
                    // from MenuCursor; non-battle screens get their UI
                    // populated by their own per-screen logic (shop labels,
                    // viewed-unit, hovered item, etc.) or stay null.
                    UI = null,
                };

                // Save raw location before overriding — needed for title screen detection
                int rawLocation = screen.Location;
                int hover = screen.Hover;

                // During battle (location=255) the raw byte doesn't tell us
                // WHICH battleground we're at. Try the live battle map-id
                // byte first — it's authoritative for random encounters and
                // updates when the current map changes (unlike
                // _lastWorldMapLocation which sticks on whatever world-map
                // location was last visited). Live-repro 2026-04-24: user
                // traveled through Lenalian Plateau (locId=29) to Siedge
                // Weald (locId=26), hit a random encounter there, but
                // curLoc= kept rendering "Lenalian Plateau" because the
                // world-map latch stuck.
                if (screen.Location == 255)
                {
                    int resolvedLocId = -1;
                    if (Explorer != null)
                    {
                        var mapRead = Explorer.ReadAbsolute(
                            (nint)GameBridge.LiveBattleMapId.Address, 1);
                        if (mapRead.HasValue
                            && GameBridge.LiveBattleMapId.IsValid((int)mapRead.Value.value))
                        {
                            resolvedLocId = GameBridge.BattleMapIdToLocation.TryResolve(
                                (int)mapRead.Value.value);
                        }
                    }
                    if (resolvedLocId >= 0)
                    {
                        screen.Location = resolvedLocId;
                    }
                    else
                    {
                        if (_lastWorldMapLocation < 0)
                            _lastWorldMapLocation = LoadLastLocation();
                        if (_lastWorldMapLocation >= 0)
                            screen.Location = _lastWorldMapLocation;
                    }
                }
                screen.LocationName = GetLocationName(screen.Location);

                int storyObj = (int)v[20];
                if (storyObj > 0 && storyObj < 255)
                {
                    screen.StoryObjective = storyObj;
                    screen.StoryObjectiveName = GetLocationName(storyObj);
                }

                int party = (int)v[0];
                int ui = (int)v[1];
                int paused = (int)v[14];
                int moveMode = (int)v[15]; // 255=tile selection active, 0=not
                int eA = (int)v[5];
                int eB = (int)v[6];
                long slot0 = v[12];
                long slot9 = v[13];
                int battleMode = (int)v[16];

                // Battle detection: use RAW location (not overridden by last_location.txt).
                // Title screen has rawLocation=255, battleMode=255, slot0=0xFFFFFFFF.
                // World map has rawLocation 0-42, battleMode=0.
                int submenuFlag = (int)v[18]; // 1=submenu/mode active (Abilities submenu, Move mode, etc.), 0=top-level menu
                int gameOverFlag = submenuFlag; // same address — game over uses submenuFlag=1 + paused=1 + battleMode=0
                int eventId = (int)v[19];
                bool inBattle = (slot0 == 255 && slot9 == 0xFFFFFFFF)
                    || (slot9 == 0xFFFFFFFF && (battleMode == 2 || battleMode == 3 || battleMode == 4));

                int locationMenuFlag = (int)v[21];
                int shopTypeIndex = (int)v[22];
                int insideShopFlag = (int)v[23];
                int shopSubMenuIndex = (int)v[24];
                int encounterFlag = (int)v[28];

                // PartyMenu tab flags — cross-session stable, override stale party byte
                int unitsTabFlag = 0, inventoryTabFlag = 0;
                if (Explorer != null)
                {
                    var utf = Explorer.ReadAbsolute((nint)0x140D3A41E, 1);
                    var itf = Explorer.ReadAbsolute((nint)0x140D3A38E, 1);
                    if (utf.HasValue) unitsTabFlag = (int)utf.Value.value;
                    if (itf.HasValue) inventoryTabFlag = (int)itf.Value.value;
                }
                // Only trust the SM's party-sub-screen signal when
                // menuDepth confirms we're in a nested panel. At depth 0
                // we're on an outer screen (WorldMap / PartyMenu / CS) —
                // passing isPartySubScreen=true at depth 0 causes the
                // detection to return "PartySubScreen" for states that
                // should be WorldMap, leading to SM-drift EqA misreports.
                bool partySubScreenSignal = !inBattle && IsPartySubScreen()
                    && screen.MenuDepth > 0;

                // BattleSequence discriminator — read the minimap-open flag.
                // BattleSequence discriminator (session 48 2026-04-19):
                // 0x1407774B4 is a u32 that reads 2 while the minimap panel is
                // OPEN and 1 on plain WorldMap (at a BattleSequence location).
                // Replaces the older 0x14077D1F8 probe, which got baked into the
                // save state and read 1 even after the panel was closed — see
                // memory/project_battle_sequence_flag_sticky.md. Live-verified
                // at Orbonne this session: open=2, close=1, reopen=2, close=1.
                // We set the flag only when the byte == 2 so the plain-WorldMap
                // case returns to the default detection.
                int battleSequenceFlag = 0;
                if (Explorer != null)
                {
                    var bsRead = Explorer.ReadAbsolute((nint)0x1407774B4, 4);
                    if (bsRead.HasValue && bsRead.Value.value == 2) battleSequenceFlag = 1;
                }

                // BattleChoice discriminator — check if the current event's
                // .mes script contains the 0xFB choice marker. Session 44
                // empirical finding: event016 (Mandalia Plain choice) is the
                // only observed event with 0xFB. Pre-scanned into the
                // EventScript.HasChoice bool at mod init.
                bool eventHasChoice = false;
                if (ScriptLookup != null && eventId >= 1 && eventId < 400)
                {
                    var script = ScriptLookup.GetScript(eventId);
                    if (script != null) eventHasChoice = script.HasChoice;
                }

                // Runtime "choice modal is drawn" flag. Session 44 4-pass
                // narrow-down at Mandalia Plain event 016:
                //   1. Diff narration → choice-visible: 2751 candidates went 0→1
                //   2. Filter by "still=1 on choice screen": 992
                //   3. Filter by "now=0 after choice resolved": 384
                //   4. Filter by "=0 on a different (non-choice) cutscene
                //      at same location": 367
                //   5. Re-enter choice modal, filter by "=1 again": 174
                //   6. After picking a choice, filter by "=0 again": 14
                // 0x140CBC38D is the first of those 14 winners — all are in
                // the 0x140CBxxxx / 0x140CCxxxx range (clustered widget state).
                // Reads 1 while choice modal is visible, 0 otherwise.
                // Paired with eventHasChoice to avoid over-firing on the
                // narration prefix of a choice event.
                // Session 48 2026-04-19: swapped to 0x140D3706D after the old
                // 0x140CBC38D stopped firing on Ch2 Mandalia choice modal
                // (read 0 while the modal was live and BattleChoice should
                // have triggered). Full-module snapshot+diff of modal-off →
                // modal-on surfaced a cluster of 0→1 flips in 0x140D370xx;
                // 0x140D3706D is the cleanest. Re-verified live: modal-off=0,
                // modal-on=1. Old byte doesn't appear in the diff at all.
                int choiceModalFlag = 0;
                if (eventHasChoice && Explorer != null)
                {
                    var cmRead = Explorer.ReadAbsolute((nint)0x140D3706D, 1);
                    if (cmRead.HasValue) choiceModalFlag = (int)cmRead.Value.value;
                }

                screen.Name = GameBridge.ScreenDetectionLogic.Detect(
                    party, ui, rawLocation, slot0, slot9,
                    battleMode, moveMode, paused, gameOverFlag,
                    screen.BattleTeam, screen.BattleActed, screen.BattleMoved,
                    eA, eB, partySubScreenSignal, eventId,
                    submenuFlag: submenuFlag, menuCursor: screen.MenuCursor,
                    hover: hover, locationMenuFlag: locationMenuFlag,
                    insideShopFlag: insideShopFlag,
                    shopSubMenuIndex: shopSubMenuIndex,
                    shopTypeIndex: shopTypeIndex,
                    unitsTabFlag: unitsTabFlag, inventoryTabFlag: inventoryTabFlag,
                    encounterFlag: encounterFlag,
                    menuDepth: screen.MenuDepth,
                    battleSequenceFlag: battleSequenceFlag,
                    eventHasChoice: eventHasChoice,
                    choiceModalFlag: choiceModalFlag);

                // BattleMoving → BattleWaiting: after battle_wait sends Enter on
                // the Wait menu item, both battleMode and menuCursor can lag for
                // hundreds of ms before reflecting the facing screen. A screen
                // poll landing in that window mislabels the facing state as a
                // tile-select (both use battleMode=2 in their fresh reads).
                long sinceWaitEnter = GameBridge.NavigationActions.LastWaitEnterTickMs >= 0
                    ? Environment.TickCount64 - GameBridge.NavigationActions.LastWaitEnterTickMs
                    : -1;
                if (GameBridge.StaleBattleMovingClassifier.ShouldOverrideToBattleWaiting(
                        screen.Name, battleMode, sinceWaitEnter))
                {
                    ModLogger.Log($"[StateOverride] BattleMoving→BattleWaiting: {sinceWaitEnter}ms since Wait Enter.");
                    screen.Name = "BattleWaiting";
                }

                // WorldMap-as-battle-residue: during certain enemy-turn
                // animations battleMode flickers to 0 while slot9 stays at
                // the battle sentinel, and the post-battle-stale rule in
                // ScreenDetectionLogic converts the frame to WorldMap.
                // Live-repro 2026-04-24: battle_wait returned [WorldMap]
                // mid-battle at t=10370ms during Lenalian Plateau random
                // encounter; next poll 2s later returned [BattleEnemiesTurn]
                // correctly, confirming a transient flicker rather than a
                // real transition. Suppress by reverting to the cached
                // last-battle-state name when it was recent enough.
                long sinceLastBattleState = _lastBattleStateTickMs >= 0
                    ? Environment.TickCount64 - _lastBattleStateTickMs
                    : -1;
                if (GameBridge.WorldMapBattleResidueClassifier.ShouldSuppress(
                        screen.Name, sinceLastBattleState))
                {
                    var restored = _lastBattleStateName ?? "Battle";
                    ModLogger.Log($"[StateOverride] WorldMap→{restored} (battle residue): {sinceLastBattleState}ms since last battle state.");
                    screen.Name = restored;
                }

                // Update battle-state tracker whenever the final label is a
                // Battle* state. Done AFTER override so the cached name
                // never captures a post-battle WorldMap frame.
                if (screen.Name != null && screen.Name.StartsWith("Battle"))
                {
                    _lastBattleStateTickMs = Environment.TickCount64;
                    _lastBattleStateName = screen.Name;
                }

                // TravelList→WorldMap override: when the SM just left
                // PartyMenu via a key press (Escape) and detection says
                // TravelList, the `ui` byte is stale at 1 (takes >500ms to
                // clear). We KNOW we're on WorldMap because we just exited
                // PartyMenu — TravelList is unreachable via Escape from PM.
                if (screen.Name == "TravelList" && ScreenMachine != null
                    && ScreenMachine.LastSetScreenFromKey
                    && ScreenMachine.CurrentScreen == GameScreen.WorldMap)
                {
                    ModLogger.Log($"[StateOverride] TravelList→WorldMap: SM just left PartyMenuUnits via key, ui byte is stale.");
                    screen.Name = "WorldMap";
                }

                // Fresh-entry menuCursor reset. On detected transitions
                // INTO BattleMyTurn from a turn-boundary state (enemy turn,
                // pause, formation, dialogue), write 0 to 0x1407FC620 so
                // the cursor byte reflects the game's own turn-start
                // reset. Without this, stale values from the prior battle
                // state persist and produce wrong ui= labels. See
                // PROPOSAL_menucursor_drift.md for the full design.
                //
                // Submenu escapes (BattleMoving, BattleAbilities, etc.)
                // are NOT fresh — the game preserves cursor when returning
                // from a submenu, and we respect that.
                if (GameBridge.FreshBattleMyTurnEntryClassifier.IsFresh(
                        _prevDetectedScreenName, screen.Name)
                    && Explorer != null)
                {
                    Explorer.Scanner.WriteByte((nint)0x1407FC620, 0);
                    // Also correct the current response's cursor — the
                    // MenuCursor byte was read before this write, so the
                    // stale value is in screen.MenuCursor. Force it to 0
                    // so the ui= mapping below renders "Move" correctly.
                    screen.MenuCursor = 0;
                    // Turn-consumed flags also reset on turn boundary —
                    // defensive clear on top of the battle_wait reset.
                    _movedThisTurn = false;
                    _actedThisTurn = false;
                    ModLogger.Log($"[MenuCursorReset] fresh entry {_prevDetectedScreenName ?? "null"} → BattleMyTurn: wrote 0x1407FC620=0");
                }
                // Track detected-screen name for the next Detect() call's
                // fresh-entry classifier. Updated even when screen.Name
                // is non-BattleMyTurn so the next transition INTO
                // BattleMyTurn has a correct prev.
                _prevDetectedScreenName = screen.Name;

                // LocationMenu UI label: shopTypeIndex at 0x140D435F0 names
                // which shop the cursor is hovering in the settlement's
                // shop list. On shop INTERIOR screens (Outfitter/Tavern/etc)
                // the ui field would show the sub-action cursor (Buy/Sell/
                // Fitting/Rumors/Errands) — deferred pending a memory scan
                // for the sub-action hover index (shopSubMenuIndex only
                // gets a distinct value AFTER Select). See TODO §10.
                if (screen.Name == "LocationMenu")
                    screen.UI = GameBridge.ShopTypeLabels.ForIndex(shopTypeIndex);

                // Gil: show on shop-adjacent and purchase-decision screens only.
                if (GameBridge.ShopGilPolicy.ShouldShowGil(screen.Name))
                    screen.Gil = v[25];

                // Shop list cursor row (0-based): only meaningful inside an Outfitter
                // sub-action. -1 elsewhere so JSON omits the field.
                if (screen.Name == "OutfitterBuy" || screen.Name == "OutfitterSell" || screen.Name == "OutfitterFitting")
                    screen.ShopListCursorIndex = (int)v[26];

                // Session 55: auto-populate screen.stockItems on
                // OutfitterBuy so Claude gets the full shop catalog
                // without a separate shop_stock call. Covers every
                // registered category for this (location, chapter)
                // in canonical tab order. Chapter defaults to 1
                // until the chapter byte hunt lands.
                if (screen.Name == "OutfitterBuy" && Explorer != null)
                {
                    try
                    {
                        int shopLocation = (int)v[2];
                        int shopChapter = 1; // TODO: replace with chapter byte read when cracked
                        var decoder = new GameBridge.ShopStockDecoder(Explorer);
                        var stock = GameBridge.ShopStockResolver.DecodeAll(decoder, shopLocation, shopChapter);
                        if (stock.Count > 0)
                            screen.StockItems = stock;
                    }
                    catch (Exception ex)
                    {
                        // Silently skip on error — the separate
                        // shop_stock bridge action is the diagnostic
                        // path. We don't want a decoder failure to
                        // break the screen response.
                        ModLogger.Log($"[StockItems] resolver threw: {ex.Message}");
                    }
                }

                if (screen.Name == "Cutscene")
                {
                    screen.EventId = eventId;
                    // Cutscenes advance on Enter (Escape is a no-op per
                    // flavor-dialog convention). Surface ui=Advance so the
                    // one-liner always carries a hovered-action label.
                    screen.UI = "Advance";
                }

                // Populate EventId + CurrentDialogueLine on any dialogue-like
                // screen so the user can pace through the scene seeing only
                // the current box. Tracker is a serial counter bumped on
                // advance_dialogue / raw Enter; reset on eventId change.
                if (screen.Name == "Cutscene" || screen.Name == "BattleDialogue" || screen.Name == "BattleChoice")
                {
                    screen.EventId = eventId;
                    if (ScriptLookup != null && eventId >= 1 && eventId < 400)
                    {
                        var script = ScriptLookup.GetScript(eventId);
                        if (script != null && script.Boxes.Count > 0)
                        {
                            int idx = _dialogueTracker.GetBoxIndex(eventId);
                            if (idx < script.Boxes.Count)
                            {
                                var box = script.Boxes[idx];
                                screen.CurrentDialogueLine = new DialogueBoxPayload
                                {
                                    Speaker = box.Speaker,
                                    Text = box.Text,
                                    BoxIndex = idx,
                                    BoxCount = script.Boxes.Count,
                                };
                            }
                        }
                    }
                }

                // CharacterDialog (flavor-text popup, e.g. Kenrick intro).
                // Only Enter advances. No cursor, no choice — but callers
                // expect ui= on every screen; "Advance" makes the single
                // action explicit.
                if (screen.Name == "CharacterDialog")
                    screen.UI = "Advance";

                // CombatSets (third CharacterStatus sidebar page —
                // placeholder screen, no inner cursor). Mirror the sidebar
                // label so ui= always reflects what the player is looking at.
                if (screen.Name == "CombatSets")
                    screen.UI = "Combat Sets";

                // Map the action menu cursor index to a label.
                // Menu always has 5 items: Move/ResetMove(0) Abilities(1) Wait(2) Status(3) AutoBattle(4).
                // After moving, index 0 is "Reset Move" instead of "Move".
                //
                // The cursor byte at 0x1407FC620 has a 1-frame stale-read race
                // after auto-advance: post-move the game UI shows "Abilities"
                // but memory still reads 0; post-cast (no move) the game UI
                // shows "Move" but memory still reads 1. EffectiveMenuCursor
                // detects the race via moved/acted flags and corrects.
                if (screen.Name == "BattleMyTurn" || screen.Name == "BattleActing")
                {
                    // Override raw battleActed/battleMoved bytes with the
                    // bridge's commit-time flags so callers see a consistent
                    // acted/moved signal across both the response fields and
                    // the UI tag computed below. The raw bytes stale-read 0
                    // transiently after a confirmed action; flags don't.
                    var (overrideActed, overrideMoved) = GameBridge.BattleActedMovedOverride.Apply(
                        rawActed: screen.BattleActed,
                        rawMoved: screen.BattleMoved,
                        actedFlag: _actedThisTurn,
                        movedFlag: _movedThisTurn);
                    if (overrideActed != screen.BattleActed || overrideMoved != screen.BattleMoved)
                    {
                        ModLogger.LogDebug($"[ActedMovedOverride] raw=({screen.BattleActed},{screen.BattleMoved}) flags=({_actedThisTurn},{_movedThisTurn}) → ({overrideActed},{overrideMoved})");
                        screen.BattleActed = overrideActed;
                        screen.BattleMoved = overrideMoved;
                    }

                    bool hasMoved = screen.BattleMoved == 1 || _movedThisTurn;
                    bool hasActed = screen.BattleActed == 1 || _actedThisTurn;
                    int effective = GameBridge.BattleAbilityNavigation.EffectiveMenuCursor(
                        memoryCursor: screen.MenuCursor,
                        moved: hasMoved,
                        acted: hasActed);
                    if (screen.MenuCursor != _lastLoggedCursor)
                    {
                        ModLogger.Log($"[UI] cursor={screen.MenuCursor} effective={effective} screen={screen.Name} moved={hasMoved} acted={hasActed} movedThisTurn={_movedThisTurn}");
                        _lastLoggedCursor = screen.MenuCursor;
                    }
                    screen.UI = effective switch
                    {
                        0 => hasMoved ? "Reset Move" : "Move",
                        1 => "Abilities",
                        2 => "Wait",
                        3 => "Status",
                        4 => "AutoBattle",
                        _ => null
                    };

                    // Surface per-slot menu availability so callers can skip
                    // grayed entries (Abilities after acting) without a
                    // wasted Enter. Only populated when the action menu is
                    // actually user-visible (BattleMyTurn — BattleActing
                    // shows the same memory state but represents a mid-turn
                    // animation where navigation shouldn't fire anyway).
                    if (screen.Name == "BattleMyTurn")
                    {
                        screen.MenuAvailability = GameBridge.BattleMenuAvailability
                            .For(moved: hasMoved ? 1 : 0, acted: hasActed ? 1 : 0)
                            .ToList();
                    }
                }

                // During targeting mode, show the ability being cast/used.
                // _lastAbilityName is set only when entered via battle_ability/battle_attack
                // helpers. For manual navigation (Select from BattleAbilities), fall back
                // to the BattleMenuTracker's selected ability/item.
                if (screen.Name == "BattleAttacking" || screen.Name == "BattleCasting")
                {
                    // ResolveOrCursor returns the ability/item if known, else
                    // falls back to "(x,y)" cursor coords — ensures the
                    // targeting screen always surfaces a ui= label the way
                    // BattleMoving does, even for menu-driven manual nav.
                    screen.UI = GameBridge.TargetingLabelResolver.ResolveOrCursor(
                        _lastAbilityName,
                        _battleMenuTracker.SelectedAbility,
                        _battleMenuTracker.SelectedItem,
                        screen.CursorX,
                        screen.CursorY);
                }

                // Battle menu tracker: set UI from tracker if in submenu
                // (entry/exit managed in SyncBattleMenuTracker, called after screen settles)
                if (screen.Name == "BattleAbilities" && _battleMenuTracker.InSubmenu)
                    screen.UI = _battleMenuTracker.CurrentItem;

                // BattleStatus shows the active unit's CharacterStatus panel
                // in-battle. menuCursor is stale (still reads 3 from the
                // action menu). Use the cached active-unit name instead.
                if (screen.Name == "BattleStatus")
                    screen.UI = GameBridge.BattleStatusUiResolver.Resolve(_cachedActiveUnitName);

                // Resolve party sub-screen to specific screen via state machine.
                // The state machine's CurrentScreen + Tab + SidebarIndex are driven by
                // key history (entry/exit tracked in OnKeyPressed). When detection
                // returns PartySubScreen / PartyMenu, OR when it falls through to
                // TravelList/WorldMap while the state machine knows we're inside the
                // party menu (e.g. Chronicle/Options tabs flip the `ui` byte to 1,
                // which the party=0&&ui=1 rule misreads as TravelList), consult the
                // state machine to resolve the actual nested screen name. See
                // TODO §10.6.
                bool stateMachineInPartyMenu = ScreenMachine != null &&
                    ScreenMachine.CurrentScreen is
                        GameScreen.PartyMenuUnits or
                        GameScreen.CharacterStatus or
                        GameScreen.EquipmentScreen or
                        GameScreen.EquipmentItemList or
                        GameScreen.JobScreen or
                        GameScreen.JobActionMenu or
                        GameScreen.JobChangeConfirmation or
                        GameScreen.SecondaryAbilities or
                        GameScreen.ReactionAbilities or
                        GameScreen.SupportAbilities or
                        GameScreen.MovementAbilities or
                        GameScreen.CombatSets or
                        GameScreen.CharacterDialog or
                        GameScreen.DismissUnit or
                        GameScreen.ChronicleEncyclopedia or
                        GameScreen.ChronicleStateOfRealm or
                        GameScreen.ChronicleEvents or
                        GameScreen.ChronicleAuracite or
                        GameScreen.ChronicleReadingMaterials or
                        GameScreen.ChronicleCollection or
                        GameScreen.ChronicleErrands or
                        GameScreen.ChronicleStratagems or
                        GameScreen.ChronicleLessons or
                        GameScreen.ChronicleAkademicReport or
                        GameScreen.OptionsSettings;

                // Drift recovery: if raw detection says root PartyMenu but the state
                // machine is in a nested PartyMenu screen (CharacterStatus, pickers,
                // etc.) AND has observed zero key events since its last SetScreen,
                // the state machine is stale (typically after a mod restart while
                // the player is already in the menu). Snap it back to PartyMenu so
                // the override below doesn't rewrite the screen name to a stale
                // nested label. See TODO §0.
                if (screen.Name == "PartyMenuUnits" && ScreenMachine != null
                    && ScreenMachine.KeysSinceLastSetScreen == 0
                    && !ScreenMachine.LastSetScreenFromKey
                    && ScreenMachine.CurrentScreen != GameScreen.PartyMenuUnits
                    && ScreenMachine.CurrentScreen is
                        GameScreen.CharacterStatus or
                        GameScreen.EquipmentScreen or
                        GameScreen.EquipmentItemList or
                        GameScreen.JobScreen or
                        GameScreen.JobActionMenu or
                        GameScreen.JobChangeConfirmation or
                        GameScreen.SecondaryAbilities or
                        GameScreen.ReactionAbilities or
                        GameScreen.SupportAbilities or
                        GameScreen.MovementAbilities or
                        GameScreen.CombatSets or
                        GameScreen.CharacterDialog or
                        GameScreen.DismissUnit or
                        GameScreen.ChronicleEncyclopedia or
                        GameScreen.ChronicleStateOfRealm or
                        GameScreen.ChronicleEvents or
                        GameScreen.ChronicleAuracite or
                        GameScreen.ChronicleReadingMaterials or
                        GameScreen.ChronicleCollection or
                        GameScreen.ChronicleErrands or
                        GameScreen.ChronicleStratagems or
                        GameScreen.ChronicleLessons or
                        GameScreen.ChronicleAkademicReport or
                        GameScreen.OptionsSettings)
                {
                    ScreenMachine.SetScreen(GameScreen.PartyMenuUnits);
                }

                // [Note 2026-04-15 session 16: tried to add a "symmetric"
                // drift fix here that snapped the state machine to WorldMap
                // whenever raw said TravelList/WorldMap and SM said
                // party-tree. **Reverted in same session** — the raw
                // detection rule `party=0 && ui=1 → TravelList` ALSO matches
                // EquipmentAndAbilities (the inner panel sets ui=1 too), so
                // the symmetric fix kicked in on every legitimate nested
                // panel visit and stomped the state machine back to WorldMap.
                // Live-test on Ramza's EquipmentAndAbilities reproduced this
                // immediately: state reported [TravelList] while game was
                // clearly on EqA. The existing override below (line ~3481)
                // is the right pattern for the original "stuck on PartyMenu
                // after WorldMap return" bug — it just needs more reliable
                // signals than `party=0 && ui=1`. See TODO §0 entry on the
                // sticky bug for the unfinished investigation.]

                // Menu-depth drift check (discovered 2026-04-14 session 13):
                // 0x14077CB67 is a party-menu-tree depth flag: 0 on outer
                // screens (WorldMap / PartyMenu / CharacterStatus) and 2
                // on inner panels (EquipmentAndAbilities / ability picker).
                // Verified stable across repeated reads and across every
                // oscillation we tested (CS↔EqA↔picker).
                //
                // If the state machine thinks we're on an inner panel but
                // depth reads 0, we're actually on CharacterStatus and the
                // state machine has drifted — snap it back. This catches
                // the common desync where a helper sent Enter from CS
                // expecting EqA, but the game didn't transition for some
                // reason, and now every subsequent key is being routed by
                // the state machine as if we're on EqA.
                //
                // Mirror: if state machine thinks we're on an outer screen
                // (PartyMenu / CharacterStatus) but depth reads 2, we're
                // actually deeper. We don't know exactly where (EqA vs
                // which picker), so the safe move is NOT to auto-snap —
                // just log for future debugging. Upward desyncs are rare
                // because they require an Enter that we didn't track.
                // Debounce with a streak counter: require 3 consecutive
                // DetectScreen calls showing the mismatch before snapping.
                // This rides out the render-lag window right after opening
                // a panel (MenuDepth takes ~50-200ms to flip from 0 to 2
                // after the Enter key is processed).
                if (ScreenMachine != null)
                {
                    bool smOnInner = ScreenMachine.CurrentScreen is
                        GameScreen.EquipmentScreen or
                        GameScreen.EquipmentItemList or
                        GameScreen.SecondaryAbilities or
                        GameScreen.ReactionAbilities or
                        GameScreen.SupportAbilities or
                        GameScreen.MovementAbilities;
                    if (smOnInner && screen.MenuDepth == 0)
                    {
                        _menuDepthDriftStreak++;
                        if (_menuDepthDriftStreak >= 3)
                        {
                            ModLogger.Log($"[StateMachine] menu-depth drift detected: SM={ScreenMachine.CurrentScreen} but menuDepth=0 for 3 consecutive reads. Snapping back to CharacterStatus.");
                            ScreenMachine.SetScreen(GameScreen.CharacterStatus);
                            // Prevent cascade: the existing drift-recovery
                            // below snaps CharacterStatus→PartyMenu when
                            // KeysSinceLastSetScreen==0 (the marker of
                            // "state machine is stale from restart"). Our
                            // recovery just intentionally set CS, so bump
                            // the counter so the stale-check doesn't fire.
                            ScreenMachine.MarkKeyProcessed();
                            _menuDepthDriftStreak = 0;
                        }
                    }
                    else
                    {
                        _menuDepthDriftStreak = 0;
                    }

                    // Upward drift fix (TODO §0 "stuck on PartyMenu after
                    // returning to WorldMap"): if the state machine is in
                    // ANY party-tree screen but raw says WorldMap AND
                    // MenuDepth==0 for 3 consecutive reads, we know the
                    // game is actually back on the world map. The MenuDepth
                    // gate is what makes this safe — the prior symmetric
                    // attempt (reverted session 16) used `party=0 && ui=1`
                    // which also matched EquipmentAndAbilities. MenuDepth==0
                    // is true on outer screens only, so a TravelList/WorldMap
                    // raw + MenuDepth==0 + SM-in-party-tree combination is
                    // a real drift signal, not a nested-panel false positive.
                    // PartyMenu's Inventory/Chronicle/Options tabs are not
                    // distinct GameScreen enum values — they share the
                    // PartyMenu screen name with a Tab discriminator. So
                    // GameScreen.PartyMenuUnits covers all four tabs here.
                    bool smInPartyTree = ScreenMachine.CurrentScreen is
                        GameScreen.PartyMenuUnits or
                        GameScreen.CharacterStatus or
                        GameScreen.EquipmentScreen or
                        GameScreen.EquipmentItemList or
                        GameScreen.JobScreen or
                        GameScreen.JobActionMenu or
                        GameScreen.JobChangeConfirmation or
                        GameScreen.SecondaryAbilities or
                        GameScreen.ReactionAbilities or
                        GameScreen.SupportAbilities or
                        GameScreen.MovementAbilities or
                        GameScreen.CombatSets or
                        GameScreen.CharacterDialog or
                        GameScreen.DismissUnit or
                        GameScreen.ChronicleEncyclopedia or
                        GameScreen.ChronicleStateOfRealm or
                        GameScreen.ChronicleEvents or
                        GameScreen.ChronicleAuracite or
                        GameScreen.ChronicleReadingMaterials or
                        GameScreen.ChronicleCollection or
                        GameScreen.ChronicleErrands or
                        GameScreen.ChronicleStratagems or
                        GameScreen.ChronicleLessons or
                        GameScreen.ChronicleAkademicReport or
                        GameScreen.OptionsSettings;
                    bool rawSaysWorldMap = screen.Name == "WorldMap" || screen.Name == "TravelList";
                    // PartyMenu Chronicle/Options tabs have menuDepth=0 but
                    // are legitimately party-tree states with no memory byte
                    // to distinguish them from WorldMap. SM is the only
                    // source of truth for these tabs — do NOT trigger drift
                    // recovery, or the SM gets stuck oscillating.
                    bool smOnNonUnitsPartyTab = ScreenMachine.CurrentScreen == GameScreen.PartyMenuUnits
                        && ScreenMachine.Tab != PartyTab.Units;
                    // JobSelection is also party-tree, menuDepth=0, and
                    // indistinguishable from WorldMap in memory. Trust SM.
                    bool smOnJobSelection = ScreenMachine.CurrentScreen == GameScreen.JobScreen;
                    if (smInPartyTree && rawSaysWorldMap && screen.MenuDepth == 0
                        && !smOnNonUnitsPartyTab && !smOnJobSelection)
                    {
                        _worldMapDriftStreak++;
                        if (_worldMapDriftStreak >= 3)
                        {
                            ModLogger.Log($"[StateMachine] world-map drift detected: SM={ScreenMachine.CurrentScreen} but raw={screen.Name} + menuDepth=0 for 3 consecutive reads. Snapping back to WorldMap.");
                            ScreenMachine.SetScreen(GameScreen.WorldMap);
                            ScreenMachine.MarkKeyProcessed();
                            _worldMapDriftStreak = 0;
                        }
                    }
                    else
                    {
                        _worldMapDriftStreak = 0;
                    }
                }

                // SM override for party-tree screens.
                // When SM is in the party tree and detection says WorldMap/
                // TravelList, trust the SM. JobSelection and CharacterStatus
                // both read menuDepth=0 but are legitimately party-tree
                // screens; detection can't disambiguate them from WorldMap
                // when ui=1 is stale. The existing _worldMapDriftStreak
                // (3 consecutive reads) catches the stale-SM case without
                // needing a menuDepth gate here.
                bool smOverrideAllowed = screen.Name == "PartySubScreen"
                    || screen.Name == "PartyMenuUnits"
                    || (stateMachineInPartyMenu
                        && (screen.Name == "TravelList" || screen.Name == "WorldMap"));
                if (smOverrideAllowed)
                {
                    if (ScreenMachine != null)
                    {
                        // Memory-backed Units-tab drift check. Discovered
                        // 2026-04-15 by the flag-hunt agents: the byte at
                        // 0x140D3A41E reliably reads 1 on PartyMenu Units
                        // tab and 0 on ALL other PartyMenu tabs (verified
                        // across multiple restart sessions and nav paths —
                        // see tmp/flag_hunt_shared.md). This is the only
                        // byte that survived cross-session stability
                        // testing; 0x140900824 and 0x14090075C looked
                        // promising in one session but drifted in another
                        // (observed 824=6 in a state B had recorded as
                        // 824=9, while the screenshot confirmed the same
                        // game tab). The other three bytes are dropped
                        // from this check — keep only the reliable one.
                        //
                        // We use this as a ONE-DIRECTION drift detector:
                        // if SM thinks Tab==Units but memory says 41E==0,
                        // SM is wrong and we move to Inventory as the
                        // next-most-likely tab (Inventory is the most
                        // common drift destination). Conversely, if SM
                        // thinks Tab != Units but memory says 41E==1, we
                        // snap back to Units. No multi-tab guessing.
                        //
                        // Only override when ScreenMachine says we're on
                        // outer PartyMenu (not nested CharacterStatus /
                        // picker / etc.) — those don't touch 41E.
                        // Reuse the tab flag values read earlier (line ~3779)
                        // to avoid a TOCTOU race where the game clears the flag
                        // between the detection read and this correction read.
                        if (ScreenMachine.CurrentScreen == GameScreen.PartyMenuUnits)
                        {
                            {
                                int v41e = unitsTabFlag;
                                int v38e = inventoryTabFlag;
                                if (v41e == 1 && ScreenMachine.Tab != PartyTab.Units)
                                {
                                    ModLogger.Log($"[FlagCombo] Units-tab drift: SM={ScreenMachine.Tab} but 41E=1. Snapping to Units.");
                                    ScreenMachine.SetTabFromMemory(PartyTab.Units);
                                }
                                else if (v41e == 0 && v38e == 1 && ScreenMachine.Tab != PartyTab.Inventory)
                                {
                                    ModLogger.Log($"[FlagCombo] Inventory-tab drift: SM={ScreenMachine.Tab} but 38E=1, 41E=0. Snapping to Inventory.");
                                    ScreenMachine.SetTabFromMemory(PartyTab.Inventory);
                                }
                                // When both flags are 0, we can't distinguish
                                // Chronicle/Options from a transient flag-clear
                                // during screen transitions. Don't guess —
                                // leave the SM's current tab alone. The flags
                                // settle within one render cycle.
                            }
                        }

                        screen.Name = ScreenMachine.CurrentScreen switch
                        {
                            GameScreen.CharacterStatus => "CharacterStatus",
                            // GameScreen.EquipmentScreen enum name is legacy (pre-rename).
                            // Surface as "EquipmentAndAbilities" to match the two-column
                            // center panel reality (equipment + abilities). Enum rename
                            // deferred to the dedicated rename session — see TODO §10.5.
                            GameScreen.EquipmentScreen => "EquipmentAndAbilities",
                            // EquipmentItemList is a generic picker — resolve to the
                            // slot-specific Equippable<Type> name using CurrentEquipmentSlot
                            // captured at Enter time.
                            GameScreen.EquipmentItemList => ScreenMachine.CurrentEquipmentSlot switch
                            {
                                EquipmentSlot.Weapon => "EquippableWeapons",
                                EquipmentSlot.Shield => "EquippableShields",
                                EquipmentSlot.Headware => "EquippableHeadware",
                                EquipmentSlot.CombatGarb => "EquippableCombatGarb",
                                EquipmentSlot.Accessory => "EquippableAccessories",
                                _ => "EquipmentItemList"
                            },
                            GameScreen.SecondaryAbilities => "SecondaryAbilities",
                            GameScreen.ReactionAbilities => "ReactionAbilities",
                            GameScreen.SupportAbilities => "SupportAbilities",
                            GameScreen.MovementAbilities => "MovementAbilities",
                            GameScreen.CombatSets => "CombatSets",
                            GameScreen.CharacterDialog => "CharacterDialog",
                            GameScreen.DismissUnit => "DismissUnit",
                            GameScreen.JobScreen => "JobSelection",
                            GameScreen.JobActionMenu => "JobActionMenu",
                            GameScreen.JobChangeConfirmation => "JobChangeConfirmation",
                            GameScreen.ChronicleEncyclopedia => "ChronicleEncyclopedia",
                            GameScreen.ChronicleStateOfRealm => "ChronicleStateOfRealm",
                            GameScreen.ChronicleEvents => "ChronicleEvents",
                            GameScreen.ChronicleAuracite => "ChronicleAuracite",
                            GameScreen.ChronicleReadingMaterials => "ChronicleReadingMaterials",
                            GameScreen.ChronicleCollection => "ChronicleCollection",
                            GameScreen.ChronicleErrands => "ChronicleErrands",
                            GameScreen.ChronicleStratagems => "ChronicleStratagems",
                            GameScreen.ChronicleLessons => "ChronicleLessons",
                            GameScreen.ChronicleAkademicReport => "ChronicleAkademicReport",
                            GameScreen.OptionsSettings => "OptionsSettings",
                            GameScreen.SaveSlotPicker => "SaveSlotPicker",
                            GameScreen.Tavern => "Tavern",
                            GameScreen.TavernRumors => "TavernRumors",
                            GameScreen.TavernErrands => "TavernErrands",
                            // On PartyMenu itself, the tab determines the screen name.
                            GameScreen.PartyMenuUnits => ScreenMachine.Tab switch
                            {
                                PartyTab.Units => "PartyMenuUnits",
                                PartyTab.Inventory => "PartyMenuInventory",
                                PartyTab.Chronicle => "PartyMenuChronicle",
                                PartyTab.Options => "PartyMenuOptions",
                                _ => "PartyMenuUnits"
                            },
                            _ => "PartyMenuUnits"
                        };
                    }
                }

                // Stale-SM recovery is handled by _worldMapDriftStreak
                // further below (3 consecutive reads of raw=WorldMap +
                // MenuDepth=0 + SM-in-party-tree). We don't need a separate
                // SM-Snap block here.

                // Clear the default "Move/Abilities/Wait/..." UI label on screens
                // where the battle menuCursor byte is meaningless. That byte is
                // populated from a battle-only address and defaults to 0 → "Move"
                // outside of battle, producing misleading labels like
                // `[WorldMap] ui=Move` or `[TravelList] ui=Move`. Screens below
                // that DO have a valid ui= set their own label later in this method.
                if (screen.Name == "WorldMap" ||
                    screen.Name == "TitleScreen")
                {
                    screen.UI = null;
                }
                // WorldMap: surface the hovered location name as ui=.
                // The cursor lands on your current location on entry, but
                // can be moved to any revealed node. hover=255 means the
                // cursor isn't over a named node yet (between locations).
                if (screen.Name == "WorldMap" && screen.Hover >= 0 && screen.Hover < 255)
                {
                    var hoveredName = GetLocationName(screen.Hover);
                    if (!string.IsNullOrEmpty(hoveredName))
                        screen.UI = hoveredName;
                }
                if (screen.Name == "EncounterDialog")
                {
                    // Cursor defaults to Fight on encounter dialogs.
                    screen.UI = "Fight";
                }
                if (screen.Name == "TravelList")
                {
                    // Hover byte (0x140787A22) goes to 254 when TravelList
                    // opens — the world-map cursor is inactive. No known
                    // memory address for the list's highlighted row yet.
                    // Blocked on a memory scan for the TravelList cursor.
                    screen.UI = null;
                }

                // Populate unlockedLocations on WorldMap / TravelList from
                // the per-location unlock array at 0x1411A10B0 (1 byte each,
                // 0x01 unlocked / 0x00 locked). Lets Claude plan routes in
                // one round-trip instead of probing with world_travel_to
                // calls. Known location IDs are 0..42 per NavigationActions
                // TravelTabs — we read 0..52 inclusive to cover any
                // late-game locations with a modest safety margin.
                if ((screen.Name == "WorldMap" || screen.Name == "TravelList")
                    && Explorer != null)
                {
                    var unlocked = new List<int>();
                    for (int loc = 0; loc <= 52; loc++)
                    {
                        var r = Explorer.ReadAbsolute((nint)(0x1411A10B0 + loc), 1);
                        if (r.HasValue && r.Value.value != 0)
                            unlocked.Add(loc);
                    }
                    if (unlocked.Count > 0)
                        screen.UnlockedLocations = unlocked.ToArray();
                }

                // Populate inventory on PartyMenuInventory from the static
                // u8 array at 0x1411A17C0 (272 bytes = one byte per FFTPatcher
                // item ID, count 0 means not owned). Found 2026-04-15 session 18
                // via 2-snapshot buy-diff — see project_inventory_store_CRACKED.md.
                // We emit every non-zero entry with ItemData-resolved name/type
                // so Claude has a complete owned-items listing in one read.
                //
                // We gate on the state-machine's Tab byte rather than the
                // detected screen name because raw detection can't tell
                // PartyMenu tabs apart (party=1 matches all of them), and
                // the state machine's tab tracking is currently the most
                // reliable signal. Matching any of the 4 PartyMenu screen
                // names keeps the payload populated even when the state
                // machine thinks we're on a different tab than the game
                // actually shows (drift scenario documented in TODO §0).
                // Inventory surface: PartyMenu tabs get the full list (via
                // ReadAll); OutfitterSell gets only sellable entries (those
                // with a known sell price); OutfitterFitting gets the full
                // list so Claude can see what's available to equip on the
                // chosen unit/slot (filtering by slot type is a follow-up
                // once we track the Fitting picker depth). All three share
                // the same DTO shape so fft.sh can render them with one
                // code path.
                bool onPartyMenuAnyTab = FFTColorCustomizer.GameBridge.ScreenNamePredicates.IsPartyMenuTab(screen.Name);
                bool onOutfitterSell = screen.Name == "OutfitterSell";
                bool onOutfitterFitting = screen.Name == "OutfitterFitting";
                if ((onPartyMenuAnyTab || onOutfitterSell || onOutfitterFitting) && Explorer != null)
                {
                    var invReader = new GameBridge.InventoryReader(Explorer);
                    var entries = onOutfitterSell
                        ? invReader.ReadSellable()
                        : invReader.ReadAll();
                    if (entries.Count > 0)
                    {
                        var payload = new List<InventoryItem>(entries.Count);
                        foreach (var e in entries)
                        {
                            payload.Add(new InventoryItem
                            {
                                Id = e.ItemId,
                                Count = e.Count,
                                Name = e.Name,
                                Type = e.Type,
                                SellPrice = e.SellPrice,
                                SellPriceVerified = e.SellPriceVerified,
                            });
                        }
                        screen.Inventory = payload;
                    }
                }

                // Populate screen.viewedUnit for unit-scoped screens. Resolves
                // via the state machine's saved PartyMenu cursor (preserved
                // across the Enter that opened CharacterStatus) → roster
                // slot whose DisplayOrder (+0x122) matches that grid index.
                // All nested PartyMenu panels share the same viewed unit
                // until Escape takes us back out.
                bool isUnitScopedScreen =
                    screen.Name == "CharacterStatus" ||
                    screen.Name == "EquipmentAndAbilities" ||
                    screen.Name == "JobSelection" ||
                    screen.Name == "JobActionMenu" ||
                    screen.Name == "JobChangeConfirmation" ||
                    screen.Name == "SecondaryAbilities" ||
                    screen.Name == "ReactionAbilities" ||
                    screen.Name == "SupportAbilities" ||
                    screen.Name == "MovementAbilities" ||
                    screen.Name == "EquippableWeapons" ||
                    screen.Name == "EquippableShields" ||
                    screen.Name == "EquippableHeadware" ||
                    screen.Name == "EquippableCombatGarb" ||
                    screen.Name == "EquippableAccessories" ||
                    screen.Name == "CombatSets" ||
                    screen.Name == "CharacterDialog" ||
                    screen.Name == "DismissUnit";
                if (isUnitScopedScreen && ScreenMachine != null && Explorer != null)
                {
                    if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                    if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);
                    var viewedSlot = _rosterReader.GetSlotByDisplayOrder(ScreenMachine.ViewedGridIndex);
                    if (viewedSlot?.Name != null)
                        screen.ViewedUnit = viewedSlot.Name;

                    // "Next: N" header value — cheapest unlearned action ability in
                    // the viewed unit's current primary-job skillset. Only render on
                    // CharacterStatus / EquipmentAndAbilities where the game itself
                    // surfaces this number.
                    if (viewedSlot != null &&
                        (screen.Name == "CharacterStatus" || screen.Name == "EquipmentAndAbilities"))
                    {
                        var primary = viewedSlot.JobName != null
                            ? GetPrimarySkillsetByJobName(viewedSlot.JobName)
                            : null;
                        if (primary != null)
                            screen.NextJp = _rosterReader.ComputeNextJp(viewedSlot.SlotIndex, primary);
                    }
                }

                // CharacterStatus sidebar label: populate screen.UI from the state
                // machine's SidebarIndex (0=Equipment & Abilities, 1=Job, 2=Combat Sets).
                // This replaces the need for a memory scan — sidebar is purely
                // keyboard-driven and the state machine tracks Up/Down reliably.
                if (screen.Name == "CharacterStatus" && ScreenMachine != null)
                {
                    screen.UI = ScreenMachine.SidebarIndex switch
                    {
                        0 => "Equipment & Abilities",
                        1 => "Job",
                        2 => "Combat Sets",
                        _ => null
                    };
                    screen.StatsExpanded = ScreenMachine.StatsExpanded;

                    // Verbose-only: aggregate equipment-derived stats for
                    // the viewed unit. Wiki-independent — computed from
                    // ItemData constants + roster equipment IDs, so IC vs
                    // PSX multiplier differences don't affect correctness.
                    // Caller uses this to answer "does this gear swap help?"
                    // without needing the full HP/MP formula path.
                    if (Explorer != null)
                    {
                        if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                        if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);
                        var vSlot = _rosterReader.GetSlotByDisplayOrder(ScreenMachine.ViewedGridIndex);
                        var vLoadout = vSlot != null ? _rosterReader.ReadLoadout(vSlot.SlotIndex) : null;
                        if (vLoadout != null)
                        {
                            ItemInfo? GetItem(int? id) => id.HasValue ? ItemData.GetItem(id.Value) : null;
                            screen.DetailedStats = UnitStatsAggregator.Aggregate(
                                helm: GetItem(vLoadout.HelmId),
                                body: GetItem(vLoadout.BodyId),
                                accessory: GetItem(vLoadout.AccessoryId),
                                weapon: GetItem(vLoadout.WeaponId),
                                leftHand: GetItem(vLoadout.LeftHandId),
                                shield: GetItem(vLoadout.ShieldId));
                        }
                    }
                }

                // EquipmentAndAbilities Effects view toggle.
                if (screen.Name == "EquipmentAndAbilities" && ScreenMachine != null)
                {
                    screen.EquipmentEffectsView = ScreenMachine.EquipmentEffectsView;
                }

                // (Per-unit equipment is surfaced via the roster grid block
                // above — no separate "viewed unit" loadout needed now that
                // every unit's data is attached to its grid entry.)

                // DismissUnit cursor label: Back (default/safe) vs Confirm.
                if (screen.Name == "DismissUnit" && ScreenMachine != null)
                {
                    screen.UI = ScreenMachine.DismissConfirmSelected ? "Confirm" : "Back";
                }

                // EquipmentAndAbilities: surface the highlighted slot label as ui=
                // and populate screen.loadout + screen.abilities for the viewed unit.
                //
                // Cursor-aware label resolution:
                //   CursorCol=0 (Equipment column) → equipment item name at row R
                //   CursorCol=1 (Ability column)   → ability slot's CURRENT value at row R
                //     row 0 = Primary skillset (job-locked)
                //     row 1 = Secondary skillset
                //     row 2 = Reaction ability
                //     row 3 = Support ability
                //     row 4 = Movement ability
                //
                // Viewed unit is resolved from the state machine's saved
                // grid position (captured when Enter opened CharacterStatus
                // from PartyMenu) → the roster slot whose DisplayOrder matches.
                // Display order is byte roster+0x122, set by the game's Sort
                // option (default: Time Recruited).
                // Clear the EqA row auto-resolve latch whenever we're NOT on
                // EqA. Next EqA entry re-runs the mirror-diff resolver so the
                // ScreenMachine's column-0 cursor matches whatever row the
                // game actually opened onto.
                if (screen.Name != "EquipmentAndAbilities" && _eqaRowAutoResolveAttempted)
                {
                    _eqaRowAutoResolveAttempted = false;
                    _lastEqaMenuDepth = 0;
                }
                if (screen.Name == "EquipmentAndAbilities" && ScreenMachine != null && Explorer != null)
                {
                    if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                    if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);

                    // Auto-fire the mirror-diff EqA row resolver ONCE per
                    // EqA entry. This fixes the stale `ui=Right Hand (none)`
                    // that persists when the state machine's CursorRow
                    // drifted before entering EqA. The resolver toggles
                    // equip/unequip on the hovered slot, diffs the mirror to
                    // identify which row transitioned, then restores. Cost
                    // ~2s, 3-4 key presses. Gated on MenuDepth == 2 so the
                    // fire doesn't race the EqA open animation (matches the
                    // picker + JobSelection resolver gates).
                    //
                    // After this runs, ScreenMachine.SetEquipmentCursor
                    // locks in the true row; Up/Down/Left/Right tracking
                    // keeps it fresh until we leave EqA (latch clears above).
                    if (!_eqaRowAutoResolveAttempted && screen.MenuDepth == 2)
                    {
                        _eqaRowAutoResolveAttempted = true;
                        var info = DoEqaRowResolve(restore: true);
                        if (info.HasValue)
                            ModLogger.Log($"[CommandBridge] auto EqA row: {info.Value.row} ({info.Value.direction})");
                    }
                    // Re-fire on drift: if MenuDepth JUST transitioned from
                    // deeper-than-2 (inside a picker / CharacterStatus side
                    // column) back to 2 (EqA main), the SM's cursor may have
                    // drifted during that round trip. Re-run the resolver to
                    // re-pin it. Costs one extra ~2s pass per picker close but
                    // prevents stale `ui=Right Hand (none)` after equipment
                    // edits. See session 24 TODO carryover.
                    else if (_eqaRowAutoResolveAttempted
                        && screen.MenuDepth == 2
                        && _lastEqaMenuDepth > 2)
                    {
                        var info = DoEqaRowResolve(restore: true);
                        if (info.HasValue)
                            ModLogger.Log($"[CommandBridge] re-fire EqA row (picker-exit): {info.Value.row} ({info.Value.direction})");
                    }
                    _lastEqaMenuDepth = screen.MenuDepth;

                    int viewedGridIndex = ScreenMachine.ViewedGridIndex;
                    var viewed = _rosterReader.GetSlotByDisplayOrder(viewedGridIndex);
                    if (viewed != null)
                    {
                        int viewedSlot = viewed.SlotIndex;
                        var lo = _rosterReader.ReadLoadout(viewedSlot);
                        var ab = _rosterReader.ReadEquippedAbilities(viewedSlot, viewed.JobName);

                        if (lo != null)
                        {
                            screen.Loadout = new Loadout
                            {
                                UnitName = viewed.Name,
                                Weapon = lo.WeaponName,
                                LeftHand = lo.LeftHandName,
                                Shield = lo.ShieldName,
                                Helm = lo.HelmName,
                                Body = lo.BodyName,
                                Accessory = lo.AccessoryName,
                            };
                        }
                        if (ab != null)
                        {
                            var payload = new AbilityLoadoutPayload
                            {
                                Primary = ab.Primary,
                                Secondary = ab.Secondary,
                                Reaction = ab.Reaction,
                                Support = ab.Support,
                                Movement = ab.Movement,
                            };

                            // Also surface the viewed unit's full learned
                            // lists so fft.sh helpers (list_reaction_abilities
                            // etc.) and Claude planning don't have to open
                            // each picker just to read them. Matches the
                            // picker-side logic (same dicts, same
                            // classification) — see the picker branch below
                            // in this file for the equivalent foreach.
                            var unlocked = _rosterReader.ReadUnlockedSkillsets(viewedSlot);
                            if (unlocked.Count > 0)
                                payload.LearnedSecondary = unlocked;

                            var passives = _rosterReader.ReadLearnedPassives(viewedSlot);
                            // Classify into three buckets keyed by ID so we
                            // can re-order by the game's canonical picker
                            // order (ReactionPickerOrder etc.) rather than
                            // the incidental learn-bitfield order, matching
                            // what the player sees in the in-game picker.
                            var reactionsById = new HashSet<byte>();
                            var supportsById = new HashSet<byte>();
                            var movementsById = new HashSet<byte>();
                            foreach (var id in passives)
                            {
                                if (AbilityData.ReactionAbilities.ContainsKey(id)) reactionsById.Add(id);
                                else if (AbilityData.SupportAbilities.ContainsKey(id)) supportsById.Add(id);
                                else if (AbilityData.MovementAbilities.ContainsKey(id)) movementsById.Add(id);
                            }
                            var reactions = OrderByPicker(reactionsById, AbilityData.ReactionPickerOrder, AbilityData.ReactionAbilities);
                            var supports = OrderByPicker(supportsById, AbilityData.SupportPickerOrder, AbilityData.SupportAbilities);
                            var movements = OrderByPicker(movementsById, AbilityData.MovementPickerOrder, AbilityData.MovementAbilities);
                            if (reactions.Count > 0) payload.LearnedReaction = reactions;
                            if (supports.Count > 0) payload.LearnedSupport = supports;
                            if (movements.Count > 0) payload.LearnedMovement = movements;

                            screen.Abilities = payload;
                        }

                        // Resolve the cursor's current item/ability name for the ui= label.
                        int row = ScreenMachine.CursorRow;
                        int col = ScreenMachine.CursorCol;
                        string? cursorLabel = null;
                        if (col == 0 && lo != null)
                        {
                            // Equipment column. Slot order matches the visible left
                            // column on EquipmentAndAbilities (Weapon/Shield/Helm/Body/Accessory).
                            cursorLabel = row switch
                            {
                                0 => lo.WeaponName,
                                1 => lo.ShieldName,
                                2 => lo.HelmName,
                                3 => lo.BodyName,
                                4 => lo.AccessoryName,
                                _ => null
                            };
                        }
                        else if (col == 1 && ab != null)
                        {
                            cursorLabel = row switch
                            {
                                0 => ab.Primary,
                                1 => ab.Secondary,
                                2 => ab.Reaction,
                                3 => ab.Support,
                                4 => ab.Movement,
                                _ => null
                            };
                        }
                        // Fallback labels for empty slots — slot-aware so the
                        // ui= line tells Claude WHICH slot is empty, not a
                        // bare "(none)" that erases all context. Equipment
                        // slots use "(none)" (single weapon/shield/helm
                        // unequipped); ability slots use "(empty)" to match
                        // the game's own copy. Primary action is job-locked,
                        // so a blank there means our skillset table is
                        // incomplete — flag explicitly. See TODO §0
                        // 2026-04-14 entry.
                        if (cursorLabel != null) screen.UI = cursorLabel;
                        else if (col == 0)
                        {
                            screen.UI = row switch
                            {
                                0 => "Right Hand (none)",
                                1 => "Left Hand (none)",
                                2 => "Headware (none)",
                                3 => "Combat Garb (none)",
                                4 => "Accessory (none)",
                                _ => "(none)"
                            };
                        }
                        else if (col == 1)
                        {
                            screen.UI = row switch
                            {
                                0 => "Primary (none — skillset table missing for this job)",
                                1 => "Secondary (empty)",
                                2 => "Reaction (empty)",
                                3 => "Support (empty)",
                                4 => "Movement (empty)",
                                _ => "(empty)"
                            };
                        }

                        // Detail panel — mirrors game's right-side info panel.
                        if (cursorLabel != null)
                            screen.UiDetail = BuildUiDetail(cursorLabel, col, row);

                        // Expose cursor position so the shell renderer can mark
                        // the selected row (`cursor -->` prefix on the hovered
                        // label). EquipmentAndAbilities uses a 2×5 grid where
                        // col 0 = equipment, col 1 = abilities.
                        screen.CursorRow = row;
                        screen.CursorCol = col;
                    }
                }

                // Picker screens (SecondaryAbilities/ReactionAbilities/SupportAbilities/
                // MovementAbilities) — surface availableAbilities for the viewed unit.
                //
                // Source of truth per slot type:
                //   Secondary: roster bitfield at +0x32 + jobIdx*3 across all 20 jobs.
                //              Skillsets with ANY learned action ability bit set are
                //              listed (proxy for "this unit has unlocked this job").
                //   Reaction/Support/Movement: still unsolved — byte 2 of the bitfield
                //              "fuzzy" per project_roster_learned_abilities.md. We
                //              currently only surface the CURRENTLY-EQUIPPED ability
                //              (from roster +0x08-+0x0D), not the full learned list.
                //
                // Viewed unit resolved the same way as EquipmentAndAbilities —
                // state machine's ViewedGridIndex → DisplayOrder lookup.
                bool isPicker = screen.Name == "SecondaryAbilities" ||
                                screen.Name == "ReactionAbilities" ||
                                screen.Name == "SupportAbilities" ||
                                screen.Name == "MovementAbilities";
                if (!isPicker && _resolvedPickerCursorAddr != 0)
                {
                    // Left the picker — drop the cached heap address. Next picker
                    // open re-resolves since heap addresses can point at stale
                    // memory after a screen transition.
                    _resolvedPickerCursorAddr = 0L;
                }
                if (isPicker && Explorer != null && ScreenMachine != null)
                {
                    // Clear the default "Move/Abilities/Wait" UI label that
                    // DetectScreen sets from the battle menuCursor address —
                    // that label is meaningless on picker screens. Picker
                    // row-cursor tracking isn't in memory yet (TODO §10.7);
                    // until it lands, omitting the label is better than
                    // showing a stale wrong one.
                    screen.UI = null;

                    if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                    if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);

                    int viewedGridIndex = ScreenMachine.ViewedGridIndex;
                    var viewed = _rosterReader.GetSlotByDisplayOrder(viewedGridIndex);
                    if (viewed != null)
                    {
                        int viewedSlot = viewed.SlotIndex;
                        var ab = _rosterReader.ReadEquippedAbilities(viewedSlot, viewed.JobName);
                        var list = new List<AvailableAbility>();

                        if (screen.Name == "SecondaryAbilities")
                        {
                            // The game's picker shows the currently-equipped
                            // skillset FIRST (matching the cursor's default
                            // position), then the rest of the unlocked skillsets
                            // in canonical (job-index) order. No (None) entry —
                            // to unequip, press Enter on the equipped one.
                            var unlocked = _rosterReader.ReadUnlockedSkillsets(viewedSlot);
                            string? equippedSecondary = ab?.Secondary;
                            // Each skillset maps to a single owning job
                            // (Martial Arts→Monk etc.). Use that as the Job
                            // label on the picker entry so Claude can see
                            // where each skillset came from.
                            if (equippedSecondary != null && unlocked.Contains(equippedSecondary))
                            {
                                list.Add(new AvailableAbility
                                {
                                    Name = equippedSecondary,
                                    IsEquipped = true,
                                    Job = SkillsetOwnerJob(equippedSecondary),
                                });
                            }
                            foreach (var s in unlocked)
                            {
                                if (s == equippedSecondary) continue; // already first
                                list.Add(new AvailableAbility
                                {
                                    Name = s,
                                    Job = SkillsetOwnerJob(s),
                                });
                            }
                        }
                        else if (screen.Name == "ReactionAbilities" ||
                                 screen.Name == "SupportAbilities" ||
                                 screen.Name == "MovementAbilities")
                        {
                            // Decode all learned passives from roster byte 2 of
                            // each per-job bitfield, then re-order by the
                            // game's canonical picker order so availableAbilities
                            // indices match what the player sees in-game (the
                            // fft.sh change_*_ability_to helpers compute
                            // Up/Down deltas off these indices). Reaction
                            // order captured live 2026-04-14 session 13;
                            // support/movement orders pending (fall back to
                            // ID-sort inside OrderByPicker).
                            var learned = _rosterReader.ReadLearnedPassives(viewedSlot);
                            string? equippedName = screen.Name switch
                            {
                                "ReactionAbilities" => ab?.Reaction,
                                "SupportAbilities" => ab?.Support,
                                "MovementAbilities" => ab?.Movement,
                                _ => null,
                            };

                            // Classify learned into the picker's bucket, then
                            // apply OrderByPicker to get names in game order.
                            var learnedForPicker = new HashSet<byte>();
                            var dict = screen.Name switch
                            {
                                "ReactionAbilities" => AbilityData.ReactionAbilities,
                                "SupportAbilities"  => AbilityData.SupportAbilities,
                                "MovementAbilities" => AbilityData.MovementAbilities,
                                _ => null,
                            };
                            var orderArr = screen.Name switch
                            {
                                "ReactionAbilities" => AbilityData.ReactionPickerOrder,
                                "SupportAbilities"  => AbilityData.SupportPickerOrder,
                                "MovementAbilities" => AbilityData.MovementPickerOrder,
                                _ => null,
                            };
                            if (dict != null)
                            {
                                foreach (var id in learned)
                                    if (dict.ContainsKey(id))
                                        learnedForPicker.Add(id);
                                foreach (var name in OrderByPicker(learnedForPicker, orderArr, dict))
                                {
                                    // Find the canonical AbilityInfo for this
                                    // name so Job + Description ride along
                                    // with each picker entry.
                                    var info = dict.Values.FirstOrDefault(a => a.Name == name);
                                    // Description in AbilityData can embed a
                                    // "Usage condition:" tail; strip it for
                                    // the compact picker row (full description
                                    // is still in BuildUiDetail for the
                                    // hovered entry).
                                    var (descMain, _) = SplitUsageCondition(info?.Description);
                                    list.Add(new AvailableAbility
                                    {
                                        Name = name,
                                        IsEquipped = name == equippedName,
                                        Job = info?.Job,
                                        Description = descMain,
                                    });
                                }
                            }
                        }

                        if (list.Count > 0) screen.AvailableAbilities = list;

                        // If we've resolved the picker cursor byte this session
                        // (via `resolve_picker_cursor` action — heap addresses
                        // shuffle so we rescan on entry), surface the currently
                        // highlighted ability as ui=<name>. Falls back to null
                        // when address is 0 (unresolved) or read fails.
                        // Auto-resolve the picker cursor on first picker-open this
                        // session. Heap addresses shuffle across game launches so we
                        // rescan each time. Costs ~1s (Down+Up+Down+Up+Up+Up = 6 keys
                        // with 300ms each while snapshotting). Subsequent reads are
                        // free (single byte) until screen transitions out.
                        if (_resolvedPickerCursorAddr == 0 && list.Count > 0 && Explorer != null)
                        {
                            int _unused;
                            ResolvePickerCursor(out _unused);
                        }

                        // Surface the currently highlighted ability as ui=<name>.
                        // Falls back to null when resolver found no candidate or read fails.
                        if (_resolvedPickerCursorAddr != 0 && list.Count > 0 && Explorer != null)
                        {
                            var curByte = Explorer.ReadAbsolute((nint)_resolvedPickerCursorAddr, 1);
                            if (curByte.HasValue)
                            {
                                int idx = (int)curByte.Value.value;
                                if (idx >= 0 && idx < list.Count)
                                    screen.UI = list[idx].Name;
                            }
                        }

                        // Detail panel for the picker — fall back to the
                        // equipped ability's detail until picker-cursor row
                        // tracking lands (TODO §10.7). That matches what
                        // Claude should default to (the game opens the picker
                        // with the cursor on the equipped row).
                        int detailRow = screen.Name switch
                        {
                            "SecondaryAbilities" => 1,
                            "ReactionAbilities" => 2,
                            "SupportAbilities" => 3,
                            "MovementAbilities" => 4,
                            _ => -1,
                        };
                        string? detailName = screen.Name switch
                        {
                            "SecondaryAbilities" => ab?.Secondary,
                            "ReactionAbilities" => ab?.Reaction,
                            "SupportAbilities" => ab?.Support,
                            "MovementAbilities" => ab?.Movement,
                            _ => null,
                        };
                        if (detailName != null && detailRow >= 0)
                            screen.UiDetail = BuildUiDetail(detailName, col: 1, row: detailRow);
                    }
                }

                // EquipmentAndAbilities outer panel — viewed-unit equipment
                // mirror ground truth. Session 19 fix for the drift class
                // that blocks every `change_*_to` equipment helper AND the
                // "state machine says TravelList but game is on EqA" class.
                //
                // Discovery (session 19 live hunt): the game keeps 3
                // synchronized mirror copies of the viewed unit's equipped
                // items in main-module memory, in EXACT EqA UI row order:
                //   0x141870854, 0x14373B004, 0x143743704
                // Each mirror is a 5-element u16 array:
                //   +0: Right Hand (Weapon)
                //   +2: Left Hand
                //   +4: Helm
                //   +6: Body
                //   +8: Accessory
                // Values are FFTPatcher item IDs (0 = empty).
                //
                // Mirror 2 (0x14373B004) turned out to be a STALE cache,
                // not a live copy. Mirrors 1 and 3 agree and update
                // together on unit switch / equip / unequip. Cross-session
                // verified 2026-04-15: mirror addresses survive game
                // restart and track the currently-viewed unit (not
                // Ramza-specific). See memory note
                // project_eqa_equipment_mirror.md.
                //
                // Promotion-first logic: we read the mirrors regardless
                // of what state machine says. If mirrors 1 and 3 agree,
                // contain at least one non-zero item, AND match a roster
                // unit's equipped items — we're ON EqA for that unit, full
                // stop. Promote screen.Name to "EquipmentAndAbilities" and
                // force screen.ViewedUnit even if the state machine
                // disagreed. This fixes the "SM says TravelList but game
                // is on EqA" drift class observed live with Wilham.
                //
                // Guard: skip EqA promotion when:
                //   (a) PartyMenu tab flags are active (mirror holds the
                //       last-viewed unit's gear, spuriously matches on PM)
                //   (b) Detection returned a world-map-side screen (WorldMap,
                //       TravelList, LocationMenu, etc.) — the mirror stays
                //       populated with stale equipment after leaving the
                //       party tree, so it would override correct detection.
                // Reuse cached tab flags from the detection read (line ~3776)
                // to avoid TOCTOU race where flags flicker during transitions.
                bool partyMenuTabFlagsActive = unitsTabFlag == 1 || inventoryTabFlag == 1;
                bool detectionSaysWorldSide = screen.Name is "WorldMap" or "TravelList"
                    or "LocationMenu" or "TitleScreen" or "Cutscene"
                    or "Outfitter" or "Tavern" or "WarriorsGuild" or "PoachersDen" or "SaveGame"
                    or "OutfitterBuy" or "OutfitterSell" or "OutfitterFitting"
                    or "EncounterDialog" or "BattleSequence" or "BattleFormation"
                    // Non-EqA PartyMenu tabs are certain-not-EqA: SM has
                    // distinct states for them. The equipment mirror stays
                    // populated on these tabs (same roster is visible) but
                    // they are NOT EqA — don't promote.
                    // Include "PartyMenuUnits" itself — when the outer Units grid
                    // is the real state, promoting to EqA is wrong. The
                    // EqA-promote original purpose was to rescue SM=CS
                    // when game is on EqA; "PartyMenuUnits" detection implies
                    // we're above CS, not at EqA.
                    or "PartyMenuUnits" or "PartyMenuInventory" or "PartyMenuChronicle" or "PartyMenuOptions"
                    // JobSelection and its modals: distinct from EqA, mirror
                    // can match but screen is not EqA.
                    or "JobSelection" or "JobActionMenu" or "JobChangeConfirmation"
                    or "CombatSets" or "DismissUnit" or "CharacterDialog";
                // Skip when SM is already in a party-tree screen — the mirror
                // always matches there (same unit's gear is displayed on
                // CharacterStatus, EqA, and pickers). Promoting would stomp
                // a correct CharacterStatus back to EqA.
                bool smAlreadyInPartyTree = ScreenMachine != null && ScreenMachine.CurrentScreen is
                    GameScreen.PartyMenuUnits or GameScreen.CharacterStatus or
                    GameScreen.EquipmentScreen or GameScreen.EquipmentItemList or
                    GameScreen.JobScreen or GameScreen.JobActionMenu or
                    GameScreen.JobChangeConfirmation or
                    GameScreen.SecondaryAbilities or GameScreen.ReactionAbilities or
                    GameScreen.SupportAbilities or GameScreen.MovementAbilities or
                    GameScreen.CombatSets or GameScreen.CharacterDialog or GameScreen.DismissUnit;
                if (Explorer != null && ScreenMachine != null && !partyMenuTabFlagsActive && !detectionSaysWorldSide && !smAlreadyInPartyTree)
                {
                    var m1 = ReadEqaMirror(0x141870854);
                    var m3 = ReadEqaMirror(0x143743704);
                    var passivesM1 = ReadEqaMirrorPassives(0x141870854);
                    var passivesM3 = ReadEqaMirrorPassives(0x143743704);

                    bool eqHasData = m1 != null && m1.Any(v => v != 0 && v != 0xFFFF);
                    bool passivesHaveData = passivesM1.HasValue
                        && (passivesM1.Value.reactionId > 0 || passivesM1.Value.supportId > 0 || passivesM1.Value.movementId > 0);
                    bool mirrorsEqMatch = m1 != null && MirrorsAgree(m1, m3);
                    bool mirrorsPassivesMatch = passivesM1.HasValue && passivesM3.HasValue
                        && passivesM1.Value.Equals(passivesM3.Value);

                    // Trust mirror if (equipment non-zero AND equip mirrors agree)
                    // OR (passives non-zero AND passive mirrors agree).
                    bool hasData = (eqHasData && mirrorsEqMatch) || (passivesHaveData && mirrorsPassivesMatch);

                    if (m1 != null && hasData)
                    {
                        // Look for a roster unit whose equipment matches
                        // (if we have equipment data) or whose passives
                        // match (if equipment is all zeros).
                        if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                        if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);
                        RosterReader.RosterSlot? matchedSlot = null;
                        EquipmentReader.Loadout? matchedLoadout = null;
                        foreach (var slot in _rosterReader.ReadAll())
                        {
                            var lo = _rosterReader.ReadLoadout(slot.SlotIndex);
                            if (lo == null) continue;
                            bool eqMatch = eqHasData && MirrorMatchesLoadout(m1, lo);
                            bool passiveMatch = false;
                            if (passivesHaveData)
                            {
                                // Cross-check mirror passives against
                                // roster's equipped abilities for this slot.
                                var rosterAbilities = _rosterReader.ReadEquippedAbilities(slot.SlotIndex, slot.JobName);
                                if (rosterAbilities != null)
                                {
                                    var mirR = passivesM1.Value.reactionId > 0 ? AbilityData.GetAbility((byte)passivesM1.Value.reactionId)?.Name : null;
                                    var mirS = passivesM1.Value.supportId  > 0 ? AbilityData.GetAbility((byte)passivesM1.Value.supportId)?.Name  : null;
                                    var mirM = passivesM1.Value.movementId > 0 ? AbilityData.GetAbility((byte)passivesM1.Value.movementId)?.Name : null;
                                    // Match when all 3 agree (including nulls when both empty).
                                    passiveMatch = (mirR ?? "") == (rosterAbilities.Reaction ?? "")
                                                && (mirS ?? "") == (rosterAbilities.Support  ?? "")
                                                && (mirM ?? "") == (rosterAbilities.Movement ?? "");
                                }
                            }
                            if (eqMatch || passiveMatch)
                            {
                                matchedSlot = slot;
                                matchedLoadout = lo;
                                break;
                            }
                        }

                        if (matchedSlot != null && matchedLoadout != null)
                        {
                            // We're on EqA for this unit. Promote if the
                            // state machine thinks otherwise — but NOT from
                            // BattleStatus, which is an in-battle overlay
                            // that shows the CharacterStatus panel and
                            // happens to render the same equipment mirror.
                            // Promoting from BattleStatus would mis-classify
                            // the screen as a PartyMenu path and strip the
                            // battle context (active unit name, etc.).
                            //
                            // Session 48: also NEVER promote from any Battle* screen.
                            // Party-unit equipment sits in the mirror bytes
                            // throughout the entire battle, so the fingerprint
                            // matches on every mid-battle frame — promoting
                            // from BattleMyTurn / BattleEnemiesTurn / BattleAlliesTurn /
                            // BattleDialogue / BattleChoice / BattleStatus etc.
                            // all wrongly clobber the true state into EqA.
                            // BattlePaused is an intentional overlay on top of
                            // battle so also skip. EqA is only valid from the
                            // PartyMenu flow (non-Battle* origin).
                            if (screen.Name != "EquipmentAndAbilities"
                                && !screen.Name.StartsWith("Battle"))
                            {
                                ModLogger.Log($"[EqA promote] SM said '{screen.Name}' but mirror matches {matchedSlot.Name} equipment. Promoting to EquipmentAndAbilities.");
                                screen.Name = "EquipmentAndAbilities";
                                // Force SM screen to match so downstream
                                // logic (UI labels, picker transitions)
                                // operates on the right state.
                                ScreenMachine.SetScreen(GameScreen.EquipmentScreen);
                            }
                            // Force ViewedUnit in case SM drifted to wrong
                            // unit — but ONLY on EqA-family screens. Setting
                            // ViewedUnit on a Battle* response pollutes the
                            // field (caller expects an active-unit identity
                            // there, not a mirror-matched roster member).
                            // Live-observed 2026-04-25 at Siedge Weald:
                            // `[EqA promote] Setting viewedUnit='Ramza'`
                            // logs during BattleMyTurn correlated with a
                            // battle_ability "Failed to enter targeting
                            // mode" recovery loop that never progressed.
                            if (!screen.Name.StartsWith("Battle")
                                && screen.ViewedUnit != matchedSlot.Name)
                            {
                                ModLogger.Log($"[EqA promote] Setting viewedUnit='{matchedSlot.Name}' (was '{screen.ViewedUnit ?? "null"}').");
                                screen.ViewedUnit = matchedSlot.Name;
                            }

                            // Passive drift detection at the row level:
                            // compare mirror[cursorRow] vs state machine's
                            // UI label.
                            int smRow = ScreenMachine.CursorRow;
                            if (smRow >= 0 && smRow < 5)
                            {
                                int itemId = m1[smRow];
                                string? itemName = itemId > 0
                                    ? ItemData.GetItem(itemId)?.Name
                                    : null;
                                string? smLabel = screen.UI;
                                if (itemName != null && smLabel != null
                                    && !smLabel.Contains(itemName))
                                {
                                    ModLogger.Log($"[EqA row drift] SM row={smRow} label='{smLabel}' but mirror says '{itemName}' at that row.");
                                }
                            }

                            // Ability section from mirror (+0x0E..+0x13):
                            // read the 3 passive ability IDs (Reaction,
                            // Support, Movement) and resolve to names.
                            // This is MEMORY GROUND TRUTH — bypasses the
                            // roster-bitfield reader entirely. Populates
                            // screen.Abilities.{reaction,support,movement}.
                            //
                            // The AbilityLoadoutPayload struct on screen
                            // may not exist yet when this block runs (the
                            // roster-path code lower down initializes it
                            // for unit-scoped screens). We create it here
                            // if needed and overwrite the 3 passive slots;
                            // the primary/secondary fields stay null and
                            // will be filled by the existing roster path.
                            var passives = ReadEqaMirrorPassives(0x141870854);
                            if (passives.HasValue)
                            {
                                var (rId, sId, mId) = passives.Value;
                                string? rName = rId > 0 ? AbilityData.GetAbility((byte)rId)?.Name : null;
                                string? sName = sId > 0 ? AbilityData.GetAbility((byte)sId)?.Name : null;
                                string? mName = mId > 0 ? AbilityData.GetAbility((byte)mId)?.Name : null;

                                screen.Abilities ??= new AbilityLoadoutPayload();
                                screen.Abilities.Reaction = rName;
                                screen.Abilities.Support  = sName;
                                screen.Abilities.Movement = mName;
                            }

                            // Primary + Secondary skillsets from mirror
                            // bytes +0x0A / +0x0C. Values are indices into
                            // the GetSkillsetName enum (7=Arts of War,
                            // 9=Martial Arts, 10=White Magicks, etc.).
                            var skillsets = ReadEqaMirrorSkillsets(0x141870854);
                            if (skillsets.HasValue)
                            {
                                var (pIdx, secIdx) = skillsets.Value;
                                screen.Abilities ??= new AbilityLoadoutPayload();
                                if (pIdx > 0)
                                {
                                    var pName = GetSkillsetName(pIdx);
                                    if (pName == null && matchedSlot?.JobName != null)
                                        pName = GetPrimarySkillsetByJobName(matchedSlot.JobName);
                                    screen.Abilities.Primary = pName;
                                }
                                if (secIdx > 0)
                                    screen.Abilities.Secondary = GetSkillsetName(secIdx);
                            }
                        }
                    }
                }

                // Equipment pickers (EquippableWeapons / Shields / Headware /
                // CombatGarb / Accessories) — partial surface per TODO §10.6.
                // What we ship this session (session 16): memory-backed cursor
                // row + equippedItem=<current> + pickerTab=<tab name>. What we
                // can't ship yet: ui=<hovered item name> + availableWeapons[]
                // list — both blocked on the per-job equippability table
                // (TODO §0). Without that table we can know the row INDEX
                // from memory but can't map it back to an item name, because
                // the in-game list is filtered by the viewed unit's job and
                // equipment proficiencies (Gallant Knight sees Ragnarok;
                // Chemist does not). The resolver still runs so the cursor
                // row is surfaced; a future session will close the mapping.
                bool isEquipPicker = screen.Name == "EquippableWeapons"
                    || screen.Name == "EquippableShields"
                    || screen.Name == "EquippableHeadware"
                    || screen.Name == "EquippableCombatGarb"
                    || screen.Name == "EquippableAccessories";
                if (!isEquipPicker && (_resolvedEquipPickerCursorAddr != 0 || _equipPickerCursorResolveAttempted))
                {
                    _resolvedEquipPickerCursorAddr = 0L;
                    _equipPickerCursorResolveAttempted = false;
                }
                if (isEquipPicker && ScreenMachine != null && Explorer != null)
                {
                    // Clear the stale battle-menu carryover label.
                    screen.UI = null;

                    // Auto-resolve on first screen call per picker visit.
                    // Gate on MenuDepth == 2 so the 6 oscillation keys don't
                    // leak into a transitioning panel — same rationale as
                    // the JobSelection resolver gate.
                    if (!_equipPickerCursorResolveAttempted && screen.MenuDepth == 2)
                    {
                        _equipPickerCursorResolveAttempted = true;
                        int _unused;
                        var info = ResolveEquipPickerCursor(out _unused);
                        if (info != null) ModLogger.Log($"[CommandBridge] auto {info}");
                    }

                    // Read the row index if we have a resolved address.
                    int? memRow = null;
                    if (_resolvedEquipPickerCursorAddr != 0)
                    {
                        var cur = Explorer.ReadAbsolute((nint)_resolvedEquipPickerCursorAddr, 1);
                        if (cur.HasValue) memRow = (int)cur.Value.value;
                    }
                    if (memRow.HasValue) screen.CursorRow = memRow.Value;

                    // Populate equippedItem=<current> from the viewed unit's
                    // roster loadout. Slot-specific field read via RosterReader.
                    if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                    if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);
                    var viewedSlot = _rosterReader.GetSlotByDisplayOrder(ScreenMachine.ViewedGridIndex);
                    if (viewedSlot != null)
                    {
                        var loadout = _rosterReader.ReadLoadout(viewedSlot.SlotIndex);
                        string? equippedItemName = ScreenMachine.CurrentEquipmentSlot switch
                        {
                            EquipmentSlot.Weapon     => loadout?.WeaponName,
                            EquipmentSlot.Shield     => loadout?.ShieldName ?? loadout?.LeftHandName,
                            EquipmentSlot.Headware   => loadout?.HelmName,
                            EquipmentSlot.CombatGarb => loadout?.BodyName,
                            EquipmentSlot.Accessory  => loadout?.AccessoryName,
                            _ => null,
                        };
                        screen.EquippedItem = equippedItemName;
                    }

                    // Surface the current picker tab name (state-machine
                    // tracked — A/D key history). Depends on the slot that
                    // opened the picker.
                    screen.PickerTab = EquipmentPickerTabs.TabName(
                        ScreenMachine.CurrentEquipmentSlot,
                        ScreenMachine.PickerTab);
                }

                // BattlePaused cursor resolver: the heap byte shuffles across
                // restarts (see project_battle_pause_cursor.md). Auto-resolve
                // on first visit per screen-open, read the row on subsequent
                // calls, map the row to a menu label. Cleared when leaving
                // BattlePaused so the next open re-resolves.
                bool isBattlePaused = screen.Name == "BattlePaused";
                if (!isBattlePaused && (_resolvedBattlePauseCursorAddr != 0 || _battlePauseCursorResolveAttempted))
                {
                    _resolvedBattlePauseCursorAddr = 0L;
                    _battlePauseCursorResolveAttempted = false;
                }
                if (isBattlePaused)
                {
                    // Session 46: prefer SM-tracked cursor over the flaky memory
                    // resolver. The resolver's byte locks onto the first-Down
                    // candidate but stops tracking live nav — live stress test
                    // 2026-04-19 confirmed cursor stayed at Retry through multiple
                    // Downs/Ups in-game. The SM-side tracker counts Up/Down key
                    // presses directly so it's always in lockstep with the game.
                    if (ScreenMachine != null)
                    {
                        int row = ScreenMachine.BattlePausedCursor;
                        screen.CursorRow = row;
                        var label = GameBridge.BattlePauseMenuLabels.ForRow(row);
                        if (label != null) screen.UI = label;
                    }
                    else if (Explorer != null)
                    {
                        // Fallback: legacy memory resolver if SM isn't available.
                        if (!_battlePauseCursorResolveAttempted)
                        {
                            _battlePauseCursorResolveAttempted = true;
                            int _unused;
                            var info = ResolveBattlePauseCursor(out _unused);
                            if (info != null) ModLogger.Log($"[CommandBridge] auto {info}");
                        }

                        if (_resolvedBattlePauseCursorAddr != 0)
                        {
                            var cur = Explorer.ReadAbsolute((nint)_resolvedBattlePauseCursorAddr, 1);
                            if (cur.HasValue)
                            {
                                int row = (int)cur.Value.value;
                                screen.CursorRow = row;
                                var label = GameBridge.BattlePauseMenuLabels.ForRow(row);
                                if (label != null) screen.UI = label;
                            }
                        }
                    }
                }

                // TavernRumors / TavernErrands cursor resolver. Heap byte
                // shuffles across restarts per project_tavern_rumor_cursor.md.
                // Gate on the State Machine's CurrentScreen, NOT screen.Name,
                // because no memory byte distinguishes TavernRumors/Errands
                // from LocationMenu — detection returns "LocationMenu" and
                // the outer command-handler rewrites the name via SM-override
                // only at response-serialization time, which runs AFTER this
                // resolver. The SM itself is the authoritative source for
                // these virtual sub-states (set by user actions, not memory).
                bool isTavernList = ScreenMachine != null
                    && (ScreenMachine.CurrentScreen == GameScreen.TavernRumors
                        || ScreenMachine.CurrentScreen == GameScreen.TavernErrands);
                if (!isTavernList && (_resolvedTavernCursorAddr != 0 || _tavernCursorResolveAttempted))
                {
                    _resolvedTavernCursorAddr = 0L;
                    _tavernCursorResolveAttempted = false;
                }
                if (isTavernList && Explorer != null)
                {
                    if (!_tavernCursorResolveAttempted)
                    {
                        _tavernCursorResolveAttempted = true;
                        int _unused;
                        var info = ResolveTavernCursor(out _unused);
                        if (info != null) ModLogger.Log($"[CommandBridge] auto {info}");
                    }

                    if (_resolvedTavernCursorAddr != 0)
                    {
                        var cur = Explorer.ReadAbsolute((nint)_resolvedTavernCursorAddr, 1);
                        if (cur.HasValue) screen.CursorRow = (int)cur.Value.value;
                    }
                }

                // JobSelection — surface cursor position + ui=<hovered job>.
                // Heap cursor byte shuffles across game sessions, so we do
                // rescan-on-entry (same pattern as picker cursors). Byte
                // value is a flat linear index into JobGridLayout.
                //
                // The grid layout varies per-character:
                // - Story characters get their unique class at (0,0):
                //   Ramza → Gallant Knight, Agrias → Holy Knight,
                //   Mustadio → Machinist, etc. See
                //   JobGridLayout.StoryCharacterUniqueClass for the full
                //   list (verified live 2026-04-15).
                // - Generic units get "Squire" at (0,0).
                // - Gender toggles (2,4): Bard for males, Dancer for
                //   females. Generic job IDs are odd=male / even=female.
                //
                // We identify the viewed unit via the state machine's
                // saved PartyMenu cursor index (preserved across the
                // Enter that opened CharacterStatus) → DisplayOrder
                // lookup in the roster. From there, jobId parity derives
                // gender for generics.
                bool onJobSelection = screen.Name == "JobSelection";
                if (!onJobSelection && (_resolvedJobCursorAddr != 0 || _jobCursorResolveAttempted))
                {
                    // Left JobSelection — drop the cached heap address and
                    // reset the attempt flag. Next JobSelection open will
                    // re-resolve.
                    _resolvedJobCursorAddr = 0L;
                    _jobCursorResolveAttempted = false;
                }
                if (onJobSelection && ScreenMachine != null && Explorer != null)
                {
                    // Clear the stale "Move/Abilities/Wait" label that
                    // DetectScreen sets from the battle menuCursor byte —
                    // meaningless outside battle.
                    screen.UI = null;

                    // Resolve the viewed unit via the roster reader.
                    if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                    if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);
                    int viewedGridIndex = ScreenMachine.ViewedGridIndex;
                    var viewed = _rosterReader.GetSlotByDisplayOrder(viewedGridIndex);

                    string? unitName = viewed?.Name;
                    bool isFemale = viewed != null && JobGridLayout.IsGenericFemale(viewed.Job);
                    // Story characters carry their own gender implicitly
                    // via the unique-class lookup — gender only matters
                    // for the generic (2,4) cell, which a story
                    // character with an out-of-range jobId won't touch
                    // (generic rows are identical regardless of gender
                    // for a story unit's display).
                    var layout = JobGridLayout.ForUnit(unitName, isFemale);

                    // Auto-resolve the heap cursor on first JobSelection read
                    // each session (matches picker behavior). Visible ~2s
                    // cursor flash. Subsequent reads are single-byte reads.
                    // If the resolver fails (no stable byte found), we don't
                    // retry — the fallback is the state machine's tracked
                    // cursor position, which handles most nav correctly.
                    //
                    // Gate on animation-lag being past. JobSelection is
                    // menuDepth=0 (per project_menudepth_gating.md), so the
                    // older menuDepth==2 gate never fired — that's been a
                    // silent bug since the resolver was first wired. Use the
                    // state-machine animation-lag signal instead: the Enter
                    // that opened JobSelection counts as KeysSinceLastSetScreen=0,
                    // and the game needs ~50-200ms to settle before we can
                    // safely send the resolver's 6 oscillation keys. Requiring
                    // the caller to issue one extra `screen` read (which
                    // typically happens naturally after open) ensures we're
                    // past the animation window.
                    if (!_jobCursorResolveAttempted
                        && ScreenMachine.KeysSinceLastSetScreen >= 1)
                    {
                        _jobCursorResolveAttempted = true;
                        int _unused;
                        var info = ResolveJobCursor(out _unused);
                        if (info != null) ModLogger.Log($"[CommandBridge] auto {info}");
                    }

                    int cursorRow = ScreenMachine.CursorRow;
                    int cursorCol = ScreenMachine.CursorCol;
                    // Drift-correction reads: _resolvedJobCursorAddr is 0 unless
                    // the resolver's liveness check passed (session 27). So
                    // this path only executes when we have a live-tracking
                    // cursor byte — no risk of bogus snaps.
                    if (_resolvedJobCursorAddr != 0)
                    {
                        var curByte = Explorer.ReadAbsolute((nint)_resolvedJobCursorAddr, 1);
                        if (curByte.HasValue)
                        {
                            int flatIdx = (int)curByte.Value.value;
                            var rc = layout.IndexToRowCol(flatIdx);
                            if (rc.HasValue)
                            {
                                cursorRow = rc.Value.Row;
                                cursorCol = rc.Value.Col;
                                // Drift-correction: snap SM to memory when
                                // they disagree. The resolver includes a
                                // liveness check (session 27) so _resolvedJobCursorAddr
                                // being non-zero means the byte actually
                                // tracks real nav, not just oscillation.
                                // SetJobCursor is no-op when out of grid
                                // bounds so a bad read can't corrupt the SM.
                                if (cursorRow != ScreenMachine.CursorRow
                                 || cursorCol != ScreenMachine.CursorCol)
                                {
                                    ModLogger.Log($"[SM-Drift] JobSelection ({ScreenMachine.CursorRow},{ScreenMachine.CursorCol}) → ({cursorRow},{cursorCol}) per heap byte");
                                    ScreenMachine.SetJobCursor(cursorRow, cursorCol);
                                }
                            }
                        }
                    }
                    screen.CursorRow = cursorRow;
                    screen.CursorCol = cursorCol;

                    var hoveredClass = layout.GetClassAt(cursorRow, cursorCol);
                    if (hoveredClass != null)
                    {
                        // Classify the cell as Locked / Visible / Unlocked.
                        // Proxy: a class is "unlocked for a unit" if that
                        // unit has any action-ability bit set in the
                        // corresponding job's learned bitfield. Party-wide
                        // unlock is the union across all roster slots. See
                        // JobGridLayout.ClassifyCell for the full rules
                        // (including Squire/Chemist always-unlocked and
                        // per-unit story-class-at-(0,0)).
                        var viewedSkillsets = viewed != null
                            ? (IReadOnlyCollection<string>)_rosterReader.ReadUnlockedSkillsets(viewed.SlotIndex)
                            : System.Array.Empty<string>();
                        var partySkillsets = new HashSet<string>();
                        foreach (var slot in _rosterReader.ReadAll())
                        {
                            foreach (var s in _rosterReader.ReadUnlockedSkillsets(slot.SlotIndex))
                                partySkillsets.Add(s);
                        }

                        var cellState = JobGridLayout.ClassifyCell(
                            hoveredClass, unitName, viewedSkillsets, partySkillsets);
                        screen.JobCellState = cellState.ToString();

                        // Surface unlock requirements on Visible cells only.
                        // Locked cells are shadow silhouettes with no info
                        // revealed in-game; Unlocked cells don't need it.
                        if (cellState == JobGridLayout.CellState.Visible)
                            screen.JobUnlockRequirements = JobGridLayout.GetUnlockRequirements(hoveredClass);

                        // Surface the class name as ui= only when the cell
                        // is at least Visible to the player (Locked cells
                        // render as shadow silhouettes in-game with no
                        // info revealed). On Visible, add a "(not
                        // unlocked)" marker so the caller knows change_job
                        // won't work.
                        screen.UI = cellState switch
                        {
                            JobGridLayout.CellState.Locked => "(locked)",
                            JobGridLayout.CellState.Visible => $"{hoveredClass} (not unlocked)",
                            _ => hoveredClass,
                        };
                    }
                }

                // PartyMenuChronicle: surface the highlighted tile name.
                // Memory hunt for the cursor address failed (UE4 widget churn
                // produced false positives — see project_shop_stock_array.md).
                // State-machine ChronicleIndex is authoritative; drift recovery
                // above snaps it back when the player re-enters the menu fresh.
                if (screen.Name == "PartyMenuChronicle" && ScreenMachine != null)
                {
                    screen.UI = ScreenStateMachine.ChronicleIndexToName(ScreenMachine.ChronicleIndex);
                }

                // PartyMenuOptions: surface the highlighted option name.
                if (screen.Name == "PartyMenuOptions" && ScreenMachine != null)
                {
                    screen.UI = ScreenStateMachine.OptionsIndexToName(ScreenMachine.OptionsIndex);
                }

                // JobActionMenu (modal on JobSelection Enter): Left=Learn
                // Abilities, Right=Change Job. Driven by key history.
                if (screen.Name == "JobActionMenu" && ScreenMachine != null)
                {
                    screen.UI = ScreenMachine.JobActionIndex == 1 ? "Change Job" : "Learn Abilities";
                }

                // JobChangeConfirmation (yes/no after Change Job).
                if (screen.Name == "JobChangeConfirmation" && ScreenMachine != null)
                {
                    screen.UI = ScreenMachine.JobChangeConfirmSelected ? "Confirm" : "Cancel";
                }

                // Roster grid on PartyMenu + nested roster-view descendants so
                // per-unit equipment is always available to Claude. One round-
                // trip beats cursor-move + re-read cycles. See TODO §10.6.
                bool onPartyTree = FFTColorCustomizer.GameBridge.ScreenNamePredicates.IsPartyTree(screen.Name);
                if (onPartyTree && Explorer != null)
                {
                    if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                    if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);
                    if (_hoveredArray == null) _hoveredArray = new HoveredUnitArray(Explorer);
                    if (_hpMpCache == null) _hpMpCache = new HpMpCache(_bridgeDirectory);
                    try
                    {
                        var slots = _rosterReader.ReadAll();
                        if (slots.Count > 0)
                        {
                            ScreenMachine?.SetRosterCount(slots.Count);
                            // Sort by DisplayOrder (+0x122) so the units list
                            // matches the grid the player actually sees.
                            slots.Sort((a, b) => a.DisplayOrder.CompareTo(b.DisplayOrder));
                            const int PartyGridCols = 5;
                            int gridRows = (slots.Count + PartyGridCols - 1) / PartyGridCols;
                            var grid = new RosterGrid
                            {
                                Count = slots.Count,
                                Max = RosterReader.MaxSlots,
                                GridCols = PartyGridCols,
                                GridRows = gridRows,
                            };
                            // Cursor + hovered name are only populated on
                            // PartyMenu root (Units tab). Nested screens
                            // (CharacterStatus, EquipmentAndAbilities) still
                            // include the full roster list but without a
                            // grid cursor.
                            if (screen.Name == "PartyMenuUnits" && ScreenMachine != null
                                && ScreenMachine.Tab == PartyTab.Units)
                            {
                                // Exit-cleanup: cached address is only valid
                                // while we're actually on the Units tab. On
                                // first re-entry the auto-resolver below will
                                // re-populate it.
                                // Auto-resolve the heap cursor byte on first
                                // PartyMenu entry (same pattern as JobSelection).
                                // Gate on MenuDepth == 0 (outer menu memory-
                                // confirmed) so the 6 raw Right/Left oscillation
                                // keys never leak into a nested panel mid-
                                // transition. The state machine flips to
                                // PartyMenu synchronously on Escape, but the
                                // close animation takes ~50-200ms; MenuDepth
                                // lags the state machine by exactly that
                                // window, matching the JobSelection fix from
                                // session 15.
                                // DISABLED 2026-04-16: auto-resolve sends 8+
                                // keypresses (Right/Left/Down/Up oscillation)
                                // that visibly bounce the cursor and never find
                                // a usable byte anyway. Keep the resolver
                                // available as an explicit `resolve_party_menu_cursor`
                                // action but don't auto-fire on screen reads.
                                // if (!_partyMenuCursorResolveAttempted && screen.MenuDepth == 0)
                                // {
                                //     _partyMenuCursorResolveAttempted = true;
                                //     int _unused;
                                //     var partyInfo = ResolvePartyMenuCursor(out _unused);
                                //     if (partyInfo != null) ModLogger.Log($"[CommandBridge] auto {partyInfo}");
                                // }

                                // Memory-backed cursor read path. Live
                                // testing 2026-04-15 session 16 showed the
                                // heap does NOT store a flat-linear
                                // `row*5+col` index for the PartyMenu grid —
                                // all 17 column-oscillation survivors
                                // failed the "+5 on Down" axis verify.
                                // Hypothesis: the grid cursor is stored as
                                // two separate bytes (row and col), or the
                                // row is held in a pointer chain we haven't
                                // located. The resolver (above) still finds
                                // a *column* byte, but decoding it alone as
                                // `row*5+col` would be wrong past row 0.
                                //
                                // Until a flat-linear byte (or a
                                // row-byte resolver) lands, we stay with
                                // the state-machine-tracked cursor. The
                                // resolver runs as instrumentation so the
                                // next debugger can inspect which bytes
                                // are candidates. `_resolvedPartyMenuCursorAddr`
                                // is guarded to only apply when it's clearly
                                // safe — currently never, since no axis-
                                // verified byte exists.
                                int cursorRow = ScreenMachine.CursorRow;
                                int cursorCol = ScreenMachine.CursorCol;

                                grid.CursorRow = cursorRow;
                                grid.CursorCol = cursorCol;
                                int gridIdx = cursorRow * PartyGridCols + cursorCol;
                                var hovered = slots.FirstOrDefault(x => x.DisplayOrder == gridIdx);
                                if (hovered != null)
                                {
                                    grid.HoveredName = hovered.Name;
                                    // Surface the hovered unit as ui=<name> so
                                    // consumers don't see the stale "ui=Move"
                                    // that bled over from the battle menu path.
                                    screen.UI = hovered.Name;
                                }
                            }
                            else if (screen.Name != "PartyMenuUnits")
                            {
                                // Left PartyMenu (either nested panel or
                                // different screen entirely). Drop cached
                                // heap address + attempt flag so next entry
                                // re-resolves cleanly.
                                if (_resolvedPartyMenuCursorAddr != 0 || _partyMenuCursorResolveAttempted)
                                {
                                    _resolvedPartyMenuCursorAddr = 0L;
                                    _partyMenuCursorResolveAttempted = false;
                                }
                            }
                            foreach (var s in slots)
                            {
                                var unit = new RosterUnit
                                {
                                    Slot = s.SlotIndex,
                                    Name = s.Name,
                                    Level = s.Level,
                                    Job = s.JobName,
                                    Brave = s.Brave,
                                    Faith = s.Faith,
                                    Jp = s.CurrentJobJp,
                                    Zodiac = s.Zodiac,
                                    DisplayOrder = s.DisplayOrder,
                                };
                                // Equipment comes from the roster slot itself
                                // (stable static array at 0x1411A18D0). The
                                // hovered-unit heap array was a red herring —
                                // verified 2026-04-14 that roster +0x0E..+0x1A
                                // holds the canonical u16 equipment IDs for
                                // every unit (FFTPatcher-keyed).
                                var lo = _rosterReader.ReadLoadout(s.SlotIndex);
                                if (lo != null)
                                {
                                    unit.Equipment = new Loadout
                                    {
                                        Weapon = lo.WeaponName,
                                        LeftHand = lo.LeftHandName,
                                        Shield = lo.ShieldName,
                                        Helm = lo.HelmName,
                                        Body = lo.BodyName,
                                        Accessory = lo.AccessoryName,
                                    };
                                }
                                // HP/MP are runtime-computed (not in roster slot).
                                // The hovered-unit heap array only populates ~4
                                // slots near the cursor — for every other unit
                                // we fall back to the per-slot disk cache.
                                // Each live observation updates the cache keyed
                                // by equipment signature, so subsequent reads
                                // surface the unit's HP/MP until equipment
                                // changes (which invalidates the entry).
                                int[] equipSig = new int[7];
                                if (lo != null)
                                {
                                    equipSig[0] = lo.HelmId ?? 0xFFFF;
                                    equipSig[1] = lo.BodyId ?? 0xFFFF;
                                    equipSig[2] = lo.AccessoryId ?? 0xFFFF;
                                    equipSig[3] = lo.WeaponId ?? 0xFFFF;
                                    equipSig[4] = lo.LeftHandId ?? 0xFFFF;
                                    equipSig[5] = 0xFFFF;
                                    equipSig[6] = lo.ShieldId ?? 0xFFFF;
                                }
                                // The HoveredUnitArray only populates ~4 slots
                                // per read (see project_hovered_unit_array_partial.md).
                                // HpMpCache fills the gap: every successful live
                                // observation is persisted keyed by (slot, equipSig),
                                // so units the player has hovered previously still
                                // surface HP/MP on subsequent reads. Equipment
                                // changes invalidate the cached entry.
                                var live = _hoveredArray.ReadStatsIfMatches(
                                    arrayIndex: s.SlotIndex,
                                    expectedBrave: s.Brave,
                                    expectedFaith: s.Faith);
                                if (live != null)
                                {
                                    unit.Hp = live.Hp;
                                    unit.MaxHp = live.MaxHp;
                                    unit.Mp = live.Mp;
                                    unit.MaxMp = live.MaxMp;
                                    _hpMpCache.Set(s.SlotIndex, equipSig,
                                        live.Hp, live.MaxHp, live.Mp, live.MaxMp);
                                }
                                else
                                {
                                    var cached = _hpMpCache.Get(s.SlotIndex, equipSig);
                                    if (cached != null)
                                    {
                                        unit.Hp = cached.Hp;
                                        unit.MaxHp = cached.MaxHp;
                                        unit.Mp = cached.Mp;
                                        unit.MaxMp = cached.MaxMp;
                                    }
                                }
                                grid.Units.Add(unit);
                            }
                            screen.Roster = grid;
                        }
                    }
                    catch (Exception rex)
                    {
                        ModLogger.LogError($"[CommandBridge] Roster read failed: {rex.Message}");
                    }
                }

                // Track world map location for auto map loading (persists to disk).
                // On WorldMap, hover is the authoritative position (rawLocation is stale after travel).
                // On EncounterDialog, rawLocation is the encounter location.
                if (GameBridge.LocationSaveLogic.ShouldSave(rawLocation, hover, screen.Name, _lastWorldMapLocation))
                {
                    int effectiveLoc = GameBridge.LocationSaveLogic.GetEffectiveLocation(rawLocation, hover, screen.Name);
                    SaveLastLocation(effectiveLoc);
                    // Also update the display location if we detected a better value
                    if (screen.Name == "WorldMap" && effectiveLoc != screen.Location)
                    {
                        screen.Location = effectiveLoc;
                        screen.LocationName = GetLocationName(effectiveLoc);
                    }
                }

                // Reset map auto-load flag when not in battle
                if (!inBattle && _battleMapAutoLoaded)
                    _battleMapAutoLoaded = false;

                // Auto-load map when first entering battle (any battle screen)
                if (inBattle && !_battleMapAutoLoaded)
                {
                    EnsureMapLoader();
                    if (_lastWorldMapLocation < 0)
                        _lastWorldMapLocation = LoadLastLocation();

                    // Try location-based lookup (fast, validated later by scan_move)
                    if (_lastWorldMapLocation >= 0 && _mapLoader != null)
                    {
                        var autoMap = _mapLoader.LoadMapForLocation(_lastWorldMapLocation);
                        if (autoMap != null)
                            ModLogger.Log($"[Map] Auto-loaded MAP{autoMap.MapNumber:D3} for location {_lastWorldMapLocation} (will validate on scan_move)");
                    }

                    _battleMapAutoLoaded = true;
                }

                // Populate cursor tile and available tiles for battle sub-states
                if (screen.Name == "BattleMoving" || screen.Name == "BattleAttacking" || screen.Name == "BattleCasting")
                    PopulateBattleTileData(screen);

                // Populate active unit name/job during battle from cached scan data.
                //
                // Why cache-only: there is no single memory address that gives us the
                // "active unit" reliably without a scan. The condensed struct at 0x14077D2A0
                // reflects the CURSOR unit (whichever unit the cursor is hovering or the
                // last unit scanned during C+Up cycling), not necessarily the active unit.
                // Battle-state nameId at 0x14077CA94 uses a different numbering scheme that
                // doesn't match roster entries. BattleTracker's AddrActiveJobId at 0x14077CA6C
                // reads the wrong unit on enemy turns.
                //
                // So active unit name/job will show as empty on the FIRST `screen` call of
                // a battle, then populate after the first scan_move runs and caches it.
                if (FFTColorCustomizer.GameBridge.ScreenNamePredicates.IsBattleState(screen.Name)
                    && (_cachedActiveUnitName != null || _cachedActiveUnitJob != null))
                {
                    screen.ActiveUnitName = _cachedActiveUnitName;
                    screen.ActiveUnitJob = _cachedActiveUnitJob;
                    screen.ActiveUnitSummary = GameBridge.ActiveUnitSummaryFormatter.Format(
                        _cachedActiveUnitName, _cachedActiveUnitJob,
                        _cachedActiveUnitX, _cachedActiveUnitY,
                        _cachedActiveUnitHp, _cachedActiveUnitMaxHp,
                        _cachedActiveUnitWeaponTag);
                }
                // Clear cached active-unit fields when transitioning to a
                // terminal battle state — the cache is stale once Ramza dies
                // / battle ends and would otherwise leak into the response.
                // Live-flagged playtest #4 2026-04-25: agent saw "Tietra"
                // (a story character not present in this battle) in the
                // header right before GameOver. The cache held a leaked
                // name from a stale scan during the death transition.
                if (screen.Name == "BattleVictory" || screen.Name == "BattleDesertion"
                    || screen.Name == "GameOver")
                {
                    _cachedActiveUnitName = null;
                    _cachedActiveUnitJob = null;
                    screen.ActiveUnitName = null;
                    screen.ActiveUnitJob = null;
                    screen.ActiveUnitSummary = null;
                }

                // Sync state machine with memory-detected top-level screens.
                // This ensures the state machine stays in sync even after restarts
                // or when it drifts from reality.
                //
                // IMPORTANT: By this point, PartyMenu-related names have already been
                // resolved by the override block above (screen.Name may read
                // PartyMenuInventory / CharacterStatus / etc.). We must NOT reset the
                // state machine to Unknown when the POST-override name is a party
                // screen. Only reset for genuine top-level screens.
                if (ScreenMachine != null)
                {
                    var expected = screen.Name switch
                    {
                        "WorldMap" => GameScreen.WorldMap,
                        "TitleScreen" => GameScreen.TitleScreen,
                        "PartyMenuUnits" => GameScreen.PartyMenuUnits,
                        "EncounterDialog" or "BattleSequence" => GameScreen.Unknown,
                        "Battle" or "GameOver" => GameScreen.Unknown,
                        _ when screen.Name.StartsWith("Battle_") => GameScreen.Unknown,
                        // "TravelList" removed: if detection landed on TravelList but
                        // the state machine is tracking a party menu tab, the override
                        // above would have rewritten screen.Name. If screen.Name still
                        // reads "TravelList" here, we really are on the travel overlay
                        // (rare but possible) — leave state machine alone rather than
                        // clobber it with Unknown.
                        _ => (GameScreen?)null
                    };
                    if (expected.HasValue && ScreenMachine.CurrentScreen != expected.Value)
                        ScreenMachine.SetScreen(expected.Value);
                }

                return screen;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[CommandBridge] DetectScreen error: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
