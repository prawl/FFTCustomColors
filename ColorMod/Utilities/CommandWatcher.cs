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
        public BattleTracker? BattleTracker { get; set; }
        public EventScriptLookup? ScriptLookup { get; set; }
        private NavigationActions? _navActions;
        private MapLoader? _mapLoader;
        private RosterReader? _rosterReader;
        private NameTableLookup? _rosterNameTable;
        private HoveredUnitArray? _hoveredArray;
        private PickerListReader? _pickerListReader;
        private readonly BattleTurnTracker _turnTracker = new();
        private bool _movedThisTurn;
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

        /// <summary>
        /// When true, game actions must go through validPaths. Raw key presses and
        /// actions not in the current screen's validPaths are blocked.
        /// Info actions (scan_move, screen, memory reads) are always allowed.
        /// </summary>
        // Strict mode disabled by default while battle_move/battle_ability menu
        // navigation is unreliable (BattleMenuTracker desync). Re-enable once
        // the tracker is fixed and gameplay commands work reliably.
        public bool StrictMode { get; set; } = false;

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
            }
            _resolvedJobCursorAddr = verified;
            return cands.Count > 0
                ? $"Resolved job cursor: 0x{_resolvedJobCursorAddr:X} ({cands.Count} candidate{(cands.Count > 1 ? "s" : "")})"
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
            "mark_blocked", "snapshot", "heap_snapshot", "diff",
            "search_bytes", "search_all", "search_memory", "search_near",
            "dump_unit", "dump_all", "write_address", "set_strict", "set_map",
            "read_dialogue", "write_byte", "dump_detection_inputs",
            "scrape_shop_items",
            "hold_key",
            "get_flag", "set_flag", "list_flags",
            "reset_state_machine",
            "resolve_picker_cursor",
            "resolve_job_cursor",
            "resolve_party_menu_cursor",
            "resolve_equip_picker_cursor",
            "resolve_eqa_row",
            "remove_equipment_at_cursor"
        };

        // Named game actions allowed in strict mode (from fft.sh helpers)
        private static readonly HashSet<string> AllowedGameActions = new()
        {
            "execute_action", "battle_wait", "battle_flee", "battle_attack", "battle_ability",
            "battle_move", "world_travel_to", "auto_move", "get_arrows",
            "advance_dialogue", "save", "load",
            "battle_retry", "battle_retry_formation",
            "buy", "sell", "change_job",
            "open_eqa", "open_job_selection", "open_character_status",
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
                ModLogger.Log("[CommandBridge] Starting polling fallback (every 50ms)");
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
                    await Task.Delay(50);
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
                        WriteResponse(blocked);
                        _lastProcessedCommandId = command.Id;
                        return;
                    }

                    _lastCommandWasQuery = isScreenQuery;

                    var response = ExecuteCommand(command);
                    response.Screen ??= DetectScreenSettled();
                    // Override detection-ambiguous names where the SM has a
                    // stronger signal (e.g. SaveSlotPicker vs TravelList).
                    if (response.Screen != null && ScreenMachine != null)
                    {
                        var resolved = ScreenDetectionLogic.ResolveAmbiguousScreen(
                            ScreenMachine.CurrentScreen, response.Screen.Name);
                        if (resolved != response.Screen.Name)
                        {
                            ModLogger.Log($"[SM-Override] Detection={response.Screen.Name} → {resolved} (SM={ScreenMachine.CurrentScreen}).");
                            response.Screen.Name = resolved;
                        }
                    }
                    SyncBattleMenuTracker(response.Screen);

                    // Attach rate-limit warning (set above if we auto-delayed).
                    if (chainWarning != null) response.ChainWarning = chainWarning;

                    // Stamp completion time for the rate-limit floor — but only
                    // for game-affecting commands. Observational queries don't
                    // press keys, so they shouldn't reset the clock.
                    if (!isObservational)
                        _lastGameCommandCompletedAt = DateTime.UtcNow;

                    // No auto-scan — Claude must call scan_move explicitly before acting.
                    // Auto-scan was removed because C+Up keypresses during settling caused
                    // the Reset Move bug and stale cache issues.

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
                    WriteResponse(response);

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
                    if (errorResponse.Screen != null)
                        errorResponse.ValidPaths = NavigationPaths.GetPaths(errorResponse.Screen);
                    WriteResponse(errorResponse);
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

                            var matches = Explorer.SearchBytesInAllMemory(patternBytes, 100);

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

                    case "battle_wait":
                        // Auto-scan before wait (battle_wait needs unit data for facing)
                        try
                        {
                            var scanCmd = new CommandRequest { Id = command.Id, Action = "scan_move" };
                            var scanRes = ExecuteNavAction(scanCmd);
                            if (scanRes.Status == "completed")
                                _turnTracker.MarkScanned();
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogError($"[CommandBridge] Pre-wait scan failed: {ex.Message}");
                        }
                        _turnTracker.ResetForNewTurn();
                        _battleMenuTracker.OnNewTurn();
                        _movedThisTurn = false;
                        _postMoveX = -1;
                        _postMoveY = -1;
                        _waitConfirmPending = false;
                        _lastAbilityName = null;
                        _cachedPrimarySkillset = null;
                        _cachedSecondarySkillset = null;
                        _cachedLearnedAbilityNames = null;
                        return ExecuteNavActionWithAutoScan(command);

                    case "battle_flee":
                        _battleMenuTracker.ReturnToMyTurn();
                        return ExecuteNavAction(command);

                    case "battle_attack":
                        goto case "battle_ability";
                    case "battle_ability":
                        // Always scan fresh before attack/ability (~15ms)
                        CommandResponse? freshScan = null;
                        {
                            var autoScanCmd = new CommandRequest { Id = command.Id, Action = "scan_move" };
                            freshScan = ExecuteNavAction(autoScanCmd);
                            if (freshScan.Status == "completed")
                                CacheLearnedAbilities(freshScan.Battle);
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
                        var actionResult = ExecuteNavAction(command);
                        if (actionResult.Status != "completed")
                            _lastAbilityName = null; // clear on failure
                        else
                        {
                            actionResult.PostAction = _navActions?.ReadPostActionState();
                        }
                        return actionResult;

                    case "battle_move":
                    case "move_grid": // legacy alias
                        // Always scan fresh before move (~15ms)
                        {
                            var moveScanCmd = new CommandRequest { Id = command.Id, Action = "scan_move" };
                            var moveScanRes = ExecuteNavAction(moveScanCmd);
                            if (moveScanRes.Status == "completed")
                                CacheLearnedAbilities(moveScanRes.Battle);
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
                    case "advance_dialogue":
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
                    case "auto_place_units":
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
                        // Infra action for `fft_resync`: hard-reset the
                        // screen state machine to WorldMap + clear every
                        // auto-resolve latch so the next screen calls re-
                        // run fresh. Use after an escape-storm has
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
                var available = paths != null ? string.Join(", ", paths.Keys) : "none";
                response.Status = "failed";
                response.Error = $"No path '{pathName}' on screen '{screen.Name}'. Available: {available}";
                return response;
            }

            // If the path specifies a high-level action, delegate.
            // battle_wait needs special handling (confirmation, pre-scan, turn reset)
            // that only exists in the main command switch — call ExecuteNavActionWithAutoScan
            // which handles the full wait cycle including facing and turn polling.
            if (!string.IsNullOrEmpty(path.Action))
            {
                command.Action = path.Action;
                if (path.LocationId != 0) command.LocationId = path.LocationId;

                if (path.Action == "battle_wait")
                {
                    // Pre-scan for facing data
                    try
                    {
                        var scanCmd = new CommandRequest { Id = command.Id, Action = "scan_move" };
                        ExecuteNavAction(scanCmd);
                    }
                    catch { }
                    _turnTracker.ResetForNewTurn();
                    _battleMenuTracker.OnNewTurn();
                    _movedThisTurn = false;
                    _waitConfirmPending = false;
                    _lastAbilityName = null;
                    _cachedPrimarySkillset = null;
                    _cachedSecondarySkillset = null;
                    _cachedLearnedAbilityNames = null;
                    return ExecuteNavActionWithAutoScan(command);
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
            var response = ExecuteNavAction(command);

            if (response.Screen != null && _turnTracker.ShouldAutoScan(response.Screen.Name, response.Screen.BattleTeam, response.Screen.BattleUnitId, response.Screen.BattleUnitHp))
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
                bool success = _inputSimulator.SendKeyPressToWindow(gameWindow, key.Vk);
                lastKeyMs = nowMs;

                response.KeyResults.Add(new KeyResult { Vk = key.Vk, Success = success });
                if (success) successCount++;

                ModLogger.LogDebug($"[CommandBridge] Key {key.Name ?? key.Vk.ToString()} (0x{key.Vk:X2}) [i={i}, +{sinceLast}ms]: {(success ? "OK" : "FAIL")}");

                if (success)
                {
                    ScreenMachine?.OnKeyPressed(key.Vk);
                    if (_battleMenuTracker.InSubmenu)
                        _battleMenuTracker.OnKeyPressed(key.Vk);
                    InvalidateJobCursorOnRowCross(key.Vk);
                    InvalidatePartyMenuCursorOnMove(key.Vk);
                    InvalidateEquipPickerCursorOnMove(key.Vk);
                    InvalidateEqaColumnCursorOnMove(key.Vk);
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
                    // Override detection-ambiguous names where the SM has a
                    // stronger signal. If we override here, also skip the
                    // SM-sync below — syncing SM to the stale detection
                    // result (e.g. TravelList when SM correctly says
                    // SaveSlotPicker) would undo the valid SM state.
                    bool overrode = false;
                    if (response.Screen != null && ScreenMachine != null)
                    {
                        var resolved = ScreenDetectionLogic.ResolveAmbiguousScreen(
                            ScreenMachine.CurrentScreen, response.Screen.Name);
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
        private DetectedScreen? DetectScreenSettled()
        {
            var first = DetectScreen();
            if (first == null) return null;

            var sw = Stopwatch.StartNew();
            string lastName = first.Name;
            DetectedScreen? last = first;
            int stableCount = 0;

            while (sw.ElapsedMilliseconds < 1000)
            {
                Thread.Sleep(50);
                var current = DetectScreen();
                if (current == null) continue;

                if (current.Name == lastName)
                {
                    stableCount++;
                    last = current;
                    if (stableCount >= 3) // 3 consecutive matches (~150ms stable)
                        return current;
                }
                else
                {
                    lastName = current.Name;
                    last = current;
                    stableCount = 0;
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
            _cachedSecondarySkillset = activeUnit.SecondaryAbility > 0
                ? GetSkillsetName(activeUnit.SecondaryAbility)
                : null;
            ModLogger.Log($"[CommandBridge] Skillsets: primary={_cachedPrimarySkillset ?? "null"}, secondary={_cachedSecondarySkillset ?? "null"} (secondaryIdx={activeUnit.SecondaryAbility})");
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
            var allAbilities = GameBridge.ActionAbilityLookup.GetSkillsetAbilities(skillsetName);
            if (allAbilities == null) return System.Array.Empty<string>();

            if (_cachedLearnedAbilityNames != null && _cachedLearnedAbilityNames.Count > 0)
            {
                // Filter to only learned abilities, preserving skillset order
                var filtered = allAbilities
                    .Where(a => _cachedLearnedAbilityNames.Contains(a.Name))
                    .Select(a => a.Name)
                    .ToArray();
                return filtered.Length > 0 ? filtered : allAbilities.Select(a => a.Name).ToArray();
            }

            // No cached data — return full skillset
            return allAbilities.Select(a => a.Name).ToArray();
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

                // During battle (location=255), use last known world map location
                if (screen.Location == 255)
                {
                    if (_lastWorldMapLocation < 0)
                        _lastWorldMapLocation = LoadLastLocation();
                    if (_lastWorldMapLocation >= 0)
                        screen.Location = _lastWorldMapLocation;
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
                    menuDepth: screen.MenuDepth);

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

                if (screen.Name == "Cutscene")
                {
                    screen.EventId = eventId;
                    // Cutscenes advance on Enter (Escape is a no-op per
                    // flavor-dialog convention). Surface ui=Advance so the
                    // one-liner always carries a hovered-action label.
                    screen.UI = "Advance";
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
                if (screen.Name == "BattleMyTurn" || screen.Name == "BattleActing")
                {
                    bool hasMoved = screen.BattleMoved == 1 || _movedThisTurn;
                    if (screen.MenuCursor != _lastLoggedCursor)
                    {
                        ModLogger.Log($"[UI] cursor={screen.MenuCursor} screen={screen.Name} moved={hasMoved} acted={screen.BattleActed} movedThisTurn={_movedThisTurn}");
                        _lastLoggedCursor = screen.MenuCursor;
                    }
                    screen.UI = screen.MenuCursor switch
                    {
                        0 => hasMoved ? "Reset Move" : "Move",
                        1 => "Abilities",
                        2 => "Wait",
                        3 => "Status",
                        4 => "AutoBattle",
                        _ => null
                    };
                }

                // During targeting mode, show the ability being cast/used.
                if ((screen.Name == "BattleAttacking" || screen.Name == "BattleCasting") && _lastAbilityName != null)
                    screen.UI = _lastAbilityName;

                // Battle menu tracker: set UI from tracker if in submenu
                // (entry/exit managed in SyncBattleMenuTracker, called after screen settles)
                if (screen.Name == "BattleAbilities" && _battleMenuTracker.InSubmenu)
                    screen.UI = _battleMenuTracker.CurrentItem;

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
                bool onPartyMenuAnyTab = screen.Name == "PartyMenuUnits"
                    || screen.Name == "PartyMenuInventory"
                    || screen.Name == "PartyMenuChronicle"
                    || screen.Name == "PartyMenuOptions";
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
                            // state machine thinks otherwise.
                            if (screen.Name != "EquipmentAndAbilities")
                            {
                                ModLogger.Log($"[EqA promote] SM said '{screen.Name}' but mirror matches {matchedSlot.Name} equipment. Promoting to EquipmentAndAbilities.");
                                screen.Name = "EquipmentAndAbilities";
                                // Force SM screen to match so downstream
                                // logic (UI labels, picker transitions)
                                // operates on the right state.
                                ScreenMachine.SetScreen(GameScreen.EquipmentScreen);
                            }
                            // Force ViewedUnit in case SM drifted to wrong unit
                            if (screen.ViewedUnit != matchedSlot.Name)
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
                    // Gate the trigger on screen.MenuDepth == 2 (inner panel
                    // confirmed by memory). The state machine flips to
                    // JobScreen synchronously on Enter, but the game's open
                    // animation takes ~50-200ms — firing the resolver during
                    // that window sends the 6 raw Right/Left keys into
                    // PartyMenu / CharacterStatus / animation, drifting the
                    // OUTER cursor. MenuDepth lags the state machine by the
                    // same animation window, so it's the right gate.
                    if (!_jobCursorResolveAttempted && screen.MenuDepth == 2)
                    {
                        _jobCursorResolveAttempted = true;
                        int _unused;
                        ResolveJobCursor(out _unused);
                    }

                    int cursorRow = ScreenMachine.CursorRow;
                    int cursorCol = ScreenMachine.CursorCol;
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

                // Roster grid on PartyMenu + every nested PartyMenu screen so
                // per-unit equipment is always available to Claude. One round-
                // trip beats cursor-move + re-read cycles. See TODO §10.6.
                bool onPartyTree = screen.Name == "PartyMenuUnits"
                    || screen.Name == "CharacterStatus"
                    || screen.Name == "EquipmentAndAbilities";
                if (onPartyTree && Explorer != null)
                {
                    if (_rosterNameTable == null) _rosterNameTable = new NameTableLookup(Explorer);
                    if (_rosterReader == null) _rosterReader = new RosterReader(Explorer, _rosterNameTable);
                    if (_hoveredArray == null) _hoveredArray = new HoveredUnitArray(Explorer);
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
                                // HP/MP are NOT stored in the roster — they're
                                // runtime-computed from job base + equipment.
                                // Deferred to a future session (see TODO §10.6).
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
                // a battle, then populate after the first scan_move runs and caches the
                // Active unit name/job populated by scan_move at turn start.

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
