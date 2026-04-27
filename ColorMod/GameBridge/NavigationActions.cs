using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// High-level navigation actions that handle multi-step key sequences internally.
    /// Claude sends intent (e.g. "battle_wait"), C# handles all key presses and polling.
    /// </summary>
    public class NavigationActions
    {
        private readonly IInputSimulator _input;
        private readonly MemoryExplorer _explorer;
        private readonly Func<DetectedScreen?> _detectScreen;
        private readonly NameTableLookup _nameTable;
        public BattleTracker? BattleTracker { get; set; }
        /// <summary>S58: lifetime/battle stat sink. Hooks fire from
        /// battle_attack / battle_ability post-HP (damage + kill),
        /// battle_move completion (tiles moved), and successful casts
        /// (ability usage). Null-safe — skips when tracker isn't wired.</summary>
        public BattleStatTracker? StatTracker { get; set; }
        public MapLoader? _mapLoader;
        public Func<string[]>? GetAbilitiesSubmenuItems { get; set; }
        public Func<string, string[]>? GetAbilityListForSkillset { get; set; }
        private IntPtr _gameWindow;

        // S58: per-battle live-HP address cache. Populated on the full
        // SearchBytesInAllMemory path; hit first on subsequent ReadLiveHp
        // calls, skipping the ~500MB scan when the cached address still
        // holds a plausible HP for the requested (maxHp, level) pair.
        // Cleared on battle-boundary transitions by the caller (owner).
        public LiveHpAddressCache LiveHpCache { get; } = new LiveHpAddressCache();

        // S58: per-battle Move/Jump cache, fallback for when heap search
        // misses (live-observed: every unit in a battle returned Mv=0
        // even though we'd read valid values earlier). Populated on each
        // successful TryReadMoveJumpFromHeap; queried on miss.
        public UnitMoveJumpCache MoveJumpCache { get; } = new UnitMoveJumpCache();

        // 2026-04-26: per-battle roster-match cache, keyed by NameId. When
        // a unit levels up mid-battle, the scanned Level shifts but the
        // roster slot's Level can lag a frame, so RosterMatcher returns
        // NameId=0 → unit.Job collapses to default 0 (Squire/Mettle) and
        // the bridge offers wrong abilities + desyncs menu navigation.
        // Populated on each successful match; queried as a fallback when
        // a fresh match misses for an active unit with a known NameId.
        public RosterMatchCache RosterMatchCache { get; } = new RosterMatchCache();

        // --- DirectInput hook for faking held C key ---
        private static volatile bool _injectCKey = false;
        private static bool _diHookInstalled = false;

        // After battle_move or battle_ability, the game auto-advances cursor to
        // Abilities (index 1) but memory at 0x1407FC620 still reads 0.
        // This flag triggers cursor correction in battle_wait and battle_ability.
        private bool _menuCursorStale = false;

        // Tick count (Environment.TickCount64) at the moment battle_wait sent the
        // Enter that commits the Wait action. The game enters BattleWaiting
        // (facing select) but both battleMode and menuCursor can lag for
        // hundreds of ms — detection during that window mislabels as
        // BattleMoving. CommandWatcher consults this via
        // StaleBattleMovingClassifier to flip the label back.
        // -1 means "never fired this session."
        public static long LastWaitEnterTickMs { get; private set; } = -1;

        // Original GetDeviceState function pointer
        private static IntPtr _originalGetDeviceState;

        // Must keep delegate alive to prevent GC collection while vtable points to it
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private unsafe delegate int GetDeviceStateDelegate(IntPtr self, uint cbData, IntPtr lpvData);
        private static GetDeviceStateDelegate? _hookDelegate;

        // DIK_C scan code in DirectInput
        private const byte DIK_C = 0x2E;

        [DllImport("dinput8.dll", EntryPoint = "DirectInput8Create")]
        private static extern int DirectInput8Create(
            IntPtr hinst, uint dwVersion, ref Guid riidltf, out IntPtr ppvOut, IntPtr punkOuter);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        private static readonly Guid IID_IDirectInput8W = new Guid("BF798031-483A-4DA2-AA99-5D64ED369700");
        private static readonly Guid GUID_SysKeyboard = new Guid("6F1D2B61-D5A0-11CF-BFC7-444553540000");

        /// <summary>
        /// Install an inline hook on DirectInput's GetDeviceState function.
        /// We patch the actual function code in dinput8.dll so ALL devices (including the
        /// game's keyboard device) go through our hook. Vtable hooks failed because the
        /// game's device had a different vtable than our temporary device.
        /// </summary>
        public unsafe void InstallDirectInputHook()
        {
            if (_diHookInstalled) return;

            try
            {
                // Create a temporary DI8 device just to discover the GetDeviceState function address
                var iid = IID_IDirectInput8W;
                IntPtr di8;
                int hr = DirectInput8Create(GetModuleHandle(null), 0x0800, ref iid, out di8, IntPtr.Zero);
                if (hr != 0)
                {
                    ModLogger.LogError($"[DI Hook] DirectInput8Create failed: 0x{hr:X8}");
                    return;
                }

                IntPtr di8Vtable = *(IntPtr*)di8;
                var createDevice = Marshal.GetDelegateForFunctionPointer<CreateDeviceDelegate>(
                    *((IntPtr*)di8Vtable + 3));

                var kbGuid = GUID_SysKeyboard;
                IntPtr device;
                hr = createDevice(di8, ref kbGuid, out device, IntPtr.Zero);
                if (hr != 0)
                {
                    ModLogger.LogError($"[DI Hook] CreateDevice failed: 0x{hr:X8}");
                    var rel = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(*((IntPtr*)di8Vtable + 2));
                    rel(di8);
                    return;
                }

                // Get the actual GetDeviceState function address from vtable[9]
                IntPtr deviceVtable = *(IntPtr*)device;
                IntPtr targetFunc = *((IntPtr*)deviceVtable + 9);

                ModLogger.Log($"[DI Hook] GetDeviceState at 0x{targetFunc:X}");

                // Save original bytes (14 bytes needed for a 64-bit absolute jump)
                _savedBytes = new byte[14];
                Marshal.Copy(targetFunc, _savedBytes, 0, 14);

                // Allocate trampoline: original bytes + jump back to targetFunc+14
                _trampolinePtr = VirtualAlloc(IntPtr.Zero, (UIntPtr)64, 0x3000 /* COMMIT|RESERVE */, 0x40 /* RWX */);
                if (_trampolinePtr == IntPtr.Zero)
                {
                    ModLogger.LogError("[DI Hook] VirtualAlloc for trampoline failed");
                    return;
                }

                // Copy saved bytes to trampoline
                Marshal.Copy(_savedBytes, 0, _trampolinePtr, 14);

                // Write jump back: FF 25 00 00 00 00 [8-byte absolute address]
                byte* tramp = (byte*)_trampolinePtr;
                tramp[14] = 0xFF;
                tramp[15] = 0x25;
                tramp[16] = 0x00;
                tramp[17] = 0x00;
                tramp[18] = 0x00;
                tramp[19] = 0x00;
                *(long*)(tramp + 20) = (long)(targetFunc + 14);

                // Store trampoline as our "original" function
                _originalGetDeviceState = _trampolinePtr;

                // Create hook delegate
                _hookDelegate = HookedGetDeviceState;
                IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(_hookDelegate);

                // Patch target function with jump to our hook
                VirtualProtect(targetFunc, (UIntPtr)14, 0x40 /* RWX */, out uint oldProtect);
                byte* target = (byte*)targetFunc;
                target[0] = 0xFF;
                target[1] = 0x25;
                target[2] = 0x00;
                target[3] = 0x00;
                target[4] = 0x00;
                target[5] = 0x00;
                *(long*)(target + 6) = (long)hookPtr;
                VirtualProtect(targetFunc, (UIntPtr)14, oldProtect, out _);

                _diHookInstalled = true;
                ModLogger.Log($"[DI Hook] Inline hook installed! Target=0x{targetFunc:X} Hook=0x{hookPtr:X} Trampoline=0x{_trampolinePtr:X}");

                // Release temp objects
                var releaseDevice = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(*((IntPtr*)deviceVtable + 2));
                releaseDevice(device);
                var releaseDi8 = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(*((IntPtr*)di8Vtable + 2));
                releaseDi8(di8);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[DI Hook] Failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static byte[]? _savedBytes;
        private static IntPtr _trampolinePtr;

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

        private static int _hookCallCount = 0;
        private static int _kbCallCount = 0;
        private static int _injectCount = 0;

        private static unsafe int HookedGetDeviceState(IntPtr self, uint cbData, IntPtr lpvData)
        {
            _hookCallCount++;

            // Call original via trampoline
            var original = Marshal.GetDelegateForFunctionPointer<GetDeviceStateDelegate>(_originalGetDeviceState);
            int hr = original(self, cbData, lpvData);

            // Only inject into keyboard state (256-byte buffer)
            if (hr == 0 && cbData == 256)
            {
                _kbCallCount++;
                if (_injectCKey)
                {
                    byte* buffer = (byte*)lpvData;
                    buffer[DIK_C] = 0x80;
                    _injectCount++;
                }
            }

            return hr;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDeviceDelegate(IntPtr self, ref Guid rguid, out IntPtr lplpDirectInputDevice, IntPtr pUnkOuter);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ReleaseDelegate(IntPtr self);

        // VK codes
        private const int VK_CONTROL = 0x11;
        private const int VK_ENTER = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_LEFT = 0x25;
        private const int VK_RIGHT = 0x27;
        private const int VK_TAB = 0x09;
        private const int VK_F = 0x46;
        private const int VK_T = 0x54;
        private const int VK_Q = 0x51;
        private const int VK_E = 0x45;

        // Legacy constant — prefer KeyDelayClassifier.DelayMsFor(vk) which
        // splits nav vs transition keys. Kept for any remaining callers
        // that want the conservative value directly. 150ms was too fast
        // for PartyMenu grid nav — keys dropped during the ~200-500ms
        // open/transition animations after Escape/Enter. 350ms matches
        // the manual-test pace that worked reliably for those.
        private const int KEY_DELAY = KeyDelayClassifier.TRANSITION_DELAY_MS;

        // Screen state machine — wired by CommandWatcher after construction.
        // SendKey() notifies this via OnKeyPressed() so compound nav helpers
        // (open_eqa, open_character_status, battle_flee, etc.) keep the SM
        // in sync with the actual game state. Without this, the SM stayed
        // at PartyMenu while the nav walked deep into CharacterStatus/EqA.
        public ScreenStateMachine? ScreenMachine { get; set; }

        public NavigationActions(IInputSimulator input, MemoryExplorer explorer, Func<DetectedScreen?> detectScreen)
        {
            _input = input;
            _explorer = explorer;
            _detectScreen = detectScreen;
            _nameTable = new NameTableLookup(explorer);
            _gameWindow = Process.GetCurrentProcess().MainWindowHandle;
        }

        /// <summary>
        /// Execute a high-level action and return the response.
        /// </summary>
        public CommandResponse Execute(CommandRequest command)
        {
            _gameWindow = Process.GetCurrentProcess().MainWindowHandle;
            var response = new CommandResponse
            {
                Id = command.Id,
                ProcessedAt = DateTime.UtcNow.ToString("o"),
                GameWindowFound = _gameWindow != IntPtr.Zero,
            };

            if (!response.GameWindowFound)
            {
                response.Status = "failed";
                response.Error = "Could not find game window handle";
                return response;
            }

            try
            {
                switch (command.Action)
                {
                    case "battle_wait":
                        return BattleWait(response, command);

                    case "battle_flee":
                        return BattleFlee(response);

                    case "navigate":
                        return Navigate(response, command.To ?? "");

                    case "world_travel_to":
                    case "travel_to":
                        return TravelTo(response, command.LocationId);

                    case "confirm_attack":
                        return ConfirmAttack(response);

                    case "move_to":
                        return MoveTo(response, command.To ?? "nearest_enemy", command.LocationId);

                    case "scan_units":
                        return ScanUnits(response);

                    case "scan_move":
                        return ScanMove(response, command);

                    case "auto_move":
                        return AutoMove(response, command);

                    case "test_c_hold":
                        return TestCHold(response);

                    case "get_arrows":
                        return GetArrows(response, command);

                    case "battle_move":
                    case "move_grid":
                        return MoveGrid(response, command);

                    case "battle_attack":
                        return BattleAttack(response, command);

                    case "battle_ability":
                        return BattleAbility(response, command);

                    case "advance_dialogue":
                        return AdvanceDialogue(response);

                    case "save":
                        return SaveGame(response);

                    case "load":
                        return LoadGame(response);

                    case "battle_retry":
                        return BattleRetry(response, formation: false);

                    case "battle_retry_formation":
                        return BattleRetry(response, formation: true);

                    case "buy":
                        return Buy(response, command);

                    case "sell":
                        return Sell(response, command);

                    case "change_job":
                        return ChangeJob(response, command);

                    case "open_eqa":
                        return OpenEqa(response, command.To ?? "Ramza");

                    case "auto_place_units":
                        return AutoPlaceUnits(response);

                    case "open_job_selection":
                        return OpenJobSelection(response, command.To ?? "Ramza");

                    case "open_character_status":
                        return OpenCharacterStatus(response, command.To ?? "Ramza");

                    case "swap_unit_to":
                        return SwapUnitTo(response, command.To ?? "");

                    default:
                        response.Status = "failed";
                        response.Error = $"Unknown navigation action: {command.Action}";
                        return response;
                }
            }
            catch (Exception ex)
            {
                response.Status = "error";
                response.Error = ex.Message;
                return response;
            }
        }

        /// <summary>
        /// battle_wait: Navigate to Wait in action menu, confirm, confirm facing,
        /// then poll until it's a friendly unit's turn again (or game over).
        /// </summary>
        /// <summary>
        /// battle_flee: Open pause menu (Tab), navigate to Return to World Map, confirm.
        /// The pause menu remembers its last cursor position, so we press Up x6 first
        /// to force the cursor back to Units(0), then Down x4 to reach ReturnToWorldMap(4).
        /// </summary>
        private CommandResponse BattleFlee(CommandResponse response)
        {
            // Validate we're in a battle state where flee makes sense.
            var curScreen = _detectScreen();
            if (curScreen != null && (
                curScreen.Name == "BattleFormation" ||
                curScreen.Name == "EncounterDialog" ||
                curScreen.Name == "WorldMap" ||
                curScreen.Name == "PartyMenuUnits"))
            {
                response.Status = "failed";
                response.Error = $"Can't battle_flee from {curScreen.Name}. Start the battle first and try again.";
                response.Screen = curScreen;
                return response;
            }

            SendKey(VK_TAB);
            Thread.Sleep(500);
            // Force cursor to top (Up at top is a no-op, so 6 presses = guaranteed top).
            for (int i = 0; i < 6; i++)
            {
                SendKey(VK_UP);
                Thread.Sleep(100);
            }
            // Now navigate Down x4 to reach ReturnToWorldMap.
            for (int i = 0; i < 4; i++)
            {
                SendKey(VK_DOWN);
                Thread.Sleep(150);
            }
            SendKey(VK_ENTER);
            Thread.Sleep(500);
            SendKey(VK_ENTER);
            Thread.Sleep(1000);
            // Poll until we're on the world map (or timeout after 10s)
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 10000)
            {
                var screen = _detectScreen();
                if (screen != null && (screen.Name == "WorldMap" || screen.Name == "TravelList"))
                {
                    response.Status = "completed";
                    response.Error = $"On {screen.Name} at loc {screen.Location}";
                    return response;
                }
                Thread.Sleep(200);
            }
            response.Status = "completed";
            response.Error = "Fled battle, transition may still be in progress";
            return response;
        }

        /// <summary>
        /// Parse a user-supplied cardinal direction into FacingStrategy's (dx, dy)
        /// convention — FFT grid where +y is south. Accepts N/S/E/W (case-insensitive)
        /// and full names. Returns null when input is null/empty/unrecognized
        /// (caller falls back to auto-pick).
        ///
        /// Live-repro 2026-04-25 Siedge Weald: user requested `battle_wait N`
        /// and Ramza ended up facing East. Root cause — this helper was using
        /// the pre-flip +y=north convention (shipped in `c36ec53` for
        /// FacingDecider.NameFor but not here), so "N" mapped to (0,+1) which
        /// in grid coords is south, and the downstream facing-arrow table
        /// rotated that into another direction via camera rotation. Now
        /// (0,-1)=North matches FacingByteDecoder (byte 2 → North = (0,-1)).
        /// </summary>
        public static (int dx, int dy)? ParseFacingDirection(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var s = input.Trim().ToUpperInvariant();
            return s switch
            {
                "N" or "NORTH" => (0, -1),  // -y = north in FFT grid
                "S" or "SOUTH" => (0, 1),   // +y = south in FFT grid
                "E" or "EAST"  => (1, 0),
                "W" or "WEST"  => (-1, 0),
                _ => null,
            };
        }

        private CommandResponse BattleWait(CommandResponse response, CommandRequest? command = null)
        {
            // Optional explicit facing from command.Pattern ("N"/"S"/"E"/"W" or full
            // "North"/"South"/"East"/"West", case-insensitive). When set, overrides
            // FacingStrategy.ComputeOptimalFacing. Returned as (faceDx, faceDy) in
            // FacingStrategy's convention: (1,0)=E, (-1,0)=W, (0,1)=N, (0,-1)=S.
            (int dx, int dy)? facingOverride = ParseFacingDirection(command?.Pattern);

            // S60 enemy-turn narrator: capture a unit-state snapshot NOW (before
            // we leave BattleMyTurn via the Wait nav). During the poll loop
            // we'll diff against this snap, advance it, and append each
            // batch of events to claude_bridge/live_events.log. The shell
            // helper truncates the log at the START of a battle_wait
            // sequence; the mod always appends so chunked calls don't
            // erase each other's history.
            //
            // Chunked mode: carry the class-level `_narratorPersistentLastSnap`
            // forward across calls so names backfilled in a prior chunk
            // survive into the next one (the scan sometimes drops enemy
            // names mid-battle). Fresh non-chunked call = reset + recapture.
            // ResetNarrator flag forces a fresh capture (shell sets it on
            // the first chunk of a new battle_wait_live sequence to avoid
            // phantom "recovered HP" events carried over from a prior battle).
            List<UnitScanDiff.UnitSnap>? narratorLastSnap;
            if (command?.ResetNarrator == true)
            {
                _narratorPersistentLastSnap = null;
            }
            bool isChunkedContinuation = command?.MaxPollMs != null
                && _narratorPersistentLastSnap != null;
            if (isChunkedContinuation)
            {
                narratorLastSnap = _narratorPersistentLastSnap;
            }
            else
            {
                // Settle delay — the game's memory regions lag slightly after a
                // player action (Phoenix Down, Cure, etc). Without this, the
                // fresh pre-snap can read stale HP values from before the just-
                // completed action, causing the first mid-poll diff to mis-
                // attribute the action's effect as a counter-attack (live-seen:
                // "Ramza countered X for 275 dmg" when Ramza's Phoenix Down
                // actually landed the 275-dmg kill on the player turn).
                //
                // 2026-04-24 bump 200→400ms: basic Attack animations with
                // Chaos Blade on-hit effects (Stone proc + damage number
                // rolls) stretch the static-array settle time beyond 200ms
                // for some action types. Narrator false-positives persisted
                // intermittently at 200ms. 400ms is a conservative bump
                // (still well under a visible-lag threshold for callers)
                // that covers the slowest action animations observed.
                Thread.Sleep(400);
                narratorLastSnap = CaptureCurrentUnitSnapshot();
                _narratorPersistentLastSnap = narratorLastSnap;
            }
            string? narratorActivePlayerName = null;
            if (_lastScannedUnits != null)
            {
                foreach (var u in _lastScannedUnits)
                {
                    if (u.IsActive) { narratorActivePlayerName = u.Name ?? u.JobNameOverride; break; }
                }
            }

            var screen = _detectScreen();

            // Turn-ending abilities (Jump) end the turn immediately — no Wait needed.
            // If we're already on another unit's turn, return success.
            //
            // Skip this early-return in chunked mode (MaxPollMs set): chunked
            // callers deliberately loop through enemy turns and expect the
            // poll to run. Early-returning "completed" would stop the loop
            // before the enemy turn actually finishes.
            if (screen != null
                && command?.MaxPollMs == null
                && (screen.Name == "BattleEnemiesTurn"
                    || screen.Name == "BattleAlliesTurn"))
            {
                response.Status = "completed";
                response.Info = "Turn already ended (ability ended turn automatically)";
                return response;
            }

            // Chunked continuation: when the bridge was called with maxPollMs
            // and the screen is NOT BattleMyTurn, the player's Wait has
            // already been committed on a prior chunked call. Skip the menu
            // nav + facing + Enter entirely and go straight to the poll
            // loop, which handles any terminal screen (Victory/Desertion/
            // GameOver) AND any transient mid-battle screen (banners, casts,
            // TitleScreen flickers) that the normal gate would reject.
            bool chunkedContinuation = command?.MaxPollMs != null
                && screen != null
                && screen.Name != "BattleMyTurn";

            if (!chunkedContinuation)
            {
                // S59: if we're deeper than BattleMyTurn (submenu, pause leak, skillset
                // list, targeting), escape back to a known action-menu state first so
                // the Wait nav below finds the action menu cursor byte.
                int bwEscape = BattleAbilityEntryReset.EscapeCountToMyTurn(screen?.Name);
                if (bwEscape > 0)
                {
                    ModLogger.Log($"[BattleWait] Entry reset: screen={screen?.Name}, Escape×{bwEscape} to reach BattleMyTurn");
                    for (int i = 0; i < bwEscape; i++)
                    {
                        SendKey(VK_ESCAPE);
                        Thread.Sleep(300);
                    }
                    screen = _detectScreen();
                    // Force action-menu cursor to slot 0 (Move) after the
                    // submenu Escape so the wait nav's key-presses count
                    // from a known base. Live-flagged 2026-04-26 P4: a
                    // battle_wait from BattleAbilities triggered a phantom
                    // unintended Move because the cursor's actual menu
                    // position differed from what the nav code assumed.
                    // Mirrors the ExecuteTurn pre-flight cursor reset
                    // (CommandWatcher.cs:4188).
                    if (screen?.Name == "BattleMyTurn")
                    {
                        try
                        {
                            _explorer.Scanner.WriteByte((nint)0x1407FC620, 0);
                            ModLogger.Log("[BattleWait] Forced action-menu cursor to slot 0 (Move) after submenu Escape");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogError($"[BattleWait] Cursor reset write failed: {ex.Message}");
                        }
                    }
                }

                if (screen == null || !BattleWaitLogic.CanStartBattleWait(screen.Name))
                {
                    // Terminal-flicker recovery: BattleVictory / GameOver /
                    // BattleDesertion can transiently surface for ~1-3s
                    // before resolving back to BattleMyTurn (or genuinely
                    // terminal). Settle + recheck before failing — the
                    // ExecuteTurn outer loop has the same pattern at the
                    // sub-step boundary, but errors that fire INSIDE the
                    // wait nav action bypass it. Live-flagged 2026-04-25
                    // playtest: agent saw 3 consecutive ranged attacks
                    // abort with this exact error, each adding 8-15s of
                    // manual recovery dance.
                    if (BattleWaitFlickerRecovery.IsRecoverableFlicker(screen?.Name))
                    {
                        ModLogger.Log($"[BattleWait] Flicker detected at start ({screen?.Name}); settle 800ms + recheck");
                        Thread.Sleep(800);
                        screen = _detectScreen();
                    }
                    if (screen == null || !BattleWaitLogic.CanStartBattleWait(screen.Name))
                    {
                        _menuCursorStale = false;
                        response.Status = "failed";
                        response.Error = $"Cannot battle_wait from screen (current: {screen?.Name ?? "null"})";
                        return response;
                    }
                }
            }

            bool skipMenu = chunkedContinuation || (screen != null && BattleWaitLogic.ShouldSkipMenuNavigation(screen.Name));

            if (skipMenu)
            {
                // After Move+Act, game already transitioned to facing screen.
                // Skip menu navigation entirely — we're already where we need to be.
                ModLogger.Log($"[BattleWait] Auto-facing detected (screen={screen.Name}), skipping menu navigation");
                _menuCursorStale = false;
                Thread.Sleep(150);
            }
            else
            {
                // Normal path: navigate action menu to Wait
                Thread.Sleep(150);

                var cursorResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
                int rawCursor = cursorResult != null ? (int)cursorResult.Value.value : screen.MenuCursor;
                bool hasMoved = screen.BattleMoved == 1 || _menuCursorStale;
                bool hasActed = screen.BattleActed == 1;
                // Detect DontAct (or other action-blocking statuses) on the active
                // unit — the visible Abilities slot is greyed and the cursor
                // auto-skips it. Without this, our nav overshoots into Status.
                bool isDisabled = false;
                if (_lastScannedUnits != null)
                {
                    foreach (var u in _lastScannedUnits)
                    {
                        if (!u.IsActive) continue;
                        if (u.StatusBytes == null || u.StatusBytes.Length < 5) break;
                        // DontAct=byte4 0x04, Sleep=byte4 0x10, Stop=byte3 0x02,
                        // Frog=byte2 0x02, Chicken=byte2 0x04, Petrify=byte1 0x80,
                        // Charm=byte4 0x20, Confusion=byte1 0x10, Berserk=byte2 0x08.
                        // Any of these renders Abilities unselectable.
                        if ((u.StatusBytes[4] & 0x04) != 0 // DontAct
                            || (u.StatusBytes[4] & 0x10) != 0 // Sleep
                            || (u.StatusBytes[3] & 0x02) != 0 // Stop
                            || (u.StatusBytes[2] & 0x02) != 0 // Frog
                            || (u.StatusBytes[2] & 0x04) != 0 // Chicken
                            || (u.StatusBytes[1] & 0x80) != 0 // Petrify
                            || (u.StatusBytes[4] & 0x20) != 0 // Charm
                            || (u.StatusBytes[1] & 0x10) != 0 // Confusion
                            || (u.StatusBytes[1] & 0x08) != 0) // Berserk
                            isDisabled = true;
                        break;
                    }
                }
                int cursor = BattleAbilityNavigation.EffectiveMenuCursor(rawCursor, moved: hasMoved, acted: hasActed, disabled: isDisabled);
                if (cursor != rawCursor)
                    ModLogger.Log($"[BattleWait] Cursor correction: raw={rawCursor} → effective={cursor} (moved={hasMoved}, acted={hasActed}, disabled={isDisabled})");
                _menuCursorStale = false; // consumed
                int target = 2; // Wait

                // DontAct path: the action menu auto-skips greyed Abilities, so
                // navigation press counts AND memory-cursor reads diverge from
                // the non-disabled case. Empirically the menu_cursor byte stays
                // stuck at the pre-disable value while the visible cursor moves
                // 2 slots per Down press (skipping Abilities). Trying to use
                // NavigateMenuCursor's verify-retry compounds the problem —
                // each retry presses Down again, walking the visible cursor
                // past Wait into Status / AutoBattle. Live-flagged 2026-04-26
                // playtest #9: 2 retries pushed the visible cursor 4 slots
                // total, landing on AutoBattle.
                //
                // Workaround: bypass NavigateMenuCursor. Press Escape to force
                // cursor to slot 0 (Move), press Down ONCE (visible jumps
                // 0→2 skipping Abilities), commit Enter via the existing
                // wait-confirm flow below. Skip the verify check.
                if (isDisabled)
                {
                    ModLogger.Log("[BattleWait] Disabled-unit path: Up×4 → Down×1 → Enter (bypassing memory-verified nav)");
                    // Up×4 saturates at slot 0 (Move) regardless of starting
                    // position in the 5-slot menu — Up doesn't wrap. We avoid
                    // Escape because it closes the action menu rather than
                    // resetting the cursor (live-flagged 2026-04-26 #2: the
                    // Escape→Down approach left us in map mode + Enter never
                    // committed Wait, the turn didn't advance).
                    for (int i = 0; i < 4; i++)
                    {
                        SendKey(VK_UP);
                        Thread.Sleep(80);
                    }
                    Thread.Sleep(100);
                    // Down once — game auto-skips greyed Abilities (slot 1),
                    // visible cursor jumps Move (0) → Wait (2).
                    SendKey(VK_DOWN);
                    Thread.Sleep(150);
                    // fall through to the Enter / facing flow below — the
                    // visible cursor is now on Wait per the auto-skip rule.
                }
                else
                {
                    ModLogger.Log($"[BattleWait] Cursor at {cursor}, navigating to {target}");
                    NavigateMenuCursor(cursor, target);
                }

                Thread.Sleep(150);
                var verifyResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
                int actual = verifyResult != null ? (int)verifyResult.Value.value : -1;
                // Skip the verify-retry on the disabled path — memory cursor
                // is unreliable when greyed slots are auto-skipped, and a
                // retry just walks the visible cursor further past Wait.
                if (!isDisabled && BattleWaitLogic.ShouldRetryVerifyAfterNav(
                        initialRaw: rawCursor, correctedCursor: cursor,
                        verifiedRaw: actual, target: target))
                {
                    ModLogger.Log($"[BattleWait] RETRY: cursor at {actual}, expected {target}. Retrying navigation.");
                    NavigateMenuCursor(actual, target);
                    Thread.Sleep(150);
                }
                else if (actual != target)
                {
                    // Verify is untrusted (stale byte, failed read). Skip retry and
                    // trust the initial nav — the Enter press below commits whatever
                    // cursor position the game actually shows.
                    ModLogger.Log($"[BattleWait] Verify read {actual} untrusted (initialRaw={rawCursor} corrected={cursor}); skipping retry.");
                }

                // Press Enter to select Wait — enters the facing screen.
                // Stamp the tick BEFORE the send so a concurrent screen poll
                // that lands between the Enter and the battleMode byte
                // catching up still sees the override window open.
                LastWaitEnterTickMs = Environment.TickCount64;
                SendKey(VK_ENTER);
                // 500ms was conservative; facing-screen render is consistently
                // under 300ms in live logs.
                Thread.Sleep(300);

                // S59: stale-cursor recovery. If Enter landed on Abilities
                // (the most common stale-cursor failure) or AutoBattle, escape
                // and retry with an additional key press toward Wait. Mirrors
                // the MoveGrid recovery pattern.
                var postEnterScreen = _detectScreen();
                if (postEnterScreen?.Name == "BattleAbilities")
                {
                    ModLogger.Log("[BattleWait] Stale-cursor recovery: landed on BattleAbilities, Escape+retry Down to Wait");
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                    SendKey(VK_DOWN);
                    Thread.Sleep(150);
                    SendKey(VK_ENTER);
                    Thread.Sleep(300);
                }
                else if (postEnterScreen?.Name == "BattleAutoBattle")
                {
                    ModLogger.Log("[BattleWait] Stale-cursor recovery: landed on BattleAutoBattle, Escape+retry Up to Wait");
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                    SendKey(VK_UP);
                    Thread.Sleep(150);
                    SendKey(VK_UP);
                    Thread.Sleep(150);
                    SendKey(VK_ENTER);
                    Thread.Sleep(300);
                }
            }

            // Face optimal direction using the rotation detected during the last move.
            // The movement system (battle_move, battle_attack) empirically detects what
            // the Right arrow key does by pressing it and reading the cursor delta.
            // That Right delta maps to a specific rotation in the facing table.
            // The camera doesn't auto-rotate when entering the facing screen, so the
            // rotation from the move is still valid here.
            var ally = _lastScannedUnits?.FirstOrDefault(u => u.Team == 0);
            var enemies = _lastScannedUnits?.Where(u => u.Team == 1 && u.Hp > 0).ToList();
            if (ally != null && enemies != null && enemies.Count > 0 && _lastDetectedRightDelta != null)
            {
                var allyPos = new FacingStrategy.UnitPosition
                {
                    GridX = ally.GridX, GridY = ally.GridY,
                    Team = ally.Team, Hp = ally.Hp, MaxHp = ally.MaxHp
                };
                var enemyPositions = enemies.Select(e => new FacingStrategy.UnitPosition
                {
                    GridX = e.GridX, GridY = e.GridY,
                    Team = e.Team, Hp = e.Hp, MaxHp = e.MaxHp
                }).ToList();

                var (faceDx, faceDy) = facingOverride ?? FacingStrategy.ComputeOptimalFacing(allyPos, enemyPositions);
                if (facingOverride.HasValue)
                    ModLogger.Log($"[BattleWait] Facing override: ({faceDx},{faceDy}) from '{command?.Pattern}'");

                // Look up the Right delta in the facing table to find the facing rotation
                var (rdx, rdy) = _lastDetectedRightDelta.Value;
                int facingRot = FacingStrategy.DeriveRotation(0, rdx, rdy); // 0 = Right key index

                if (facingRot >= 0)
                {
                    string? arrowKey = FacingStrategy.GetFacingArrowKey(facingRot, faceDx, faceDy);
                    if (arrowKey != null)
                    {
                        int vk = arrowKey switch
                        {
                            "Right" => VK_RIGHT, "Left" => VK_LEFT,
                            "Up" => VK_UP, "Down" => VK_DOWN,
                            _ => VK_RIGHT
                        };
                        _input.SendKeyPressToWindow(_gameWindow, vk);
                        Thread.Sleep(200);
                        ModLogger.Log($"[BattleWait] Facing ({faceDx},{faceDy}) rightDelta=({rdx},{rdy}) facingRot={facingRot} key={arrowKey}");
                    }
                }
                else
                {
                    ModLogger.Log($"[BattleWait] Could not derive facing rotation from rightDelta=({rdx},{rdy})");
                }
            }
            else if (_lastDetectedRightDelta == null)
            {
                ModLogger.Log("[BattleWait] No rotation data — accepting default facing");
            }

            // Confirm facing (game prompts "press F to confirm"). Fall back to
            // Enter if F doesn't advance — S56 live-observed: battle_wait sent
            // F while game was on facing-confirm, screen stayed on BattleMoving
            // for the full 120s poll budget. Manual Enter press after that
            // advanced the screen normally, so the facing-confirm accepts
            // either key but F occasionally drops. Enter-fallback closes that.
            SendKey(VK_F);
            // 500ms was pessimistic — the facing confirm animation is short.
            // 300ms still gives the Enter fallback below time to decide.
            Thread.Sleep(300);
            var postFace = _detectScreen();
            if (postFace != null && (postFace.Name == "BattleMoving" || postFace.Name == "BattleAttacking"))
            {
                ModLogger.Log($"[BattleWait] F key didn't advance facing (still {postFace.Name}); retrying with Enter.");
                SendKey(VK_ENTER);
                Thread.Sleep(300);
            }

            // Hold Ctrl for fast-forward — focus-aware (Travel pattern).
            // Global SendInput is what FFT's DirectInput reader requires, but
            // a globally-held Ctrl turns terminal keystrokes into shortcuts
            // when the user tabs away. Release on focus-loss, re-assert on
            // regain. PostMessage path keeps the game-side signal alive.
            bool ctrlHeldGlobally = false;
            if (IsGameForeground())
            {
                SendInputKeyDown(VK_CONTROL);
                ctrlHeldGlobally = true;
            }
            _input.SendKeyDownToWindow(_gameWindow, VK_CONTROL);
            ModLogger.Log($"[BattleWait] Holding Ctrl for fast-forward (globalHeld={ctrlHeldGlobally})");

            // S60 chunked mode: if the caller set command.MaxPollMs, the poll
            // returns "partial" after that window if friendly turn hasn't
            // arrived yet. Default: 120000ms (blocking until friendly turn).
            long maxPollMs = command?.MaxPollMs ?? 120000L;
            bool partialTimeout = false;

            // Poll until it's a friendly unit's turn again (or chunked timeout)
            var sw = Stopwatch.StartNew();
            string lastScreen = "";
            int narratorPollIter = 0;
            try
            {
                while (sw.ElapsedMilliseconds < maxPollMs)
                {
                    // 300ms → 150ms: faster friendly-turn detection. At 300ms we
                    // could be ~300ms late noticing BattleMyTurn after enemy
                    // animations finish. Halving the poll interval cuts that
                    // observation lag. The CPU cost is trivial; _detectScreen
                    // is a cached memory read.
                    Thread.Sleep(150);
                    narratorPollIter++;

                    // Focus-aware Ctrl maintenance: release globally when the
                    // user tabs away so terminal typing isn't hijacked into
                    // shortcuts; re-assert when the game regains focus.
                    bool gameFg = IsGameForeground();
                    if (gameFg && !ctrlHeldGlobally)
                    {
                        SendInputKeyDown(VK_CONTROL);
                        ctrlHeldGlobally = true;
                        ModLogger.Log("[BattleWait] Re-asserted Ctrl (game regained focus)");
                    }
                    else if (!gameFg && ctrlHeldGlobally)
                    {
                        SendInputKeyUp(VK_CONTROL);
                        ctrlHeldGlobally = false;
                        ModLogger.Log("[BattleWait] Released global Ctrl (user tabbed away)");
                    }

                    // S60 narrator: every ~450ms (every 3rd 150ms tick) capture a
                    // fresh unit snapshot, diff against the last, and append any
                    // change events (plus counter/self-destruct inferences) to
                    // live_events.log. Each iteration is at most one memory read
                    // + one short write, so poll cadence stays close to 150ms
                    // per iteration in the common "no change" case.
                    if (narratorPollIter % 3 == 0)
                    {
                        EmitNarrationBatch(ref narratorLastSnap, narratorActivePlayerName);
                    }

                    var current = _detectScreen();
                    if (current == null) continue;

                    if (current.Name != lastScreen)
                    {
                        ModLogger.Log($"[BattleWait] Screen: {current.Name} team={current.BattleTeam} act={current.BattleActed} mov={current.BattleMoved}");
                        lastScreen = current.Name;
                    }

                    // If we hit BattlePaused due to stale flag, send Escape to clear
                    if (current.Name == "BattlePaused")
                    {
                        SendKey(VK_ESCAPE);
                        Thread.Sleep(300);
                        continue;
                    }

                    // Mid-battle banners that need a single Enter to dismiss.
                    // S45 shipped detection for these; S56 live-observed that
                    // battle_wait's 120s poll never advanced when a unit's
                    // queued spell triggered BattleAbilityLearnedBanner during
                    // the enemy-turns wait. Send Enter to dismiss and keep
                    // polling.
                    if (current.Name == "BattleAbilityLearnedBanner"
                        || current.Name == "BattleRewardObtainedBanner")
                    {
                        ModLogger.Log($"[BattleWait] Dismissing {current.Name}");
                        SendKey(VK_ENTER);
                        Thread.Sleep(300);
                        continue;
                    }

                    // Friendly unit's turn — we're done. BattleMyTurn is the
                    // clean action-menu state. Also accept BattleActing (unit
                    // post-move, action pending) since auto-acted allies (e.g.
                    // Auto-battle setting, or Haste-driven double turns) can
                    // reach a player turn that's already mid-action when our
                    // poll catches it.
                    if (current.Name == "BattleMyTurn" || current.Name == "BattleActing")
                    {
                        // Confirm-modal escape: if BattleActing pops a
                        // Yes/No/Confirm/Cancel modal (e.g. "Reset position?"
                        // or a left-over crystal-move confirm), the wait
                        // would otherwise return with the modal stuck on
                        // screen and follow-up commands all see [BattleActing]
                        // 30s+ until manual rescue. Send Escape to dismiss
                        // the modal then re-detect once. Live-flagged
                        // 2026-04-25 P2 playtest, hit twice.
                        if (current.Name == "BattleActing")
                        {
                            // Check whether a modal is open by sending Escape
                            // and seeing if the screen advances. If it does
                            // (BattleActing → BattleMyTurn), the modal was
                            // present and is now gone.
                            SendKey(VK_ESCAPE);
                            Thread.Sleep(300);
                            var postEsc = _detectScreen();
                            if (postEsc?.Name == "BattleMyTurn")
                            {
                                ModLogger.Log("[BattleWait] BattleActing modal dismissed via Escape; landed on BattleMyTurn");
                                current = postEsc;
                            }
                            // If still BattleActing after Escape, it's the
                            // legitimate "post-move action pending" case;
                            // proceed to exit normally.
                        }

                        response.Info = $"Friendly turn after {sw.ElapsedMilliseconds}ms (screen={current.Name})";

                        // S60 narrator: final catch-up — captures events that
                        // landed after the last 450ms poll tick before we exit.
                        EmitNarrationBatch(ref narratorLastSnap, narratorActivePlayerName);
                        break;
                    }

                    // Game over
                    if (current.Name == "GameOver")
                    {
                        response.Error = "Game Over";
                        break;
                    }

                    // Battle ended — Victory/Desertion screens are their own
                    // terminus; the post-loop cleanup below handles Desertion
                    // auto-dismiss. Exit the poll so we don't timeout waiting
                    // for a friendly turn that will never come.
                    if (current.Name == "BattleVictory"
                        || current.Name == "BattleDesertion")
                    {
                        response.Info = $"Battle ended: {current.Name} after {sw.ElapsedMilliseconds}ms";
                        // S60: narrator persistent snap belongs to THIS battle;
                        // clear it so the next battle starts fresh and we don't
                        // emit phantom "recovered HP" / "lost status" events
                        // from a stale prior-battle snapshot.
                        _narratorPersistentLastSnap = null;
                        break;
                    }
                }

                if (sw.ElapsedMilliseconds >= maxPollMs)
                {
                    if (maxPollMs < 120000L)
                    {
                        // Chunked mode: poll window elapsed without seeing a
                        // friendly turn. Return "partial" so the caller knows
                        // to call battle_wait again. Any narration accumulated
                        // in this window is already in live_events.log.
                        partialTimeout = true;
                        response.Info = $"Still waiting ({sw.ElapsedMilliseconds}ms elapsed, no friendly turn yet)";

                        // S60 narrator: final catch-up for chunked timeout —
                        // capture any events after the last 450ms narrator tick
                        // so the chunk's log is complete before returning.
                        EmitNarrationBatch(ref narratorLastSnap, narratorActivePlayerName);
                    }
                    else
                    {
                        response.Error = "Timeout waiting for friendly turn (120s)";
                    }
                }
            }
            finally
            {
                // Always release Ctrl on every exit path (break, throw, timeout).
                if (ctrlHeldGlobally) SendInputKeyUp(VK_CONTROL);
                _input.SendKeyUpToWindow(_gameWindow, VK_CONTROL);
                ModLogger.Log("[BattleWait] Turn wait complete (Ctrl released)");
            }

            // Auto-dismiss post-battle screens (Victory auto-advances, Desertion needs Enter)
            var postScreen = _detectScreen();
            if (postScreen?.Name == "BattleDesertion")
            {
                ModLogger.Log("[BattleWait] Dismissing Desertion warning");
                // May have multiple warnings (one per unit near threshold)
                for (int i = 0; i < 5; i++)
                {
                    SendKey(VK_ENTER);
                    Thread.Sleep(500);
                    postScreen = _detectScreen();
                    if (postScreen?.Name != "BattleDesertion") break;
                }
            }

            response.Status = partialTimeout ? "partial" : "completed";
            return response;
        }

        /// <summary>
        /// battle_attack: Navigate to Abilities→Attack, enter targeting mode,
        /// empirically detect rotation, navigate cursor to target tile, confirm.
        /// Usage: {"action":"battle_attack","locationId":x,"unitIndex":y}
        /// </summary>
        private CommandResponse BattleAttack(CommandResponse response, CommandRequest command)
        {
            int targetX = command.LocationId;
            int targetY = command.UnitIndex;

            // Catch the foot-gun: caller sent {"x":8,"y":11} instead of
            // {"locationId":8,"unitIndex":11} — defaults are -1, 0 which
            // would fall through to the nav loop and surface a confusing
            // "Cursor miss: at (0,0) expected (-1,0)" error.
            var argError = TileTargetValidator.Validate(targetX, targetY, "battle_attack");
            if (argError != null)
            {
                response.Status = "failed";
                response.Error = argError;
                return response;
            }

            // Pre-flight: refuse during another team's turn (Escape pauses
            // the game). See SendKey guard for the same defense-in-depth.
            var preflightAtk = _detectScreen();
            if (preflightAtk != null
                && (preflightAtk.Name == "BattleEnemiesTurn" || preflightAtk.Name == "BattleAlliesTurn"))
            {
                response.Status = "failed";
                response.Error = $"Not your turn (current: {preflightAtk.Name}). Wait for BattleMyTurn before attacking.";
                response.Screen = preflightAtk;
                return response;
            }

            // Up-front range validation against the cached attack tile set
            // from the most recent scan_move. Without this, an out-of-range
            // battle_attack opens the targeting mode, the cursor navigates
            // wherever the requested coords are (including off-grid), and
            // the result is "MISSED" or a confusing nav error instead of a
            // clear "out of range" rejection. battle_move has the same up-
            // front guard via _lastValidMoveTiles. Skip when no scan has
            // populated the cache yet — the nav loop will surface its own
            // error in that path.
            if (_lastValidAttackTiles != null
                && _lastValidAttackTiles.Count > 0
                && !_lastValidAttackTiles.Contains((targetX, targetY)))
            {
                response.Status = "failed";
                response.Error = $"Tile ({targetX},{targetY}) is not in basic-Attack range. Run scan_move first and pick from the active unit's Attack ValidTargetTiles ({_lastValidAttackTiles.Count} valid tiles).";
                return response;
            }

            var screen = WaitForTurnState(timeoutMs: 1000, out long waitedMs);

            // S59: allow recoverable battle-menu states (submenu, pause leak,
            // skillset list, targeting) — the entry reset below escapes back
            // to BattleMyTurn. Mirrors the battle_ability relaxation.
            bool isRecoverable = screen != null
                && BattleAbilityEntryReset.IsResetableBattleScreen(screen.Name);
            if (screen == null
                || (screen.Name != "BattleMyTurn" && screen.Name != "BattleActing" && !isRecoverable))
            {
                response.Status = "failed";
                response.Error = $"Not on BattleMyTurn/BattleActing (current: {screen?.Name ?? "null"}) after {waitedMs}ms wait";
                return response;
            }

            // Guard: one Action per turn. Refuse up-front so callers know to Wait
            // instead of retry-spamming — stops us from navigating into a grayed
            // Abilities menu and stalling. Only reliable on the action menu.
            if ((screen.Name == "BattleMyTurn" || screen.Name == "BattleActing")
                && screen.BattleActed == 1)
            {
                response.Status = "failed";
                response.Error = "You've already acted this turn. You cannot perform another action.";
                return response;
            }

            // S59: escape-to-known-state if we're deeper than BattleMyTurn.
            int baEscape = BattleAbilityEntryReset.EscapeCountToMyTurn(screen.Name);
            if (baEscape > 0)
            {
                ModLogger.Log($"[BattleAttack] Entry reset: screen={screen.Name}, Escape×{baEscape}");
                for (int i = 0; i < baEscape; i++)
                {
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                }
                screen = _detectScreen();
                if (screen == null || (screen.Name != "BattleMyTurn" && screen.Name != "BattleActing"))
                {
                    response.Status = "failed";
                    response.Error = $"Escape-to-known-state failed: expected BattleMyTurn/BattleActing after {baEscape} Escapes, got {screen?.Name ?? "null"}";
                    return response;
                }
                // Re-check the Act-consumed guard after escape. Without this,
                // retrying battle_attack from a post-action recoverable state
                // (BattleMoving / BattleAttacking) would escape back to
                // BattleMyTurn, pass through the guard above (which only fired
                // on initial BattleMyTurn/BattleActing entry), and then fail
                // silently deep in the targeting flow with "Failed to enter
                // targeting mode" once the menu Abilities slot turned out
                // to be grayed. Live-surfaced battle-play gap.
                if (screen.BattleActed == 1)
                {
                    response.Status = "failed";
                    response.Error = "Act already used this turn — only Move or Wait remain.";
                    return response;
                }
            }

            // Step 1: Navigate menu to Abilities (always index 1).
            // Menu is stable: Move/ResetMove(0) Abilities(1) Wait(2) Status(3) AutoBattle(4)
            // Trust the raw memory cursor — EffectiveMenuCursor corrections cause more bugs.
            var cursorResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
            int cursor = cursorResult != null ? (int)cursorResult.Value.value : screen.MenuCursor;
            NavigateMenuCursor(cursor, 1);
            SendKey(VK_ENTER); // Open Abilities submenu
            Thread.Sleep(300);

            // S58: verify Abilities submenu actually opened. If a charging
            // confirmation modal was pending (game shows "Cancel charging
            // spell?" when a unit with Charging status comes up), that first
            // Enter dismisses the modal instead of opening Abilities — and
            // we're still on BattleMyTurn. Retry the Enter once.
            var postOpenScreen = _detectScreen();
            if (postOpenScreen != null
                && postOpenScreen.Name != "BattleAbilities"
                && postOpenScreen.Name != "BattleAttacking"
                && (postOpenScreen.Name == "BattleMyTurn" || postOpenScreen.Name == "BattleActing"))
            {
                ModLogger.Log($"[BattleAttack] Abilities submenu didn't open (still {postOpenScreen.Name}); retry Enter");
                SendKey(VK_ENTER);
                Thread.Sleep(300);
            }

            // Step 2: Force submenu cursor to Attack (index 0).
            // The Abilities submenu REMEMBERS the previous selection within a turn (e.g.
            // after Escape from Martial Arts, re-entering lands on Martial Arts, not
            // Attack). Resolve actual position via ui= then Up-to-top so blind Enter
            // always picks Attack. See feedback_submenu_sticky_cursor.md.
            var submenuItemsAtk = GetAbilitiesSubmenuItems?.Invoke() ?? new[] { "Attack" };
            string? uiSubmenu = _detectScreen()?.UI;
            int submenuCursor = 0;
            if (uiSubmenu != null)
            {
                int uiIdx = BattleAbilityNavigation.FindSkillsetIndex(uiSubmenu, submenuItemsAtk);
                if (uiIdx >= 0)
                {
                    // ui= lags by one keypress: shows where cursor WAS. Actual = next item.
                    submenuCursor = (uiIdx + 1) % submenuItemsAtk.Length;
                }
            }
            // Up (wrapping) gets us to Attack (index 0) in `submenuCursor` presses.
            // If already at 0, no presses needed. If at 3 of 5, press Up 3 times.
            int upsNeeded = submenuCursor;
            ModLogger.Log($"[BattleAttack] Submenu cursor at {submenuCursor} ('{(submenuCursor < submenuItemsAtk.Length ? submenuItemsAtk[submenuCursor] : "?")}'), pressing Up x{upsNeeded}");
            for (int i = 0; i < upsNeeded; i++)
            {
                SendKey(VK_UP);
                // Matches ability-list Down (150ms) — 300ms was outlier.
                Thread.Sleep(150);
            }

            // Now select Attack (top item in submenu)
            SendKey(VK_ENTER);
            // 500ms was conservative; BattleAttacking transition is short.
            Thread.Sleep(300);

            // S58: poll up to 1500ms for the BattleAbilities → BattleAttacking
            // transition. Live-observed: the initial 300ms sleep sometimes
            // lands while the game is still rendering the submenu close
            // animation; a single Enter retry bumps past it. Previously
            // this bailed with "Failed to enter targeting mode" after one
            // look, forcing the caller to Escape + retry manually.
            screen = _detectScreen();
            int transitionRetry = 0;
            var transitionSw = Stopwatch.StartNew();
            while (transitionSw.ElapsedMilliseconds < 1500
                && (screen == null || screen.Name != "BattleAttacking"))
            {
                if (screen != null && screen.Name == "BattleAbilities" && transitionRetry == 0)
                {
                    // One retry Enter — handles the "submenu still open"
                    // race where the first Enter didn't register.
                    ModLogger.Log("[BattleAttack] Still on BattleAbilities — retry Enter");
                    SendKey(VK_ENTER);
                    transitionRetry++;
                    Thread.Sleep(200);
                }
                else
                {
                    Thread.Sleep(80);
                }
                screen = _detectScreen();
            }

            if (screen == null || screen.Name != "BattleAttacking")
            {
                response.Status = "failed";
                response.Error = $"Failed to enter targeting mode (current: {screen?.Name ?? "null"})";
                EscapeToMyTurn();
                return response;
            }

            // Step 3: Read starting cursor position
            var startPos = ReadGridPos();
            ModLogger.Log($"[BattleAttack] Targeting mode, cursor at ({startPos.x},{startPos.y}), target ({targetX},{targetY})");

            int deltaX = targetX - startPos.x;
            int deltaY = targetY - startPos.y;

            if (deltaX == 0 && deltaY == 0)
            {
                // Already on target — confirm
                SendKey(VK_ENTER);
                Thread.Sleep(300);
                SendKey(VK_ENTER);
                Thread.Sleep(300);
                response.Status = "completed";
                response.Info = $"Attacked ({targetX},{targetY}) — cursor was already on target";
                return response;
            }

            // Step 4: Empirical rotation detection — press Right, read delta
            _input.SendKeyPressToWindow(_gameWindow, VK_RIGHT);
            Thread.Sleep(150);
            var testPos = ReadGridPos();
            int rdx = testPos.x - startPos.x;
            int rdy = testPos.y - startPos.y;

            if (rdx == 0 && rdy == 0)
            {
                // Right didn't move (at boundary) — try Down
                _input.SendKeyPressToWindow(_gameWindow, VK_DOWN);
                Thread.Sleep(150);
                testPos = ReadGridPos();
                int ddx = testPos.x - startPos.x;
                int ddy = testPos.y - startPos.y;
                // Undo Down
                _input.SendKeyPressToWindow(_gameWindow, VK_UP);
                Thread.Sleep(150);

                if (ddx != 0 || ddy != 0)
                {
                    // Down = 90° CW from Right → Right = (-ddy, ddx)
                    rdx = -ddy;
                    rdy = ddx;
                    _lastDetectedRightDelta = (rdx, rdy);
                    ModLogger.Log($"[BattleAttack] Rotation from Down: ({ddx},{ddy}) → Right=({rdx},{rdy})");
                }
                else if (_lastDetectedRightDelta != null)
                {
                    // Fallback: use cached rotation from previous move/attack this turn
                    (rdx, rdy) = _lastDetectedRightDelta.Value;
                    ModLogger.Log($"[BattleAttack] Rotation fallback (cached): Right=({rdx},{rdy})");
                }
                else
                {
                    // Last resort: read camera rotation from memory
                    var camResult = _explorer.ReadAbsolute((nint)AddrCameraRotation, 1);
                    if (camResult != null)
                    {
                        (rdx, rdy) = AttackDirectionLogic.RightDeltaFromCameraRotation((int)camResult.Value.value);
                        _lastDetectedRightDelta = (rdx, rdy);
                        ModLogger.Log($"[BattleAttack] Rotation fallback (camera): raw={camResult.Value.value} → Right=({rdx},{rdy})");
                    }
                    else
                    {
                        response.Status = "failed";
                        response.Error = "Could not detect rotation — cursor didn't move";
                        EscapeToMyTurn();
                        return response;
                    }
                }
            }
            else
            {
                // Undo the Right press
                _input.SendKeyPressToWindow(_gameWindow, VK_LEFT);
                Thread.Sleep(150);
                _lastDetectedRightDelta = (rdx, rdy);
                ModLogger.Log($"[BattleAttack] Rotation: Right=({rdx},{rdy})");
            }

            // Step 5: Compute arrow key for target delta
            string? arrowName = AttackDirectionLogic.ComputeArrowForDelta(rdx, rdy, deltaX, deltaY);
            if (arrowName == null)
            {
                // Target isn't one arrow press away — need multi-step navigation
                // Navigate X then Y, same as move_grid
                NavigateGrid(deltaX, deltaY, FindRotationFromRight(rdx, rdy));
            }
            else
            {
                int vk = arrowName switch
                {
                    "Right" => VK_RIGHT,
                    "Left" => VK_LEFT,
                    "Up" => VK_UP,
                    "Down" => VK_DOWN,
                    _ => 0
                };
                if (vk != 0)
                {
                    _input.SendKeyPressToWindow(_gameWindow, vk);
                    Thread.Sleep(150);
                }
            }

            // Step 6: Verify cursor is on target
            var finalPos = ReadGridPos();
            if (finalPos.x != targetX || finalPos.y != targetY)
            {
                ModLogger.Log($"[BattleAttack] WARN: cursor at ({finalPos.x},{finalPos.y}), expected ({targetX},{targetY})");
                response.Error = $"Cursor miss: at ({finalPos.x},{finalPos.y}) expected ({targetX},{targetY})";
                EscapeToMyTurn();
                response.Status = "failed";
                return response;
            }

            // Step 7: Read target's pre-attack HP, level, and damage preview
            int preHp = ReadStaticArrayHpAt(targetX, targetY);
            int targetMaxHp = ReadStaticArrayMaxHpAt(targetX, targetY);
            int targetLevel = ReadStaticArrayFieldAt(targetX, targetY, 0x0D) & 0xFF; // level is 1 byte
            // Damage preview projection (projDamage, projHitPct) is NOT wired up.
            // Session 30 hunt confirmed the preview widget data is not colocated with
            // any (attacker MaxHP+MaxHP) or (target HP+MaxHP) pattern in 0x140xxx,
            // 0x141xxx, 0x15Axxx, or 0x4166xxx regions. See
            // memory/project_damage_preview_hunt_s30.md. Post-attack delta from
            // ReadLiveHp is the ground-truth damage signal.
            int projDamage = 0, projHitPct = 0;
            ModLogger.Log($"[BattleAttack] Pre-attack: target HP={preHp}/{targetMaxHp} lv={targetLevel}");

            // Step 8: Confirm attack — Enter (select target) + Enter (confirm "Target this tile?")
            SendKey(VK_ENTER);
            Thread.Sleep(300);
            SendKey(VK_ENTER);

            // Step 9: Wait for animation, then read live HP from readonly memory.
            // The static array is stale mid-turn, but SearchBytesAllRegions finds
            // live copies in readonly regions (0x141xxx, 0x15Axxx) that update immediately.
            response.Status = "completed";
            // Previously Thread.Sleep(2000) — pessimistic fixed wait for the
            // attack animation. Poll for a post-animation resolved state
            // instead: BattleMoving (facing confirm) / BattleActing / the
            // re-targeting BattleAttacking / BattleVictory / GameOver. Cap
            // at 2500ms so extra-long animations still complete.
            // Minimum 300ms floor lets the animation actually start before
            // we start polling (otherwise we'd see the pre-animation state).
            Thread.Sleep(300);
            var animSw = Stopwatch.StartNew();
            while (animSw.ElapsedMilliseconds < 2200)
            {
                var s = _detectScreen();
                if (s != null && (s.Name == "BattleMoving"
                    || s.Name == "BattleActing" || s.Name == "BattleAttacking"
                    || s.Name == "BattleVictory" || s.Name == "GameOver"))
                {
                    break;
                }
                Thread.Sleep(100);
            }
            int liveHp = ReadLiveHp(targetMaxHp, preHp, targetLevel);
            int staticHpAtk = ReadStaticArrayHpAt(targetX, targetY);
            int postHp = liveHp;
            // Dual-read defense: ReadLiveHp's heap fingerprint search can
            // collide with another unit's struct and report fabricated
            // damage when the actual attack missed. Cross-check against
            // the static array slot at the target tile; prefer static
            // when live disagrees by more than half MaxHp. Same pattern
            // as the X-Potion phantom-heal fix in BattleAbility (S60),
            // applied to basic Attack here. Live-flagged 2026-04-25 P2:
            // `battle_attack 2 6` reported HIT (318→274/318) but the
            // Summoner was unchanged at 318/318 in the next scan.
            //
            // 2026-04-26: skip the override when live==0 and preHp>0.
            // That's the KO signal — live correctly read the dead
            // unit's struct at HP=0 while the static array hasn't
            // refreshed yet (still showing pre-attack HP). Trust live=0
            // over the stale static here. Live-flagged at Siedge Weald:
            // Skeleton KO'd but reported MISSED because static=85
            // overrode live=0.
            bool koSignal = liveHp == 0 && preHp > 0;
            if (!koSignal
                && staticHpAtk >= 0 && staticHpAtk <= targetMaxHp
                && System.Math.Abs(liveHp - staticHpAtk) > targetMaxHp / 2)
            {
                ModLogger.Log($"[BattleAttack] HP mismatch: live={liveHp} static={staticHpAtk} — preferring static");
                postHp = staticHpAtk;
            }
            ModLogger.Log($"[BattleAttack] Post-attack: live HP={liveHp} static={staticHpAtk} chose={postHp} (was {preHp})");

            // S56 live-observed: after an attack MISS, the game re-opens the
            // attack-targeting screen asking the player to pick another target.
            // A successful HIT/KO advances directly to the facing-confirm
            // (BattleMoving). The post-animation screen state is the
            // authoritative hit/miss signal — ReadLiveHp's heap fingerprint
            // search can fall back to preHp on a hit, which previously caused
            // false MISSED reports.
            var postAttack = _detectScreen();
            string? postName = postAttack?.Name;
            // Terminal-flicker tolerance: BattleVictory / GameOver /
            // BattleDesertion can transiently flash mid-attack even when
            // enemies remain alive (live-flagged 2026-04-26 P4: Lloyd's
            // Blaze Gun shot reported BattleVictory while 2 enemies still
            // standing). Settle and recheck up to 3×500ms before
            // trusting a terminal classification.
            if (postName == "BattleVictory" || postName == "GameOver"
                || postName == "BattleDesertion")
            {
                for (int recheck = 0; recheck < 3; recheck++)
                {
                    Thread.Sleep(500);
                    var s = _detectScreen();
                    if (s?.Name != null && s.Name != "BattleVictory"
                        && s.Name != "GameOver" && s.Name != "BattleDesertion")
                    {
                        ModLogger.Log($"[BattleAttack] Terminal-state flicker resolved: {postName} → {s.Name}");
                        postAttack = s;
                        postName = s.Name;
                        break;
                    }
                }
            }
            // BattleAttacking-flicker tolerance: on a HIT or KO, the
            // engine sometimes briefly leaves the targeting screen up
            // before advancing to BattleMoving (facing-confirm). The
            // existing wait loop breaks as soon as it sees ANY of the
            // accepted post-states, so it can latch onto the transient
            // BattleAttacking and the classifier reports MISSED for an
            // attack that landed. Live-flagged 2026-04-26 (twice):
            // Goblin and Skeleton both KO'd but reported MISSED.
            // Resolve by re-settling once more — if the screen advances
            // to BattleMoving / BattleActing, that's the real outcome.
            // Real misses stay at BattleAttacking and fall through to
            // the existing Miss classification.
            if (postName == "BattleAttacking")
            {
                Thread.Sleep(500);
                var s = _detectScreen();
                if (s?.Name == "BattleMoving" || s?.Name == "BattleActing"
                    || s?.Name == "BattleVictory")
                {
                    ModLogger.Log($"[BattleAttack] BattleAttacking-flicker resolved: {postName} → {s.Name}");
                    postAttack = s;
                    postName = s.Name;
                    // Re-read postHp now that the engine has settled —
                    // the post-KO struct teardown often completes during
                    // this 500ms window, so a stale or unreadable HP
                    // becomes a real 0 / static-array-cleared signal.
                    int reLiveHp = ReadLiveHp(targetMaxHp, preHp, targetLevel);
                    int reStaticHp = ReadStaticArrayHpAt(targetX, targetY);
                    if (reLiveHp >= 0 || reStaticHp >= 0)
                    {
                        liveHp = reLiveHp;
                        staticHpAtk = reStaticHp;
                        postHp = reLiveHp >= 0 ? reLiveHp : reStaticHp;
                    }
                }
            }
            // Pin the resolved screen on the response so the outer
            // ProcessCommand wrapper's `response.Screen ??= DetectScreenSettled(...)`
            // doesn't re-read the screen (and potentially re-catch the
            // flicker we just settled away). 2026-04-26 P5: agent saw
            // [BattleVictory] mid-battle even after this recheck because
            // the wrapper read again. Pinning closes the gap.
            response.Screen = postAttack;
            // When BOTH ReadLiveHp and the static-array-tile read fail,
            // the target's struct has likely been recycled (post-KO
            // teardown) — feed that signal into the classifier so a
            // BattleAttacking-flicker-with-unreadable-HP gets correctly
            // promoted to Ko instead of falling through to Miss.
            bool targetGone = (liveHp < 0 && staticHpAtk < 0);
            var outcome = AttackOutcomeClassifier.Classify(postName, preHp, postHp, targetGone);

            // Still send Escape to back out of miss re-targeting; the action
            // is consumed but the menu is open.
            if (postName == "BattleAttacking")
            {
                ModLogger.Log("[BattleAttack] Still on BattleAttacking post-animation (miss re-targeting); sending Escape");
                SendKey(VK_ESCAPE);
                Thread.Sleep(300);
            }

            string dmgStr = projDamage > 0 ? $" ({projDamage} dmg, {projHitPct}% hit)" : "";
            string from = $"from ({startPos.x},{startPos.y})";
            string at = $"({targetX},{targetY})";
            bool hpKnown = postHp >= 0 && postHp != preHp;
            switch (outcome)
            {
                case AttackOutcome.Ko:
                    response.Info = hpKnown
                        ? $"Attacked {at} {from} — KO'd!{dmgStr} ({preHp}→0/{targetMaxHp})"
                        : $"Attacked {at} {from} — KO'd!{dmgStr}";
                    break;
                case AttackOutcome.Hit:
                    response.Info = hpKnown
                        ? $"Attacked {at} {from} — HIT{dmgStr} ({preHp}→{postHp}/{targetMaxHp})"
                        : $"Attacked {at} {from} — HIT{dmgStr} (damage unread)";
                    break;
                case AttackOutcome.Miss:
                    response.Info = $"Attacked {at} {from} — MISSED!{dmgStr}";
                    break;
                default:
                    response.Info = hpKnown
                        ? $"Attacked {at} {from}{dmgStr} ({preHp}→{postHp}/{targetMaxHp})"
                        : $"Attacked {at} {from}{dmgStr} (outcome unread)";
                    break;
            }
            ModLogger.Log($"[BattleAttack] {response.Info}");

            // S58: record damage/kill into stat tracker. Classifier handles
            // read-failure / overkill / miss edge cases.
            RecordAttackStats("Attack", startPos, targetX, targetY, preHp, postHp);

            // Populate PostAction with the CASTER's position (didn't move
            // during the attack) so the formatter doesn't mix the target
            // tile with caster HP. Without this, ReadGridPos() returns the
            // target tile (cursor sat there at confirm) and the response
            // line reads "→ (targetX,targetY) HP=casterHP" which misleads
            // any caller tracking enemy HP from the response payload.
            response.PostAction = ReadPostActionState(startPos.x, startPos.y);

            return response;
        }

        /// <summary>S58: snapshot the last-scanned battle roster as plain
        /// UnitSnapshot records for execute_turn kill-diff. Returns null
        /// when no scan is cached.</summary>
        public List<UnitSnapshot>? LastScannedUnitSnapshots()
        {
            if (_lastScannedUnits == null || _lastScannedUnits.Count == 0) return null;
            var result = new List<UnitSnapshot>(_lastScannedUnits.Count);
            foreach (var u in _lastScannedUnits)
            {
                var label = UnitDisplayName.For(u.Name, u.JobNameOverride);
                result.Add(new UnitSnapshot(label, u.JobNameOverride, u.Team, u.Hp, u.MaxHp));
            }
            return result;
        }

        /// <summary>S58: resolve active unit's name from the last scan
        /// for stat-tracker calls. Falls back to JobNameOverride for
        /// enemies (whose Name is null) so stats read "Minotaur" instead
        /// of literal "(unknown)".</summary>
        private string GetActiveUnitNameForStats()
        {
            var u = _lastScannedUnits?.FirstOrDefault(u => u.IsActive);
            return UnitDisplayName.For(u?.Name, u?.JobNameOverride);
        }

        /// <summary>
        /// S58: convert a post-attack HP delta into BattleStatTracker hook
        /// calls. No-op if the tracker isn't wired. Looks up attacker and
        /// target names from the last scan snapshot.
        /// </summary>
        private void RecordAttackStats(string ability, (int x, int y) startPos, int targetX, int targetY, int preHp, int postHp)
        {
            if (StatTracker == null) return;
            var ev = HpTransitionClassifier.Classify(preHp, postHp);
            if (ev == HpTransitionEvent.None) return;

            var attackerUnit = _lastScannedUnits?
                .FirstOrDefault(u => u.GridX == startPos.x && u.GridY == startPos.y);
            var targetUnit = _lastScannedUnits?
                .FirstOrDefault(u => u.GridX == targetX && u.GridY == targetY);
            string attacker = UnitDisplayName.For(attackerUnit?.Name, attackerUnit?.JobNameOverride);
            string target = UnitDisplayName.For(targetUnit?.Name, targetUnit?.JobNameOverride);

            int amount = HpTransitionClassifier.Magnitude(preHp, postHp);
            switch (ev)
            {
                case HpTransitionEvent.Damage:
                    StatTracker.OnDamageDealt(attacker, target, amount, ability);
                    break;
                case HpTransitionEvent.Kill:
                    StatTracker.OnDamageDealt(attacker, target, amount, ability);
                    StatTracker.OnKill(attacker, target);
                    break;
                case HpTransitionEvent.Heal:
                    StatTracker.OnHeal(attacker, target, amount, ability);
                    break;
                case HpTransitionEvent.Raise:
                    StatTracker.OnRaise(attacker, target);
                    break;
            }
        }

        /// <summary>
        /// Derive the rotation index (0-3) from empirical Right delta.
        /// Used to bridge AttackDirectionLogic with NavigateGrid.
        /// </summary>
        private int FindRotationFromRight(int rdx, int rdy)
        {
            for (int r = 0; r < 4; r++)
            {
                var (dx, dy) = ArrowGridDelta[r, 0]; // index 0 = Right
                if (dx == rdx && dy == rdy) return r;
            }
            return 0;
        }

        /// <summary>
        /// battle_ability: Navigate to Abilities submenu, select a skillset + ability, confirm.
        /// For self-targeting abilities (Shout, Focus): just confirms.
        /// For targeted abilities: navigates cursor to target tile and confirms.
        /// Usage: {"action":"battle_ability","description":"Shout"} (self-target)
        /// Usage: {"action":"battle_ability","description":"Throw Stone","locationId":4,"unitIndex":8} (targeted)
        /// </summary>
        private CommandResponse BattleAbility(CommandResponse response, CommandRequest command)
        {
            string? abilityName = command.Description;
            if (string.IsNullOrEmpty(abilityName))
            {
                response.Status = "failed";
                response.Error = "Missing ability name in 'description' field";
                return response;
            }

            // If it's "Attack", delegate to BattleAttack
            if (abilityName == "Attack")
                return BattleAttack(response, command);

            // Pre-flight: refuse during another team's turn (Escape pauses
            // the game). See SendKey guard for the same defense-in-depth.
            var preflightAbi = _detectScreen();
            if (preflightAbi != null
                && (preflightAbi.Name == "BattleEnemiesTurn" || preflightAbi.Name == "BattleAlliesTurn"))
            {
                response.Status = "failed";
                response.Error = $"Not your turn (current: {preflightAbi.Name}). Wait for BattleMyTurn before using {abilityName}.";
                response.Screen = preflightAbi;
                return response;
            }

            var screen = WaitForTurnState(timeoutMs: 1000, out long waitedMs);

            // S59: if we landed on a deeper battle-menu state (submenu, skillset
            // list, targeting, or a pause-menu leak from a previous command) the
            // entry reset below can escape back to BattleMyTurn. Only reject the
            // state if it's not a recoverable one — preserves the old fail-fast
            // for out-of-battle screens while fixing the pause-leak cascade.
            bool isRecoverable = screen != null
                && BattleAbilityEntryReset.IsResetableBattleScreen(screen.Name);
            if (screen == null
                || (screen.Name != "BattleMyTurn" && screen.Name != "BattleActing" && !isRecoverable))
            {
                response.Status = "failed";
                response.Error = $"Not on BattleMyTurn/BattleActing (current: {screen?.Name ?? "null"}) after {waitedMs}ms wait";
                return response;
            }

            // Guard: a unit only gets one Action per turn. If BattleActed==1
            // we'd navigate into the menu, open Abilities, then stall on a
            // grayed-out skillset. Fail fast with a clear message so callers
            // know to Wait instead of retry-spamming. Only checked once we're
            // on the action menu — deeper states may report stale flag values.
            if ((screen.Name == "BattleMyTurn" || screen.Name == "BattleActing")
                && screen.BattleActed == 1)
            {
                response.Status = "failed";
                response.Error = "You've already acted this turn. You cannot perform another action.";
                return response;
            }

            // Step 0: Escape-to-known-state pre-reset.
            // Both the submenu cursor (Attack/White Magicks/...) and ability-list
            // cursor (Cure/Cura/...) are UE4 widget bytes that REMEMBER the
            // previously-selected index within a turn. Without a reliable way to
            // READ those cursor bytes (S55 AOB hunt failed structurally —
            // project_ability_list_cursor_addr.md), we reset them DETERMINISTICALLY
            // by escaping fully back to BattleMyTurn first. Widgets get
            // reconstructed on re-entry, both cursors return to idx 0, and the
            // downstream Down×submenuIdx / Down×abilityIdx navigation is correct.
            // Cost: ~0-3 escapes (typically 0 — caller is almost always on
            // BattleMyTurn already). S59: now also handles BattlePaused leaks.
            int escapeCount = BattleAbilityEntryReset.EscapeCountToMyTurn(screen.Name);
            if (escapeCount > 0)
            {
                ModLogger.Log($"[BattleAbility] Entry reset: screen={screen.Name}, Escape×{escapeCount} to reach BattleMyTurn");
                for (int i = 0; i < escapeCount; i++)
                {
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                }
                // Re-detect after escapes — action menu should now be visible.
                screen = _detectScreen();
                if (screen == null || (screen.Name != "BattleMyTurn" && screen.Name != "BattleActing"))
                {
                    response.Status = "failed";
                    response.Error = $"Escape-to-known-state failed: expected BattleMyTurn/BattleActing after {escapeCount} Escapes, got {screen?.Name ?? "null"}";
                    return response;
                }
                // Re-check Act-consumed after escape (sister to BattleAttack).
                // Retrying battle_ability from a post-action recoverable state
                // lands here; without this check the flow runs the Abilities
                // menu nav and silently fails later on a grayed submenu slot.
                if (screen.BattleActed == 1)
                {
                    response.Status = "failed";
                    response.Error = "Act already used this turn — only Move or Wait remain.";
                    return response;
                }
            }

            // Step 1: Navigate to Abilities in action menu (always index 1).
            // The menu always has 5 items — Move becomes "Reset Move" after moving,
            // but never disappears. Indices are stable:
            //   Move/ResetMove(0) Abilities(1) Wait(2) Status(3) AutoBattle(4)
            // After battle_move, the game auto-advances cursor to Abilities (1)
            // but the memory address 0x1407FC620 still reads 0. Use _menuCursorStale to correct.
            var cursorResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
            int rawCursor = cursorResult != null ? (int)cursorResult.Value.value : screen.MenuCursor;
            bool hasMoved = screen.BattleMoved == 1 || _menuCursorStale;
            bool hasActed = screen.BattleActed == 1;
            int cursor = BattleAbilityNavigation.EffectiveMenuCursor(rawCursor, moved: hasMoved, acted: hasActed);
            if (cursor != rawCursor)
                ModLogger.Log($"[BattleAbility] Cursor correction: raw={rawCursor} → effective={cursor} (moved={hasMoved}, acted={hasActed})");
            _menuCursorStale = false; // consumed
            NavigateMenuCursor(cursor, 1);
            SendKey(VK_ENTER);

            // Unified Step 1+3: poll for BattleAbilities (success). If
            // we're still on MyTurn/Acting after ~400ms, the Enter didn't
            // register — retry once. Total time budget 1500ms covers both
            // fast-submenu (~150-300ms) and retry cases (~900ms).
            // Replaces: old Thread.Sleep(500) + Step 3 one-shot retry
            // (Thread.Sleep(500) + Enter + Thread.Sleep(1000)) + extra
            // poll loop at submenu nav. Saves 350-500ms in the fast case,
            // 900-1100ms in the retry case.
            bool submenuReady = false;
            bool retried = false;
            var submenuSw = Stopwatch.StartNew();
            while (submenuSw.ElapsedMilliseconds < 1500)
            {
                var s = _detectScreen();
                if (s?.Name == "BattleAbilities")
                {
                    submenuReady = true;
                    ModLogger.Log($"[BattleAbility] Submenu detected after {submenuSw.ElapsedMilliseconds}ms");
                    break;
                }
                if (!retried && submenuSw.ElapsedMilliseconds > 400
                    && (s?.Name == "BattleMyTurn" || s?.Name == "BattleActing"))
                {
                    ModLogger.Log($"[BattleAbility] Still on {s.Name} after {submenuSw.ElapsedMilliseconds}ms — retrying Enter");
                    var retryRead = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
                    int retryCursor = retryRead != null ? (int)retryRead.Value.value : 1;
                    NavigateMenuCursor(retryCursor, 1);
                    SendKey(VK_ENTER);
                    retried = true;
                }
                Thread.Sleep(50);
            }

            // Step 2: Get available skillsets from cached scan data
            var submenuItems = GetAbilitiesSubmenuItems?.Invoke() ?? new[] { "Attack" };
            ModLogger.Log($"[BattleAbility] Submenu items: {string.Join(", ", submenuItems)}");

            var availableSkillsets = submenuItems.Where(s => s != "Attack").ToArray();
            var location = BattleAbilityNavigation.FindAbility(abilityName, availableSkillsets);
            if (location == null)
            {
                response.Status = "failed";
                response.Error = $"Ability '{abilityName}' not found in available skillsets: {string.Join(", ", submenuItems)}";
                SendKey(VK_ESCAPE);
                return response;
            }

            var loc = location.Value;
            int targetX = command.LocationId;
            int targetY = command.UnitIndex;

            // Self-target auto-fill: when no coords given, default to the
            // caster's tile if it's in the ability's valid set (or the
            // ability is flagged isSelfTarget). Covers:
            //  - Self-only abilities (Shout, Focus): isSelfTarget=true
            //  - Self-targetable allies (Tailwind, Steel, Salve, X-Potion):
            //    HRange numeric but caster's tile IS one of the valid target
            //    tiles. Live-flagged playtests: agent saw `<Ramza SELF>` in
            //    scan, called `battle_ability "Tailwind"` without coords,
            //    expected self-cast — got "requires a target tile" error.
            // Risk: a different ally might have wanted the buff. We treat
            // the user's explicit "no coords" as "I meant self" since the
            // scan marker promises self is a valid target.
            bool autoSelfTarget = false;
            if ((targetX < 0 || targetY < 0))
            {
                if (loc.isSelfTarget)
                {
                    autoSelfTarget = true;
                }
                else if (_lastValidAbilityTiles != null
                    && _lastValidAbilityTiles.TryGetValue(abilityName, out var validTilesSelfCheck))
                {
                    // Resolve caster pos and check if it's in the ability's
                    // valid-target set. If so, default to it. Use the
                    // scan-canonical position over the live cursor read
                    // — cursor may sit elsewhere from C+Up cycling.
                    var activeAllyCheck = GetActiveAlly();
                    var cursorCheck = ReadGridPos();
                    var (casterX, casterY) = CasterPositionResolver.Resolve(
                        activeAllyCheck?.GridX, activeAllyCheck?.GridY,
                        cursorCheck.x, cursorCheck.y);
                    if (casterX >= 0 && casterY >= 0
                        && validTilesSelfCheck.Contains((casterX, casterY)))
                    {
                        autoSelfTarget = true;
                    }
                }
            }
            if (autoSelfTarget)
            {
                // Prefer the active unit's actual position from the last
                // scan over the live cursor read — cursor may sit on
                // another unit (e.g. Wilham at (10,10)) from a prior
                // C+Up cycle, causing the auto-fill to target the wrong
                // tile. Live-flagged 2026-04-25 playtest.
                //
                // Stale-scan defense: if cursor disagrees with scan's
                // active-unit position, the scan is stale (e.g. the
                // unit moved via `execute_action ConfirmMove` which
                // doesn't trigger our post-move re-scan, or after a
                // partial execute_turn recovery). Force a fresh scan
                // so the resolver gets accurate data — second occurrence
                // of "Used Shout → (10,10) HP=528/528" wrong-tile bug
                // 2026-04-25 P2.
                var activeAlly = GetActiveAlly();
                var cursorPos = ReadGridPos();
                bool scanLooksStale = activeAlly != null
                    && cursorPos.x >= 0 && cursorPos.y >= 0
                    && (activeAlly.GridX != cursorPos.x || activeAlly.GridY != cursorPos.y);
                if (scanLooksStale)
                {
                    ModLogger.Log($"[BattleAbility] Scan-stale detected (scan active at ({activeAlly!.GridX},{activeAlly.GridY}), cursor at ({cursorPos.x},{cursorPos.y})) — forcing fresh CollectUnitPositionsFull");
                    try { CollectUnitPositionsFull(); } catch { /* fall back to cursor below */ }
                    activeAlly = GetActiveAlly();
                }
                var (resolvedX, resolvedY) = CasterPositionResolver.Resolve(
                    activeAlly?.GridX, activeAlly?.GridY,
                    cursorPos.x, cursorPos.y);
                if (resolvedX >= 0 && resolvedY >= 0)
                {
                    targetX = resolvedX;
                    targetY = resolvedY;
                    ModLogger.Log($"[BattleAbility] Auto-targeting caster at ({targetX},{targetY}) for self-target ability '{abilityName}' (active={activeAlly?.Name ?? "?"} via {(activeAlly != null ? "scan" : "cursor")})");
                }
            }

            if (!loc.isSelfTarget && (targetX < 0 || targetY < 0))
            {
                response.Status = "failed";
                response.Error = $"Ability '{abilityName}' requires a target tile. Usage: battle_ability \"{abilityName}\" <x> <y>";
                SendKey(VK_ESCAPE);
                return response;
            }

            // Up-front range validation: reject when the target tile isn't
            // in this ability's valid set (cached from the last scan_move).
            // Without this, an out-of-range cast enters targeting mode,
            // navigates the cursor wherever, and silently returns a phantom-
            // success "Used X on (Y)" message. Live-flagged playtest #3
            // 2026-04-25: Phoenix Down out of range said "Used Phoenix Down
            // on (4,6)" with no actual cast.
            // Skip when no cache exists (caller didn't run scan_move first)
            // or the ability isn't in the cache (unscanned ability — let
            // the nav loop surface its own error).
            if (_lastValidAbilityTiles != null
                && _lastValidAbilityTiles.TryGetValue(abilityName, out var validTiles)
                && validTiles.Count > 0
                && !validTiles.Contains((targetX, targetY)))
            {
                response.Status = "failed";
                response.Error = $"Tile ({targetX},{targetY}) is not in {abilityName}'s valid target range. Run scan_move first and pick from this ability's validTargetTiles ({validTiles.Count} valid tiles cached).";
                return response;
            }

            // Find the skillset index in the submenu items array
            int skillsetIdx = BattleAbilityNavigation.FindSkillsetIndex(loc.skillsetName, submenuItems);
            if (skillsetIdx < 0)
            {
                response.Status = "failed";
                response.Error = $"Skillset '{loc.skillsetName}' not in submenu: {string.Join(", ", submenuItems)}";
                SendKey(VK_ESCAPE);
                return response;
            }

            ModLogger.Log($"[BattleAbility] Navigating submenu: skillsetIdx={skillsetIdx} for '{loc.skillsetName}' in [{string.Join(", ", submenuItems)}]");
            if (!submenuReady)
            {
                ModLogger.Log($"[BattleAbility] WARN: submenu not confirmed ready — proceeding anyway");
            }
            // 150ms floor absorbs widget-init lag post-detection.
            Thread.Sleep(150);

            // Submenu navigation using global cursor counter at 0x140C0EB20.
            // The submenu WRAPS and ui= LAGS by one keypress (reports previous position).
            // ui= after entering submenu shows the PREVIOUS cursor position. The actual
            // cursor is one past that. We use this to determine where we are, then press
            // Down the right number of times to reach the target.
            //
            // Approach: read ui= to determine actual position (ui shows prev, so actual =
            // index of ui + 1, mod itemCount). Then compute how many Downs to reach target.
            screen = _detectScreen();
            string? uiAfterEntry = screen?.UI;
            int currentIdx = 0; // default to Attack if we can't determine
            if (uiAfterEntry != null)
            {
                int uiIdx = BattleAbilityNavigation.FindSkillsetIndex(uiAfterEntry, submenuItems);
                if (uiIdx >= 0)
                {
                    // ui= lags by one: it shows where cursor WAS. Actual = next item.
                    currentIdx = (uiIdx + 1) % submenuItems.Length;
                }
            }
            ModLogger.Log($"[BattleAbility] ui='{uiAfterEntry}' → actual cursor at index {currentIdx} ('{submenuItems[currentIdx]}'), target={skillsetIdx} ('{loc.skillsetName}')");

            // Compute downs needed (wrapping forward)
            int downsNeeded = (skillsetIdx - currentIdx + submenuItems.Length) % submenuItems.Length;
            ModLogger.Log($"[BattleAbility] Pressing Down x{downsNeeded}");

            // Read counter baseline for verification
            var counterBefore = _explorer.ReadAbsolute((nint)0x140C0EB20, 2);
            int baseline = counterBefore != null ? (int)counterBefore.Value.value : -1;

            for (int i = 0; i < downsNeeded; i++)
            {
                SendKey(VK_DOWN);
                // 200ms → 100ms. Matches the tightened ability-list Down (80ms)
                // closely. Submenu has 3-5 entries typically so savings are
                // modest per action (100-200ms) but add up over a session.
                Thread.Sleep(100);
            }

            // Verify via counter delta
            if (baseline >= 0)
            {
                var counterAfter = _explorer.ReadAbsolute((nint)0x140C0EB20, 2);
                int actual = counterAfter != null ? (int)counterAfter.Value.value : -1;
                int delta = actual - baseline;
                ModLogger.Log($"[BattleAbility] Counter: {baseline} → {actual} (delta={delta}, expected={downsNeeded})");
                if (delta != downsNeeded)
                    ModLogger.Log($"[BattleAbility] WARN: counter delta mismatch! Some keypresses may have been lost.");
            }

            // Enter the skillset. Pre-press settle is to let the last submenu
            // Down register before Enter fires on top of it. 150ms is enough
            // for input debounce; the post-Enter settle below absorbs the
            // skillset-list render.
            Thread.Sleep(150);
            SendKey(VK_ENTER);
            Thread.Sleep(300);

            // Step 4: Navigate to the ability within the skillset
            // Use the learned ability list (from scan) to determine the correct index.
            // The game only shows learned abilities, so the menu index may differ
            // from the hardcoded skillset index.
            var learnedAbilities = GetAbilityListForSkillset?.Invoke(loc.skillsetName);
            int abilityIndex = loc.indexInSkillset; // fallback to hardcoded

            if (learnedAbilities != null && learnedAbilities.Length > 0)
            {
                int learnedIdx = System.Array.IndexOf(learnedAbilities, abilityName);
                if (learnedIdx >= 0)
                    abilityIndex = learnedIdx;
                ModLogger.Log($"[BattleAbility] Learned abilities for {loc.skillsetName}: [{string.Join(", ", learnedAbilities)}], {abilityName} at index {abilityIndex}");
            }
            else
            {
                ModLogger.Log($"[BattleAbility] No learned ability data, using hardcoded index {abilityIndex}");
            }

            // The escape-to-known-state pre-reset (Step 0) guarantees the
            // ability-list cursor is at index 0 when we enter a skillset:
            // escaping all the way back to BattleMyTurn destroys the widget
            // state, and re-entering the skillset constructs a fresh widget
            // with cursor at 0. No blind Up-wrap needed (the old
            // Up×(listSize+1) was off-by-one because Up wraps from 0 to last,
            // and Up with verification can't work — the cursor counter reports
            // negative deltas on wrap).
            int listLength = learnedAbilities?.Length ?? 0;
            int listSize = listLength > 0 ? listLength : 16; // Fallback cap
            var listPlan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 0, targetIndex: abilityIndex, listSize: listSize);
            ModLogger.Log($"[BattleAbility] ListNav from 0 to {abilityIndex} (listSize={listSize}): {listPlan.Direction}×{listPlan.PressCount}");

            int listVk = listPlan.Direction == AbilityListCursorNavPlanner.Direction.Up
                ? VK_UP
                : VK_DOWN;
            for (int i = 0; i < listPlan.PressCount; i++)
            {
                SendKey(listVk);
                // Per-press hold: 150ms → 80ms. SendKey itself already
                // has a 25ms down-hold (session 8e); 80ms more between
                // presses gives the game ~105ms per press before the
                // next fires. For a 14-deep list (Holy) that's ~1ms/press
                // × 7 = 550ms vs old 1050ms — half a second saved on the
                // deepest abilities.
                Thread.Sleep(80);
            }

            // Pre-cast verification (best-effort): if the game surfaces a
            // recognizable ui= in the ability list AND it doesn't match our
            // target, fail fast. ui= isn't always populated for ability-list
            // states (widget-driven, lags the bridge's tracker) so a null
            // reading can't fail — but a clear mismatch MUST fail rather
            // than casting the wrong ability. (Aurablast incident:
            // Kenrick's Martial Arts list in the bridge was missing entries
            // that the game actually had, so Down×2 landed on a different
            // ability than intended.)
            Thread.Sleep(100);
            string? highlighted = _detectScreen()?.UI;
            ModLogger.Log($"[BattleAbility] Pre-cast verify: requested='{abilityName}' highlighted='{highlighted ?? "(null)"}'");
            if (!string.IsNullOrEmpty(highlighted)
                && !string.Equals(highlighted, abilityName, StringComparison.OrdinalIgnoreCase))
            {
                ModLogger.Log($"[BattleAbility] MISMATCH: requested '{abilityName}' but highlighted '{highlighted}'; escaping out");
                SendKey(VK_ESCAPE); Thread.Sleep(200);
                SendKey(VK_ESCAPE); Thread.Sleep(200);
                SendKey(VK_ESCAPE); Thread.Sleep(200);
                response.Status = "failed";
                response.Error = $"Ability selection mismatch: requested '{abilityName}' but cursor is on '{highlighted}'. Learned-ability list may be out of sync with the game. Call `screen` to refresh and retry.";
                return response;
            }

            // Step 5: Select the ability. Post-Enter settle gives the game
            // time to transition out of the ability list into targeting / cast.
            // 500ms was conservative; 300ms covers observed transition.
            SendKey(VK_ENTER);
            Thread.Sleep(300);

            // Cast-time abilities (CastSpeed > 0) queue in the Combat Timeline and
            // resolve when their CT counter reaches 100 — the unit still needs to
            // Wait to end the current turn. Use "Queued" instead of "Used" so Claude
            // doesn't assume the effect landed.
            string verb = loc.castSpeed > 0 ? "Queued" : "Used";
            string ctSuffix = loc.castSpeed > 0 ? $" (ct={loc.castSpeed})" : "";
            // Auto-end-turn abilities (Jump): engine ends the turn as part of the
            // ability execution. Claude should skip the Wait step. Flag in response
            // so `battle_ability` callers don't follow up with a redundant Wait.
            bool autoEndsTurn = AutoEndTurnAbilities.IsAutoEndTurn(abilityName);
            string autoEndSuffix = autoEndsTurn ? " — TURN ENDED" : "";

            // Step 6: Handle targeting
            if (loc.isTrueSelfOnly)
            {
                // True self-only abilities (Focus, Shout): apply instantly, single confirm
                SendKey(VK_ENTER);
                // 300ms → 150ms animation-start floor. With 40ms-interval
                // poll in WaitForActionResolved, the shorter floor means
                // we catch the resolved state sooner.
                Thread.Sleep(150);
                WaitForActionResolved(timeoutMs: 2000, out _);
                response.Status = "completed";
                response.Info = $"{verb} {abilityName} (self-target){ctSuffix}{autoEndSuffix}";
                StatTracker?.OnAbilityUsed(GetActiveUnitNameForStats(), abilityName);
                response.Screen = ResolveTerminalFlicker(_detectScreen());
                return response;
            }

            if (loc.isSelfTarget)
            {
                // Self-radius abilities (Chakra, Cyclone, Purification): game shows AoE
                // preview centered on caster. Need to wait for the preview to appear,
                // then confirm twice (select target + confirm cast).
                // Was 500+500+300 fixed sleeps = 1300ms floor. Trimmed to
                // 250+250+300 = 800ms since the AoE preview render is fast
                // in practice and WaitForActionResolved below is authoritative.
                Thread.Sleep(250); // wait for AoE preview to render
                SendKey(VK_ENTER);
                Thread.Sleep(250);
                SendKey(VK_ENTER); // confirm cast
                Thread.Sleep(300);
                WaitForActionResolved(timeoutMs: 2000, out _);
                response.Status = "completed";
                response.Info = $"{verb} {abilityName} (self-radius AoE){ctSuffix}{autoEndSuffix}";
                StatTracker?.OnAbilityUsed(GetActiveUnitNameForStats(), abilityName);
                response.Screen = ResolveTerminalFlicker(_detectScreen());
                return response;
            }

            // S60: for non-cast targeted abilities, capture the target's pre-action
            // HP so we can render a (pre→post/max) delta after the cast resolves —
            // same shape as BattleAttack's HIT/KO line. Skipped for cast-time
            // abilities (they queue; HP won't change until the cast triggers later).
            int abilityPreHp = -1, abilityTargetMaxHp = -1, abilityTargetLevel = 0;
            bool canMeasureAbilityDelta = loc.castSpeed == 0;
            if (canMeasureAbilityDelta)
            {
                abilityPreHp = ReadStaticArrayHpAt(targetX, targetY);
                abilityTargetMaxHp = ReadStaticArrayMaxHpAt(targetX, targetY);
                abilityTargetLevel = ReadStaticArrayFieldAt(targetX, targetY, 0x0D) & 0xFF;
                ModLogger.Log($"[BattleAbility] Pre-cast target HP={abilityPreHp}/{abilityTargetMaxHp} lv={abilityTargetLevel} at ({targetX},{targetY})");
            }

            // Targeted ability: we should now be in targeting mode.
            // BattleAttacking = basic Attack command (battleMode=4)
            // BattleCasting   = skillset ability target selection (battleMode=1)
            screen = _detectScreen();
            if (screen == null || (screen.Name != "BattleAttacking" && screen.Name != "BattleCasting"))
            {
                // Some abilities go straight to targeting without battleMode 1 or 4 —
                // tolerate any in-battle state and let the caller handle it downstream.
                if (!ScreenNamePredicates.IsBattleState(screen?.Name))
                {
                    response.Status = "failed";
                    response.Error = $"Failed to enter targeting mode for {abilityName} (current: {screen?.Name ?? "null"})";
                    EscapeToMyTurn();
                    return response;
                }
            }

            // Navigate cursor to target tile (reuse BattleAttack's targeting logic).
            // Resolve start position via the active unit's scan-canonical
            // tile when available — cursor read alone can lie if it's
            // sitting on another unit from prior C+Up cycling. The cursor
            // tile is still used for delta calc since the GAME cursor is
            // wherever it actually is on the screen, but we trust the scan
            // for the caster's identity coords (used by PostAction pin).
            // Same stale-scan defense as the auto-fill path above.
            var cursorAtStart = ReadGridPos();
            var activeAtStart = GetActiveAlly();
            bool startScanStale = activeAtStart != null
                && cursorAtStart.x >= 0 && cursorAtStart.y >= 0
                && (activeAtStart.GridX != cursorAtStart.x || activeAtStart.GridY != cursorAtStart.y);
            if (startScanStale)
            {
                ModLogger.Log($"[BattleAbility] startPos: scan-stale (scan ({activeAtStart!.GridX},{activeAtStart.GridY}) vs cursor ({cursorAtStart.x},{cursorAtStart.y})); refreshing");
                try { CollectUnitPositionsFull(); } catch { /* fall back to cursor */ }
                activeAtStart = GetActiveAlly();
            }
            var (startCasterX, startCasterY) = CasterPositionResolver.Resolve(
                activeAtStart?.GridX, activeAtStart?.GridY,
                cursorAtStart.x, cursorAtStart.y);
            var startPos = (x: startCasterX, y: startCasterY);
            ModLogger.Log($"[BattleAbility] Targeting {abilityName}, cursor at ({cursorAtStart.x},{cursorAtStart.y}), caster at ({startPos.x},{startPos.y}), target ({targetX},{targetY})");

            int deltaX = targetX - cursorAtStart.x;
            int deltaY = targetY - cursorAtStart.y;

            if (deltaX == 0 && deltaY == 0)
            {
                SendKey(VK_ENTER);
                Thread.Sleep(500);
                SendKey(VK_ENTER); // confirm target
                Thread.Sleep(500);
                SendKey(VK_ENTER); // Unit/Tile dialog (selects "Unit" default; harmless if no dialog)
                Thread.Sleep(300);
                WaitForActionResolved(timeoutMs: 2000, out _);
                string hpDelta = "";
                if (canMeasureAbilityDelta && abilityPreHp >= 0)
                {
                    // Dual-read static + live; prefer static on big disagreement.
                    // See the matching block below for rationale (X-Potion bug).
                    int liveHp = ReadLiveHp(abilityTargetMaxHp, abilityPreHp, abilityTargetLevel);
                    int staticHp = ReadStaticArrayHpAt(targetX, targetY);
                    int postHp = liveHp;
                    if (staticHp >= 0 && staticHp <= abilityTargetMaxHp
                        && System.Math.Abs(liveHp - staticHp) > abilityTargetMaxHp / 2)
                    {
                        ModLogger.Log($"[BattleAbility] Self-cast HP mismatch: live={liveHp} static={staticHp} — preferring static");
                        postHp = staticHp;
                    }
                    hpDelta = AbilityHpDeltaFormatter.Format(abilityPreHp, postHp, abilityTargetMaxHp,
                        isRevive: IsReviveAbilityName(abilityName));
                    ModLogger.Log($"[BattleAbility] Self-cast post: live={liveHp} static={staticHp} chose={postHp} (was {abilityPreHp}) at ({targetX},{targetY})");
                }
                response.Status = "completed";
                response.Info = $"{verb} {abilityName} on ({targetX},{targetY}){hpDelta} — cursor was already on target{ctSuffix}{autoEndSuffix}";
                StatTracker?.OnAbilityUsed(GetActiveUnitNameForStats(), abilityName);
                // PostAction with the caster's position — caster doesn't
                // move during a self-cast, and we don't want the trailing
                // suffix to mix target-tile coords with caster HP.
                response.PostAction = ReadPostActionState(startPos.x, startPos.y);
                // Pin a flicker-resolved screen so the outer wrapper's
                // ??= fallback doesn't catch a transient terminal state.
                // 2026-04-26 P6: agent saw [BattleVictory] after Magma
                // Surge while 2 enemies still alive. The "no-nav" branch
                // (cursor already on target) was missing the same screen
                // pin the main BattleAbility return path now has.
                response.Screen = ResolveTerminalFlicker(_detectScreen());
                return response;
            }

            // Empirical rotation detection
            _input.SendKeyPressToWindow(_gameWindow, VK_RIGHT);
            Thread.Sleep(150);
            var testPos = ReadGridPos();
            int rdx = testPos.x - startPos.x;
            int rdy = testPos.y - startPos.y;

            if (rdx == 0 && rdy == 0)
            {
                _input.SendKeyPressToWindow(_gameWindow, VK_DOWN);
                Thread.Sleep(150);
                testPos = ReadGridPos();
                int ddx = testPos.x - startPos.x;
                int ddy = testPos.y - startPos.y;
                _input.SendKeyPressToWindow(_gameWindow, VK_UP);
                Thread.Sleep(150);

                if (ddx != 0 || ddy != 0)
                {
                    rdx = -ddy;
                    rdy = ddx;
                    ModLogger.Log($"[BattleAbility] Rotation from Down: ({ddx},{ddy}) → Right=({rdx},{rdy})");
                }
                else if (_lastDetectedRightDelta != null)
                {
                    // Fallback: use cached rotation from previous move/attack this turn
                    (rdx, rdy) = _lastDetectedRightDelta.Value;
                    ModLogger.Log($"[BattleAbility] Rotation fallback (cached): Right=({rdx},{rdy})");
                }
                else
                {
                    // Last resort: read camera rotation from memory
                    var camResult = _explorer.ReadAbsolute((nint)AddrCameraRotation, 1);
                    if (camResult != null)
                    {
                        (rdx, rdy) = AttackDirectionLogic.RightDeltaFromCameraRotation((int)camResult.Value.value);
                        _lastDetectedRightDelta = (rdx, rdy);
                        ModLogger.Log($"[BattleAbility] Rotation fallback (camera): raw={camResult.Value.value} → Right=({rdx},{rdy})");
                    }
                    else
                    {
                        response.Status = "failed";
                        response.Error = $"Could not detect rotation for {abilityName} targeting";
                        EscapeToMyTurn();
                        return response;
                    }
                }
            }
            else
            {
                _input.SendKeyPressToWindow(_gameWindow, VK_LEFT);
                Thread.Sleep(150);
            }

            _lastDetectedRightDelta = (rdx, rdy);

            // Navigate to target
            string? arrowName = AttackDirectionLogic.ComputeArrowForDelta(rdx, rdy, deltaX, deltaY);
            if (arrowName == null)
            {
                NavigateGrid(deltaX, deltaY, FindRotationFromRight(rdx, rdy));
            }
            else
            {
                int vk = arrowName switch
                {
                    "Right" => VK_RIGHT,
                    "Left" => VK_LEFT,
                    "Up" => VK_UP,
                    "Down" => VK_DOWN,
                    _ => 0
                };
                if (vk != 0)
                {
                    _input.SendKeyPressToWindow(_gameWindow, vk);
                    Thread.Sleep(150);
                }
            }

            // Verify cursor position
            var finalPos = ReadGridPos();
            if (finalPos.x != targetX || finalPos.y != targetY)
            {
                ModLogger.Log($"[BattleAbility] WARN: cursor at ({finalPos.x},{finalPos.y}), expected ({targetX},{targetY})");
                // Aggressive cancel: send up to 3 Escapes spaced 300ms,
                // poll for return to BattleMyTurn after each. The single
                // Escape used to leave callers stranded if the targeting
                // mode didn't accept it cleanly — the caller's next call
                // would land on an unexpected screen and the action
                // could even commit at the wrong tile (live-flagged
                // 2026-04-26 P4: Tanglevine cursor miss landed Lloyd in
                // BattleEnemiesTurn with action consumed).
                bool aborted = false;
                for (int esc = 0; esc < 3; esc++)
                {
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                    var s = _detectScreen();
                    if (s?.Name == "BattleMyTurn" || s?.Name == "BattleActing")
                    {
                        aborted = true;
                        break;
                    }
                }
                response.Status = "failed";
                response.Error = aborted
                    ? $"Cursor miss: at ({finalPos.x},{finalPos.y}) expected ({targetX},{targetY}) — aborted cleanly"
                    : $"Cursor miss: at ({finalPos.x},{finalPos.y}) expected ({targetX},{targetY}) — could NOT abort, action may have committed at wrong tile";
                return response;
            }

            // Confirm target + Unit/Tile dialog
            SendKey(VK_ENTER);
            Thread.Sleep(500);
            SendKey(VK_ENTER); // confirm target
            Thread.Sleep(500);
            SendKey(VK_ENTER); // Unit/Tile dialog (selects "Unit" default; harmless if no dialog)
            Thread.Sleep(300);

            // Wait for the game to settle back to a control state (BattleMyTurn
            // or BattleActing) or the post-battle screens (Victory/Desertion).
            // Without this, response.Screen gets set to BattleAttacking or
            // BattleCasting from a stale read during the cast animation, and
            // downstream session logs + subsequent commands see a confused
            // state. Budget 2s — spell-cast animations can exceed 1s.
            WaitForActionResolved(timeoutMs: 2000, out long settleMs);
            if (settleMs >= 2000)
                ModLogger.Log($"[BattleAbility] Post-cast settle timeout ({settleMs}ms); returning anyway.");

            string finalHpDelta = "";
            if (canMeasureAbilityDelta && abilityPreHp >= 0)
            {
                // Dual-read: static array AND live heap. The agent's playtest #4
                // saw an X-Potion phantom heal `(275→719/719)` where ReadLiveHp
                // returned a wrong-struct match (Ramza-MaxHp=719 collided with
                // some saved-game-state heap region). The static array at
                // +0x14 of the unit's slot is authoritative AFTER settle —
                // it's what the game itself wrote. Prefer static when the
                // two disagree by more than a reasonable damage/heal range.
                int liveHp = ReadLiveHp(abilityTargetMaxHp, abilityPreHp, abilityTargetLevel);
                int staticHp = ReadStaticArrayHpAt(targetX, targetY);
                int postHp = liveHp;
                if (staticHp >= 0 && staticHp <= abilityTargetMaxHp)
                {
                    // If live disagrees with static by more than the maxHp's
                    // worth (one delta range), trust static — likely a heap
                    // collision in ReadLiveHp.
                    if (System.Math.Abs(liveHp - staticHp) > abilityTargetMaxHp / 2)
                    {
                        ModLogger.Log($"[BattleAbility] Post-cast HP mismatch: live={liveHp} static={staticHp} maxHp={abilityTargetMaxHp} — preferring static (likely heap-search false-positive)");
                        postHp = staticHp;
                    }
                }
                finalHpDelta = AbilityHpDeltaFormatter.Format(abilityPreHp, postHp, abilityTargetMaxHp,
                    isRevive: IsReviveAbilityName(abilityName));
                ModLogger.Log($"[BattleAbility] Post-cast: live={liveHp} static={staticHp} chose={postHp} (pre={abilityPreHp}) at ({targetX},{targetY})");
            }

            response.Status = "completed";
            response.Info = $"{verb} {abilityName} on ({targetX},{targetY}){finalHpDelta}{ctSuffix}{autoEndSuffix}";
            StatTracker?.OnAbilityUsed(GetActiveUnitNameForStats(), abilityName);
            // Caster didn't move; pin PostAction to the caster's start
            // position so the formatter doesn't mix target-tile coords
            // with caster HP (would mislead any caller tracking enemy HP
            // from the response payload).
            response.PostAction = ReadPostActionState(startPos.x, startPos.y);
            // Pin a flicker-resolved screen so the outer ProcessCommand
            // wrapper doesn't re-read and catch a transient terminal
            // state. Same fix as BattleAttack — 2026-04-26 P5: agent
            // saw `[BattleVictory]` after Tanglevine while 2 enemies
            // were still alive.
            response.Screen = ResolveTerminalFlicker(_detectScreen());
            return response;
        }

        /// <summary>
        /// Settle terminal-state flickers (BattleVictory / GameOver /
        /// BattleDesertion) by polling up to 3×500ms before trusting them.
        /// Used by BattleAttack and BattleAbility to pin response.Screen
        /// so the outer ProcessCommand wrapper's `??=` fallback doesn't
        /// re-read and surface a flicker we already settled. 2026-04-26 P5.
        /// </summary>
        private DetectedScreen? ResolveTerminalFlicker(DetectedScreen? initial)
        {
            if (initial?.Name == null) return initial;
            if (initial.Name != "BattleVictory" && initial.Name != "GameOver"
                && initial.Name != "BattleDesertion")
                return initial;
            for (int recheck = 0; recheck < 3; recheck++)
            {
                Thread.Sleep(500);
                var s = _detectScreen();
                if (s?.Name != null && s.Name != "BattleVictory"
                    && s.Name != "GameOver" && s.Name != "BattleDesertion")
                {
                    ModLogger.Log($"[FlickerResolve] {initial.Name} → {s.Name}");
                    return s;
                }
            }
            return initial;
        }

        /// <summary>
        /// navigate: Go to a target screen from wherever we are.
        /// Supports: WorldMap, PartyMenu, TravelList
        /// </summary>
        private CommandResponse Navigate(CommandResponse response, string targetScreen)
        {
            if (string.IsNullOrEmpty(targetScreen))
            {
                response.Status = "failed";
                response.Error = "Target screen ('to') is required";
                return response;
            }

            int maxSteps = 10;
            for (int step = 0; step < maxSteps; step++)
            {
                var screen = _detectScreen();
                if (screen == null)
                {
                    response.Status = "failed";
                    response.Error = "Could not detect current screen";
                    return response;
                }

                // Already there?
                if (string.Equals(screen.Name, targetScreen, StringComparison.OrdinalIgnoreCase))
                {
                    response.Status = "completed";
                    return response;
                }

                // Determine what key to press to get closer to the target
                bool progressed = false;
                switch (screen.Name)
                {
                    case "TravelList":
                        // Always go back to WorldMap first
                        SendKey(VK_ESCAPE);
                        WaitForScreen("WorldMap", 2000);
                        progressed = true;
                        break;

                    case "WorldMap":
                        if (targetScreen.Equals("PartyMenuUnits", StringComparison.OrdinalIgnoreCase))
                        {
                            SendKey(VK_ESCAPE);
                            WaitForScreen("PartyMenuUnits", 2000);
                            progressed = true;
                        }
                        else if (targetScreen.Equals("TravelList", StringComparison.OrdinalIgnoreCase))
                        {
                            SendKey(VK_T);
                            WaitForScreen("TravelList", 2000);
                            progressed = true;
                        }
                        break;

                    case "PartyMenuUnits":
                        if (targetScreen.Equals("WorldMap", StringComparison.OrdinalIgnoreCase))
                        {
                            SendKey(VK_ESCAPE);
                            WaitForScreen("WorldMap", 2000);
                            progressed = true;
                        }
                        break;

                    case "CharacterStatus":
                        SendKey(VK_ESCAPE);
                        Thread.Sleep(300);
                        progressed = true;
                        break;

                    case "BattlePaused":
                        SendKey(VK_TAB);
                        Thread.Sleep(300);
                        progressed = true;
                        break;

                    default:
                        // Try Escape as a generic "go back"
                        SendKey(VK_ESCAPE);
                        Thread.Sleep(300);
                        progressed = true;
                        break;
                }

                if (!progressed)
                {
                    response.Status = "failed";
                    response.Error = $"Don't know how to get from {screen.Name} to {targetScreen}";
                    return response;
                }
            }

            response.Status = "failed";
            response.Error = $"Could not reach {targetScreen} within {maxSteps} steps";
            return response;
        }

        /// <summary>
        /// travel: Open travel list, find the target location, select it, enter.
        ///
        /// Proven flow (tested manually):
        ///   - T opens travel list (remembers last tab)
        ///   - E switches to next tab (cursor resets to top on tab switch)
        ///   - Enter selects highlighted item, closes list, updates hover address
        ///   - T reopens list (remembers tab + cursor position)
        ///   - Down advances cursor within tab
        ///
        /// Strategy:
        ///   1. Open list, press E to switch tab (resets to top), Enter to identify
        ///      tab via hover. Repeat E until on the correct tab for our target.
        ///   2. Once on correct tab, T reopens, Down+Enter to scan items until
        /// Travel list tab orders — hardcoded from in-game verification.
        /// Each array contains location IDs in the order they appear in the UI.
        /// Tab 0 = Settlements, Tab 1 = Battlegrounds, Tab 2 = Miscellaneous.
        /// </summary>
        private static readonly int[][] TravelTabs = {
            // Settlements (tab 0) — verified in-game 2026-04-06
            new[] { 0, 2, 3, 1, 5, 4, 6, 9, 10, 11, 12, 8, 7, 13, 14 },
            // Battlegrounds (tab 1) — verified in-game 2026-04-06
            new[] { 24, 26, 28, 29, 25, 32, 35, 37, 30, 39, 33, 31, 38, 40, 34, 42, 41, 36, 27 },
            // Miscellaneous (tab 2) — verified in-game 2026-04-06
            new[] { 17, 15, 19, 18, 21, 16, 23, 22 },
        };

        // Which tab each location ID belongs to, and its index within that tab
        private static readonly Dictionary<int, (int tab, int index)> TravelLookup;

        static NavigationActions()
        {
            TravelLookup = new Dictionary<int, (int, int)>();
            for (int tab = 0; tab < TravelTabs.Length; tab++)
                for (int idx = 0; idx < TravelTabs[tab].Length; idx++)
                    TravelLookup[TravelTabs[tab][idx]] = (tab, idx);
        }

        private CommandResponse TravelTo(CommandResponse response, int locationId)
        {
            if (!TravelLookup.TryGetValue(locationId, out var target))
            {
                response.Status = "failed";
                response.Error = $"Location {locationId} not in travel list. Valid: {string.Join(",", TravelLookup.Keys.OrderBy(k => k))}";
                return response;
            }

            // Refuse same-location travel — the game opens the travel list
            // with the cursor on the current node, the blind "press Enter to
            // confirm" then selects it, and input routing falls into an
            // undefined sub-window where subsequent keys get swallowed (the
            // "Dorter shop run got stuck" repro from 2026-04-14, see TODO
            // §3). Detect via the world location byte at 0x14077D208 and
            // bail out with a clear remediation message before any keys fly.
            var standingRead = _explorer?.ReadAbsolute((nint)0x14077D208, 1);
            int standingLoc = standingRead.HasValue ? (int)standingRead.Value.value : -1;
            if (standingLoc == locationId)
            {
                response.Status = "rejected";
                response.Error = $"Already at location {locationId}. Use execute_action EnterLocation to open the location menu instead of world_travel_to.";
                return response;
            }

            // Refuse travel to locked/unrevealed locations. Array at
            // 0x1411A10B0 is 1 byte per location id (0x01 = unlocked,
            // 0x00 = locked). Live-verified 2026-04-15 session 16.
            // Without this check, world_travel_to for a locked location
            // navigates the travel list past the locked entry and opens
            // the wrong settlement.
            var unlockRead = _explorer?.ReadAbsolute((nint)(0x1411A10B0 + locationId), 1);
            if (unlockRead.HasValue && unlockRead.Value.value == 0)
            {
                response.Status = "rejected";
                response.Error = $"Location {locationId} is locked (unrevealed). Advance the story to unlock it.";
                return response;
            }

            var screen = _detectScreen();
            if (screen == null || (screen.Name != "WorldMap" && screen.Name != "TravelList"))
            {
                // Stale-state bypass: after battle_flee returns, screen detection can stay stuck
                // at BattleMyTurn because unit slot memory (0x14077CA30/54) persists until the
                // game reallocates it. The combination of:
                //   - battleMode == 0     (no active battle)
                //   - rawLocation valid   (distinguishes from attack-animation flicker where it's 255)
                //   - party == 0          (not in pause/party menu)
                // means we're genuinely on the world map with stale slot memory.
                // See memory/feedback_flee_stale_state.md.
                bool looksStaleWorldMap = false;
                if (ScreenNamePredicates.IsBattleState(screen?.Name))
                {
                    var bmResult = _explorer.ReadAbsolute((nint)0x140900650, 1);
                    var locResult = _explorer.ReadAbsolute((nint)0x14077D208, 1);
                    int bm = bmResult != null ? (int)bmResult.Value.value : -1;
                    int loc = locResult != null ? (int)locResult.Value.value : -1;
                    // battleMode==0 AND valid location = we're not in active battle, even though
                    // unit slots still show populated. Party flag is NOT checked because the
                    // pause menu can leave it set to 1 after flee. See memory/feedback_flee_stale_state.md.
                    if (bm == 0 && loc >= 0 && loc <= 42)
                    {
                        ModLogger.Log($"[TravelTo] Stale screen={screen.Name}, battleMode=0, loc={loc} — bypassing");
                        looksStaleWorldMap = true;
                    }
                }

                if (!looksStaleWorldMap)
                {
                    response.Status = "failed";
                    response.Error = $"Must be on WorldMap (current: {screen?.Name})";
                    return response;
                }
            }

            // 1. Open travel list — cursor starts on current location
            SendKey(VK_T);
            Thread.Sleep(500);

            // Read current cursor position via Enter + hover
            SendKey(VK_ENTER);
            Thread.Sleep(300);

            var hoverCheck = _explorer.ReadAbsolute((nint)0x140787A22, 1);
            int currentLoc = hoverCheck != null ? (int)hoverCheck.Value.value : -1;

            int currentTab = 0;
            int currentIdx = 0;
            if (TravelLookup.TryGetValue(currentLoc, out var cur))
            {
                currentTab = cur.tab;
                currentIdx = cur.index;
            }

            // 2. Reopen and navigate to destination
            SendKey(VK_T);
            Thread.Sleep(500);

            int tabsToMove = (target.tab - currentTab + 3) % 3;
            for (int i = 0; i < tabsToMove; i++)
            {
                SendKey(VK_E);
                Thread.Sleep(300);
            }

            int fromIdx = tabsToMove > 0 ? 0 : currentIdx;
            int steps = target.index - fromIdx;

            if (steps > 0)
                for (int i = 0; i < steps; i++) { SendKey(VK_DOWN); Thread.Sleep(150); }
            else if (steps < 0)
                for (int i = 0; i < -steps; i++) { SendKey(VK_UP); Thread.Sleep(150); }

            // 3. Press Enter to center on destination node
            SendKey(VK_ENTER);
            Thread.Sleep(500);

            // 4. Press Enter to confirm travel
            SendKey(VK_ENTER);
            Thread.Sleep(300); // Brief wait before starting poll

            // 4.5. Hold Ctrl to speed up movement — FOCUS-AWARE.
            //
            // Ctrl must be asserted GLOBALLY via SendInput for FFT's
            // DirectInput reader to register the held state (PostMessage
            // alone doesn't satisfy DirectInput). But that means every
            // window sees Ctrl held. If the user tabs to their terminal
            // to type feedback, every keystroke becomes Ctrl+key — broken
            // copy/paste, broken text input.
            //
            // Fix (session 24): only hold Ctrl while the game window has
            // foreground. In the poll loop, check foreground each tick;
            // release globally when focus leaves, re-assert when it
            // returns. The PostMessage path keeps the game-side signal
            // alive during poll iterations.
            bool ctrlHeldGlobally = false;
            if (IsGameForeground())
            {
                SendInputKeyDown(VK_CONTROL);
                ctrlHeldGlobally = true;
            }
            _input.SendKeyDownToWindow(_gameWindow, VK_CONTROL);
            ModLogger.Log($"[Travel] Holding Ctrl for fast-forward (globalHeld={ctrlHeldGlobally})");

            // 5-9. Poll until arrival or encounter
            var sw = Stopwatch.StartNew();
            int stableWorldMapCount = 0;
            try
            {
                while (sw.ElapsedMilliseconds < 30000) // 30s max travel time
                {
                    Thread.Sleep(200);

                    // Focus-aware Ctrl-hold maintenance. If the user tabs
                    // away, release globally so typing isn't hijacked; when
                    // they tab back, re-assert so fast-forward resumes.
                    bool gameFg = IsGameForeground();
                    if (gameFg && !ctrlHeldGlobally)
                    {
                        // Re-assert. SetForegroundWindow in SendInputKeyDown
                        // is a no-op here because game already IS foreground.
                        SendInputKeyDown(VK_CONTROL);
                        ctrlHeldGlobally = true;
                        ModLogger.Log("[Travel] Re-asserted Ctrl (game regained focus)");
                    }
                    else if (!gameFg && ctrlHeldGlobally)
                    {
                        SendInputKeyUp(VK_CONTROL);
                        ctrlHeldGlobally = false;
                        ModLogger.Log("[Travel] Released global Ctrl (user tabbed away)");
                    }

                    // Check encounter via memory directly (faster than DetectScreen)
                    var encA = _explorer.ReadAbsolute((nint)0x140900824, 1);
                    var encB = _explorer.ReadAbsolute((nint)0x140900828, 1);
                    if (encA != null && encB != null && encA.Value.value != encB.Value.value)
                    {
                        // 6. Encounter detected — release Ctrl and report
                        if (ctrlHeldGlobally) SendInputKeyUp(VK_CONTROL);
                        _input.SendKeyUpToWindow(_gameWindow, VK_CONTROL);
                        ctrlHeldGlobally = false;
                        ModLogger.Log("[Travel] Encounter detected, released Ctrl");

                        Thread.Sleep(500); // Let dialog settle
                        var encScreen = _detectScreen();
                        response.Status = "encounter";
                        response.Error = $"Encounter at {encScreen?.LocationName ?? "unknown"} (loc {encScreen?.Location}). Fight (Enter) or Flee (Down+Enter).";
                        response.Screen = encScreen;
                        return response;
                    }

                    // 9. Detect arrival: WorldMap screen stable for 1+ second (no more animation)
                    var current = _detectScreen();
                    if (current != null && current.Name == "WorldMap")
                    {
                        stableWorldMapCount++;
                        if (stableWorldMapCount >= 5) // 5 x 200ms = 1 second stable
                        {
                            response.Status = "completed";
                            response.Error = $"Arrived at destination (loc {current.Location})";
                            break;
                        }
                    }
                    else
                    {
                        stableWorldMapCount = 0;
                    }
                }

                if (sw.ElapsedMilliseconds >= 30000)
                {
                    response.Status = "completed";
                    response.Error = "Travel timeout after 30s";
                }
            }
            finally
            {
                // 10. Always release Ctrl (both paths).
                if (ctrlHeldGlobally) SendInputKeyUp(VK_CONTROL);
                _input.SendKeyUpToWindow(_gameWindow, VK_CONTROL);
                ModLogger.Log("[Travel] Released Ctrl (final)");
            }

            return response;
        }

        /// <summary>
        /// confirm_attack: Press F twice (select target + confirm), then poll until
        /// the battle reaches a terminal state (BattleMyTurn, Battle, GameOver, BattlePaused).
        /// The attack animation can last several seconds with transient BattleActing/Moving states.
        /// </summary>
        private CommandResponse ConfirmAttack(CommandResponse response)
        {
            var screen = _detectScreen();
            // Accept both basic Attack targeting and skillset ability casting —
            // the F-to-confirm mechanic is the same for both.
            if (screen == null || (screen.Name != "BattleAttacking" && screen.Name != "BattleCasting"))
            {
                response.Status = "failed";
                response.Error = $"Not on BattleAttacking/BattleCasting screen (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Press F to select target
            SendKey(VK_F);
            Thread.Sleep(300);

            // Press F again to confirm attack
            SendKey(VK_F);
            Thread.Sleep(500);

            // Poll until we reach a terminal battle state
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 10000)
            {
                Thread.Sleep(200);
                screen = _detectScreen();
                if (screen == null) continue;

                // Terminal states: the action has fully resolved
                if (screen.Name == "BattleMyTurn" ||
                    screen.Name == "Battle" ||
                    screen.Name == "GameOver" ||
                    screen.Name == "BattlePaused")
                {
                    response.Status = "completed";
                    return response;
                }
            }

            response.Status = "completed_timeout";
            return response;
        }

        /// <summary>
        /// scan_units: Collect all unit positions via C+Up cycling and return them.
        /// Standalone action for testing — no movement, just reads positions.
        /// </summary>
        private CommandResponse ScanUnits(CommandResponse response)
        {
            var screen = _detectScreen();
            if (!ScreenNamePredicates.IsBattleState(screen?.Name))
            {
                response.Status = "failed";
                response.Error = $"Not in battle (current: {screen?.Name ?? "null"})";
                return response;
            }

            var units = CollectUnitPositionsFull();

            // Build structured JSON response
            response.Units = BuildUnitResponses(units);

            // Also keep the summary string for backward compatibility
            var lines = new List<string>();
            lines.Add($"units={units.Count}");
            foreach (var u in units)
            {
                string teamName = u.Team == 0 ? "PLAYER" : u.Team == 2 ? "ALLY" : "ENEMY";
                string nameStr = u.Name != null ? $" {u.Name}" : "";
                string jobStr = u.JobNameOverride ?? (u.Job > 0 ? GameStateReporter.GetJobName(u.Job) : null) ?? "";
                if (jobStr.Length > 0) jobStr = $"({jobStr})";
                var statuses = StatusDecoder.Decode(u.StatusBytes);
                string statusStr = statuses.Count > 0 ? $" [{string.Join(",", statuses)}]" : "";
                lines.Add($"[{teamName}]{nameStr}{jobStr} ({u.GridX},{u.GridY}) Lv{u.Level} HP={u.Hp}/{u.MaxHp} MP={u.Mp}/{u.MaxMp} PA={u.PA} MA={u.MA} Mv={u.Move} Jmp={u.Jump} Br={u.Brave} Fa={u.Faith} CT={u.CT} Spd={u.Speed} Exp={u.Exp}{statusStr}");
            }

            response.Status = units.Count > 0 ? "completed" : "failed";
            response.Error = string.Join(" | ", lines);
            return response;
        }

        /// <summary>
        /// scan_move: Scan all units, compute valid movement tiles using map data,
        /// and return both unit positions and valid tiles in one response.
        /// Requires set_map to have been called first.
        /// Move/Jump can be overridden via locationId (move) and unitIndex (jump).
        /// </summary>
        private CommandResponse ScanMove(CommandResponse response, CommandRequest command)
        {
            var screen = _detectScreen();
            if (!ScreenNamePredicates.IsBattleState(screen?.Name))
            {
                response.Status = "failed";
                response.Error = $"Not in battle (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Scan-safe screens. scan_move uses C+Up cycling to walk all units, which
            // reads the condensed struct as the cursor lands on each. During enemy turns
            // or ability/attack animations, cycling can corrupt game state or return
            // stale data because the game is mid-transition. Restrict to states where
            // the player has control of the cursor.
            //
            // Allowed:
            //   BattleMyTurn       — start of player turn, no action yet
            //   BattleMoving       — player picking a move destination
            //   BattleAbilities    — player browsing their ability submenu
            //   BattleWaiting      — post-action facing selection
            //   BattlePaused       — pause menu open over the battle
            //
            // Blocked:
            //   BattleAttacking    — C+Up disrupts attack targeting cursor
            //   BattleCasting      — C+Up disrupts spell targeting cursor
            //   BattleActing       — player's unit is mid-animation
            //   BattleAlliesTurn   — neutral/NPC guest's turn
            //   BattleEnemiesTurn  — enemy is acting
            //   Battle              — ambiguous sub-state (fall-through case)
            //   BattleVictory      — battle over
            //   BattleGameOver     — KO'd
            var allowedStates = new HashSet<string>
            {
                "BattleMyTurn", "BattleMoving",
                "BattleAbilities", "BattleWaiting", "BattlePaused"
            };
            if (!allowedStates.Contains(screen.Name))
            {
                response.Status = "blocked";
                response.Error = $"Cannot scan during {screen.Name} — wait for BattleMyTurn";
                return response;
            }

            // 1. Scan units
            var units = CollectUnitPositionsFull();
            var ally = units.FirstOrDefault(u => u.IsActive)
                    ?? units.FirstOrDefault(u => u.Team == 0);
            if (ally == null)
            {
                response.Status = "failed";
                response.Error = "No ally found in scan";
                return response;
            }

            // 2. Get move/jump - use overrides if provided, otherwise from scan.
            // When the per-unit heap Move/Jump read fails (live-observed: a
            // broken-armor MaxHp change invalidated the struct anchor
            // mid-battle, collapsing Mv/Jp to 0), fall back to the static
            // JobBaseStatsTable — approximate BFS input beats an empty Move
            // tile set every time.
            string? allyJobName = ally.JobNameOverride
                ?? (ally.Team == 0 ? GameStateReporter.GetJobName(ally.Job) : null);
            var (fbMove, fbJump) = MoveJumpFallbackResolver.Resolve(ally.Move, ally.Jump, allyJobName);
            int moveStat = command.LocationId > 0 ? command.LocationId : fbMove;
            int jumpStat = command.UnitIndex > 0 ? command.UnitIndex : fbJump;

            // 3. Get all occupied positions (enemies, allies, dead units — everything except active unit)
            var occupiedPositions = BattleFieldHelper.GetOccupiedPositions(
                units.Select(u => (u.GridX, u.GridY, u.Team, u.Hp, u == ally)).ToList());
            var enemyPositions = GetEnemyPositions(); // still needed for facing/distance calculations

            // 4. Build structured battle state
            var battleState = new BattleState { InBattle = true };
            battleState.ActiveUnit = new ActiveUnitState
            {
                Team = ally.Team,
                Level = ally.Level,
                X = ally.GridX,
                Y = ally.GridY,
                Hp = ally.Hp,
                MaxHp = ally.MaxHp,
                Mp = ally.Mp,
                MaxMp = ally.MaxMp,
                NameId = ally.NameId,
                Name = ally.Name,
                JobId = ally.Job,
                JobName = ally.JobNameOverride ?? GameStateReporter.GetJobName(ally.Job),
                Brave = ally.Brave,
                Faith = ally.Faith,
                Move = ally.Move,
                Jump = ally.Jump,
                PA = ally.PA,
                MA = ally.MA,
                Equipment = ally.Equipment,
            };
            // Position→unit index for annotating ability target tiles with occupant info.
            // Built once outside the per-unit loop so every ability lookup is O(1).
            // Separate dictionaries for alive and dead units — most abilities should
            // not show dead units as targets (only revival abilities like Phoenix Down).
            var aliveByPos = new Dictionary<(int x, int y), ScannedUnit>();
            var deadByPos = new Dictionary<(int x, int y), ScannedUnit>();
            // Revive index: dead units PLUS alive-undead-status units. Phoenix
            // Down / Raise reverse life state, so on Undead enemies they're an
            // instant KO move — those tiles need to appear in the target list
            // even though the unit isn't dead. See ReviveTargetClassifier.
            var reviveByPos = new Dictionary<(int x, int y), ScannedUnit>();
            const byte UndeadStatusBit = 0x10; // matches StatusDecoder
            foreach (var posUnit in units)
            {
                // Petrified units are ALIVE by HP but untargetable in battle
                // (the game refuses Attack / cast on them; only Gold Needle /
                // Remedy clears the status). Exclude them from aliveByPos so
                // ability validTargetTiles don't suggest wasted actions.
                // They also don't belong in deadByPos (Phoenix Down / Raise
                // don't work on statues). They just drop out of both indexes.
                var lifeState = StatusDecoder.GetLifeState(posUnit.StatusBytes);
                if (lifeState == "petrified")
                    continue;
                bool isDead = posUnit.Hp <= 0 && posUnit.MaxHp > 0;
                if (isDead)
                {
                    deadByPos[(posUnit.GridX, posUnit.GridY)] = posUnit;
                    reviveByPos[(posUnit.GridX, posUnit.GridY)] = posUnit;
                }
                else
                {
                    aliveByPos[(posUnit.GridX, posUnit.GridY)] = posUnit;
                    bool isUndead = posUnit.StatusBytes != null
                        && posUnit.StatusBytes.Length > 0
                        && (posUnit.StatusBytes[0] & UndeadStatusBit) != 0;
                    if (isUndead)
                        reviveByPos[(posUnit.GridX, posUnit.GridY)] = posUnit;
                }
            }

            // Read the player inventory bytes ONCE per scan. Used to populate
            // HeldCount / Unusable on Chemist Items / Samurai Iaido / Ninja Throw
            // abilities via SkillsetItemLookup. Zero extra cost for non-inventory
            // skillsets (the lookup returns null immediately). The inventory
            // store is a flat u8 array at 0x1411A17C0 (272 bytes, cracked session 18).
            byte[]? inventoryBytes = null;
            try
            {
                var invReader = new InventoryReader(_explorer);
                inventoryBytes = invReader.ReadRaw();
            }
            catch (System.Exception ex)
            {
                ModLogger.Log($"[ScanMove] inventory read failed: {ex.Message}");
            }

            // Move/Jump exposed via ActiveUnit for Claude's decision-making
            foreach (var u in units)
            {
                bool isActive = u == ally;
                int dist = Math.Abs(u.GridX - ally.GridX) + Math.Abs(u.GridY - ally.GridY);

                // Compute job name first so we can use it for monster ability lookup
                var jobName = u.JobNameOverride
                    ?? (u.Team == 0 ? GameStateReporter.GetJobName(u.Job) : null);

                // Resolve abilities:
                //  - Active player unit: filter learned list by equipped skillsets
                //  - Enemy monster: look up fixed kit by class name (MonsterAbilities)
                //  - Enemy human or unknown: null (their abilities are per-encounter and
                //    we don't have a way to read them yet)
                List<AbilityEntry>? abilities = null;
                if (u.LearnedAbilities.Count > 0)
                {
                    // Only the active player unit gets valid-target tiles populated.
                    // Enemies and idle allies don't need them — Claude is only planning
                    // the current turn.
                    var abilityMap = isActive ? _mapLoader?.CurrentMap : null;

                    // For Items-as-secondary range nerf below, we need the
                    // unit's primary skillset name and current support
                    // ability — Items at R=4 only applies when (primary ==
                    // Items) OR (support == "Throw Items").
                    var unitJobName = u.JobNameOverride ?? GameStateReporter.GetJobName(u.Job);
                    var unitPrimary = unitJobName != null
                        ? Utilities.CommandWatcher.GetPrimarySkillsetByJobName(unitJobName)
                        : null;
                    var unitSupport = u.SupportAbility;

                    abilities = FilterAbilitiesBySkillsets(u).Select(rawA =>
                    {
                        // Items abilities are stored at R=4 (Chemist primary).
                        // When run as secondary by a non-Chemist without
                        // Throw Items, the engine actually treats them as
                        // R=1. Rebind `a` to a copy with the adjusted
                        // range so EVERY downstream calc (target-tile
                        // expansion, projectile classifier, splash centers)
                        // sees the right value.
                        var skillsetForAbility = ActionAbilityLookup.GetSkillsetForAbilityId(rawA.Id);
                        var adjustedHRange = skillsetForAbility != null
                            ? ItemRangeAdjuster.Adjust(skillsetForAbility, rawA.HRange, unitPrimary, unitSupport)
                            : rawA.HRange;
                        var a = adjustedHRange == rawA.HRange
                            ? rawA
                            : rawA with { HRange = adjustedHRange };
                        var entry = new AbilityEntry
                        {
                            Name = a.Name,
                            Mp = a.MpCost,
                            HRange = a.HRange,
                            VRange = a.VRange,
                            AoE = a.AoE,
                            HoE = a.HoE,
                            Target = a.Target,
                            Effect = a.Effect,
                            CastSpeed = a.CastSpeed,
                            Element = a.Element,
                            AddedEffect = a.AddedEffect,
                            Reflectable = a.Reflectable,
                            Arithmetickable = a.Arithmetickable,
                        };

                        // Inventory-gated held count for Chemist Items / Samurai
                        // Iaido / Ninja Throw. SkillsetItemLookup returns null for
                        // any (skillset, ability) that isn't inventory-gated, so
                        // we probe all three and take the first non-null. Cheap
                        // when inventoryBytes is already cached in the outer scope.
                        if (inventoryBytes != null)
                        {
                            int? held =
                                SkillsetItemLookup.TryGetHeldCount("Items", a.Name, inventoryBytes)
                                ?? SkillsetItemLookup.TryGetHeldCount("Iaido", a.Name, inventoryBytes)
                                ?? SkillsetItemLookup.TryGetHeldCount("Throw", a.Name, inventoryBytes);
                            if (held.HasValue)
                            {
                                entry.HeldCount = held.Value;
                                entry.Unusable = held.Value == 0;
                            }
                        }

                        // Display name for a unit. Story chars use roster name.
                        // Player recruits (team 0) can trust GetJobName(Job) because
                        // the roster populates Job correctly. For enemies we only
                        // trust JobNameOverride (set by fingerprint lookup) — their
                        // raw Job byte is polluted by UI buffer leak (often shows
                        // Ramza's job), so we fall back to "?" for unfingerprinted.
                        string UnitDisplayName(ScannedUnit su)
                        {
                            if (!string.IsNullOrEmpty(su.Name)) return su.Name!;
                            if (!string.IsNullOrEmpty(su.JobNameOverride)) return su.JobNameOverride!;
                            if (su.Team == 0) return GameStateReporter.GetJobName(su.Job) ?? "?";
                            return "?";
                        }

                        // Shared helper: annotate a raw (x,y) with occupant info.
                        // Uses aliveByPos for normal abilities, reviveByPos for
                        // revival (dead + alive-undead — PD on Undead enemies
                        // is a kill move via reverse-revive).
                        bool isReviveAbility = AbilityTargetCalculator.IsRevivalAbility(a);
                        var tileIndex = isReviveAbility ? reviveByPos : aliveByPos;

                        // LoS rule: only physical projectiles (ranged Attack, Ninja
                        // Throw) are blocked by terrain. Magic/summons fly over walls.
                        // ProjectileAbilityClassifier encodes the canonical FFT rule.
                        string? abilitySkillset = ActionAbilityLookup.GetSkillsetForAbilityId(a.Id);
                        bool wantsLosCheck = abilityMap != null
                            && AbilityTargetCalculator.IsPointTarget(a)
                            && ProjectileAbilityClassifier.IsProjectile(abilitySkillset, a.Name, a.HRange);
                        int attackerElev = 0;
                        if (wantsLosCheck)
                        {
                            attackerElev = (int)System.Math.Round(abilityMap!.GetDisplayHeight(u.GridX, u.GridY));
                        }

                        ValidTargetTile AnnotateTile(int tx, int ty)
                        {
                            var tile = new ValidTargetTile { X = tx, Y = ty };
                            if (tileIndex.TryGetValue((tx, ty), out var occ))
                            {
                                if (occ == u)
                                    tile.Occupant = "self";
                                else if (occ.Team == 0 || occ.Team == 2)
                                    tile.Occupant = "ally";
                                else
                                    tile.Occupant = "enemy";
                                tile.UnitName = UnitDisplayName(occ);
                                tile.Affinity = ElementAffinityAnnotator.ComputeMarker(
                                    a.Element,
                                    occ.ElementAbsorb, occ.ElementCancel, occ.ElementHalf,
                                    occ.ElementWeak, occ.ElementStrengthen);
                                // Attack arc: only meaningful for enemy targets (backstab
                                // bonus applies to attacking foes), and requires known facing.
                                if (tile.Occupant == "enemy" && !string.IsNullOrEmpty(occ.Facing))
                                {
                                    tile.Arc = BackstabArcCalculator.ComputeArc(
                                        u.GridX, u.GridY, occ.GridX, occ.GridY, occ.Facing);
                                }
                                // Revive-ability intent tag (Phoenix Down, Raise, etc).
                                // PD on undead enemy = kill move; PD on dead enemy =
                                // resurrects them; etc. Surface so the agent picks
                                // the right tile for the right reason.
                                if (isReviveAbility)
                                {
                                    var intent = ReviveTargetClassifier.Classify(
                                        targetTeam: occ.Team,
                                        casterTeam: u.Team,
                                        targetHp: occ.Hp,
                                        targetStatusBytes: occ.StatusBytes);
                                    tile.Intent = intent switch
                                    {
                                        ReviveIntent.Revive => "REVIVE",
                                        ReviveIntent.ReviveEnemy => "REVIVE-ENEMY!",
                                        ReviveIntent.Ko => "KO",
                                        ReviveIntent.KoAlly => "KO-ALLY!",
                                        _ => null,
                                    };
                                }
                            }
                            if (wantsLosCheck && abilityMap != null)
                            {
                                int targetElev = (int)System.Math.Round(abilityMap.GetDisplayHeight(tx, ty));
                                bool clear = LineOfSightCalculator.HasLineOfSight(
                                    u.GridX, u.GridY, attackerElev,
                                    tx, ty, targetElev,
                                    (x, y) => (int)System.Math.Round(abilityMap.GetDisplayHeight(x, y)));
                                if (!clear) tile.LosBlocked = true;
                            }
                            return tile;
                        }

                        // Self-target abilities (HRange=Self): target is always the caster
                        if (a.HRange == "Self")
                        {
                            entry.ValidTargetTiles = new List<ValidTargetTile>
                            {
                                AnnotateTile(u.GridX, u.GridY)
                            };
                        }
                        // Point-target abilities (AoE=1, numeric HRange): the clicked
                        // tile IS the entire effect. Populate validTargetTiles with
                        // annotated per-tile occupant info.
                        else if (abilityMap != null && AbilityTargetCalculator.IsPointTarget(a))
                        {
                            var tiles = AbilityTargetCalculator.GetValidTargetTiles(
                                u.GridX, u.GridY, a, abilityMap, u.Jump);
                            if (tiles.Count > 0)
                            {
                                entry.ValidTargetTiles = tiles
                                    .OrderBy(t => t.y).ThenBy(t => t.x)
                                    .Select(t => AnnotateTile(t.x, t.y))
                                    .ToList();
                            }
                        }
                        // Radius-AoE abilities (AoE>1, numeric HRange): the clicked tile
                        // is the splash CENTER. Populate validTargetTiles with valid
                        // centers, then compute bestCenters — the top ~5 centers ranked
                        // by splash hit count — so Claude can pick an optimal placement.
                        else if (abilityMap != null && AbilityTargetCalculator.IsRadiusTarget(a))
                        {
                            var centers = AbilityTargetCalculator.GetValidTargetTiles(
                                u.GridX, u.GridY, a, abilityMap, u.Jump);
                            if (centers.Count > 0)
                            {
                                entry.ValidTargetTiles = centers
                                    .OrderBy(t => t.y).ThenBy(t => t.x)
                                    .Select(t => AnnotateTile(t.x, t.y))
                                    .ToList();

                                // Rank centers by splash hits. Enemy-target abilities
                                // favor max enemies with minimal friendly fire; ally-target
                                // abilities favor max allies caught in the splash.
                                // Summon abilities skip friendly tiles — no ally penalty.
                                bool wantsAlly = a.Target.Contains("ally") || a.Target.Contains("self");
                                bool isSummon = ActionAbilityLookup.IsSummonAbility(a.Name);
                                var scoredCenters = new List<(int score, SplashCenter center)>();
                                foreach (var c in centers)
                                {
                                    var splash = AbilityTargetCalculator.GetSplashTiles(
                                        c.x, c.y, a, abilityMap);
                                    var enemies = new List<string>();
                                    var allies = new List<string>();
                                    var enemyAff = new List<string?>();
                                    var allyAff = new List<string?>();
                                    foreach (var st in splash)
                                    {
                                        if (!aliveByPos.TryGetValue(st, out var hitUnit)) continue;
                                        string? aff = ElementAffinityAnnotator.ComputeMarker(
                                            a.Element,
                                            hitUnit.ElementAbsorb, hitUnit.ElementCancel,
                                            hitUnit.ElementHalf, hitUnit.ElementWeak,
                                            hitUnit.ElementStrengthen);
                                        if (hitUnit.Team == 0 || hitUnit.Team == 2)
                                        {
                                            // Summons: ally-target (Moogle/Carbuncle/Faerie) hits allies;
                                            // enemy-target (Shiva/Ramuh/etc.) skips allies entirely.
                                            if (!isSummon || wantsAlly)
                                            {
                                                allies.Add(UnitDisplayName(hitUnit));
                                                allyAff.Add(aff);
                                            }
                                        }
                                        else
                                        {
                                            // Summons: enemy-target hits enemies;
                                            // ally-target (Moogle/Carbuncle/Faerie) skips enemies.
                                            if (!isSummon || !wantsAlly)
                                            {
                                                enemies.Add(UnitDisplayName(hitUnit));
                                                enemyAff.Add(aff);
                                            }
                                        }
                                    }
                                    if (enemies.Count == 0 && allies.Count == 0) continue;
                                    int score = AbilityTargetCalculator.ComputeSplashScore(
                                        enemies.Count, allies.Count, wantsAlly, isSummon);
                                    // Element-affinity adjustment — weak enemies rank
                                    // higher, absorbing enemies rank lower. Silent when
                                    // ability has no element (affinity lists are null).
                                    score += AbilityTargetCalculator.SplashAffinityAdjustment(
                                        enemyAff, allyAff, wantsAlly);
                                    // Only attach affinity lists when the ability has an
                                    // element AND at least one hit has a non-null marker.
                                    bool anyEnemyAff = enemyAff.Exists(x => x != null);
                                    bool anyAllyAff = allyAff.Exists(x => x != null);
                                    scoredCenters.Add((score, new SplashCenter
                                    {
                                        X = c.x, Y = c.y,
                                        Enemies = enemies,
                                        Allies = allies,
                                        EnemyAffinities = anyEnemyAff ? enemyAff : null,
                                        AllyAffinities = anyAllyAff ? allyAff : null,
                                    }));
                                }
                                if (scoredCenters.Count > 0)
                                {
                                    entry.BestCenters = scoredCenters
                                        .OrderByDescending(t => t.score)
                                        .ThenBy(t => t.center.Y).ThenBy(t => t.center.X)
                                        .Take(5)
                                        .Select(t => t.center)
                                        .ToList();
                                }
                            }
                        }
                        // Line abilities (Shape=Line): caster picks a cardinal
                        // direction by clicking a seed tile. validTargetTiles holds
                        // the up-to-4 valid seed tiles; bestDirections ranks each
                        // direction by line hits so Claude can pick the best aim.
                        else if (abilityMap != null && AbilityTargetCalculator.IsLineTarget(a))
                        {
                            var seeds = AbilityTargetCalculator.GetValidTargetTiles(
                                u.GridX, u.GridY, a, abilityMap, u.Jump);
                            if (seeds.Count > 0)
                            {
                                entry.ValidTargetTiles = seeds
                                    .OrderBy(t => t.y).ThenBy(t => t.x)
                                    .Select(t => AnnotateTile(t.x, t.y))
                                    .ToList();

                                bool wantsAllyLine = a.Target.Contains("ally") || a.Target.Contains("self");
                                var scoredDirections = new List<(int score, DirectionalHit hit)>();
                                foreach (var (label, dx, dy) in AbilityTargetCalculator.CardinalDirections)
                                {
                                    int seedX = u.GridX + dx;
                                    int seedY = u.GridY + dy;
                                    // Skip directions whose seed isn't valid (off-map,
                                    // unwalkable, or fails HoE from caster).
                                    if (!seeds.Contains((seedX, seedY))) continue;

                                    var lineTiles = AbilityTargetCalculator.GetLineTiles(
                                        u.GridX, u.GridY, dx, dy, a, abilityMap);
                                    var enemies = new List<string>();
                                    var allies = new List<string>();
                                    var enemyAff = new List<string?>();
                                    var allyAff = new List<string?>();
                                    foreach (var lt in lineTiles)
                                    {
                                        if (!aliveByPos.TryGetValue(lt, out var hitUnit)) continue;
                                        string? aff = ElementAffinityAnnotator.ComputeMarker(
                                            a.Element,
                                            hitUnit.ElementAbsorb, hitUnit.ElementCancel,
                                            hitUnit.ElementHalf, hitUnit.ElementWeak,
                                            hitUnit.ElementStrengthen);
                                        if (hitUnit.Team == 0 || hitUnit.Team == 2)
                                        {
                                            allies.Add(UnitDisplayName(hitUnit));
                                            allyAff.Add(aff);
                                        }
                                        else
                                        {
                                            enemies.Add(UnitDisplayName(hitUnit));
                                            enemyAff.Add(aff);
                                        }
                                    }
                                    if (enemies.Count == 0 && allies.Count == 0) continue;
                                    int score = wantsAllyLine ? allies.Count : (enemies.Count - allies.Count);
                                    score += AbilityTargetCalculator.SplashAffinityAdjustment(
                                        enemyAff, allyAff, wantsAllyLine);
                                    bool anyEnemyAff = enemyAff.Exists(x => x != null);
                                    bool anyAllyAff = allyAff.Exists(x => x != null);
                                    scoredDirections.Add((score, new DirectionalHit
                                    {
                                        Direction = label,
                                        Seed = new[] { seedX, seedY },
                                        Enemies = enemies,
                                        Allies = allies,
                                        EnemyAffinities = anyEnemyAff ? enemyAff : null,
                                        AllyAffinities = anyAllyAff ? allyAff : null,
                                    }));
                                }
                                if (scoredDirections.Count > 0)
                                {
                                    entry.BestDirections = scoredDirections
                                        .OrderByDescending(t => t.score)
                                        .ThenBy(t => t.hit.Direction)
                                        .Select(t => t.hit)
                                        .ToList();
                                }
                            }
                        }

                        // Self-radius abilities (HRange="Self", AoE>1): splash
                        // centered on the caster with no target-picking. Emit the
                        // affected tiles directly so Claude can see what would be
                        // hit (Cyclone's enemies, Chakra's allies, etc.).
                        else if (abilityMap != null && AbilityTargetCalculator.IsSelfRadius(a))
                        {
                            var splash = AbilityTargetCalculator.GetSelfRadiusTiles(
                                u.GridX, u.GridY, a, abilityMap);
                            if (splash.Count > 0)
                            {
                                entry.ValidTargetTiles = splash
                                    .OrderBy(t => t.y).ThenBy(t => t.x)
                                    .Select(t => AnnotateTile(t.x, t.y))
                                    .ToList();
                            }
                        }

                        return entry;
                    }).ToList();

                    // Cache valid-target tiles per ability for up-front
                    // range validation in battle_ability. Only populated for
                    // the active player unit (validTargetTiles isn't computed
                    // for non-active units). Lookup is case-insensitive.
                    if (isActive && abilities != null)
                    {
                        var abilityCache = new Dictionary<string, HashSet<(int x, int y)>>(
                            System.StringComparer.OrdinalIgnoreCase);
                        foreach (var ae in abilities)
                        {
                            if (string.IsNullOrEmpty(ae.Name)) continue;
                            if (ae.ValidTargetTiles == null) continue;
                            var tiles = new HashSet<(int x, int y)>(
                                ae.ValidTargetTiles.Select(t => (t.X, t.Y)));
                            abilityCache[ae.Name] = tiles;
                        }
                        _lastValidAbilityTiles = abilityCache;
                    }
                }
                else if (u.Team != 0 && jobName != null)
                {
                    var monsterAbs = MonsterAbilities.GetAbilities(jobName);
                    if (monsterAbs != null && monsterAbs.Length > 0)
                    {
                        // Look up full metadata (range, AoE, target, element, effect) for each
                        // ability name from MonsterAbilityLookup. Falls back to name-only for
                        // abilities not yet in the metadata dict.
                        abilities = monsterAbs.Select(name =>
                        {
                            var info = MonsterAbilityLookup.GetByName(name);
                            if (info == null)
                                return new AbilityEntry { Name = name };
                            return new AbilityEntry
                            {
                                Name = info.Name,
                                Mp = info.MpCost,
                                HRange = info.HRange,
                                VRange = info.VRange,
                                AoE = info.AoE,
                                HoE = info.HoE,
                                Target = info.Target,
                                Effect = info.Effect,
                                CastSpeed = info.CastSpeed,
                                Element = info.Element,
                                AddedEffect = info.AddedEffect,
                                Reflectable = info.Reflectable,
                                Arithmetickable = info.Arithmetickable,
                            };
                        }).ToList();
                    }
                }

                // Display height: matches Move tiles' h= rendering (Height + SlopeHeight/2)
                // so the shell can compare caster vs enemy elevation directly.
                double tileH = 0;
                if (_mapLoader?.CurrentMap is var mapForH && mapForH != null
                    && mapForH.InBounds(u.GridX, u.GridY))
                {
                    var t = mapForH.Tiles[u.GridX, u.GridY];
                    tileH = t.Height + t.SlopeHeight / 2.0;
                }

                battleState.Units.Add(new BattleUnitState
                {
                    Name = u.Name,
                    Team = u.Team,
                    JobId = u.Job,
                    // For enemies, unit.Job is polluted by UI buffer leak (often shows
                    // the active player's job). Only trust fingerprint match for them.
                    // Players can fall back to GetJobName(u.Job) since roster sets it.
                    JobName = jobName,
                    Level = u.Level,
                    X = u.GridX,
                    Y = u.GridY,
                    H = tileH,
                    Hp = u.Hp,
                    MaxHp = u.MaxHp,
                    Mp = u.Mp,
                    MaxMp = u.MaxMp,
                    PositionKnown = true,
                    Distance = isActive ? 0 : dist,
                    IsActive = isActive,
                    CT = u.CT,
                    Speed = u.Speed,
                    // Facing decoded from the unit's slot +0x35 byte in the static
                    // battle array. Session 30 live-verified encoding. See
                    // memory/project_facing_byte_s30.md. Null when out-of-range
                    // (shouldn't occur in normal play).
                    Facing = u.Facing,
                    ElementAbsorb = u.ElementAbsorb,
                    ElementNull = u.ElementCancel,
                    ElementHalf = u.ElementHalf,
                    ElementWeak = u.ElementWeak,
                    ElementStrengthen = u.ElementStrengthen,
                    SecondaryAbility = u.SecondaryAbility,
                    LifeState = StatusDecoder.GetLifeState(u.StatusBytes) is var ls && ls != "alive" ? ls
                        : (u.Hp <= 0 && u.MaxHp > 0 ? "dead" : null),
                    // Filter Crystal/Dead/Treasure/Petrify out of the alive-statuses
                    // list — they're surfaced separately as lifeState. Without
                    // the filter the rendered row carried both `[Treasure]`
                    // (status bit) and ` TREASURE` (lifeState), and worse
                    // collapsed alive `[Defending,Charging]` together with
                    // life-state `[Treasure,Dead]` into one bracketed blob —
                    // playtest #7 friction.
                    Statuses = StatusDecoder.DecodeAliveStatuses(u.StatusBytes) is var s && s.Count > 0 ? s : null,
                    Abilities = abilities,
                    Reaction = u.ReactionAbility,
                    Support = u.SupportAbility,
                    Movement = u.MovementAbility,
                    // Live heap Move/Jump is only populated for the active unit
                    // (CollectUnitPositionsFull runs TryReadMoveJumpFromHeap once).
                    // For non-active units, fall back to JobBaseStatsTable via
                    // MoveJumpFallbackResolver — approximate class base values,
                    // enough for threat assessment (Claude reads "enemy 4 tiles
                    // out with Mv=4" as reachable next turn without needing live
                    // effective stats).
                    Move = MoveJumpFallbackResolver.Resolve(u.Move, u.Jump, jobName).move,
                    Jump = MoveJumpFallbackResolver.Resolve(u.Move, u.Jump, jobName).jump,
                    // Pre-compute weapon banner tag for the active-unit header.
                    // Null when unarmed or no known weapon in equipment.
                    WeaponTag = (u.Team == 0 && u.Equipment != null)
                        ? (ItemData.ComposeWeaponTag(u.Equipment) is var _wt && !string.IsNullOrEmpty(_wt) ? _wt : null)
                        : null,
                    // Pre-compute non-weapon equipment summary (shield/helm/body/
                    // accessory) so the active-unit line can surface defensive
                    // loadout. Null when only weapon equipped or no roster data.
                    EquipmentTag = (u.Team == 0 && u.Equipment != null)
                        ? (ItemData.ComposeEquipmentTag(u.Equipment) is var _et && !string.IsNullOrEmpty(_et) ? _et : null)
                        : null,
                });

                // Prepend basic Attack to the active player unit's ability list.
                // Range determined by equipped weapon (gun=8, bow=5, crossbow=4, melee=1).
                // VR=0 falls back to casterJump via the calculator.
                if (isActive && u.Team == 0)
                {
                    var abilityMap = _mapLoader?.CurrentMap;
                    var attackInfo = ItemData.BuildAttackAbilityInfo(u.Equipment);
                    var attackEntry = new AbilityEntry
                    {
                        Name = "Attack",
                        HRange = attackInfo.HRange,
                        AoE = 1,
                        Target = "enemy",
                        Effect = attackInfo.Effect,
                    };
                    if (abilityMap != null)
                    {
                        var attackTiles = AbilityTargetCalculator.GetValidTargetTiles(
                            u.GridX, u.GridY, attackInfo, abilityMap, u.Jump);
                        if (attackTiles.Count > 0)
                        {
                            // Session 48: thread LoS through basic Attack too. Ranged
                            // Attack (bow / gun / crossbow) is a physical projectile,
                            // so the canonical projectile rule applies — classifier
                            // already knows to check range>1 via attackInfo.HRange.
                            bool wantsLos = ProjectileAbilityClassifier.IsProjectile(
                                "Attack", "Attack", attackInfo.HRange);
                            int attackerElevA = wantsLos
                                ? (int)System.Math.Round(abilityMap.GetDisplayHeight(u.GridX, u.GridY))
                                : 0;

                            // Cache the basic-Attack range for battle_attack
                            // up-front validation. Without this, an out-of-
                            // range battle_attack call enters the targeting
                            // mode, navigates the cursor anywhere (including
                            // off-grid like (0,0)), and surfaces "MISSED" or
                            // a confusing nav error instead of a clear range
                            // rejection. Mirrors _lastValidMoveTiles cached
                            // from the move BFS above.
                            _lastValidAttackTiles = new HashSet<(int, int)>(
                                attackTiles.Select(t => (t.x, t.y)));

                            attackEntry.ValidTargetTiles = attackTiles
                                .OrderBy(t => t.y).ThenBy(t => t.x)
                                .Select(t =>
                                {
                                    var tile = new ValidTargetTile { X = t.x, Y = t.y };
                                    if (aliveByPos.TryGetValue((t.x, t.y), out var occ))
                                    {
                                        tile.Occupant = occ == u ? "self"
                                            : (occ.Team == 0 || occ.Team == 2) ? "ally"
                                            : "enemy";
                                        tile.UnitName = !string.IsNullOrEmpty(occ.Name) ? occ.Name
                                            : !string.IsNullOrEmpty(occ.JobNameOverride) ? occ.JobNameOverride
                                            : occ.Team == 0 ? GameStateReporter.GetJobName(occ.Job) ?? "?"
                                            : "?";
                                    }
                                    if (wantsLos)
                                    {
                                        int targetElev = (int)System.Math.Round(abilityMap.GetDisplayHeight(t.x, t.y));
                                        bool clear = LineOfSightCalculator.HasLineOfSight(
                                            u.GridX, u.GridY, attackerElevA,
                                            t.x, t.y, targetElev,
                                            (x, y) => (int)System.Math.Round(abilityMap.GetDisplayHeight(x, y)));
                                        if (!clear) tile.LosBlocked = true;
                                    }
                                    return tile;
                                })
                                .ToList();
                        }
                    }
                    var unitState = battleState.Units[battleState.Units.Count - 1];
                    unitState.Abilities ??= new List<AbilityEntry>();
                    unitState.Abilities.Insert(0, attackEntry);
                }
            }

            // --- Helper: filter abilities to only equipped skillsets ---
            List<ActionAbilityInfo> FilterAbilitiesBySkillsets(ScannedUnit unit)
            {
                var jobName = unit.JobNameOverride ?? GameStateReporter.GetJobName(unit.Job);
                var primary = jobName != null
                    ? Utilities.CommandWatcher.GetPrimarySkillsetByJobName(jobName)
                    : null;
                var secondary = unit.SecondaryAbility > 0
                    ? Utilities.CommandWatcher.GetSkillsetName(unit.SecondaryAbility)
                    : null;

                // Primary source of truth: the per-job learned-action bitfield read from
                // the roster slot at +0x32 + jobIdx*3. This is the ONLY reliable way to
                // see what Ramza has learned across all jobs, because the condensed struct
                // at +0x28 always holds his Mettle list regardless of current primary.
                // Fall back to the condensed struct for units we couldn't roster-match.
                if (unit.LearnedBitfieldByJobIdx != null && unit.LearnedBitfieldByJobIdx.Count > 0)
                {
                    var result = new List<ActionAbilityInfo>();

                    if (primary != null)
                    {
                        int primaryJobIdx = AbilityData.GetJobIdxBySkillsetName(primary);
                        if (primaryJobIdx >= 0 && unit.LearnedBitfieldByJobIdx.TryGetValue(primaryJobIdx, out var pBytes))
                        {
                            result.AddRange(ActionAbilityLookup.GetLearnedAbilitiesFromBitfield(
                                primary, pBytes.byte0, pBytes.byte1));
                        }
                    }

                    if (secondary != null && secondary != primary)
                    {
                        int secondaryJobIdx = AbilityData.GetJobIdxBySkillsetName(secondary);
                        if (secondaryJobIdx >= 0 && unit.LearnedBitfieldByJobIdx.TryGetValue(secondaryJobIdx, out var sBytes))
                        {
                            result.AddRange(ActionAbilityLookup.GetLearnedAbilitiesFromBitfield(
                                secondary, sBytes.byte0, sBytes.byte1));
                        }
                    }

                    if (result.Count > 0) return result;
                }

                // Fallback: filter the condensed struct's learned list by equipped skillsets.
                var equipped = new List<string>();
                if (primary != null) equipped.Add(primary);
                if (secondary != null) equipped.Add(secondary);

                if (equipped.Count == 0)
                    return unit.LearnedAbilities; // can't filter, return all

                return ActionAbilityLookup.FilterBySkillsets(unit.LearnedAbilities, equipped);
            }

            // Turn order: the C+Up scan traverses units in timeline order.
            // The scan order IS the turn order — unit 1 scanned = next to act, etc.
            var turnOrder = new List<TurnOrderEntry>();
            foreach (var u in units)
            {
                bool isActive = u == ally;
                turnOrder.Add(new TurnOrderEntry
                {
                    Name = u.Name,
                    Team = u.Team == 0 ? "PLAYER" : u.Team == 2 ? "ALLY" : "ENEMY",
                    Level = u.Level,
                    Hp = u.Hp,
                    MaxHp = u.MaxHp,
                    X = u.GridX,
                    Y = u.GridY,
                    CT = u.CT,
                    IsActive = isActive,
                });
            }
            battleState.TurnOrder = turnOrder.Count > 0 ? turnOrder : null;
            battleState.BattleWon = BattleFieldHelper.AllEnemiesDefeated(battleState.Units);

            response.Battle = battleState;

            // Diagnostic lines for logging only (not in response)
            var lines = new List<string>();

            // 5. Auto-detect map — validate any pre-loaded map against unit positions + ally height
            if (_mapLoader != null)
            {
                var allPositions = units.Where(u => u.GridX >= 0 && u.GridY >= 0)
                                        .Select(u => (u.GridX, u.GridY)).ToList();

                // Read ally height once for all validation steps
                var heightResult = _explorer.ReadAbsolute((nint)0x140C6492C, 4);
                double allyHeight = heightResult != null ? (double)heightResult.Value.value / 10.0 : -1;

                // Validate bounds + walkability only. Height is unreliable for validation
                // (memory address may not correspond to map tile height) — used as
                // tiebreaker in DetectMap fingerprinting instead.
                bool ValidateMap(MapData map)
                {
                    return allPositions.All(p => map.InBounds(p.Item1, p.Item2) && map.IsWalkable(p.Item1, p.Item2));
                }

                // Resolve location ID (from screen or persisted file)
                var currentScreen = _detectScreen();
                int locId = currentScreen?.Location ?? -1;
                if (locId < 0 || locId > 42)
                {
                    try
                    {
                        var lastLocPath = System.IO.Path.Combine(_mapLoader.MapDataDir, "..", "last_location.txt");
                        if (System.IO.File.Exists(lastLocPath))
                            locId = int.Parse(System.IO.File.ReadAllText(lastLocPath).Trim());
                    }
                    catch { }
                }

                // Try 0 (session 48): the game's own scenario struct lives on the
                // heap with the REAL map ID at +0x30. Authoritative for battle
                // map — doesn't drift when a random-encounter fires at a
                // different location than the travel target (had us loading
                // MAP076 Zeklaus while the game was on MAP086 Dugeura Pass
                // during a mid-travel encounter). The scan returns multiple
                // candidates in preference order. We further narrow the pool
                // by dimension-tightness: among candidates that ValidateMap
                // passes, pick the one with the smallest excess width/height
                // beyond the max unit position (same scoring as DetectMap).
                // Session 48 2026-04-19: authoritative live map-id byte at
                // 0x14077D83C — found via snapshot/diff between Dugeura Pass
                // (map 86 / 0x56) and Beddha Sandwaste (map 82 / 0x52). Eight
                // addresses flip in lockstep; picked the lowest. This byte is
                // the game's own current-battle-map id — doesn't drift from
                // rawLocation like the locId-based lookups did (random
                // encounter fired at Dugeura while rawLocation stayed on
                // Zeklaus travel target). Verified live.
                {
                    var mapIdRead = _explorer.ReadAbsolute((nint)LiveBattleMapId.Address, 1);
                    if (mapIdRead.HasValue)
                    {
                        int liveMapId = (int)mapIdRead.Value.value;
                        if (LiveBattleMapId.IsValid(liveMapId))
                        {
                            var loaded = _mapLoader.LoadMap(liveMapId);
                            if (loaded != null && ValidateMap(loaded))
                            {
                                lines.Add($"MAP{liveMapId:D3} (live map-id byte 0x{LiveBattleMapId.Address:X})");
                            }
                            else
                            {
                                ModLogger.Log($"[Map] Live map-id {liveMapId} failed ValidateMap; falling through");
                                _mapLoader.ClearMap();
                            }
                        }
                    }
                }

                // Try 1: Random encounter map lookup — fallback when DetectMap
                // can't resolve (e.g. before the battle has units in place).
                if (_mapLoader.CurrentMap == null && locId >= 0 && locId <= 42)
                {
                    int reMap = _mapLoader.GetRandomEncounterMap(locId);
                    if (reMap >= 0)
                    {
                        var loaded = _mapLoader.LoadMap(reMap);
                        if (loaded != null && ValidateMap(loaded))
                        {
                            lines.Add($"MAP{reMap:D3} (random encounter, loc {locId})");
                        }
                        else
                        {
                            _mapLoader.ClearMap();
                        }
                    }
                }

                // Try 2: Story battle map lookup
                if (_mapLoader.CurrentMap == null && locId >= 0 && locId <= 42)
                {
                    var locMap = _mapLoader.LoadMapForLocation(locId);
                    if (locMap != null)
                    {
                        if (ValidateMap(locMap))
                        {
                            lines.Add($"MAP{locMap.MapNumber:D3} (location {locId} lookup)");
                        }
                        else
                        {
                            ModLogger.Log($"[Map] Location lookup MAP{locMap.MapNumber:D3} invalid, trying fingerprint");
                            _mapLoader.ClearMap();
                        }
                    }
                }

                // Try 2: Fingerprint detection (unit positions + height)
                if (_mapLoader.CurrentMap == null && allPositions.Count > 0)
                {
                    int detected = _mapLoader.DetectMap(allPositions, ally.GridX, ally.GridY, allyHeight);
                    if (detected >= 0)
                    {
                        _mapLoader.LoadMap(detected);
                        lines.Add($"MAP{detected:D3} (fingerprint, allyH={allyHeight})");
                    }
                    else
                    {
                        lines.Add($"MAP DETECTION FAILED (allyH={allyHeight}, {units.Count} units)");
                    }
                }
            }

            // 6. Compute valid tiles if map is loaded. Delegates to
            // MovementBfs.ComputeValidTiles so scan_move and the live-Move-mode
            // tile renderer share the same canonical edge-height + ally-traversal
            // rules (session 29 pt.5/6). Previously this inlined its own BFS that
            // used display-height averaging and treated all occupied tiles as
            // blocking — which broke Wilham's move-through-ally paths.
            var validPaths = new Dictionary<string, PathEntry>();
            if (_mapLoader?.CurrentMap != null)
            {
                var map = _mapLoader.CurrentMap;
                double GetDisplayHeight(int x, int y)
                {
                    if (!map.InBounds(x, y)) return -1;
                    var t = map.Tiles[x, y];
                    return t.Height + t.SlopeHeight / 2.0;
                }

                // Build ally/enemy position sets from the scan just performed.
                // The active unit is excluded from both sets (it's the start).
                // Dead units (corpses) occupy their tile and cannot be stopped on,
                // but DO allow pass-through — same BFS semantics as allies. Crystal
                // and treasure "units" have left the field, so their tile is empty
                // (skip them entirely).
                var enemySet = new HashSet<(int, int)>();
                var allySet = new HashSet<(int, int)>();
                foreach (var u in units)
                {
                    if (u == ally) continue;
                    var lifeState = StatusDecoder.GetLifeState(u.StatusBytes);
                    if (lifeState == "crystal" || lifeState == "treasure") continue;
                    if (lifeState == "dead")
                    {
                        allySet.Add((u.GridX, u.GridY));
                        continue;
                    }
                    if (u.Hp <= 0) continue; // extra safety: HP=0 but lifeState=alive shouldn't happen
                    if (u.Team == 1) enemySet.Add((u.GridX, u.GridY));
                    else if (u.Team == 0) allySet.Add((u.GridX, u.GridY));
                    // team==2 (neutral/NPC): don't block — see feedback_summon_no_friendly_fire memory.
                }

                var tiles = MovementBfs.ComputeValidTiles(
                    map, ally.GridX, ally.GridY, moveStat, jumpStat,
                    enemyPositions: enemySet, allyPositions: allySet);

                // Self-correction: if BFS found 0 tiles, the map is probably wrong.
                if (tiles.Count == 0 && moveStat > 0)
                {
                    ModLogger.Log($"[ScanMove] BFS returned 0 tiles on MAP{map.MapNumber:D3} — rejecting and retrying");
                    _mapLoader.RejectCurrentMap();

                    var allPositions2 = units.Select(u => (u.GridX, u.GridY)).ToList();
                    var heightResult2 = _explorer.ReadAbsolute((nint)0x140C6492C, 4);
                    double allyHeight2 = heightResult2 != null ? (double)heightResult2.Value.value / 10.0 : -1;
                    int retryMap = _mapLoader.DetectMap(allPositions2, ally.GridX, ally.GridY, allyHeight2);
                    if (retryMap >= 0)
                    {
                        _mapLoader.LoadMap(retryMap);
                        lines.Add($"MAP{map.MapNumber:D3} rejected (0 tiles), retried → MAP{retryMap:D3}");
                    }
                    else
                    {
                        lines.Add($"MAP{map.MapNumber:D3} rejected (0 tiles), no alternative found");
                    }
                }

                // Attach display-height so the compact renderer can show h= values.
                var tileList = tiles
                    .Select(tp => new Utilities.TilePosition
                    {
                        X = tp.X,
                        Y = tp.Y,
                        H = GetDisplayHeight(tp.X, tp.Y)
                    })
                    .ToList();

                // Sort tiles by min Manhattan distance to nearest enemy so
                // the most-actionable repositions appear first in the
                // rendered list. BFS-visit order is unhelpful — LLM agent
                // had to mentally cross-ref against the unit list to find
                // tiles near enemies (live-flagged 2026-04-25 playtest).
                var enemyList = enemySet.Select(e => (e.Item1, e.Item2)).ToList();
                tileList = MoveTileSorter.SortByNearestEnemy(tileList, enemyList);

                // Cache for battle_move validation
                _lastValidMoveTiles = new HashSet<(int, int)>(tileList.Select(t => (t.X, t.Y)));

                validPaths["ValidMoveTiles"] = new PathEntry
                {
                    Desc = $"{tileList.Count} tiles from ({ally.GridX},{ally.GridY}) Mv={moveStat} Jmp={jumpStat} enemies={enemySet.Count}",
                    Tiles = tileList
                };

                lines.Add($"validTiles={tileList.Count} move={moveStat} jump={jumpStat} enemies={enemySet.Count} allies={allySet.Count}");
            }
            else
            {
                lines.Add("NO MAP — detection failed and no manual set_map");
            }

            // Attack tiles: 4 cardinal neighbors of active unit with arrow key mapping
            {
                var camResult = _explorer.ReadAbsolute((nint)AddrCameraRotation, 1);
                int rotation = camResult != null ? (int)((camResult.Value.value - 1 + 4) % 4) : 0;
                string[] dirNames = { "Right", "Left", "Up", "Down" };
                int[][] cardinals = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1} };
                var attackTileList = new List<AttackTileInfo>();
                foreach (var delta in cardinals)
                {
                    int tx = ally.GridX + delta[0];
                    int ty = ally.GridY + delta[1];
                    int dir = FindDirForDelta(rotation, delta[0], delta[1]);
                    string arrowName = dirNames[dir];
                    var occupantUnit = units.FirstOrDefault(u => u.GridX == tx && u.GridY == ty && u != ally);
                    // Dead / crystal / treasure / petrified occupants, and
                    // HP<=0 units with not-yet-propagated status bits, are
                    // not attackable — render the tile as "empty" so
                    // scan_move output doesn't suggest wasted actions
                    // against corpses. Extracted to AttackTileOccupantClassifier
                    // 2026-04-24 to add the HP>0 guard behind live-repro.
                    string occupant = AttackTileOccupantClassifier.ClassifyOccupant(
                        hasOccupant: occupantUnit != null,
                        hp: occupantUnit?.Hp ?? 0,
                        statusBytes: occupantUnit?.StatusBytes,
                        team: occupantUnit?.Team ?? 0);
                    bool occupantAttackable = occupant == "ally" || occupant == "enemy";
                    var tile = new AttackTileInfo { X = tx, Y = ty, Arrow = arrowName, Occupant = occupant };
                    // Surface the job name for any known occupant (attackable
                    // or not) so the render can show "dead (Bomb)" rather
                    // than a bare "dead". Only compute HP/Arc for attackable
                    // ones (Arc requires a live facing direction and HP
                    // display is meaningless on corpses).
                    if (occupantUnit != null)
                    {
                        tile.JobName = occupantUnit.JobNameOverride
                            ?? (occupantUnit.Team == 0 ? GameStateReporter.GetJobName(occupantUnit.Job) : null);
                    }
                    if (occupantAttackable)
                    {
                        tile.Hp = occupantUnit!.Hp;
                        tile.MaxHp = occupantUnit.MaxHp;
                        if (occupant == "enemy" && !string.IsNullOrEmpty(occupantUnit.Facing))
                        {
                            tile.Arc = BackstabArcCalculator.ComputeArc(
                                ally.GridX, ally.GridY, occupantUnit.GridX, occupantUnit.GridY, occupantUnit.Facing);
                        }
                    }
                    // Mark whether the basic Attack can actually reach this
                    // cardinal tile. Ranged weapons have MinRange ≥ 2, so
                    // d=1 cardinals are out of range. Match against the
                    // ability's valid-tile set we just populated.
                    tile.InRange = _lastValidAttackTiles != null
                        && _lastValidAttackTiles.Contains((tx, ty));
                    attackTileList.Add(tile);
                }
                validPaths["AttackTiles"] = new PathEntry
                {
                    Desc = $"4 cardinal tiles from ({ally.GridX},{ally.GridY}) rot={rotation}",
                    AttackTiles = attackTileList
                };
            }

            // Recommended facing for Wait command
            {
                var livingEnemies = units
                    .Where(u => u.Team == 1 && u.Hp > 0)
                    .Select(u => new FacingStrategy.UnitPosition
                    {
                        GridX = u.GridX, GridY = u.GridY,
                        Team = u.Team, Hp = u.Hp, MaxHp = u.MaxHp
                    })
                    .ToList();
                var allyPos = new FacingStrategy.UnitPosition
                {
                    GridX = ally.GridX, GridY = ally.GridY,
                    Team = ally.Team, Hp = ally.Hp, MaxHp = ally.MaxHp
                };
                // Session 47: delegate through FacingDecider so the
                // dx/dy→name formatting lives in one place.
                var decision = FacingDecider.Decide(null, allyPos, livingEnemies);
                validPaths["RecommendedFacing"] = new PathEntry
                {
                    Desc = $"Face {decision.DirectionName} — {decision.Front} front, {decision.Side} side, {decision.Back} back",
                    Facing = new FacingInfo
                    {
                        Dx = decision.Dx,
                        Dy = decision.Dy,
                        Direction = decision.DirectionName,
                        Front = decision.Front,
                        Side = decision.Side,
                        Back = decision.Back
                    }
                };
            }

            if (lines.Count > 0)
                ModLogger.Log($"[ScanMove] {string.Join(" | ", lines)}");

            response.Status = "completed";
            response.ValidPaths = validPaths;
            return response;
        }

        /// <summary>
        /// auto_move: Full autonomous turn — scan units, compute valid tiles,
        /// pick safest tile (farthest from enemies), move there, wait for next turn.
        /// Handles rotation detection empirically (test press + verify).
        /// locationId = move stat, unitIndex = jump stat.
        /// </summary>
        private CommandResponse AutoMove(CommandResponse response, CommandRequest command)
        {
            var screen = _detectScreen();
            if (screen == null || screen.Name != "BattleMyTurn")
            {
                response.Status = "failed";
                response.Error = $"Not on BattleMyTurn (current: {screen?.Name ?? "null"})";
                return response;
            }

            int moveStat = command.LocationId > 0 ? command.LocationId : 4;
            int jumpStat = command.UnitIndex > 0 ? command.UnitIndex : 3;

            // 1. Scan all units
            var units = CollectUnitPositionsFull();
            var ally = units.FirstOrDefault(u => u.Team == 0);
            if (ally == null)
            {
                response.Status = "failed";
                response.Error = "No ally found";
                return response;
            }

            var enemyPositions = GetEnemyPositions();
            int curDist = enemyPositions.Count > 0
                ? enemyPositions.Min(e => Math.Abs(ally.GridX - e.Item1) + Math.Abs(ally.GridY - e.Item2))
                : 99;

            // 2. Compute valid tiles from map
            var map = _mapLoader?.CurrentMap;
            if (map == null)
            {
                response.Status = "failed";
                response.Error = "No map loaded — call set_map first";
                return response;
            }

            // BFS
            double GetDisplayHeight(int x, int y)
            {
                if (!map.InBounds(x, y)) return -1;
                var t = map.Tiles[x, y];
                return t.Height + t.SlopeHeight / 2.0;
            }

            var visited = new Dictionary<(int, int), int>();
            var queue = new Queue<(int x, int y, int cost)>();
            visited[(ally.GridX, ally.GridY)] = 0;
            queue.Enqueue((ally.GridX, ally.GridY, 0));
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
                    if (enemyPositions.Contains((nx, ny))) continue;
                    double nh = GetDisplayHeight(nx, ny);
                    if (nh < 0 || ch < 0) continue;
                    if (Math.Abs(nh - ch) > jumpStat) continue;
                    int nc = cost + 1;
                    if (nc > moveStat) continue;
                    if (!visited.ContainsKey((nx, ny)) || visited[(nx, ny)] > nc)
                    {
                        visited[(nx, ny)] = nc;
                        queue.Enqueue((nx, ny, nc));
                    }
                }
            }
            visited.Remove((ally.GridX, ally.GridY));

            // 3. Pick safest tile
            int bestX = ally.GridX, bestY = ally.GridY;
            int bestDist = curDist;
            foreach (var kv in visited)
            {
                int d = enemyPositions.Count > 0
                    ? enemyPositions.Min(e => Math.Abs(kv.Key.Item1 - e.Item1) + Math.Abs(kv.Key.Item2 - e.Item2))
                    : 99;
                if (d > bestDist)
                {
                    bestDist = d;
                    bestX = kv.Key.Item1;
                    bestY = kv.Key.Item2;
                }
            }

            var enemyStr = string.Join(" ", enemyPositions.Select(e => $"({e.Item1},{e.Item2})"));
            bool shouldMove = (bestX != ally.GridX || bestY != ally.GridY);

            if (shouldMove)
            {
                ModLogger.Log($"[AutoMove] ({ally.GridX},{ally.GridY})->({bestX},{bestY}) dist {curDist}->{bestDist} enemies={enemyStr}");

                // 4. Enter Move mode
                NavigateToMove();
                SendKey(VK_ENTER);
                Thread.Sleep(500);

                // 5. Read actual position from cursor (authoritative, not scan)
                var startPos = ReadGridPos();
                ModLogger.Log($"[AutoMove] Scan pos=({ally.GridX},{ally.GridY}) Cursor pos=({startPos.x},{startPos.y})");

                // If scan position was wrong, recompute BFS from actual position
                if (startPos.x != ally.GridX || startPos.y != ally.GridY)
                {
                    ModLogger.Log($"[AutoMove] Scan/cursor mismatch — recomputing BFS from ({startPos.x},{startPos.y})");
                    visited.Clear();
                    visited[(startPos.x, startPos.y)] = 0;
                    queue.Clear();
                    queue.Enqueue((startPos.x, startPos.y, 0));
                    while (queue.Count > 0)
                    {
                        var (bx, by, bc) = queue.Dequeue();
                        if (bc >= moveStat) continue;
                        double bch = GetDisplayHeight(bx, by);
                        foreach (var bd in dirs)
                        {
                            int bnx = bx + bd[0], bny = by + bd[1];
                            if (!map.InBounds(bnx, bny) || !map.IsWalkable(bnx, bny)) continue;
                            if (enemyPositions.Contains((bnx, bny))) continue;
                            double bnh = GetDisplayHeight(bnx, bny);
                            if (bnh < 0 || bch < 0 || Math.Abs(bnh - bch) > jumpStat) continue;
                            int bnc = bc + 1;
                            if (bnc > moveStat) continue;
                            if (!visited.ContainsKey((bnx, bny)) || visited[(bnx, bny)] > bnc)
                            { visited[(bnx, bny)] = bnc; queue.Enqueue((bnx, bny, bnc)); }
                        }
                    }
                    visited.Remove((startPos.x, startPos.y));

                    // Re-pick best tile
                    curDist = enemyPositions.Count > 0
                        ? enemyPositions.Min(e => Math.Abs(startPos.x - e.Item1) + Math.Abs(startPos.y - e.Item2))
                        : 99;
                    bestX = startPos.x; bestY = startPos.y; bestDist = curDist;
                    foreach (var kv in visited)
                    {
                        int bd = enemyPositions.Count > 0
                            ? enemyPositions.Min(e => Math.Abs(kv.Key.Item1 - e.Item1) + Math.Abs(kv.Key.Item2 - e.Item2))
                            : 99;
                        if (bd > bestDist) { bestDist = bd; bestX = kv.Key.Item1; bestY = kv.Key.Item2; }
                    }
                }

                // If best is still our position, cancel and wait
                if (bestX == startPos.x && bestY == startPos.y)
                {
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                    response.Error = $"Stay at ({startPos.x},{startPos.y}) dist={curDist} — no better tile. Enemies={enemyStr}";
                    ModLogger.Log($"[AutoMove] No better tile, cancelling move");
                    goto doWait;
                }

                response.Error = $"Moved ({startPos.x},{startPos.y})->target=({bestX},{bestY}) dist={curDist}->{bestDist} enemies={enemyStr}";

                // 6. Detect rotation empirically: try Right, check delta
                int rdx = 0, rdy = 0;
                int[] testKeys = { VK_RIGHT, VK_DOWN, VK_LEFT, VK_UP };
                int[] undoKeys = { VK_LEFT, VK_UP, VK_RIGHT, VK_DOWN };

                for (int i = 0; i < 4; i++)
                {
                    _input.SendKeyPressToWindow(_gameWindow, testKeys[i]);
                    Thread.Sleep(150);
                    var testPos = ReadGridPos();
                    int tdx = testPos.x - startPos.x, tdy = testPos.y - startPos.y;
                    // Only undo if the test key actually moved the cursor
                    if (tdx != 0 || tdy != 0)
                    {
                        _input.SendKeyPressToWindow(_gameWindow, undoKeys[i]);
                        Thread.Sleep(150);
                    }

                    if (tdx != 0 || tdy != 0)
                    {
                        // Derive Right delta from whichever key worked
                        switch (i)
                        {
                            case 0: rdx = tdx; rdy = tdy; break;                  // Right
                            case 1: rdx = -tdy; rdy = tdx; break;                 // Down → Right = (-dy, dx)
                            case 2: rdx = -tdx; rdy = -tdy; break;                // Left → Right = opposite
                            case 3: rdx = tdy; rdy = -tdx; break;                 // Up → Right = (dy, -dx)
                        }
                        _lastDetectedRightDelta = (rdx, rdy);
                        ModLogger.Log($"[AutoMove] Rotation detected via {(new[]{"Right","Down","Left","Up"})[i]}=({tdx},{tdy}) → Right=({rdx},{rdy})");
                        break;
                    }
                }

                if (rdx == 0 && rdy == 0)
                {
                    rdx = 0; rdy = 1; // fallback
                    ModLogger.Log("[AutoMove] Could not detect rotation, using fallback Right=(0,1)");
                }

                // 6. Navigate to target
                int dx = bestX - startPos.x;
                int dy = bestY - startPos.y;
                // Down = (rdy, -rdx) perpendicular to Right
                int ddx = rdy, ddy = -rdx;

                int aRight, aDown;
                if (rdx != 0)
                {
                    aRight = dx / rdx;
                    aDown = dy / ddy;
                }
                else
                {
                    aRight = dy / rdy;
                    aDown = dx / ddx;
                }

                int rightVK = aRight >= 0 ? VK_RIGHT : VK_LEFT;
                int downVK = aDown >= 0 ? VK_DOWN : VK_UP;

                for (int i = 0; i < Math.Abs(aRight); i++)
                {
                    _input.SendKeyPressToWindow(_gameWindow, rightVK);
                    Thread.Sleep(80);
                }
                for (int i = 0; i < Math.Abs(aDown); i++)
                {
                    _input.SendKeyPressToWindow(_gameWindow, downVK);
                    Thread.Sleep(80);
                }

                var finalPos = ReadGridPos();
                ModLogger.Log($"[AutoMove] Cursor at ({finalPos.x},{finalPos.y}) target=({bestX},{bestY})");
                response.Error = $"Moved ({startPos.x},{startPos.y})->({finalPos.x},{finalPos.y}) target=({bestX},{bestY}) dist={curDist}->{bestDist} enemies={enemyStr}";

                // 7. Confirm move
                _input.SendKeyPressToWindow(_gameWindow, VK_F);
                Thread.Sleep(500);

                // Wait for move to confirm
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 3000)
                {
                    var check = _detectScreen();
                    if (check != null && check.Name == "BattleMyTurn") break;
                    Thread.Sleep(100);
                }
            }
            else
            {
                response.Error = $"Stay at ({ally.GridX},{ally.GridY}) dist={curDist} — already safest. Enemies={enemyStr}";
                ModLogger.Log($"[AutoMove] Staying put at ({ally.GridX},{ally.GridY}) dist={curDist}");
            }

            // 8. Wait (end turn with Ctrl fast-forward)
            doWait:
            Thread.Sleep(300);
            var preWait = _detectScreen();
            if (preWait != null && preWait.Name == "BattleMyTurn")
            {
                int waitCursor = preWait.MenuCursor;
                // After battle_move, game auto-advances cursor to Abilities (1)
                // but 0x1407FC620 still reads 0. Apply same correction as BattleAbility.
                if (_menuCursorStale && waitCursor == 0)
                {
                    ModLogger.Log("[AutoMove] Post-move cursor correction for Wait: 0 → 1");
                    waitCursor = 1;
                }
                NavigateMenuCursor(waitCursor, 2);
                Thread.Sleep(200);

                // Verify cursor is on Wait (2) before pressing Enter
                var cursorCheck = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
                int cursorVal = cursorCheck != null ? (int)cursorCheck.Value.value : -1;
                ModLogger.Log($"[AutoMove] Wait cursor={cursorVal} (expect 2)");

                SendKey(VK_ENTER);
                Thread.Sleep(500);
                SendKey(VK_ENTER);

                // Hold Ctrl for fast-forward, poll until friendly turn
                Thread.Sleep(500);
                SendInputKeyDown(VK_CONTROL);
                _input.SendKeyDownToWindow(_gameWindow, VK_CONTROL);

                try
                {
                    var sw2 = Stopwatch.StartNew();
                    while (sw2.ElapsedMilliseconds < 120000)
                    {
                        Thread.Sleep(300);
                        var current = _detectScreen();
                        if (current == null) continue;
                        if (current.Name == "BattlePaused") { SendKey(VK_ESCAPE); Thread.Sleep(300); continue; }
                        if (current.Name == "BattleMyTurn") { response.Error += $" | Next turn after {sw2.ElapsedMilliseconds}ms"; break; }
                        if (current.Name == "GameOver") { response.Error += " | GAME OVER"; break; }
                    }
                }
                finally
                {
                    SendInputKeyUp(VK_CONTROL);
                    _input.SendKeyUpToWindow(_gameWindow, VK_CONTROL);
                }
            }

            response.Status = "completed";
            return response;
        }

        /// <summary>
        /// test_c_hold: Use SendInput with SCANCODE flag to hold C, then press Up.
        /// DirectInput games need scan codes, not virtual keys.
        /// </summary>
        private CommandResponse TestCHold(CommandResponse response)
        {
            var results = new List<string>();

            // Dismiss battle action menu
            SendKey(VK_ESCAPE);
            Thread.Sleep(500);

            var start = ReadGridPos();
            results.Add($"start=({start.x},{start.y})");

            // Hold C via SendInput with SCANCODE flag (for DirectInput)
            // ALSO hold via keybd_event (for GetAsyncKeyState)
            // ALSO send WM_KEYDOWN via PostMessage (for WndProc)
            // Belt, suspenders, AND duct tape.
            SendInputKeyDown(VK_C);
            _input.SendKeyDownToWindow(_gameWindow, VK_C);
            PostMessage(_gameWindow, 0x0100, (IntPtr)VK_C, IntPtr.Zero);
            Thread.Sleep(500);

            // Press Up via PostMessage (proven to work for key presses)
            for (int i = 0; i < 5; i++)
            {
                // Re-assert C held every iteration
                SendInputKeyDown(VK_C);
                Thread.Sleep(50);
                _input.SendKeyPressToWindow(_gameWindow, VK_UP);
                Thread.Sleep(300);
                var pos = ReadGridPos();
                int team = ReadCursorTeam();
                results.Add($"up{i}:({pos.x},{pos.y})t{team}");
            }

            // Release C everywhere
            SendInputKeyUp(VK_C);
            _input.SendKeyUpToWindow(_gameWindow, VK_C);
            PostMessage(_gameWindow, 0x0101, (IntPtr)VK_C, IntPtr.Zero);
            Thread.Sleep(200);

            // Re-open menu
            SendKey(VK_F);
            Thread.Sleep(300);

            response.Status = "completed";
            response.Error = string.Join(" | ", results);
            return response;
        }

        // SendInput helpers using SCAN CODES — required for DirectInput games
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion u; }

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern ushort MapVirtualKey(uint uCode, uint uMapType);

        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_KEYUP_SI = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// True if the game window currently has focus. Used to gate global
        /// SendInput calls so we don't hijack Ctrl/other modifiers when the
        /// user has tabbed to another app (e.g. typing feedback to the AI
        /// while travel fast-forward is running). Session 24 investigation:
        /// globally-held Ctrl was turning every keystroke in the user's
        /// terminal into Ctrl+letter shortcuts.
        /// </summary>
        private bool IsGameForeground()
        {
            return GetForegroundWindow() == _gameWindow;
        }

        private void SendInputKeyDown(int vk)
        {
            SetForegroundWindow(_gameWindow);
            ushort scan = MapVirtualKey((uint)vk, 0);
            var input = new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = scan, dwFlags = KEYEVENTF_SCANCODE } } };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }
        private void SendInputKeyUp(int vk)
        {
            ushort scan = MapVirtualKey((uint)vk, 0);
            var input = new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = scan, dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP_SI } } };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }
        private void SendInputKeyPress(int vk)
        {
            SendInputKeyDown(vk);
            Thread.Sleep(50);
            SendInputKeyUp(vk);
        }

        // Grid cursor addresses (actual map position, always accurate)
        private const long AddrGridX = 0x140C64A54;
        private const long AddrGridY = 0x140C6496C;

        // Condensed struct base (cursor-selected unit, updates on C+Up hover)
        private const long AddrCondensedBase = 0x14077D2A0;
        private const long AddrCursorTeam = 0x14077D2A2;

        // UI buffer (cursor-selected unit, has Move/Jump/Job/Brave/Faith)
        private const long AddrUIBuffer = 0x1407AC7C0;

        // Camera rotation address (byte, incrementing counter, mod 4 = rotation 0-3)
        private const long AddrCameraRotation = 0x14077C970;

        /// <summary>
        /// Arrow key → grid delta mapping, verified at all 4 rotations.
        /// Each arrow press moves exactly 1 cell along one grid axis.
        /// Index: [rotation % 4, direction] where 0=Right, 1=Left, 2=Up, 3=Down.
        /// </summary>
        /// <summary>
        /// move_grid: Enter Move mode, navigate cursor to target grid (x,y), confirm with F.
        /// Uses empirical rotation detection (test one arrow press, observe delta).
        /// Usage: {"action":"move_grid","locationId":2,"unitIndex":0}
        ///   locationId = target grid X, unitIndex = target grid Y
        /// </summary>
        private CommandResponse MoveGrid(CommandResponse response, CommandRequest command)
        {
            int targetX = command.LocationId;
            int targetY = command.UnitIndex;

            // Catch the foot-gun: caller sent {"x":8,"y":11} instead of
            // {"locationId":8,"unitIndex":11} — defaults are -1, 0 which
            // would fall through to "Tile (-1,0) is not in the valid move
            // range" — technically true but unhelpful, the real problem is
            // the JSON shape.
            var argError = TileTargetValidator.Validate(targetX, targetY, "battle_move");
            if (argError != null)
            {
                response.Status = "failed";
                response.Error = argError;
                return response;
            }

            // Pre-flight: refuse to act during another team's turn. Any
            // keypress in BattleEnemiesTurn / BattleAlliesTurn pauses the
            // game (Escape opens pause). Caller should poll `screen` until
            // BattleMyTurn returns.
            var preflight = _detectScreen();
            if (preflight != null
                && (preflight.Name == "BattleEnemiesTurn" || preflight.Name == "BattleAlliesTurn"))
            {
                response.Status = "failed";
                response.Error = $"Not your turn (current: {preflight.Name}). Wait for BattleMyTurn before moving.";
                response.Screen = preflight;
                return response;
            }

            // Validate target tile against last scan_move results
            ModLogger.Log($"[MoveGrid] Validating ({targetX},{targetY}): validTiles={_lastValidMoveTiles?.Count ?? -1}");
            if (!MoveValidator.IsValidTile(targetX, targetY, _lastValidMoveTiles))
            {
                response.Status = "failed";
                response.Error = MoveValidator.GetInvalidTileError(targetX, targetY, _lastValidMoveTiles);
                return response;
            }

            // Enter Move mode if on BattleMyTurn
            var screen = _detectScreen();

            // S59: if we're deeper than BattleMyTurn (submenu, pause-menu leak,
            // skillset list), escape back to BattleMyTurn first so the Move-mode
            // entry below finds the action menu. Same pattern as battle_ability.
            int escapeCount = BattleAbilityEntryReset.EscapeCountToMyTurn(screen?.Name);
            if (escapeCount > 0)
            {
                ModLogger.Log($"[MoveGrid] Entry reset: screen={screen?.Name}, Escape×{escapeCount} to reach BattleMyTurn");
                for (int i = 0; i < escapeCount; i++)
                {
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                }
                screen = _detectScreen();
            }

            // Guard: one Move per turn. After moving, the menu slot 0 reads as
            // "Reset Move" — selecting it cancels the move rather than moving
            // again, which is almost never what the caller wanted. Refuse.
            if (screen != null && screen.BattleMoved == 1)
            {
                response.Status = "failed";
                response.Error = "You've already moved this turn. You cannot move again (menu slot 0 would Reset Move, not relocate).";
                return response;
            }

            if (screen != null && screen.Name == "BattleMyTurn")
            {
                // Menu always has 5 items — Move/ResetMove(0) is always present.
                // Read cursor from memory and navigate to index 0.
                var cursorResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
                int cursor = cursorResult != null ? (int)cursorResult.Value.value : 0;
                if (cursor != 0)
                {
                    NavigateMenuCursor(cursor, 0);
                }
                SendKey(VK_ENTER);
                // 500ms → 300ms. The BattleMoving transition is short in
                // logs; if we miss it here the null-check below fails and
                // surfaces a clean "Not in Move mode" error anyway.
                Thread.Sleep(300);
                screen = _detectScreen();

                // S59: stale-cursor recovery. If the menuCursor byte was stale
                // and Enter landed us in the Abilities submenu instead of Move
                // mode, the cursor byte was 1 but the target was 0. The game
                // accepted Up visually; only memory lagged. Escape and retry
                // with an extra blind Up — this compensates for the stale read
                // without needing to verify the byte moved.
                if (screen != null && screen.Name == "BattleAbilities")
                {
                    ModLogger.Log("[MoveGrid] Stale-cursor recovery: landed on BattleAbilities, Escape+retry with extra Up");
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                    SendKey(VK_UP);
                    Thread.Sleep(150);
                    SendKey(VK_ENTER);
                    Thread.Sleep(300);
                    screen = _detectScreen();
                }
            }

            if (screen == null || screen.Name != "BattleMoving")
            {
                response.Status = "failed";
                response.Error = $"Not in Move mode (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Validate target against the game's own movement tile list (authoritative source).
            // This catches tiles that BFS incorrectly included (dead units, wrong Move stat, etc.)
            var gameTileBytes = _explorer.Scanner.ReadBytes((nint)GameTileList.Address, GameTileList.MaxBytes);
            var gameTiles = GameTileList.Parse(gameTileBytes);
            if (gameTiles.Count > 0 && !gameTiles.Contains((targetX, targetY)))
            {
                SendKey(VK_ESCAPE); // exit Move mode
                Thread.Sleep(300);
                response.Status = "failed";
                response.Error = $"Tile ({targetX},{targetY}) is not reachable (game reports {gameTiles.Count} valid tiles). Pick from the game's valid tile list.";
                return response;
            }

            var startPos = ReadGridPos();
            int deltaX = targetX - startPos.x;
            int deltaY = targetY - startPos.y;

            if (deltaX == 0 && deltaY == 0)
            {
                // Cursor already at target tile — fire confirm. Note:
                // cursor position is NOT the unit position. Make the
                // message say so explicitly. Live-flagged 2026-04-26 P3:
                // agent read "Already at (5,7)" as "unit is at (5,7)"
                // and got confused when the unit was actually elsewhere.
                _input.SendKeyPressToWindow(_gameWindow, VK_F);
                Thread.Sleep(500);
                response.Status = "completed";
                response.Info = $"Cursor already on ({targetX},{targetY}) — sent confirm; unit moves from its current tile to here";
                return response;
            }

            // Detect rotation empirically: try each arrow key until one moves the cursor
            int rdx = 0, rdy = 0; // What "Right" does to grid coords
            int[] testKeys = { VK_RIGHT, VK_DOWN, VK_LEFT, VK_UP };

            for (int i = 0; i < 4; i++)
            {
                _input.SendKeyPressToWindow(_gameWindow, testKeys[i]);
                Thread.Sleep(150);
                var testPos = ReadGridPos();
                int tdx = testPos.x - startPos.x, tdy = testPos.y - startPos.y;

                if (tdx != 0 || tdy != 0)
                {
                    // Undo this test press
                    int[] undoKeys = { VK_LEFT, VK_UP, VK_RIGHT, VK_DOWN };
                    _input.SendKeyPressToWindow(_gameWindow, undoKeys[i]);
                    Thread.Sleep(150);

                    // Derive what Right does from whichever key moved
                    switch (i)
                    {
                        case 0: rdx = tdx; rdy = tdy; break;           // Right itself
                        case 1: rdx = -tdy; rdy = tdx; break;          // Down → Right = (-dy, dx)
                        case 2: rdx = -tdx; rdy = -tdy; break;         // Left → Right = opposite
                        case 3: rdx = tdy; rdy = -tdx; break;          // Up → Right = (dy, -dx)
                    }
                    _lastDetectedRightDelta = (rdx, rdy);
                    ModLogger.Log($"[MoveGrid] Rotation: key={i} delta=({tdx},{tdy}) → Right=({rdx},{rdy})");
                    break;
                }
                // Key didn't move cursor (boundary) — do NOT press undo, just try next key
            }

            if (rdx == 0 && rdy == 0)
            {
                // S59: detection failed (probably stale grid-pos memory reads
                // — same family as the menuCursor stale-byte issue). If we've
                // cached a rotation from an earlier successful move this
                // session, reuse it. Camera rotation doesn't change between
                // units on the same map without an explicit rotate, so the
                // cached delta is usually still valid. Better to attempt a
                // possibly-wrong move than abort the whole action.
                if (_lastDetectedRightDelta.HasValue)
                {
                    (rdx, rdy) = _lastDetectedRightDelta.Value;
                    ModLogger.Log($"[MoveGrid] Rotation detection failed at ({startPos.x},{startPos.y}); reusing cached Right=({rdx},{rdy})");
                }
                else
                {
                    response.Status = "failed";
                    response.Error = $"Could not detect rotation from ({startPos.x},{startPos.y})";
                    SendKey(VK_ESCAPE); // Exit move mode
                    Thread.Sleep(300);
                    return response;
                }
            }

            // Compute arrow presses needed
            // Down = perpendicular to Right = (rdy, -rdx)
            int ddx = rdy, ddy = -rdx;
            int aRight, aDown;
            if (rdx != 0) { aRight = deltaX / rdx; aDown = deltaY / ddy; }
            else { aRight = deltaY / rdy; aDown = deltaX / ddx; }

            int rightVK = aRight >= 0 ? VK_RIGHT : VK_LEFT;
            int downVK = aDown >= 0 ? VK_DOWN : VK_UP;

            ModLogger.Log($"[MoveGrid] ({startPos.x},{startPos.y})->({targetX},{targetY}) Right=({rdx},{rdy}) presses: {Math.Abs(aRight)}x{(aRight >= 0 ? "Right" : "Left")} {Math.Abs(aDown)}x{(aDown >= 0 ? "Down" : "Up")}");

            // Execute arrow presses
            for (int i = 0; i < Math.Abs(aRight); i++)
            {
                _input.SendKeyPressToWindow(_gameWindow, rightVK);
                Thread.Sleep(100);
            }
            for (int i = 0; i < Math.Abs(aDown); i++)
            {
                _input.SendKeyPressToWindow(_gameWindow, downVK);
                Thread.Sleep(100);
            }

            var finalPos = ReadGridPos();
            bool onTarget = finalPos.x == targetX && finalPos.y == targetY;

            if (!onTarget)
            {
                ModLogger.Log($"[MoveGrid] MISS: cursor=({finalPos.x},{finalPos.y}) target=({targetX},{targetY}) — cancelling");
                SendKey(VK_ESCAPE);
                Thread.Sleep(300);
                response.Status = "failed";
                response.Error = $"Navigation miss: ({startPos.x},{startPos.y})->({finalPos.x},{finalPos.y}) target=({targetX},{targetY}) Right=({rdx},{rdy})";
                return response;
            }

            // Confirm move with F
            _input.SendKeyPressToWindow(_gameWindow, VK_F);
            // 500ms was a pre-poll fixed wait; now the poll loop below
            // handles detection. 150ms lets F register before we start
            // reading state.
            Thread.Sleep(150);

            // Poll up to 8s for the move to complete. battleMode flickers to 1
            // (BattleCasting) transiently during the walk animation — ignore it entirely
            // since real casting can't happen during move confirmation. Also ignore
            // BattleFormation (battleMode=1 edge case).
            //
            // Session 28 captured a repeatable false-negative: after
            // auto_place_units on turn 1, battle_move reports NOT CONFIRMED while
            // the unit actually walked to the target (mod log `Unit=(newX,newY)`
            // appeared seconds later). See TODO §0. Rich tracing below captures
            // the detector state + screen.Name trail so next repro has enough
            // data to root-cause.
            bool confirmed = false;
            string lastScreenSeen = "null";
            int polls = 0;
            int stalePokes = 0;
            const int MaxStalePokes = 3;
            var sw = Stopwatch.StartNew();
            long lastPokeMs = 0;
            // 8s → 12s: Ramza's Gallant Knight move animation can run long
            // when there are intervening BattleVictory flicker rechecks
            // (#2 of 2026-04-25 P2 playtest). Longer ceiling reduces
            // false-negative NOT CONFIRMED on slow turns. Cost is bounded
            // — only the genuinely-stuck cases pay the extra 4s.
            while (sw.ElapsedMilliseconds < 12000)
            {
                var check = _detectScreen();
                polls++;
                if (check?.Name != null) lastScreenSeen = check.Name;
                // Skip BattleVictory flicker — same root cause as the
                // execute_turn outer loop. If we exit the move-confirm
                // wait on a flicker, the caller sees confused state.
                if (check?.Name == "BattleVictory" || check?.Name == "GameOver"
                    || check?.Name == "BattleDesertion")
                {
                    ModLogger.Log($"[MoveGrid] Skipping terminal-flicker {check.Name} (likely transient)");
                    Thread.Sleep(200);
                    continue;
                }
                // Stale-state poke: if we've been on BattleMoving for >3s
                // post-confirm-F, the game may be showing a modal we
                // don't classify as a distinct screen (e.g. an IC
                // remaster "Move here?" Yes/No prompt). Send Enter to
                // try to dismiss/accept. Up to 3 pokes spaced 2s apart.
                // Live-flagged 2026-04-26 P3: 3 of 5 moves NOT CONFIRMED
                // with lastScreen=BattleMoving for full timeout window.
                if (check?.Name == "BattleMoving"
                    && sw.ElapsedMilliseconds > 3000
                    && stalePokes < MaxStalePokes
                    && (sw.ElapsedMilliseconds - lastPokeMs) > 2000)
                {
                    ModLogger.Log($"[MoveGrid] Stale BattleMoving at {sw.ElapsedMilliseconds}ms — sending Enter poke #{stalePokes + 1}");
                    SendKey(VK_ENTER);
                    Thread.Sleep(300);
                    stalePokes++;
                    lastPokeMs = sw.ElapsedMilliseconds;
                    continue;
                }
                // Require the "player in control" states BattleMyTurn or
                // BattleActing (post-move, action pending) as the confirmation
                // signal. S56 live-observed: accepting any non-BattleMoving
                // battle state caused execute_turn bundled flows to advance
                // to the next sub-step while the move was STILL resolving —
                // BattleEnemiesTurn appeared transiently for ~1-2s during
                // the walk animation tail, MoveGrid saw it and returned
                // "confirmed", the follow-up battle_attack sub-step's state
                // gate failed because the game wasn't actually back to
                // BattleMyTurn yet.
                if (check != null && (check.Name == "BattleMyTurn"
                    || check.Name == "BattleActing"))
                {
                    ModLogger.Log($"[MoveGrid] confirmed via screen.Name={check.Name} after {sw.ElapsedMilliseconds}ms ({polls} polls)");
                    confirmed = true; break;
                }

                // Auto-dismiss chest/crystal dialogs that fire when the
                // target tile holds a treasure or crystallized unit. The
                // "Yes" option is pre-selected on both the move-confirm
                // popup ("Move to this tile and open the chest?" / "Use
                // the crystal...?") and the reward banner ("Obtained X!").
                // Enter accepts both. 500ms after-sleep gives the modal
                // animation time to advance so the next poll sees the
                // following state, not a re-entry of the same modal.
                if (check != null && (check.Name == "BattleCrystalMoveConfirm"
                    || check.Name == "BattleRewardObtainedBanner"
                    || check.Name == "BattleAbilityLearnedBanner"))
                {
                    ModLogger.Log($"[MoveGrid] Auto-dismissing {check.Name} (Enter on default-Yes)");
                    SendKey(VK_ENTER);
                    Thread.Sleep(500);
                    continue;
                }

                Thread.Sleep(100);
            }

            if (!confirmed)
            {
                ModLogger.LogError($"[MoveGrid] TIMEOUT: 8s elapsed without confirm. lastScreenSeen={lastScreenSeen} polls={polls} target=({targetX},{targetY}) cursorFinal=({finalPos.x},{finalPos.y})");
                response.Status = "failed";
                response.Error = $"({startPos.x},{startPos.y})->({finalPos.x},{finalPos.y}) NOT CONFIRMED (timeout, lastScreen={lastScreenSeen})";
                return response;
            }

            // Verify the unit actually moved by reading the active unit's
            // authoritative position from the static battle array slot
            // (NOT the cursor — cursor sits on the move-confirm tile
            // even when the unit didn't actually move). 2026-04-26 P5:
            // `execute_turn 8 8` reported success but Lloyd stayed at
            // (9,8). The old check used ReadPostActionState which reads
            // CondensedBase (cursor-hovered unit) — it returned the
            // cursor's tile, not the unit's, so the rejection check
            // trivially passed.
            try { CollectUnitPositionsFull(); } catch { /* fallback below */ }
            var activeAfterMove = GetActiveAlly();
            if (activeAfterMove != null
                && (startPos.x != targetX || startPos.y != targetY)
                && activeAfterMove.GridX == startPos.x && activeAfterMove.GridY == startPos.y)
            {
                ModLogger.Log($"[MoveGrid] REJECTED — active unit still at ({startPos.x},{startPos.y}) per scan, but cursor reached ({finalPos.x},{finalPos.y})");
                response.Status = "failed";
                response.Error = $"({startPos.x},{startPos.y})->({finalPos.x},{finalPos.y}) REJECTED — unit still at start position (cursor reached target but unit did not commit)";
                return response;
            }

            response.Status = "completed";
            response.Info = $"({startPos.x},{startPos.y})->({finalPos.x},{finalPos.y}) CONFIRMED";
            _menuCursorStale = true;
            // Invalidate the basic-Attack tile cache: the active unit just
            // moved, so the cached range from the pre-move scan is stale.
            // battle_attack will skip its up-front guard until the next
            // scan_move repopulates the cache, falling through to nav-loop
            // error handling for that single call. Better than rejecting
            // a now-valid target with a stale "out of range" error.
            _lastValidAttackTiles = null;
            // Same for the per-ability tile cache: pre-move ranges are
            // stale (every ability's reachable set changes when the caster
            // moves). Live-flagged playtest #3 2026-04-25: agent moved,
            // then battle_ability rejected with "not in valid range" using
            // the OLD range. Invalidate so battle_ability falls through to
            // its nav loop until the next scan_move repopulates.
            _lastValidAbilityTiles = null;

            // S58: credit tiles-moved to the active unit. Distance is
            // Manhattan (grid movement is one step per tile, no diagonal).
            int tilesMoved = System.Math.Abs(finalPos.x - startPos.x)
                           + System.Math.Abs(finalPos.y - startPos.y);
            if (tilesMoved > 0)
                StatTracker?.OnMove(GetActiveUnitNameForStats(), tilesMoved);

            return response;
        }

        /// <summary>
        /// get_arrows: Given a target grid position, compute the arrow key sequence to get there.
        /// Scans units to find current positions, reads camera rotation, returns arrow names.
        /// Also executes the arrows if "to" is set to "execute".
        /// </summary>
        private CommandResponse GetArrows(CommandResponse response, CommandRequest command)
        {
            // Scan units to get positions
            var units = CollectUnitPositionsFull();
            var ally = units.FirstOrDefault(u => u.Team == 0);
            var enemies = units.Where(u => u.Team != 0).ToList();

            if (ally == null)
            {
                response.Status = "failed";
                response.Error = "Could not find ally";
                return response;
            }

            // Find target: nearest enemy, then pick adjacent tile closest to ally
            var nearest = enemies.OrderBy(e => Math.Abs(e.GridX - ally.GridX) + Math.Abs(e.GridY - ally.GridY)).First();
            var adj = new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }
                .Select(a => (gx: nearest.GridX + a.Item1, gy: nearest.GridY + a.Item2))
                .OrderBy(t => Math.Abs(t.gx - ally.GridX) + Math.Abs(t.gy - ally.GridY))
                .First();

            int deltaX = adj.gx - ally.GridX;
            int deltaY = adj.gy - ally.GridY;

            // Read camera rotation
            var camResult = _explorer.ReadAbsolute((nint)AddrCameraRotation, 1);
            int rotation = camResult != null ? (int)((camResult.Value.value - 1 + 4) % 4) : 0;

            // Build arrow sequence
            var arrows = new List<string>();
            string[] dirNames = { "Right", "Left", "Up", "Down" };

            if (deltaX != 0)
            {
                int dir = FindDirForDelta(rotation, deltaX > 0 ? 1 : -1, 0);
                for (int i = 0; i < Math.Abs(deltaX); i++)
                    arrows.Add(dirNames[dir]);
            }
            if (deltaY != 0)
            {
                int dir = FindDirForDelta(rotation, 0, deltaY > 0 ? 1 : -1);
                for (int i = 0; i < Math.Abs(deltaY); i++)
                    arrows.Add(dirNames[dir]);
            }

            var info = $"Ally=({ally.GridX},{ally.GridY}) Enemy=({nearest.GridX},{nearest.GridY}) Target=({adj.gx},{adj.gy}) delta=({deltaX},{deltaY}) rot={rotation} arrows={string.Join(" ", arrows)}";
            ModLogger.Log($"[GetArrows] {info}");

            // If command.To == "execute", actually do the move
            if (command.To == "execute" && arrows.Count > 0)
            {
                // Enter Move mode from BattleMyTurn
                var screen = _detectScreen();
                if (screen != null && screen.Name == "BattleMyTurn")
                {
                    NavigateToMove(); // Navigate to Move (don't trust stale menuCursor)
                    SendKey(VK_ENTER);
                    Thread.Sleep(500);
                }

                // Re-read rotation after entering Move mode (may auto-rotate)
                camResult = _explorer.ReadAbsolute((nint)AddrCameraRotation, 1);
                rotation = camResult != null ? (int)(camResult.Value.value % 4) : 0;

                // Recompute arrows with fresh rotation
                arrows.Clear();
                if (deltaX != 0)
                {
                    int dir = FindDirForDelta(rotation, deltaX > 0 ? 1 : -1, 0);
                    for (int i = 0; i < Math.Abs(deltaX); i++)
                        arrows.Add(dirNames[dir]);
                }
                if (deltaY != 0)
                {
                    int dir = FindDirForDelta(rotation, 0, deltaY > 0 ? 1 : -1);
                    for (int i = 0; i < Math.Abs(deltaY); i++)
                        arrows.Add(dirNames[dir]);
                }

                // Execute arrows
                foreach (var arrow in arrows)
                {
                    int vk = arrow switch { "Right" => VK_RIGHT, "Left" => VK_LEFT, "Up" => VK_UP, "Down" => VK_DOWN, _ => 0 };
                    if (vk != 0)
                    {
                        _input.SendKeyPressToWindow(_gameWindow, vk);
                        Thread.Sleep(100);
                    }
                }

                // Read where cursor ended up
                var finalPos = ReadGridPos();
                info += $" | executed, cursor=({finalPos.x},{finalPos.y})";

                // Confirm move with F
                _input.SendKeyPressToWindow(_gameWindow, VK_F);
                Thread.Sleep(500);

                var postScreen = _detectScreen();
                if (postScreen != null && postScreen.Name == "BattleMyTurn")
                    info += " | MOVE CONFIRMED";
                else
                    info += $" | post={postScreen?.Name}";
            }

            response.Status = "completed";
            response.Error = info;
            return response;
        }

        /// <summary>
        /// Arrow key → grid delta for AddrGridX (0x140C64A54) and AddrGridY (0x140C6496C).
        ///
        /// Source: BATTLE_COORDINATES.md all 4 rotations verified in one session.
        /// Doc uses (docX, docY). Our addresses: dx=AddrGridX, dy=AddrGridY.
        /// Mapping: our (dx, dy) = (-docX, -docY) — negate both axes.
        ///
        /// Verified 2026-04-05 at rot=0: Right=dy-1 ✓, Down=dx-1 ✓
        ///
        /// Index: [rotation % 4, direction] where 0=Right, 1=Left, 2=Up, 3=Down
        /// </summary>
        // Rotation table: maps effective rotation to arrow key grid deltas.
        // Raw rotation counter at 0x14077C970 is offset by 1:
        //   effective_rot = (raw_counter - 1 + 4) % 4
        // Empirically verified: raw%4=1 → Right=(0,+1) Down=(+1,0) → matches index 0.
        private static readonly (int dx, int dy)[,] ArrowGridDelta = {
            // eff=0: Right=(0,+1)  Left=(0,-1)  Up=(-1,0)   Down=(+1,0)
            { (0,1), (0,-1), (-1,0), (1,0) },
            // eff=1: Right=(+1,0)  Left=(-1,0)  Up=(0,+1)   Down=(0,-1)
            { (1,0), (-1,0), (0,1), (0,-1) },
            // eff=2: Right=(0,-1)  Left=(0,+1)  Up=(+1,0)   Down=(-1,0)
            { (0,-1), (0,1), (1,0), (-1,0) },
            // eff=3: Right=(-1,0)  Left=(+1,0)  Up=(0,-1)   Down=(0,+1)
            { (-1,0), (1,0), (0,-1), (0,1) },
        };

        private static readonly int[] DirVKs = { VK_RIGHT, VK_LEFT, VK_UP, VK_DOWN };
        private static readonly int[] OppositeDir = { 1, 0, 3, 2 }; // Right<->Left, Up<->Down

        private const int VK_C = 0x43;

        /// <summary>
        /// move_to: Find the nearest enemy using C+Up cycling, then move beside them.
        ///
        /// Algorithm:
        ///   1. Hold C key, press Up to cycle through all units in turn order
        ///   2. Read grid position + team for each unit → build position map
        ///   3. Release C key
        ///   4. Find nearest enemy from the collected positions
        ///   5. Enter Move mode, navigate to adjacent tile, confirm
        /// </summary>
        private CommandResponse MoveTo(CommandResponse response, string target, int tileIndex)
        {
            var screen = _detectScreen();
            if (!ScreenNamePredicates.IsBattleState(screen?.Name))
            {
                response.Status = "failed";
                response.Error = $"Not in battle (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Step 1: Collect all unit positions via C+Up cycling
            var units = CollectUnitPositionsFull();
            if (units.Count == 0)
            {
                response.Status = "failed";
                response.Error = "Could not collect unit positions";
                return response;
            }

            // Find ally and nearest enemy
            var ally = units.FirstOrDefault(u => u.Team == 0);
            var enemies = units.Where(u => u.Team != 0).ToList();

            if (ally == null || enemies.Count == 0)
            {
                response.Status = "failed";
                response.Error = $"Ally or enemies not found (units={units.Count}, enemies={enemies.Count})";
                return response;
            }

            var nearest = enemies
                .OrderBy(e => Math.Abs(e.GridX - ally.GridX) + Math.Abs(e.GridY - ally.GridY))
                .First();
            ModLogger.Log($"[MoveTo] Ally=({ally.GridX},{ally.GridY}) Nearest enemy=({nearest.GridX},{nearest.GridY}) team={nearest.Team}");

            // Step 2: Enter Move mode
            screen = _detectScreen();
            if (screen != null && screen.Name == "BattleMyTurn")
            {
                NavigateToMove();
                SendKey(VK_ENTER);
                Thread.Sleep(500);
                screen = _detectScreen();
            }

            if (screen == null || screen.Name != "BattleMoving")
            {
                response.Status = "failed";
                response.Error = $"Not in Move mode (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Read rotation after entering Move
            var camResult = _explorer.ReadAbsolute((nint)AddrCameraRotation, 1);
            int rotation = camResult != null ? (int)((camResult.Value.value - 1 + 4) % 4) : 0;

            // Step 3: Try each adjacent tile around the enemy
            // Order: prefer tiles closer to ally
            var adjacents = new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }
                .Select(a => (gx: nearest.GridX + a.Item1, gy: nearest.GridY + a.Item2))
                .OrderBy(t => Math.Abs(t.gx - ally.GridX) + Math.Abs(t.gy - ally.GridY))
                .ToArray();

            foreach (var adj in adjacents)
            {
                // Navigate from current cursor to target
                var curPos = ReadGridPos();
                int deltaX = adj.gx - curPos.x;
                int deltaY = adj.gy - curPos.y;

                ModLogger.Log($"[MoveTo] Trying ({adj.gx},{adj.gy}), cursor at ({curPos.x},{curPos.y}), delta=({deltaX},{deltaY})");
                NavigateGrid(deltaX, deltaY, rotation);

                // Try confirm
                _input.SendKeyPressToWindow(_gameWindow, VK_F);
                Thread.Sleep(500);

                screen = _detectScreen();
                if (screen != null && screen.Name == "BattleMyTurn")
                {
                    ModLogger.Log($"[MoveTo] SUCCESS at grid=({adj.gx},{adj.gy})");
                    response.Status = "completed";
                    return response;
                }

                ModLogger.Log($"[MoveTo] Tile ({adj.gx},{adj.gy}) invalid");
            }

            SendKey(VK_ESCAPE);
            Thread.Sleep(200);
            response.Status = "failed";
            response.Error = $"No valid adjacent tile near enemy ({nearest.GridX},{nearest.GridY})";
            return response;
        }

        /// <summary>
        /// Collect all unit positions by holding C and pressing Up to cycle through turn order.
        /// Each cycle step snaps the cursor to a unit, allowing us to read their grid position and team.
        /// </summary>
        /// <summary>Rich unit data from static battle array scan.</summary>
        internal class ScannedUnit
        {
            public bool IsActive;  // true for the unit whose turn it is
            public int GridX, GridY;
            /// <summary>Cardinal facing direction ("South"/"West"/"North"/"East") decoded
            /// from the unit slot's +0x35 byte. Null if the byte is out of range.
            /// Session 30 confirmed live across 4 player units at Siedge Weald.</summary>
            public string? Facing;
            /// <summary>Element-affinity masks from unit slot +0x5A..+0x5E. Each
            /// field is a list of element names. Session 30 live-verified via
            /// Flame/Ice/Kaiser/Venetian shields + Gaia Gear + Chameleon Robe.
            /// See memory/project_element_affinity_s30.md.</summary>
            public List<string>? ElementAbsorb;     // +0x5A — damage becomes heal
            public List<string>? ElementCancel;     // +0x5B — complete immunity
            public List<string>? ElementHalf;       // +0x5C — damage × 0.5
            public List<string>? ElementWeak;       // +0x5D — damage × 1.5
            public List<string>? ElementStrengthen; // +0x5E — own outgoing × 1.25
            public int Team;       // 0=ally, 1+=enemy
            public int Level;
            public int NameId;     // Sequential battle index (NOT roster nameId)
            public int RosterNameId; // Roster nameId (for story character lookup)
            public string? Name;   // Resolved character name (e.g. "Ramza")
            public int Exp;
            public int CT;
            public int Hp, MaxHp;
            public int Mp, MaxMp;
            public int PA, MA;
            public int Move, Jump;
            public int Job;
            public int Brave, Faith;
            public int Speed;
            public int SecondaryAbility; // roster +0x07 secondary skillset index
            public byte[] StatusBytes = new byte[5]; // 5-byte status bitfield
            public List<ActionAbilityInfo> LearnedAbilities = new(); // from condensed struct FFFF list
            /// <summary>
            /// Per-job learned-action-ability bitfield read from the roster slot at
            /// +0x32 + jobIdx*3. Keyed by jobIdx (see ActionAbilityLookup.GetJobJpOffset).
            /// Value is the 2-byte MSB-first bitfield (byte0, byte1) for that job.
            /// Populated only for matched player units. Null for enemies.
            /// </summary>
            public Dictionary<int, (byte byte0, byte byte1)>? LearnedBitfieldByJobIdx;
            public List<int>? Equipment;  // roster equipment IDs (7 slots, 0xFF/0xFFFF filtered out)
            public byte[]? ClassFingerprint;  // 11 bytes at heap struct +0x69 (see ClassFingerprintLookup)
            public string? JobNameOverride;   // Resolved class name from fingerprint (fallback for enemies)
            public string? ReactionAbility;   // Equipped reaction ability name (from heap bitfield at +0x74)
            public string? SupportAbility;    // Equipped support ability name (from heap bitfield at +0x78)
            public string? MovementAbility;   // Equipped movement ability name (from heap bitfield at +0x7D)
        }

        /// <summary>Convert internal ScannedUnit list to JSON-serializable response objects.</summary>
        private static List<Utilities.ScannedUnitResponse> BuildUnitResponses(List<ScannedUnit> units)
        {
            var result = new List<Utilities.ScannedUnitResponse>();
            foreach (var u in units)
            {
                string teamName = u.Team == 0 ? "PLAYER" : u.Team == 2 ? "ALLY" : "ENEMY";
                string? className = u.JobNameOverride ?? (u.Job > 0 ? GameStateReporter.GetJobName(u.Job) : null);
                // DecodeAliveStatuses filters Crystal/Dead/Treasure/Petrify out of
                // the rendered alive-statuses block — those surface separately
                // via LifeState. Mixing them in `[Foo,Bar,Baz]` confuses the
                // visual scan (playtest #7: `[Treasure]` for a crystallized
                // unit looked indistinguishable from `[Defending]`).
                var statuses = StatusDecoder.DecodeAliveStatuses(u.StatusBytes);
                bool isActive = u.IsActive;

                var resp = new Utilities.ScannedUnitResponse
                {
                    Name = u.Name,
                    Class = className,
                    Team = teamName,
                    X = u.GridX, Y = u.GridY,
                    Hp = u.Hp, MaxHp = u.MaxHp,
                    Mp = u.Mp, MaxMp = u.MaxMp,
                    Pa = u.PA, Ma = u.MA,
                    Speed = u.Speed, Ct = u.CT,
                    Brave = u.Brave, Faith = u.Faith,
                    Level = u.Level, Exp = u.Exp,
                    Move = u.Move, Jump = u.Jump,
                    IsActive = isActive,
                    Reaction = u.ReactionAbility,
                    Support = u.SupportAbility,
                    Movement = u.MovementAbility,
                    // Read full lifeState (alive/dead/crystal/treasure/petrified)
                    // from the status bits — the prior `Hp<=0 ? "dead" : null`
                    // missed crystallized/treasure units (Hp=0 with the
                    // Crystal/Treasure bit set). Falls back to HP-only when
                    // GetLifeState returns "alive" (e.g. status bytes empty
                    // for a freshly KO'd unit before the Dead bit lands).
                    LifeState = StatusDecoder.GetLifeState(u.StatusBytes) is var ls5077 && ls5077 != "alive"
                        ? ls5077
                        : (u.Hp <= 0 && u.MaxHp > 0 ? "dead" : null),
                };
                if (statuses.Count > 0) resp.Statuses = statuses;
                result.Add(resp);
            }
            return result;
        }

        /// <summary>
        /// Last empirically detected Right arrow delta from grid navigation.
        /// Used by BattleWait to determine facing rotation without reading camera address.
        /// Set during battle_move/battle_attack/auto_move whenever rotation detection runs.
        /// </summary>
        private (int dx, int dy)? _lastDetectedRightDelta;
        private readonly UnitNameCache _unitNameCache = new();

        /// <summary>Last scan results cached for BFS enemy blocking.</summary>
        private List<ScannedUnit>? _lastScannedUnits;

        /// <summary>
        /// Session 51: named snapshots of scanned-unit lists for enemy-turn
        /// play-by-play reporting. `scan_snapshot <label>` writes here; `scan_diff
        /// <from> <to>` reads. Stored as immutable UnitSnap value copies so the
        /// live _lastScannedUnits evolution doesn't mutate old snapshots.
        /// </summary>
        private readonly Dictionary<string, List<UnitScanDiff.UnitSnap>> _namedSnapshots = new();

        public bool SaveNamedSnapshot(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return false;
            if (_lastScannedUnits == null || _lastScannedUnits.Count == 0)
            {
                try { CollectUnitPositionsFull(); } catch { }
            }
            if (_lastScannedUnits == null) return false;
            var snaps = new List<UnitScanDiff.UnitSnap>(_lastScannedUnits.Count);
            foreach (var u in _lastScannedUnits)
            {
                var statuses = u.StatusBytes != null && u.StatusBytes.Length == 5
                    ? StatusDecoder.Decode(u.StatusBytes)
                    : null;
                snaps.Add(new UnitScanDiff.UnitSnap(
                    Name: u.Name ?? u.JobNameOverride,
                    RosterNameId: u.RosterNameId,
                    Team: u.Team,
                    GridX: u.GridX,
                    GridY: u.GridY,
                    Hp: u.Hp,
                    MaxHp: u.MaxHp,
                    Statuses: statuses,
                    ClassFingerprint: u.ClassFingerprint,
                    Speed: u.Speed,
                    PA: u.PA,
                    MA: u.MA));
            }
            _namedSnapshots[label] = snaps;
            return true;
        }

        public List<UnitScanDiff.UnitSnap>? GetNamedSnapshot(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            return _namedSnapshots.TryGetValue(label, out var s) ? s : null;
        }

        public int NamedSnapshotCount => _namedSnapshots.Count;

        /// <summary>
        /// S60: Capture current unit positions as an immutable <see cref="UnitScanDiff.UnitSnap"/>
        /// list, suitable for `battle_wait`'s pre/post narration diff. Reuses the conversion
        /// logic from <see cref="SaveNamedSnapshot(string)"/> but returns the list directly
        /// instead of storing it under a label. Returns null if the scan failed or there
        /// are no units (e.g. pre-battle state, or memory-read failure).
        /// </summary>
        public List<UnitScanDiff.UnitSnap>? CaptureCurrentUnitSnapshotPublic() => CaptureCurrentUnitSnapshot();

        private List<UnitScanDiff.UnitSnap>? CaptureCurrentUnitSnapshot()
        {
            try { CollectUnitPositionsFull(); } catch { return null; }
            if (_lastScannedUnits == null || _lastScannedUnits.Count == 0) return null;
            var snaps = new List<UnitScanDiff.UnitSnap>(_lastScannedUnits.Count);
            foreach (var u in _lastScannedUnits)
            {
                var statuses = u.StatusBytes != null && u.StatusBytes.Length == 5
                    ? StatusDecoder.Decode(u.StatusBytes)
                    : null;
                snaps.Add(new UnitScanDiff.UnitSnap(
                    Name: u.Name ?? u.JobNameOverride,
                    RosterNameId: u.RosterNameId,
                    Team: u.Team,
                    GridX: u.GridX,
                    GridY: u.GridY,
                    Hp: u.Hp,
                    MaxHp: u.MaxHp,
                    Statuses: statuses,
                    ClassFingerprint: u.ClassFingerprint,
                    Speed: u.Speed,
                    PA: u.PA,
                    MA: u.MA));
            }
            return snaps;
        }

        /// <summary>
        /// S60 narrator: when the scan drops enemy names between pre and post
        /// snapshots (a known upstream bug), UnitScanDiff can't pair units
        /// keyed by name|fingerprint. This helper backfills post-snap names
        /// from pre-snap using (Team, MaxHP) as the identity key — MaxHP is
        /// the most stable per-unit signature we have short of finding the
        /// game's actual unit-id byte.
        ///
        /// Only backfills when the post-snap unit's Name is null/empty AND
        /// exactly one pre-snap unit has that (Team, MaxHP) pair AND the
        /// pre-snap unit has a name. Never overrides an already-present name.
        /// </summary>
        private static List<UnitScanDiff.UnitSnap> BackfillNamesFromSnap(
            List<UnitScanDiff.UnitSnap> preSnap,
            List<UnitScanDiff.UnitSnap> postSnap)
        {
            // (Team, MaxHP) → name. Duplicates are removed from the map so we
            // never silently attribute to the wrong unit.
            var keyToName = new Dictionary<(int team, int maxHp), string>();
            var duplicates = new HashSet<(int team, int maxHp)>();
            foreach (var u in preSnap)
            {
                if (string.IsNullOrEmpty(u.Name)) continue;
                var k = (u.Team, u.MaxHp);
                if (duplicates.Contains(k)) continue;
                if (keyToName.ContainsKey(k))
                {
                    keyToName.Remove(k);
                    duplicates.Add(k);
                    continue;
                }
                keyToName[k] = u.Name!;
            }

            if (keyToName.Count == 0) return postSnap;

            var result = new List<UnitScanDiff.UnitSnap>(postSnap.Count);
            foreach (var u in postSnap)
            {
                if (!string.IsNullOrEmpty(u.Name))
                {
                    result.Add(u);
                    continue;
                }
                var k = (u.Team, u.MaxHp);
                if (keyToName.TryGetValue(k, out var backfilledName))
                {
                    result.Add(u with { Name = backfilledName });
                }
                else
                {
                    result.Add(u);
                }
            }
            return result;
        }

        /// <summary>
        /// S60 narrator: return copies of the snaps with ClassFingerprint nulled
        /// out. Fingerprints aren't stable between scans (a post-HP-change heap
        /// match can land on a different address, returning different signature
        /// bytes for the "same" unit). After name backfill, falling back to
        /// name-only identity is both stable and sufficient for the diff engine.
        /// </summary>
        private static List<UnitScanDiff.UnitSnap> StripFingerprints(List<UnitScanDiff.UnitSnap> snaps)
        {
            var result = new List<UnitScanDiff.UnitSnap>(snaps.Count);
            foreach (var u in snaps) result.Add(u with { ClassFingerprint = null });
            return result;
        }

        /// <summary>
        /// S60 narrator: capture a fresh unit snapshot, diff it against the
        /// supplied previous snapshot, run the raw renderer + the counter-attack
        /// and self-destruct inferrers, append every resulting "&gt; ..." line
        /// to claude_bridge/live_events.log, and advance `lastSnap` to the
        /// fresh capture. All three emit sites in BattleWait go through this
        /// helper so every narration window gets the same enrichments.
        /// </summary>
        private void EmitNarrationBatch(
            ref List<UnitScanDiff.UnitSnap>? lastSnap,
            string? activePlayerName)
        {
            if (lastSnap == null) return;
            var current = CaptureCurrentUnitSnapshot();
            if (current == null) return;
            current = BackfillNamesFromSnap(lastSnap, current);
            var preForDiff = StripFingerprints(lastSnap);
            var postForDiff = StripFingerprints(current);
            var rawEvents = UnitScanDiff.Compare(preForDiff, postForDiff);
            // 2026-04-26 Mandalia: when units lack stable identity (no
            // name, no roster nameId, no class fingerprint — all 4
            // generic enemies labelled `[ENEMY]`), UnitScanDiff falls
            // back to position-derived keys, so a single move emits a
            // remove+add pair that downstream filters can't dedupe by
            // label. Recombine matching-HP same-team pairs into single
            // moved events first.
            var pairFusedEvents = MovedEventReconstructor.Reconstruct(rawEvents);
            // Suppress phantom A→B→A move pairs from mid-animation scan
            // races (live-flagged 2026-04-25 P2). 3s window covers an
            // enemy turn cycle without crossing into another genuine
            // turn.
            var moveFiltered = _moveArtifactCoalescer.Filter(pairFusedEvents, DateTime.UtcNow);
            // Drop "moved" events whose destination is held by a
            // different-named unit in the post-snap — rank-based identity
            // matching for duplicate-name enemies can attribute one
            // Skeleton's move to another's path. Live-flagged 2026-04-26:
            // narrator said "Skeleton moved (3,7) → (3,3)" while Ramza
            // was at (3,3).
            var collisionFiltered = CollidingMoveFilter.Filter(moveFiltered, current);
            // Suppress phantom-KO clusters (damaged-to-zero + joined for
            // the same unit name in one batch — transient bad scan made
            // the unit appear to die and re-spawn). Live-flagged
            // 2026-04-26: Time Mage emitted KO + Dead-status + joined
            // despite being alive throughout.
            var events = PhantomKoCoalescer.Filter(collisionFiltered);
            if (events.Count > 0)
            {
                var counterLines = CounterAttackInferrer.Infer(events, activePlayerName ?? "", current);
                var selfDestructLines = SelfDestructInferrer.Infer(events);
                var criticalHpLines = CriticalHpInferrer.Infer(events, current);

                // S60 Phase 2.5: suppress raw "> X died" when a counter-KO or
                // self-destruct line already mentions the death — avoids the
                // duplicate-info noise in the narration.
                var suppressedKoLabels = new HashSet<string>();
                foreach (var e in events)
                {
                    if (e.Kind != "ko" || e.Team != "ENEMY") continue;
                    foreach (var cl in counterLines)
                    {
                        if (cl.Contains($"countered {e.Label} for") && cl.Contains($" {e.Label} died"))
                            suppressedKoLabels.Add(e.Label);
                    }
                    foreach (var sdl in selfDestructLines)
                    {
                        if (sdl.StartsWith($"> {e.Label} self-destructed"))
                            suppressedKoLabels.Add(e.Label);
                    }
                }

                var allLines = new List<string>();
                allLines.AddRange(BattleNarratorRenderer.Render(
                    events, activePlayerName ?? "", suppressedKoLabels));
                allLines.AddRange(counterLines);
                allLines.AddRange(selfDestructLines);
                allLines.AddRange(criticalHpLines);
                if (allLines.Count > 0)
                    NarrationEventLog.AppendLines(allLines);
            }
            lastSnap = current;
            // Keep the class-level persistent snap in sync with the ref advance.
            // Chunked continuations reload from this field at BattleWait entry.
            _narratorPersistentLastSnap = current;
        }

        /// <summary>
        /// S60 narrator: persistent last-snap carried across chunked BattleWait
        /// calls. The scan sometimes drops enemy names between turns; by keeping
        /// the name-backfilled snap around, we ensure subsequent chunks have a
        /// stable source of names for identity matching and event labels.
        /// Reset at the start of a non-chunked BattleWait call (see BattleWait).
        /// </summary>
        private List<UnitScanDiff.UnitSnap>? _narratorPersistentLastSnap;

        /// <summary>
        /// Hardcoded revive-ability names so BattleAbility can flag a
        /// no-op revive (`Used Phoenix Down on (X,Y)` with target still
        /// 0 HP). Live-flagged 2026-04-26 P3 playtest. Add new revive
        /// abilities here when discovered.
        /// </summary>
        private static bool IsReviveAbilityName(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name == "Phoenix Down"
                || name == "Raise"
                || name == "Arise"
                || name == "Revive"
                || name == "Life Song";
        }

        /// <summary>
        /// Cross-batch tracker that suppresses phantom A→B→A move pairs
        /// in the streaming narrator. See <see cref="MoveArtifactCoalescer"/>.
        /// </summary>
        private readonly MoveArtifactCoalescer _moveArtifactCoalescer
            = new(System.TimeSpan.FromSeconds(3));

        /// <summary>Last computed valid move tiles from scan_move BFS. Used by battle_move to validate targets.</summary>
        private HashSet<(int x, int y)>? _lastValidMoveTiles;
        private HashSet<(int x, int y)>? _lastValidAttackTiles;

        /// <summary>
        /// Per-ability valid target tile cache, keyed by ability name (case-
        /// insensitive). Populated during scan_move from each ability's
        /// validTargetTiles list. battle_ability uses this to reject out-of-
        /// range calls up-front rather than entering targeting mode and
        /// silently returning a phantom-success message. Self-target abilities
        /// (HRange=Self) are stored with the caster's tile as the only valid
        /// entry. Live-flagged 2026-04-25 playtest #3: Phoenix Down out of
        /// range said "Used Phoenix Down on (4,6)" with no actual cast.
        /// </summary>
        private Dictionary<string, HashSet<(int x, int y)>>? _lastValidAbilityTiles;


        /// <summary>Get enemy grid positions from last scan for BFS blocking.
        /// Only live enemies block — dead enemy tiles are traversable (see
        /// GetAllyPositions which includes corpses).</summary>
        public HashSet<(int, int)> GetEnemyPositions()
        {
            var result = new HashSet<(int, int)>();
            if (_lastScannedUnits == null) return result;
            foreach (var u in _lastScannedUnits)
            {
                var lifeState = StatusDecoder.GetLifeState(u.StatusBytes);
                if (lifeState != "alive") continue;
                // Only block enemy units (team=1), not neutrals/NPCs (team=2)
                if (u.Team == 1 && u.Hp > 0)
                    result.Add((u.GridX, u.GridY));
            }
            return result;
        }

        /// <summary>Get positions of allied units and corpses (pass-through but
        /// not stoppable). Corpses behave the same as allies for BFS purposes —
        /// crystallized / treasure units' tiles are empty.</summary>
        public HashSet<(int, int)> GetAllyPositions()
        {
            var result = new HashSet<(int, int)>();
            if (_lastScannedUnits == null) return result;
            foreach (var u in _lastScannedUnits)
            {
                var lifeState = StatusDecoder.GetLifeState(u.StatusBytes);
                if (lifeState == "crystal" || lifeState == "treasure") continue;
                // Corpses block stopping but not pass-through — add regardless of team.
                if (lifeState == "dead")
                {
                    result.Add((u.GridX, u.GridY));
                    continue;
                }
                if (u.Team == 0 && u.Hp > 0 && !u.IsActive)
                    result.Add((u.GridX, u.GridY));
            }
            return result;
        }

        /// <summary>Get active unit from last scan.</summary>
        internal ScannedUnit? GetActiveAlly()
        {
            return _lastScannedUnits?.FirstOrDefault(u => u.IsActive)
                ?? _lastScannedUnits?.FirstOrDefault(u => u.Team == 0);
        }

        /// <summary>
        /// Session 48: returns (x, y) of every roster-matched player unit from the
        /// most recent scan. Used by `cheat_kill_enemies` to distinguish which
        /// battle-array slots belong to the player so they don't get KO'd.
        /// Triggers a fresh scan if none is cached.
        /// </summary>
        public List<(int x, int y)> GetPlayerSlotPositions()
        {
            if (_lastScannedUnits == null || _lastScannedUnits.Count == 0)
            {
                try { CollectUnitPositionsFull(); } catch { }
            }
            var result = new List<(int, int)>();
            if (_lastScannedUnits == null) return result;
            foreach (var u in _lastScannedUnits)
            {
                if (u.Team == 0 && u.GridX >= 0 && u.GridY >= 0)
                    result.Add((u.GridX, u.GridY));
            }
            return result;
        }

        /// <summary>
        /// Session 49: returns (Hp, MaxHp) pairs for every player unit from the
        /// most recent scan. Used by `cheat_kill_enemies` (master HP table
        /// rewrite) to distinguish player slots from enemy slots by matching
        /// HP fingerprints — the master table has no team byte.
        /// Triggers a fresh scan if none is cached.
        /// See memory/project_master_hp_store.md.
        /// </summary>
        public List<(int Hp, int MaxHp)> GetPlayerHpFingerprints()
        {
            if (_lastScannedUnits == null || _lastScannedUnits.Count == 0)
            {
                try { CollectUnitPositionsFull(); } catch { }
            }
            var result = new List<(int, int)>();
            if (_lastScannedUnits == null) return result;
            foreach (var u in _lastScannedUnits)
            {
                if (u.Team == 0 && u.MaxHp > 0)
                    result.Add((u.Hp, u.MaxHp));
            }
            return result;
        }

        /// <summary>
        /// Session 51: resolve a named player unit to its (Hp, MaxHp)
        /// fingerprint for single-unit cheat operations (kill_one). Matches
        /// JobNameOverride or name case-insensitively. Returns null if no
        /// scanned player unit matches.
        /// </summary>
        public (int Hp, int MaxHp)? GetPlayerFingerprintByName(string name)
        {
            if (_lastScannedUnits == null || _lastScannedUnits.Count == 0)
            {
                try { CollectUnitPositionsFull(); } catch { }
            }
            if (_lastScannedUnits == null) return null;
            foreach (var u in _lastScannedUnits)
            {
                if (u.Team != 0 || u.MaxHp <= 0) continue;
                if (u.Name != null && u.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return (u.Hp, u.MaxHp);
            }
            return null;
        }

        // Addresses that control the C-key cursor cycling mode.
        private const long AddrCursorCycleFlag1 = 0x140D3A400;
        private const long AddrCursorCycleFlag2 = 0x14077CA5C;

        /// <summary>
        /// Public entry point for auto-scan. Called by CommandWatcher when a new turn starts.
        /// </summary>
        public void AutoScanUnits()
        {
            CollectUnitPositionsFull();
        }

        private List<ScannedUnit> CollectUnitPositionsFull()
        {
            var units = new List<ScannedUnit>();

            // === Phase 1: Read active unit data from condensed struct (slot 0) ===
            // The condensed struct has reliable stats for the active unit and its ability list.
            var activeReads = _explorer.ReadMultiple(new (nint, int)[]
            {
                ((nint)(AddrCondensedBase + 0x00), 2), // 0: level
                ((nint)(AddrCondensedBase + 0x02), 2), // 1: team
                ((nint)(AddrCondensedBase + 0x04), 2), // 2: nameId
                ((nint)(AddrCondensedBase + 0x06), 1), // 3: speed
                ((nint)(AddrCondensedBase + 0x08), 2), // 4: exp
                ((nint)(AddrCondensedBase + 0x0A), 2), // 5: CT
                ((nint)(AddrCondensedBase + 0x0C), 2), // 6: HP
                ((nint)(AddrCondensedBase + 0x10), 2), // 7: maxHP
                ((nint)(AddrCondensedBase + 0x12), 2), // 8: MP
                ((nint)(AddrCondensedBase + 0x16), 2), // 9: maxMP
                ((nint)(AddrCondensedBase + 0x18), 2), // 10: PA
                ((nint)(AddrCondensedBase + 0x1A), 2), // 11: MA
                ((nint)(AddrUIBuffer + 0x24), 2),      // 12: Move
                ((nint)(AddrUIBuffer + 0x26), 2),      // 13: Jump
            });
            int activeHp = (int)activeReads[6];
            int activeMaxHp = (int)activeReads[7];
            int activeNameId = (int)activeReads[2];

            // === Phase 2: Scan static battle array for ALL units ===
            // The battle array has fixed-stride 0x200 slots with this layout per slot:
            //   +0x0C: exp(byte)  +0x0D: level(byte)
            //   +0x0E: origBrave  +0x0F: brave  +0x10: origFaith  +0x11: faith
            //   +0x12: team(u16)  — 0=party, 1=enemy, 2=ally/neutral
            //   +0x14: HP(u16)    +0x16: MaxHP(u16)
            //   +0x18: MP(u16)    +0x1A: MaxMP(u16)
            //   +0x33: gridX(byte)  +0x34: gridY(byte)
            //   +0x45: status bytes (5 bytes)
            // Player units are at positive offsets from BattleArrayBase + 0x200,
            // enemy units are at negative offsets (up to ~16 slots back).
            const long BattleArrayBase = 0x140893C00;
            const int ArrayStride = 0x200;
            const int ScanSlotsBack = 20;  // how far back for enemies
            const int ScanSlotsForward = 10; // how far forward for players
            int totalSlots = ScanSlotsBack + ScanSlotsForward;

            try
            {
                // Batch-read key fields for all candidate slots
                const int FieldsPerSlot = 15;
                var slotReads = new (nint, int)[totalSlots * FieldsPerSlot];
                for (int s = 0; s < totalSlots; s++)
                {
                    long sb = BattleArrayBase + (long)(s - ScanSlotsBack + 1) * ArrayStride;
                    slotReads[s * FieldsPerSlot + 0] = ((nint)(sb + 0x0C), 1);  // exp
                    slotReads[s * FieldsPerSlot + 1] = ((nint)(sb + 0x0D), 1);  // level
                    slotReads[s * FieldsPerSlot + 2] = ((nint)(sb + 0x14), 2);  // HP
                    slotReads[s * FieldsPerSlot + 3] = ((nint)(sb + 0x16), 2);  // MaxHP
                    slotReads[s * FieldsPerSlot + 4] = ((nint)(sb + 0x18), 2);  // MP
                    slotReads[s * FieldsPerSlot + 5] = ((nint)(sb + 0x1A), 2);  // MaxMP
                    slotReads[s * FieldsPerSlot + 6] = ((nint)(sb + 0x33), 1);  // gridX
                    slotReads[s * FieldsPerSlot + 7] = ((nint)(sb + 0x34), 1);  // gridY
                    slotReads[s * FieldsPerSlot + 8] = ((nint)(sb + 0x0E), 1);  // origBrave
                    slotReads[s * FieldsPerSlot + 9] = ((nint)(sb + 0x10), 1);  // origFaith
                    slotReads[s * FieldsPerSlot + 10] = ((nint)(sb + 0x12), 2); // inBattleFlag
                    slotReads[s * FieldsPerSlot + 11] = ((nint)(sb + 0x22), 1); // PA (total)
                    slotReads[s * FieldsPerSlot + 12] = ((nint)(sb + 0x23), 1); // MA (total)
                    slotReads[s * FieldsPerSlot + 13] = ((nint)(sb + 0x24), 1); // Speed
                    slotReads[s * FieldsPerSlot + 14] = ((nint)(sb + 0x25), 1); // CT
                }
                var sv = _explorer.ReadMultiple(slotReads);

                // Discover valid units from the array and build ScannedUnit list
                var usedSlots = new HashSet<int>();
                for (int s = 0; s < totalSlots; s++)
                {
                    int exp = (int)sv[s * FieldsPerSlot + 0];
                    int lvl = (int)sv[s * FieldsPerSlot + 1];
                    int hp =  (int)sv[s * FieldsPerSlot + 2];
                    int maxHp = (int)sv[s * FieldsPerSlot + 3];
                    int mp =  (int)sv[s * FieldsPerSlot + 4];
                    int maxMp = (int)sv[s * FieldsPerSlot + 5];
                    int gx =  (int)sv[s * FieldsPerSlot + 6];
                    int gy =  (int)sv[s * FieldsPerSlot + 7];
                    int brave = (int)sv[s * FieldsPerSlot + 8];
                    int faith = (int)sv[s * FieldsPerSlot + 9];
                    int inBattle = (int)sv[s * FieldsPerSlot + 10];

                    // Session 49 filter: accept a slot if EITHER inBattle==1
                    // OR CT > 0. Live capture at Siedge Weald showed 8 slots
                    // in the battle array with valid level+HP+pos, but only
                    // 3 had inBattle=1. Of the remaining 5, 1 was a real
                    // visible Bomb (inBattle=0 but CT=100) and 4 were ghosts
                    // (inBattle=0 AND CT=0 — likely stale slots retained
                    // from a prior battle, since the table doesn't fully
                    // zero across restarts). All 4 real visible enemies had
                    // CT > 0 confirmed against user-reported stats.
                    //
                    // inBattle=1 is still allowed because on battle-start
                    // frame 1 CT may not yet have ticked up. inBattle=2
                    // (observed as HP=8257/32 garbage) is filtered.
                    //
                    // Enemy slots with CT==0 AND inBattle!=1 are ghosts —
                    // the scan loop skips them. Ramza's player slot
                    // (inBattle=1) is always included.
                    int ctField = (int)sv[s * FieldsPerSlot + 14];
                    if (inBattle != 1 && ctField == 0) continue;
                    if (inBattle != 0 && inBattle != 1) continue;

                    // Validate: must have reasonable stats and position within map bounds
                    if (lvl < 1 || lvl > 99) continue;
                    if (maxHp <= 0 || maxHp >= 2000) continue;
                    if (exp > 99) continue;
                    if (gx > 30 || gy > 30) continue;

                    // Team: initially set to 1 (enemy). Roster matching downstream will
                    // correct player units to 0. The static array doesn't have a team field
                    // matching the condensed struct convention (0=party,1=enemy).
                    int team = 1;

                    long slotBase = BattleArrayBase + (long)(s - ScanSlotsBack + 1) * ArrayStride;

                    // Status bytes (5 bytes at +0x45)
                    var statusBytes = _explorer.Scanner.ReadBytes((nint)(slotBase + 0x45), 5);

                    // Facing byte at +0x35 (immediately after gridY at +0x34).
                    // Encoding: 0=South, 1=West, 2=North, 3=East (session 30 live-verified).
                    var facingByteArr = _explorer.Scanner.ReadBytes((nint)(slotBase + 0x35), 1);
                    string? facingName = facingByteArr.Length == 1
                        ? FacingByteDecoder.DecodeName(facingByteArr[0])
                        : null;

                    // Element-affinity bytes at +0x5A..+0x5E (Absorb/Cancel/Half/Weak/Strengthen).
                    // Same element bit layout across all 5 fields. Session 30 live-verified.
                    var elemBytes = _explorer.Scanner.ReadBytes((nint)(slotBase + 0x5A), 5);
                    List<string>? absorbList = null, cancelList = null, halfList = null, weakList = null, strengthenList = null;
                    if (elemBytes.Length == 5)
                    {
                        var a = ElementAffinityDecoder.Decode(elemBytes[0]);
                        var c = ElementAffinityDecoder.Decode(elemBytes[1]);
                        var h = ElementAffinityDecoder.Decode(elemBytes[2]);
                        var w = ElementAffinityDecoder.Decode(elemBytes[3]);
                        var s2 = ElementAffinityDecoder.Decode(elemBytes[4]);
                        if (a.Count > 0) absorbList = a;
                        if (c.Count > 0) cancelList = c;
                        if (h.Count > 0) halfList = h;
                        if (w.Count > 0) weakList = w;
                        if (s2.Count > 0) strengthenList = s2;
                    }

                    // Check if this is the active unit (match by HP+MaxHP with condensed struct)
                    bool isActive = (hp == activeHp && maxHp == activeMaxHp);

                    int paTotal = (int)sv[s * FieldsPerSlot + 11];
                    int maTotal = (int)sv[s * FieldsPerSlot + 12];
                    int speed =   (int)sv[s * FieldsPerSlot + 13];
                    int ct =      (int)sv[s * FieldsPerSlot + 14];

                    var unit = new ScannedUnit
                    {
                        GridX = gx, GridY = gy,
                        Facing = facingName,
                        ElementAbsorb = absorbList,
                        ElementCancel = cancelList,
                        ElementHalf = halfList,
                        ElementWeak = weakList,
                        ElementStrengthen = strengthenList,
                        Level = lvl, Team = team, Exp = exp,
                        Hp = hp, MaxHp = maxHp, Mp = mp, MaxMp = maxMp,
                        Brave = brave, Faith = faith,
                        PA = paTotal, MA = maTotal, Speed = speed, CT = ct,
                    };

                    if (isActive)
                    {
                        unit.IsActive = true;
                        unit.Team = (int)activeReads[1];
                        unit.NameId = (int)activeReads[2];
                        // Session 48: the battle-array slot's gridX/gridY bytes
                        // at +0x33/+0x34 DON'T update when a unit moves during
                        // its turn — they hold the pre-move position for the
                        // whole turn. Override the active unit's position with
                        // the live grid-cursor address (AddrGridX/AddrGridY at
                        // 0x140C64A54 / 0x140C6496C) which IS live. Without
                        // this, scan_move reports stale coords after battle_move
                        // and Attack range / AttackTiles are computed from the
                        // wrong origin. Non-active units keep the slot bytes
                        // (only the active unit's position is in AddrGridX/Y).
                        var livePos = ReadGridPos();
                        if (livePos.x >= 0 && livePos.y >= 0)
                        {
                            unit.GridX = livePos.x;
                            unit.GridY = livePos.y;
                        }
                        // Try to read Move/Jump from the per-unit heap struct
                        // (canonical per-unit effective stats). UIBuffer at +0x24/+0x26
                        // holds the cursor-hovered unit's BASE stats — cursor-hovered
                        // unit might not be the active unit, and base stats ignore
                        // equipment and movement-ability bonuses.
                        // Heap struct layout (session 29 live-verified):
                        //   +0x10: HP (u16)
                        //   +0x12: MaxHP (u16)
                        //   +0x22: Move (u8)
                        //   +0x23: Jump (u8)
                        var heapStats = TryReadMoveJumpFromHeap(unit.Hp, unit.MaxHp);
                        if (heapStats.HasValue)
                        {
                            unit.Move = heapStats.Value.move;
                            unit.Jump = heapStats.Value.jump;
                        }
                        else
                        {
                            // Heap search missed — narrow filter (0x4000000000..0x4200000000,
                            // RW private/mapped only, 500MB budget) covers ~11MB of UE4
                            // heap and misses most unit structs. Historical fallback used
                            // UIBuffer+0x24/+0x26 but that's the CURSOR-HOVERED unit's
                            // BASE stats — unrelated to the active unit's EFFECTIVE Move.
                            // S56 repro: Wilham (Monk base Mv=3) got UIBuffer Mv=4, BFS
                            // produced false-positive tiles including (7,9), game
                            // refused the move, unit stuck in Move mode.
                            //
                            // Honest fix: set Move=Jump=0. MovementBfs.ComputeValidTiles
                            // returns empty for Move=0 (BFS_MoveZero_ReturnsEmpty). Scan
                            // output surfaces "Mv=0 Jmp=0" so Claude sees the heap read
                            // failed and doesn't attempt battle_move with bogus tiles.
                            unit.Move = 0;
                            unit.Jump = 0;
                            ModLogger.Log($"[CollectPositions] Active unit HP={unit.Hp}/{unit.MaxHp}: heap Move/Jump read failed, setting Mv=0 Jp=0 (was UIBuffer fallback — wrong data)");
                        }

                        // Read learned abilities from condensed struct ability list
                        var abilityBytes = _explorer.Scanner.ReadBytes((nint)(AddrCondensedBase + 0x28), 64);
                        if (abilityBytes.Length > 0)
                        {
                            var learnedIds = ActionAbilityLookup.ParseLearnedIdsFromBytes(abilityBytes);
                            unit.LearnedAbilities = ActionAbilityLookup.ResolveLearnedAbilities(learnedIds);
                        }
                    }

                    if (statusBytes.Length == 5)
                    {
                        unit.StatusBytes = statusBytes;
                        var decoded = StatusDecoder.Decode(statusBytes);
                        if (decoded.Count > 0)
                            ModLogger.Log($"[CollectPositions] Unit ({gx},{gy}) statuses: [{string.Join(",", decoded)}]");
                    }

                    units.Add(unit);
                    usedSlots.Add(s);
                    ModLogger.Log($"[CollectPositions] Unit {units.Count}: ({gx},{gy}) t{team} lv{lvl} hp={hp}/{maxHp} br={brave} fa={faith}{(isActive ? " [ACTIVE]" : "")}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log($"[CollectPositions] Battle array read failed: {ex.Message}");
            }

            // Resolve unit identity from roster for player units (team=0)
            // Match by level + brave + faith to find the roster slot, then read nameId, job, secondary
            // (Can't match by HP — roster stores formation HP which differs from battle HP with equipment)
            const long AddrRosterBase = 0x1411A18D0;
            const int RosterStride = 0x258;
            const int RosterMaxSlots = 20;
            const int RosterOffNameId = 0x230;
            const int RosterOffLevel = 0x1D;
            const int RosterOffBrave = 0x1E;
            const int RosterOffFaith = 0x1F;
            const int RosterOffJob = 0x02;
            const int RosterOffSecondary = 0x07;

            try
            {
                // Match against ALL units — the condensed struct team field can be stale
                // during C+Up cycling, so we can't trust it to pre-filter. If a unit matches
                // a roster entry, we KNOW it's a player unit regardless of scanned team.
                var playerUnits = units.ToList();
                if (playerUnits.Count > 0)
                {
                    // Batch-read roster fields for all slots
                    const int FieldsPerSlot = 6;
                    var rosterReads = new (nint, int)[RosterMaxSlots * FieldsPerSlot];
                    for (int s = 0; s < RosterMaxSlots; s++)
                    {
                        long slotAddr = AddrRosterBase + s * RosterStride;
                        rosterReads[s * FieldsPerSlot + 0] = ((nint)(slotAddr + RosterOffNameId), 2);
                        rosterReads[s * FieldsPerSlot + 1] = ((nint)(slotAddr + RosterOffLevel), 1);
                        rosterReads[s * FieldsPerSlot + 2] = ((nint)(slotAddr + RosterOffBrave), 1);
                        rosterReads[s * FieldsPerSlot + 3] = ((nint)(slotAddr + RosterOffFaith), 1);
                        rosterReads[s * FieldsPerSlot + 4] = ((nint)(slotAddr + RosterOffJob), 1);
                        rosterReads[s * FieldsPerSlot + 5] = ((nint)(slotAddr + RosterOffSecondary), 1);
                    }
                    var rosterValues = _explorer.ReadMultiple(rosterReads);

                    // Build roster slot array
                    var rosterSlots = new RosterSlot[RosterMaxSlots];
                    for (int s = 0; s < RosterMaxSlots; s++)
                    {
                        rosterSlots[s] = new RosterSlot
                        {
                            NameId = (int)rosterValues[s * FieldsPerSlot + 0],
                            Level = (int)rosterValues[s * FieldsPerSlot + 1],
                            Brave = (int)rosterValues[s * FieldsPerSlot + 2],
                            Faith = (int)rosterValues[s * FieldsPerSlot + 3],
                            Job = (int)rosterValues[s * FieldsPerSlot + 4],
                            Secondary = (int)rosterValues[s * FieldsPerSlot + 5],
                        };
                    }

                    // Build scanned unit identity array
                    var identities = playerUnits.Select(u => new ScannedUnitIdentity
                    {
                        Level = u.Level, Brave = u.Brave, Faith = u.Faith, Hp = u.Hp, Team = u.Team
                    }).ToArray();

                    // Match using two-pass algorithm (known brave/faith first, then active unit)
                    var matches = RosterMatcher.Match(identities, rosterSlots);

                    for (int i = 0; i < playerUnits.Count && i < matches.Length; i++)
                    {
                        var m = matches[i];
                        var unit = playerUnits[i];
                        // 2026-04-26 Mandalia post-level-up: scanned Level
                        // shifts faster than the roster slot's Level byte,
                        // so RosterMatcher returns NameId=0 for the active
                        // unit. The active-unit memory read carries a
                        // stable NameId (unit.NameId) that we can use as
                        // a fallback key into the per-battle cache.
                        if (m.NameId <= 0)
                        {
                            if (unit.IsActive && unit.NameId > 0)
                            {
                                var cachedMatch = RosterMatchCache.Get(unit.NameId);
                                if (cachedMatch.HasValue)
                                {
                                    m = cachedMatch.Value;
                                    ModLogger.Log($"[CollectPositions] Roster miss for active unit ({unit.GridX},{unit.GridY}) NameId={unit.NameId} — using cached match (Job={m.Job}, Secondary={m.Secondary})");
                                }
                            }
                            if (m.NameId <= 0) continue;
                        }
                        else
                        {
                            // Cache fresh successful match for future fallback.
                            RosterMatchCache.Put(m.NameId, m);
                        }
                        if (unit.Team != 0)
                        {
                            ModLogger.Log($"[CollectPositions] Correcting team for ({unit.GridX},{unit.GridY}): was {unit.Team}, roster match → 0");
                            unit.Team = 0;
                        }
                        unit.RosterNameId = m.NameId;
                        // Prefer story character name (hardcoded for known nameIds).
                        // Fall back to the roster name table keyed by slot index.
                        // The table stores per-roster-slot records where the first
                        // null-terminated string in each record is the chosen
                        // display name. Generic recruits (Knight named "Kenrick"
                        // etc.) get their real names from this lookup.
                        unit.Name = UnitNameLookup.GetName(m.NameId)
                            ?? (m.SlotIndex >= 0 ? _nameTable.GetNameBySlot(m.SlotIndex) : null);
                        unit.Job = m.Job;
                        unit.Brave = m.Brave;
                        unit.Faith = m.Faith;
                        unit.SecondaryAbility = m.Secondary;

                        // Read this unit's per-job learned-action-ability bitfield from
                        // the roster slot at +0x32 + jobIdx*3 (2 bytes per job, MSB-first).
                        // Only read the primary and secondary skillsets' jobs to keep the
                        // work scoped. See memory/project_roster_learned_abilities.md.
                        if (m.SlotIndex >= 0)
                        {
                            long slotAddr = AddrRosterBase + m.SlotIndex * RosterStride;
                            unit.LearnedBitfieldByJobIdx = new Dictionary<int, (byte, byte)>();

                            var jobsToRead = new HashSet<int>();
                            // Primary: resolve via skillset name → job idx. This handles
                            // story character jobs (Gallant Knight → Mettle → idx 0) that
                            // GetJobJpOffset doesn't know about.
                            var primaryJobName = unit.JobNameOverride ?? GameStateReporter.GetJobName(m.Job);
                            var primarySkillset = primaryJobName != null
                                ? Utilities.CommandWatcher.GetPrimarySkillsetByJobName(primaryJobName)
                                : null;
                            if (primarySkillset != null)
                            {
                                int primaryJobIdx = AbilityData.GetJobIdxBySkillsetName(primarySkillset);
                                if (primaryJobIdx >= 0) jobsToRead.Add(primaryJobIdx);
                            }
                            // Secondary: secondary is a skillset index; map to the owning job idx.
                            var secondarySkillset = Utilities.CommandWatcher.GetSkillsetName(m.Secondary);
                            if (secondarySkillset != null)
                            {
                                int secondaryJobIdx = AbilityData.GetJobIdxBySkillsetName(secondarySkillset);
                                if (secondaryJobIdx >= 0) jobsToRead.Add(secondaryJobIdx);
                            }

                            foreach (var jobIdx in jobsToRead)
                            {
                                long bitfieldAddr = slotAddr + 0x32 + jobIdx * 3;
                                var bytesRead = _explorer.Scanner.ReadBytes((nint)bitfieldAddr, 2);
                                if (bytesRead != null && bytesRead.Length >= 2)
                                {
                                    unit.LearnedBitfieldByJobIdx[jobIdx] = (bytesRead[0], bytesRead[1]);
                                }
                            }

                            // Read equipment slots (7 × uint16 at roster +0x0E)
                            const int equipStart = 0x0E;
                            var equipReads = _explorer.ReadMultiple(Enumerable.Range(0, 7)
                                .Select(i => ((nint)(slotAddr + equipStart + i * 2), 2))
                                .ToArray());
                            var eq = new List<int>();
                            for (int e = 0; e < 7; e++)
                            {
                                int eqId = (int)equipReads[e];
                                if (eqId != 0xFF && eqId != 0xFFFF)
                                    eq.Add(eqId);
                            }
                            if (eq.Count > 0)
                                unit.Equipment = eq;

                            // Read equipped passive abilities from roster (+0x08/+0x0A/+0x0C)
                            var passiveReads = _explorer.ReadMultiple(new[]
                            {
                                ((nint)(slotAddr + 0x08), 1), // reaction ID
                                ((nint)(slotAddr + 0x09), 1), // reaction equipped flag
                                ((nint)(slotAddr + 0x0A), 1), // support ID
                                ((nint)(slotAddr + 0x0B), 1), // support equipped flag
                                ((nint)(slotAddr + 0x0C), 1), // movement ID
                                ((nint)(slotAddr + 0x0D), 1), // movement equipped flag
                            });
                            if ((int)passiveReads[1] == 1)
                            {
                                var id = (byte)(int)passiveReads[0];
                                unit.ReactionAbility = AbilityData.ReactionAbilities.TryGetValue(id, out var ra) ? ra.Name : null;
                            }
                            if ((int)passiveReads[3] == 1)
                            {
                                var id = (byte)(int)passiveReads[2];
                                unit.SupportAbility = AbilityData.SupportAbilities.TryGetValue(id, out var sa) ? sa.Name : null;
                            }
                            if ((int)passiveReads[5] == 1)
                            {
                                var id = (byte)(int)passiveReads[4];
                                unit.MovementAbility = AbilityData.MovementAbilities.TryGetValue(id, out var ma) ? ma.Name : null;
                            }
                        }
                        // For story characters, the roster's job field at +0x02 equals
                        // their nameId rather than a real job ID (e.g. Marach job=26
                        // which PSX maps to Dragoon). StoryCharacterJob dict provides
                        // the canonical job name for these characters.
                        // Session 48: Ramza (nameId=1) needs chapter-aware
                        // job lookup — his job byte collides with generic
                        // PSX IDs (0x01 = Chemist but Ramza Ch2 Squire).
                        // Check Ramza first so he bypasses the generic fallback.
                        var storyJob = m.NameId == 1
                            ? CharacterData.GetRamzaJob(m.Job)
                            : CharacterData.GetStoryJob(m.NameId);
                        if (storyJob != null)
                        {
                            unit.JobNameOverride = storyJob;
                        }
                        else if (unit.Team == 0)
                        {
                            // For generic player units, use the roster job ID to set
                            // the job name. This prevents the fingerprint lookup from
                            // overriding with a monster class (e.g. Wilham roster job=82
                            // matched as "Steelhawk" instead of "Summoner" via fingerprint).
                            var rosterJobName = CharacterData.GetJobName(m.Job);
                            if (rosterJobName != null)
                                unit.JobNameOverride = rosterJobName;
                        }
                        ModLogger.Log($"[CollectPositions] Matched ({unit.GridX},{unit.GridY}) as {unit.Name ?? $"unit"} job={m.Job} bra={m.Brave} fai={m.Faith} (rosterNameId={m.NameId}){(storyJob != null ? $" → storyJob={storyJob}" : "")}");
                    }

                    // Session 48 fix: the HP-only isActive check at line 3886
                    // can flag MULTIPLE slots as active when two units share HP
                    // (Lv 1 Ramza + Lv 1 Delita both at 49/49 in Mandalia
                    // Ch2). That makes downstream scan output attribute the
                    // active-unit ability list to the wrong slot. After
                    // roster-match resolves nameIds, keep IsActive only on the
                    // unit whose RosterNameId matches the condensed struct's
                    // active nameId. If no roster-matched unit claims it,
                    // leave the HP-match result as fallback (unmatched active
                    // unit like Delita pre-recruitment).
                    var activeCandidates = units.Where(u => u.IsActive).ToList();
                    if (activeCandidates.Count > 1 && activeNameId > 0)
                    {
                        var trueActive = activeCandidates.FirstOrDefault(u => u.RosterNameId == activeNameId);
                        if (trueActive != null)
                        {
                            foreach (var u in activeCandidates)
                                if (u != trueActive) u.IsActive = false;
                            ModLogger.Log($"[CollectPositions] De-duped active: kept ({trueActive.GridX},{trueActive.GridY}) nameId={activeNameId}, demoted {activeCandidates.Count - 1} HP-match duplicate(s)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log($"[CollectPositions] Name lookup failed: {ex.Message}");
            }

            // Class fingerprint lookup: find each unit's heap struct and read bytes at +0x69.
            // This is the only reliable way to identify enemy classes — the UI buffer returns
            // Job=1 (Chemist) for all enemies, and enemies aren't in the roster.
            // See memory/project_class_fingerprint.md for the investigation.
            try
            {
                foreach (var unit in units)
                {
                    if (unit.JobNameOverride != null) continue;  // Already resolved (e.g. via roster)
                    if (unit.MaxHp <= 0) continue;

                    // Search for the 4-byte HP+MaxHP pattern. HP is at struct +0x10, MaxHP at +0x12.
                    // Unique enough per unit since real FFT battles rarely have two units with
                    // identical HP AND MaxHP AND the same class state.
                    // For wounded units (HP != MaxHP), this naturally matches only that unit.
                    // For full-HP enemies (HP == MaxHP), it may have duplicates (e.g., two Chemists
                    // with the same HP roll), but we take the first match since same class → same fingerprint.
                    var hpPattern = new byte[]
                    {
                        (byte)(unit.Hp & 0xFF), (byte)(unit.Hp >> 8),
                        (byte)(unit.MaxHp & 0xFF), (byte)(unit.MaxHp >> 8),
                    };

                    // Search the heap unit struct range. Originally hardcoded to
                    // 0x4160000000..0x4180000000, but some battles have unit structs outside
                    // this narrow range. Widened to 0x4000000000..0x4200000000 to cover more
                    // UE4 heap addresses. The upper limit intentionally stops before
                    // 0x420000_0000 (graphics data) to avoid false positives in sequential
                    // float arrays that coincidentally match HP byte patterns.
                    var heapMatches = _explorer.SearchBytesInAllMemory(
                        hpPattern, maxResults: 16, minAddr: 0x4000000000L, maxAddr: 0x4200000000L);
                    if (heapMatches.Count == 0)
                    {
                        var cached = _unitNameCache.Get(unit.GridX, unit.GridY)
                            ?? _unitNameCache.GetByStats(unit.MaxHp, unit.Level, unit.Team);
                        if (cached != null)
                        {
                            unit.JobNameOverride = cached;
                            ModLogger.Log($"[CollectPositions] Cache hit ({unit.GridX},{unit.GridY}) → {cached} (no heap match; pos+stats fallback)");
                        }
                        else
                        {
                            ModLogger.Log($"[CollectPositions] No heap match for ({unit.GridX},{unit.GridY}) hp={unit.Hp}/{unit.MaxHp}");
                        }
                        continue;
                    }

                    // Score each non-zero-fingerprint candidate by how well its
                    // level byte (at struct+0x09) agrees with the scanned level.
                    // When multiple heap slots match a common (HP, MaxHP) pattern
                    // (live-observed: Archer at HP=4/MaxHP=452 relabeled
                    // across scans as Black Goblin then Knight via false-
                    // positive heap hits), the level filter steers selection
                    // to the real struct. HeapUnitMatchClassifier treats
                    // expectedLevel==0 as unknown so no candidate is penalised,
                    // preserving first-match behavior when pre-scan level reads
                    // haven't settled.
                    byte[]? fpBytes = null;
                    int bestScore = int.MinValue;
                    long bestBaseForLog = 0;
                    int bestCandLevelForLog = -1;
                    foreach (var match in heapMatches)
                    {
                        var candidateBase = (long)match.address - 0x10;
                        var candidateFpAddr = (nint)(candidateBase + 0x69);
                        var candidateBytes = _explorer.Scanner.ReadBytes(candidateFpAddr, 11);
                        if (candidateBytes.Length != 11) continue;
                        // Skip if all-zero (dead/reserved slot).
                        bool allZero = true;
                        for (int bi = 0; bi < 11; bi++)
                            if (candidateBytes[bi] != 0) { allZero = false; break; }
                        if (allZero) continue;

                        int candLevel = -1;
                        var levelRead = _explorer.Scanner.ReadBytes((nint)(candidateBase + 0x09), 1);
                        if (levelRead.Length == 1) candLevel = levelRead[0];
                        int score = HeapUnitMatchClassifier.Score(candLevel, unit.Level);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            fpBytes = candidateBytes;
                            bestBaseForLog = candidateBase;
                            bestCandLevelForLog = candLevel;
                        }
                    }
                    if (fpBytes != null && heapMatches.Count > 1)
                        ModLogger.Log($"[CollectPositions] Heap match ({unit.GridX},{unit.GridY}) hp={unit.Hp}/{unit.MaxHp} lv={unit.Level}: picked base=0x{bestBaseForLog:X} candLevel={bestCandLevelForLog} score={bestScore} from {heapMatches.Count} candidates");
                    if (fpBytes == null)
                    {
                        var cached2 = _unitNameCache.Get(unit.GridX, unit.GridY)
                            ?? _unitNameCache.GetByStats(unit.MaxHp, unit.Level, unit.Team);
                        if (cached2 != null)
                        {
                            unit.JobNameOverride = cached2;
                            ModLogger.Log($"[CollectPositions] Cache hit ({unit.GridX},{unit.GridY}) → {cached2} (zero fingerprint; pos+stats fallback)");
                        }
                        else
                        {
                            ModLogger.Log($"[CollectPositions] All heap matches had zero fingerprint for ({unit.GridX},{unit.GridY}) hp={unit.Hp}/{unit.MaxHp}");
                        }
                        continue;
                    }

                    unit.ClassFingerprint = fpBytes;
                    var jobName = ClassFingerprintLookup.GetJobName(fpBytes, team: unit.Team);
                    if (jobName != null)
                    {
                        unit.JobNameOverride = jobName;
                        // Cache by both position and stats. Stats key (maxHp,
                        // level, team) survives moves and deaths so subsequent
                        // scans recover the name even when the unit relocates
                        // or the heap fingerprint zeroes out post-KO.
                        _unitNameCache.Set(unit.GridX, unit.GridY, unit.MaxHp, unit.Level, unit.Team, jobName);
                        ModLogger.Log($"[CollectPositions] Fingerprint match ({unit.GridX},{unit.GridY}) → {jobName}");
                    }
                    else
                    {
                        var fpKey = ClassFingerprintLookup.ToKey(fpBytes);
                        ModLogger.Log($"[CollectPositions] Unknown fingerprint ({unit.GridX},{unit.GridY}): {fpKey} hp={unit.Hp}/{unit.MaxHp} lv={unit.Level}");
                    }

                    // Read equipped passive abilities from heap struct bitfields.
                    // Reaction: 4 bytes at +0x74, Support: 5 bytes at +0x78.
                    // See BATTLE_MEMORY_MAP.md section 16 "Passive Ability Bitfields".
                    try
                    {
                        var structBase = (long)heapMatches[0].address - 0x10;
                        var reactionBytes = _explorer.Scanner.ReadBytes((nint)(structBase + 0x74), 4);
                        var supportBytes = _explorer.Scanner.ReadBytes((nint)(structBase + 0x78), 5);

                        if (reactionBytes.Length == 4)
                            unit.ReactionAbility = PassiveAbilityDecoder.DecodeReaction(reactionBytes);
                        if (supportBytes.Length == 5)
                            unit.SupportAbility = PassiveAbilityDecoder.DecodeSupport(supportBytes);

                        if (unit.ReactionAbility != null || unit.SupportAbility != null)
                            ModLogger.Log($"[CollectPositions] Passives ({unit.GridX},{unit.GridY}): reaction={unit.ReactionAbility ?? "none"} support={unit.SupportAbility ?? "none"}");
                    }
                    catch (Exception pex)
                    {
                        ModLogger.Log($"[CollectPositions] Passive ability read failed ({unit.GridX},{unit.GridY}): {pex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log($"[CollectPositions] Fingerprint lookup failed: {ex.Message}");
            }

            _lastScannedUnits = units;
            return units;
        }

        // PostMessage for sending keys to game window
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Navigate the grid cursor by the given delta using arrow keys.
        /// </summary>
        private void NavigateGrid(int deltaX, int deltaY, int rotation)
        {
            int delay = 40;

            // Move along X axis
            if (deltaX != 0)
            {
                int dir = deltaX > 0
                    ? FindDirForDelta(rotation, 1, 0)  // +X
                    : FindDirForDelta(rotation, -1, 0); // -X
                int vk = DirVKs[dir];
                for (int i = 0; i < Math.Abs(deltaX); i++)
                {
                    _input.SendKeyPressToWindow(_gameWindow, vk);
                    Thread.Sleep(delay);
                }
            }

            // Move along Y axis
            if (deltaY != 0)
            {
                int dir = deltaY > 0
                    ? FindDirForDelta(rotation, 0, 1)  // +Y
                    : FindDirForDelta(rotation, 0, -1); // -Y
                int vk = DirVKs[dir];
                for (int i = 0; i < Math.Abs(deltaY); i++)
                {
                    _input.SendKeyPressToWindow(_gameWindow, vk);
                    Thread.Sleep(delay);
                }
            }
        }

        /// <summary>
        /// Find which direction index (0=Right,1=Left,2=Up,3=Down) produces
        /// the given grid delta at the given rotation.
        /// </summary>
        private int FindDirForDelta(int rotation, int wantDx, int wantDy)
        {
            for (int d = 0; d < 4; d++)
            {
                var (dx, dy) = ArrowGridDelta[rotation, d];
                if (dx == wantDx && dy == wantDy) return d;
            }
            return 0; // fallback
        }

        // Movement tile list address (7 bytes per entry: X Y elev flag 0 0 0)
        private const long AddrMoveTileBase = 0x140C66315;

        /// <summary>
        /// Read the valid movement tile list and convert from world coords to grid coords.
        /// Uses the current cursor position (= unit's grid pos) and tile[0] (= unit's world pos)
        /// to compute the offset.
        /// </summary>
        private HashSet<(int gx, int gy)> ReadValidTilesAsGrid((int x, int y) cursorGridPos)
        {
            var tiles = new HashSet<(int gx, int gy)>();
            try
            {
                var raw = _explorer.Scanner.ReadBytes((nint)AddrMoveTileBase, 210); // 30 tiles max
                if (raw.Length < 7) return tiles;

                // Tile[0] = unit's world position. Compute offset.
                int worldX0 = raw[0];
                int worldY0 = raw[1];
                int offsetX = worldX0 - cursorGridPos.x;
                int offsetY = worldY0 - cursorGridPos.y;

                for (int i = 0; i < raw.Length - 6; i += 7)
                {
                    int wx = raw[i];
                    int wy = raw[i + 1];
                    int flag = raw[i + 3];
                    if (flag == 0 && wx == 0 && wy == 0) break;

                    int gx = wx - offsetX;
                    int gy = wy - offsetY;
                    tiles.Add((gx, gy));
                }

                ModLogger.Log($"[ReadValidTiles] {tiles.Count} valid tiles, offset=({offsetX},{offsetY})");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[ReadValidTiles] Failed: {ex.Message}");
            }
            return tiles;
        }

        /// <summary>
        /// Read battleTeam from the condensed struct (cursor-highlighted unit).
        /// Returns 0 for friendly/empty, non-zero for enemy.
        /// </summary>
        private int ReadCursorTeam()
        {
            try
            {
                var result = _explorer.ReadAbsolute((nint)AddrCursorTeam, 2);
                return result != null ? (int)result.Value.value : -1;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Escape out of targeting/abilities/menus back to BattleMyTurn.
        /// Sends up to 5 Escape presses, checking screen state after each.
        /// </summary>
        private void EscapeToMyTurn()
        {
            for (int i = 0; i < 5; i++)
            {
                var screen = _detectScreen();
                if (screen?.Name == "BattleMyTurn") return;
                SendKey(VK_ESCAPE);
                Thread.Sleep(300);
            }
        }

        /// <summary>
        /// Read projected damage and hit% from the attacker's heap struct.
        /// The game writes these while the cursor is on a target in targeting mode:
        ///   Hit% at statBase - 62 (u16)
        ///   Damage at statBase - 96 (u16)
        /// Finds the attacker's struct by searching readonly memory for MaxHP+MaxHP.
        /// </summary>
        private (int damage, int hitPct) ReadDamagePreview(int attackerMaxHp)
        {
            if (attackerMaxHp <= 0) return (0, 0);
            try
            {
                byte[] pattern = {
                    (byte)(attackerMaxHp & 0xFF), (byte)(attackerMaxHp >> 8),
                    (byte)(attackerMaxHp & 0xFF), (byte)(attackerMaxHp >> 8)
                };
                // Search readonly regions where the copy with preview data was
                // historically expected to live.
                var matches = _explorer.SearchBytesInAllMemory(
                    pattern, 20, 0x140800000L, 0x15C000000L, broadSearch: true);

                foreach (var (addr, _) in matches)
                {
                    nint statBase = addr - 10; // MaxHP is at +10 from stat base
                    byte levelByte = 0;
                    try { levelByte = _explorer.Scanner.ReadByte(statBase + 1); } catch { continue; }
                    if (levelByte < 1 || levelByte > 99) continue;

                    var hitBytes = _explorer.Scanner.ReadBytes(statBase - 62, 2);
                    var dmgBytes = _explorer.Scanner.ReadBytes(statBase - 96, 2);
                    if (hitBytes.Length < 2 || dmgBytes.Length < 2) continue;

                    int hitPct = BitConverter.ToUInt16(hitBytes, 0);
                    int damage = BitConverter.ToUInt16(dmgBytes, 0);

                    // The copy with real preview data has hit% in 1-100 range.
                    // Session 30 live-verified this condition is never met in IC
                    // remaster — see memory/project_damage_preview_hunt_s30.md.
                    if (hitPct < 1 || hitPct > 100) continue;
                    if (damage <= 0 || damage > 9999) continue;

                    ModLogger.Log($"[DamagePreview] Hit={hitPct}% Dmg={damage} (struct 0x{statBase:X}, lv={levelByte})");
                    return (damage, hitPct);
                }
                ModLogger.Log($"[DamagePreview] No preview found ({matches.Count} MaxHP matches)");
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"[DamagePreview] Error: {ex.Message}");
            }
            return (0, 0);
        }

        /// <summary>
        /// Read a unit's live HP by searching all readable memory for their MaxHP+MaxHP
        /// pattern and reading the HP at the same struct offset. The static array at
        /// 0x140893C00 is stale mid-turn, but readonly copies in 0x141xxx/0x15Axxx
        /// update immediately after damage. Returns -1 if not found.
        /// </summary>
        private int ReadLiveHp(int unitMaxHp, int preAttackHp, int targetLevel = 0)
        {
            if (unitMaxHp <= 0) return -1;
            try
            {
                // S58: cache fast path. Cached addresses survive within a
                // battle; try them before the expensive full-memory scan.
                var cached = LiveHpCache.GetCachedAddresses(unitMaxHp, targetLevel);
                if (cached != null)
                {
                    foreach (var addr in cached)
                    {
                        nint hpAddr = (nint)addr - 2;
                        var hpBytes = _explorer.Scanner.ReadBytes(hpAddr, 2);
                        if (hpBytes.Length < 2) continue;
                        int hp = BitConverter.ToUInt16(hpBytes, 0);
                        // Plausibility: HP must be within [0, maxHp]. If out of
                        // range the address is stale (battle changed or the
                        // page recycled).
                        if (hp < 0 || hp > unitMaxHp) continue;
                        // If HP moved off preAttackHp, we have the live address.
                        // If HP equals preAttackHp, it could be the stale copy OR
                        // the live one before damage landed — fall through to
                        // full search to disambiguate.
                        if (hp != preAttackHp)
                        {
                            ModLogger.Log($"[ReadLiveHp] cache hit: HP={hp} at 0x{(long)addr:X}");
                            return hp;
                        }
                    }
                    // No cached address showed motion — re-scan (the live
                    // address may have relocated or preAttackHp was simply
                    // unchanged by this attack).
                    LiveHpCache.Invalidate(unitMaxHp, targetLevel);
                }

                // Search all readable memory for structs with this unit's MaxHP.
                // The struct layout has: ... HP(u16) MaxHP(u16) MP(u16) ...
                // Search for a longer context pattern to reduce false matches:
                // Use the pre-attack snapshot: preHp(u16) + MaxHP(u16)
                // Then also search for MaxHP+MaxHP (the "saved" copy pattern).
                // Compare all found HPs — the one that differs from preAttackHp is live.

                // Search readonly memory for any struct with this unit's MaxHP.
                // The struct has HP(u16) immediately before MaxHP(u16).
                // The readonly copies update in real-time — if damage landed,
                // HP will differ from preAttackHp.
                byte[] maxHpBytes = {
                    (byte)(unitMaxHp & 0xFF), (byte)(unitMaxHp >> 8)
                };
                // Search two ranges: the static array for the stale reference,
                // then readonly regions for the live data.
                var staleMatches = _explorer.SearchBytesInAllMemory(
                    maxHpBytes, 10, 0x140893000L, 0x140895000L, broadSearch: true);
                var liveMatches = _explorer.SearchBytesInAllMemory(
                    maxHpBytes, 100, 0x141000000L, 0x15C000000L, broadSearch: true);
                var matches = new List<(nint address, string context)>();
                matches.AddRange(staleMatches);
                matches.AddRange(liveMatches);

                // Collect valid unit structs. Read 8 bytes of context (the stat pattern:
                // exp level origBr br origFa fa turnFlag 00) to fingerprint each struct.
                // Only accept a "changed" HP if its context matches a "stale" copy's context.
                var staleContexts = new List<(byte[] ctx, nint addr)>();
                var changedEntries = new List<(int hp, byte[] ctx, nint addr)>();

                foreach (var (addr, _) in matches)
                {
                    nint hpAddr = addr - 2;
                    var hpBytes = _explorer.Scanner.ReadBytes(hpAddr, 2);
                    if (hpBytes.Length < 2) continue;
                    int hp = BitConverter.ToUInt16(hpBytes, 0);
                    if (hp > unitMaxHp) continue;

                    // Read stat context: 8 bytes starting at statBase (= hpAddr - 8)
                    nint statBase = hpAddr - 8;
                    var ctx = _explorer.Scanner.ReadBytes(statBase, 8);
                    if (ctx.Length < 8) continue;

                    // Verify level at byte 1
                    if (ctx[1] < 1 || ctx[1] > 99) continue;

                    if (hp == preAttackHp)
                        staleContexts.Add((ctx, addr));
                    else
                        changedEntries.Add((hp, ctx, addr));
                }

                // Match by level. Use targetLevel from static array if available,
                // otherwise fall back to stale context level.
                int expectedLevel = targetLevel > 0 ? targetLevel
                    : staleContexts.Count > 0 ? staleContexts[0].ctx[1] : 0;

                if (expectedLevel > 0)
                {
                    foreach (var (hp, ctx, addr) in changedEntries)
                    {
                        if (ctx[1] == expectedLevel)
                        {
                            ModLogger.Log($"[ReadLiveHp] Live HP={hp} at 0x{addr:X} (lv={ctx[1]})");
                            // S58: memoize for next attack on this target.
                            LiveHpCache.Remember(unitMaxHp, targetLevel, addr);
                            return hp;
                        }
                    }
                }

                // Log detail for debugging
                foreach (var (sc, sa) in staleContexts)
                    ModLogger.Log($"[ReadLiveHp] STALE at 0x{sa:X}: lv={sc[1]} hp={preAttackHp}");
                foreach (var (ch, cc, ca) in changedEntries)
                    ModLogger.Log($"[ReadLiveHp] CHANGED at 0x{ca:X}: lv={cc[1]} hp={ch}");
                ModLogger.Log($"[ReadLiveHp] stale={staleContexts.Count} changed={changedEntries.Count} ({matches.Count} total)");
                return preAttackHp;
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"[ReadLiveHp] Error: {ex.Message}");
            }
            return -1;
        }

        /// <summary>
        /// Read projected damage and hit% from the attacker's heap struct during targeting mode.
        /// The game writes these values relative to the unit's battle struct:
        ///   Hit% at statBase - 62 (u16, 0-100)
        ///   Damage at statBase - 96 (u16)
        /// Finds the struct by searching for the MaxHP+MaxHP pattern (both copies match
        /// even when the unit is damaged). Verified across multiple targets 2026-04-12.
        /// </summary>
        private (int damage, int hitPct) ReadDamagePreview(int attackerHp, int attackerMaxHp)
        {
            if (attackerMaxHp <= 0) return (0, 0);
            try
            {
                // Search ALL readable memory for MaxHP+MaxHP pattern. The copy with
                // damage preview data lives in a non-standard memory region (not
                // PAGE_READWRITE private). Use broadSearch to scan all readable regions.
                byte[] pattern = {
                    (byte)(attackerMaxHp & 0xFF), (byte)(attackerMaxHp >> 8),
                    (byte)(attackerMaxHp & 0xFF), (byte)(attackerMaxHp >> 8)
                };
                var matches = _explorer.SearchBytesInAllMemory(pattern, 20, 0L, long.MaxValue, broadSearch: true);

                foreach (var (addr, _) in matches)
                {
                    nint statBase = addr - 8; // HP is at +8 from stat pattern start
                    // Verify level byte at +1 is reasonable
                    byte levelByte = _explorer.Scanner.ReadByte(statBase + 1);
                    if (levelByte < 1 || levelByte > 99)
                    {
                        ModLogger.Log($"[DamagePreview] Rejected 0x{addr:X}: level={levelByte}");
                        continue;
                    }

                    // Read hit% at statBase - 62 and damage at statBase - 96
                    var hitBytes = _explorer.Scanner.ReadBytes(statBase - 62, 2);
                    var dmgBytes = _explorer.Scanner.ReadBytes(statBase - 96, 2);
                    if (hitBytes.Length < 2 || dmgBytes.Length < 2) continue;

                    int hitPct = BitConverter.ToUInt16(hitBytes, 0);
                    int damage = BitConverter.ToUInt16(dmgBytes, 0);
                    ModLogger.Log($"[DamagePreview] Candidate 0x{addr:X}: lv={levelByte} hit={hitPct} dmg={damage}");

                    // Sanity check
                    if (hitPct > 100 || damage > 9999) continue;
                    if (hitPct == 0 && damage == 0) continue;

                    ModLogger.Log($"[DamagePreview] Hit={hitPct}% Damage={damage} (struct 0x{statBase:X})");
                    return (damage, hitPct);
                }
                ModLogger.Log($"[DamagePreview] No valid struct found for MaxHP={attackerMaxHp} ({matches.Count} candidates)");
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"[DamagePreview] Error: {ex.Message}");
            }
            return (0, 0);
        }

        /// <summary>
        /// Read a unit's HP from the static battle array by scanning all slots for
        /// a unit at the given grid position. Returns -1 if not found.
        /// </summary>
        private int ReadStaticArrayHpAt(int gridX, int gridY)
        {
            return ReadStaticArrayFieldAt(gridX, gridY, 0x14); // HP offset
        }

        private int ReadStaticArrayMaxHpAt(int gridX, int gridY)
        {
            return ReadStaticArrayFieldAt(gridX, gridY, 0x16); // MaxHP offset
        }

        private int ReadStaticArrayFieldAt(int gridX, int gridY, int fieldOffset)
        {
            const long Base = 0x140893C00;
            const int Stride = 0x200;
            const int SlotsBack = 20;
            const int TotalSlots = 30;
            try
            {
                for (int s = 0; s < TotalSlots; s++)
                {
                    long sb = Base + (long)(s - SlotsBack + 1) * Stride;
                    var reads = _explorer.ReadMultiple(new[]
                    {
                        ((nint)(sb + 0x12), 2), // inBattleFlag
                        ((nint)(sb + 0x33), 1), // gridX
                        ((nint)(sb + 0x34), 1), // gridY
                        ((nint)(sb + fieldOffset), 2), // requested field
                    });
                    if ((int)reads[0] != 1) continue;
                    if ((int)reads[1] == gridX && (int)reads[2] == gridY)
                        return (int)reads[3];
                }
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Read the current cursor grid position.
        /// </summary>
        private (int x, int y) ReadGridPos()
        {
            try
            {
                var results = _explorer.ReadMultiple(new[]
                {
                    ((nint)AddrGridX, 1),
                    ((nint)AddrGridY, 1),
                });
                return ((int)results[0], (int)results[1]);
            }
            catch { return (-1, -1); }
        }

        // ================================================================
        // Compound navigation: party menu tree
        // ================================================================

        /// <summary>
        /// Navigate from any screen to a unit's EquipmentAndAbilities.
        /// Handles: escape to WorldMap → PartyMenu → cursor to unit →
        /// CharacterStatus → EquipmentAndAbilities. All internal, one
        /// round-trip from Claude's perspective.
        /// </summary>
        private CommandResponse OpenEqa(CommandResponse response, string unitName)
        {
            var result = NavigateToCharacterStatus(response, unitName);
            if (result.Status == "failed") return result;

            // CharacterStatus sidebar defaults to "Equipment & Abilities" (index 0).
            // Press Enter to open EqA.
            SendKey(VK_ENTER);
            Thread.Sleep(300);

            var screen = _detectScreen();
            if (screen != null)
            {
                result.Screen = screen;
                result.Status = "completed";
            }
            return result;
        }

        /// <summary>
        /// Navigate from any screen to a unit's JobSelection.
        /// </summary>
        private CommandResponse OpenJobSelection(CommandResponse response, string unitName)
        {
            var result = NavigateToCharacterStatus(response, unitName);
            if (result.Status == "failed") return result;

            // Sidebar: Down (to Job at index 1) → Enter
            SendKey(VK_DOWN);
            Thread.Sleep(200);
            SendKey(VK_ENTER);
            Thread.Sleep(300);

            var screen = _detectScreen();
            if (screen != null)
            {
                result.Screen = screen;
                result.Status = "completed";
            }
            return result;
        }

        /// <summary>
        /// Swap the viewed unit on a nested party-tree screen (CharacterStatus,
        /// EquipmentAndAbilities, JobSelection, or ability/equipment pickers)
        /// via Q/E cycling. Resolves target name to displayOrder, compares
        /// against the SM's current ViewedGridIndex, and sends the shortest
        /// Q/E sequence via UnitCyclePlanner.
        /// </summary>
        private CommandResponse SwapUnitTo(CommandResponse response, string unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName))
            {
                response.Status = "failed";
                response.Error = "swap_unit_to requires a unit name";
                return response;
            }

            var curScreen = _detectScreen();
            string curName = curScreen?.Name ?? "Unknown";
            bool validHere =
                curName == "CharacterStatus" ||
                curName == "EquipmentAndAbilities" ||
                curName == "JobSelection" ||
                curName == "EquippableWeapons" ||
                curName == "EquippableShields" ||
                curName == "EquippableHeadware" ||
                curName == "EquippableCombatGarb" ||
                curName == "EquippableAccessories" ||
                curName == "AbilityPicker";
            if (!validHere)
            {
                response.Status = "failed";
                response.Error = $"swap_unit_to only works from a nested party-tree screen (CharacterStatus/EqA/JobSelection/pickers). Current: {curName}";
                response.Screen = curScreen;
                return response;
            }

            var rosterReader = new RosterReader(_explorer, _nameTable);
            var allSlots = rosterReader.ReadAll();
            RosterReader.RosterSlot? targetSlot = null;
            foreach (var slot in allSlots)
            {
                if (slot.Name != null && slot.Name.Equals(unitName, StringComparison.OrdinalIgnoreCase))
                {
                    targetSlot = slot;
                    break;
                }
            }
            if (targetSlot == null)
            {
                response.Status = "failed";
                response.Error = $"Unit '{unitName}' not found in roster";
                response.Screen = curScreen;
                return response;
            }

            int rosterCount = allSlots.Count;
            int fromIndex = ScreenMachine?.ViewedGridIndex ?? 0;
            int toIndex = targetSlot.DisplayOrder;

            // ViewedGridIndex is row*5+col over a 5-col grid with gaps; on
            // a 14-unit roster, indexes 0..13 are occupied and rosterCount
            // is the ring size. If fromIndex falls outside [0, rosterCount),
            // clamp — the SM occasionally parks at an unused grid cell.
            if (fromIndex < 0 || fromIndex >= rosterCount) fromIndex = 0;

            var plan = UnitCyclePlanner.Plan(fromIndex, toIndex, rosterCount);
            if (plan.Keys.Length == 0 && fromIndex != toIndex)
            {
                response.Status = "failed";
                response.Error = $"UnitCyclePlanner rejected ({fromIndex}→{toIndex}, n={rosterCount})";
                response.Screen = curScreen;
                return response;
            }

            foreach (char k in plan.Keys)
            {
                int vk = k == 'Q' ? VK_Q : VK_E;
                SendKey(vk);
                Thread.Sleep(250);
            }

            response.Status = "completed";
            response.Info = plan.Keys.Length == 0
                ? $"Already viewing {unitName}"
                : $"Sent {plan.Keys.Length}×{plan.Keys[0]} to cycle {fromIndex}→{toIndex}";
            response.Screen = _detectScreen();
            return response;
        }

        /// <summary>
        /// Navigate from any screen to a unit's CharacterStatus.
        /// </summary>
        private CommandResponse OpenCharacterStatus(CommandResponse response, string unitName)
        {
            var result = NavigateToCharacterStatus(response, unitName);
            if (result.Status != "failed")
            {
                var screen = _detectScreen();
                if (screen != null) result.Screen = screen;
            }
            return result;
        }

        /// <summary>
        /// Internal: navigate to CharacterStatus for a named unit. Handles
        /// escaping from any party-tree or WorldMap screen, opening PartyMenu,
        /// finding the unit in the roster, navigating the grid cursor, and
        /// pressing Enter to open CharacterStatus.
        /// </summary>
        private CommandResponse NavigateToCharacterStatus(CommandResponse response, string unitName)
        {
            // Step 1: Find the target unit in the roster first (fail-fast
            // before any navigation).
            var rosterReader = new RosterReader(_explorer, _nameTable);
            var allSlots = rosterReader.ReadAll();
            RosterReader.RosterSlot? targetSlot = null;
            foreach (var slot in allSlots)
            {
                if (slot.Name != null && slot.Name.Equals(unitName, StringComparison.OrdinalIgnoreCase))
                {
                    targetSlot = slot;
                    break;
                }
            }
            if (targetSlot == null)
            {
                response.Status = "failed";
                response.Error = $"Unit '{unitName}' not found in roster";
                return response;
            }

            int displayOrder = targetSlot.DisplayOrder;
            int rosterCount = allSlots.Count;
            int gridRows = (rosterCount + 4) / 5;

            // Step 2: Build the plan via NavigationPlanner. Single source of
            // truth for the key sequence — shared with `dry_run_nav`
            // (bridge action) and NavigationPlannerTests. Any future tweak
            // to the sequence lands in the planner and both paths get it.
            var currentScreen = _detectScreen();
            string currentName = currentScreen?.Name ?? "Unknown";
            var plan = NavigationPlanner.PlanNavigateToCharacterStatus(
                currentName, displayOrder, rosterCount);
            if (!plan.Ok)
            {
                response.Status = "failed";
                response.Error = plan.Error;
                return response;
            }

            // Step 3: Sync SM roster/grid to actual counts BEFORE firing
            // nav keys, so the SM's wrap math matches the game's.
            if (ScreenMachine != null)
            {
                ScreenMachine.RosterCount = rosterCount;
                ScreenMachine.GridRows = gridRows;
            }

            // Step 4: Execute the plan. Honor EarlyExitOnScreen hints for
            // the escape-storm group — after each step's settle, poll
            // DetectScreen with 2-consecutive-reads-agree confirmation. If
            // both reads return the hinted screen, skip the rest of that
            // group. 2-read confirm defends against mid-animation stale
            // detection: between the Escape fire and full UI transition,
            // detection may briefly return the intermediate screen. The
            // original (pre-planner) code had this race too, but was papered
            // over by the fact that the loop ran `DetectScreen` immediately
            // after Thread.Sleep(300) — if it got a stale read, next
            // iteration would see WorldMap anyway. With the planner-based
            // execution we're more sensitive to stale reads because the
            // escape-storm group has fixed upper bound; if we miss the
            // transition signal, all 8 escapes fire.
            string? skipGroupId = null;
            foreach (var step in plan.Steps)
            {
                if (skipGroupId != null && step.GroupId == skipGroupId) continue;
                if (skipGroupId != null && step.GroupId != skipGroupId)
                    skipGroupId = null;  // exited the skip group

                SendKey(step.VkCode);
                Thread.Sleep(step.SettleMs);

                if (step.EarlyExitOnScreen != null)
                {
                    // 2-read confirm: first read may be stale during animation;
                    // small extra delay + second read verifies the transition
                    // stabilized before committing to the early-exit decision.
                    var check1 = _detectScreen();
                    if (check1?.Name == step.EarlyExitOnScreen)
                    {
                        Thread.Sleep(100);
                        var check2 = _detectScreen();
                        if (check2?.Name == step.EarlyExitOnScreen)
                            skipGroupId = step.GroupId;
                    }
                }
            }

            response.Status = "completed";
            return response;
        }

        /// <summary>
        /// auto_place_units: Accept default unit placement on BattleFormation
        /// and start battle. Places only Ramza (Enter×2 — select tile + confirm)
        /// then commences (Space + Enter). Leaves the other 3 slots unplaced
        /// so solo-Ramza playtests work without having to pre-empty the party.
        /// Polls until a battle state appears.
        /// </summary>
        private CommandResponse AutoPlaceUnits(CommandResponse response)
        {
            // S59: poll for BattleFormation entry instead of a fixed 4s sleep.
            // Story battles (Dorter) have longer formation animations that
            // raced the old Enter sequence and caused crashes on 1st/2nd try
            // (session 45). Poll up to 12s — settled formations take 3-6s
            // typically, story battles up to 10s. Fall through after the
            // timeout so short-path random encounters still work if detection
            // transiently reports TravelList (documented transition lie).
            var formationSw = Stopwatch.StartNew();
            bool formationReady = false;
            while (formationSw.ElapsedMilliseconds < 12000)
            {
                var s = _detectScreen();
                if (s != null && s.Name == "BattleFormation")
                {
                    formationReady = true;
                    // Extra 500ms breathing room after detection flips so the
                    // Enter presses don't race the last animation frame.
                    Thread.Sleep(500);
                    break;
                }
                Thread.Sleep(500);
            }
            if (!formationReady)
            {
                ModLogger.Log($"[AutoPlaceUnits] BattleFormation not detected after 12s (screen={_detectScreen()?.Name ?? "null"}); proceeding with key sequence as fallback");
                // Safety-net sleep: the old 4s was observed to work on the
                // 3rd try for Dorter. Keep it as the fallback budget when
                // detection is lying about the transition.
                Thread.Sleep(4000);
            }

            // Place only Ramza: Enter (select tile) + Enter (confirm).
            // The remaining 3 slots stay unplaced so solo-Ramza playtests work.
            SendKey(VK_ENTER);
            Thread.Sleep(200);
            SendKey(VK_ENTER);
            Thread.Sleep(400);

            // Commence battle: Space (open dialog) + Enter (confirm Yes)
            SendKey(0x20); // VK_SPACE
            Thread.Sleep(500);
            SendKey(VK_ENTER);
            Thread.Sleep(1000);

            // Poll for battle state (intro animations can take several seconds)
            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(1000);
                var screen = _detectScreen();
                if (screen != null && AutoPlaceUnitsEndState.IsBattleStartedState(screen.Name))
                {
                    response.Screen = screen;
                    response.Status = "completed";
                    response.Info = $"Battle started ({screen.Name})";
                    return response;
                }
            }

            response.Status = "completed";
            response.Info = "Formation sequence sent, waiting for battle to start";
            var finalScreen = _detectScreen();
            if (finalScreen != null) response.Screen = finalScreen;
            return response;
        }

        // --- Helpers ---

        private void SendKey(int vk)
        {
            // Guard: refuse to send keys during BattleEnemiesTurn /
            // BattleAlliesTurn. Any key during enemy turn opens the pause
            // menu (live-observed playtest #3 2026-04-25: a stray Escape
            // mid-enemy-turn paused the game and the next battle_move
            // failed "Not in Move mode" because the state was BattlePaused).
            // Higher-level helpers should pre-flight-fail on these states
            // rather than reach here, but this is defense-in-depth for the
            // post-attack/post-cast cleanup paths that may race a state
            // transition.
            var pre = _detectScreen();
            if (pre != null
                && (pre.Name == "BattleEnemiesTurn" || pre.Name == "BattleAlliesTurn"))
            {
                ModLogger.Log($"[SendKey] BLOCKED vk=0x{vk:X2} — screen={pre.Name} (would open pause menu). Caller should wait for BattleMyTurn.");
                return;
            }
            _input.SendKeyPressToWindow(_gameWindow, vk);
            // Keep the state machine in sync so compound nav helpers don't
            // leave it stuck at PartyMenu while the game advances deep into
            // CharacterStatus / EqA / pickers. Matches how CommandWatcher's
            // key-by-key path calls OnKeyPressed after each press.
            ScreenMachine?.OnKeyPressed(vk);
            // Per-key classification: cursor nav keys (Up/Down/Left/Right)
            // sleep 200ms, everything else (Enter/Escape/Space/Tab/letters
            // used for tab cycling) sleeps 350ms. Shaves ~1-2s off flows
            // that fire many cursor keys in a row (e.g. party grid nav).
            Thread.Sleep(KeyDelayClassifier.DelayMsFor(vk));
        }

        /// <summary>
        /// Read per-unit Move/Jump from the UE4 heap unit struct. Locates the
        /// struct by searching the heap range for the unit's HP+MaxHP u16 pair
        /// (HP at struct+0x10, MaxHP at +0x12) and reads Move at +0x22, Jump at
        /// +0x23. Returns null if no match found or the bytes are zero (stale
        /// slot).
        ///
        /// Verified session 29 on MAP074:
        ///   Kenrick (Knight) HP=586 MaxHP=586 → Move=3, Jump=3 ✓
        ///   Lloyd (Dragoon)  HP=549 MaxHP=628 → Move=5, Jump=4 ✓
        ///   Archer          HP=453 MaxHP=453 → Move=3, Jump=3 ✓ (canonical)
        /// </summary>
        private (int move, int jump)? TryReadMoveJumpFromHeap(int hp, int maxHp)
        {
            if (hp <= 0 || maxHp <= 0 || _explorer == null) return null;

            // The heap struct stores HP and MaxHP as two u16s, BOTH holding
            // the MaxHP value (the "base" HP for the unit). Current/live HP
            // lives in the static battle array, not here. S56 live-observed:
            // Lloyd at HP=370/628 had NO matches for the old "current HP +
            // MaxHP" pattern `72 01 74 02` anywhere in memory — because the
            // heap struct has `74 02 74 02` (628, 628) regardless of damage.
            //
            // Search for `MaxHP, MaxHP` pair instead. Stable across damage
            // taken, works for every unit including damaged ones.
            var maxHpPattern = new byte[]
            {
                (byte)(maxHp & 0xFF), (byte)(maxHp >> 8),
                (byte)(maxHp & 0xFF), (byte)(maxHp >> 8),
            };

            // Narrow search first: RW private/mapped heap only, fast.
            var result = TryMoveJumpMatches(maxHpPattern, hp, maxHp, broad: false);
            if (result.HasValue)
            {
                MoveJumpCache.Put(maxHp, result.Value.move, result.Value.jump);
                return result;
            }

            // Broad fallback: includes IMAGE-mapped and WRITECOPY memory up
            // to 16MB per region, 2GB total budget.
            ModLogger.Log($"[TryReadMoveJumpFromHeap] HP={hp}/{maxHp}: narrow miss, retrying broad");
            result = TryMoveJumpMatches(maxHpPattern, hp, maxHp, broad: true);
            if (result.HasValue)
            {
                MoveJumpCache.Put(maxHp, result.Value.move, result.Value.jump);
                return result;
            }

            // S58: final fallback — check the per-battle cache. If we
            // successfully read Move/Jump for this unit earlier this
            // battle, reuse that instead of collapsing to Mv=0. Prevents
            // the whole-battle "0 valid tiles" failure mode when the heap
            // struct relocates to an address outside our search range
            // mid-battle.
            var cached = MoveJumpCache.Get(maxHp);
            if (cached.HasValue)
            {
                ModLogger.Log($"[TryReadMoveJumpFromHeap] HP={hp}/{maxHp}: heap miss, using cached Mv={cached.Value.move} Jp={cached.Value.jump}");
                return cached.Value;
            }
            // 2026-04-26 Mandalia: Ramza levelled up mid-battle, MaxHp
            // shifted 391→393. Heap search misses for the new MaxHp
            // (struct may have relocated, or pattern no longer matches),
            // and the keyed cache holds 391 — unreachable. Most-recent
            // entry is Ramza's pre-level-up Mv/Jp, which is correct
            // (Mv/Jp don't change at level-up). Soft-locks the player
            // otherwise.
            var recent = MoveJumpCache.GetMostRecent();
            if (recent.HasValue)
            {
                ModLogger.Log($"[TryReadMoveJumpFromHeap] HP={hp}/{maxHp}: keyed cache miss (level-up shift?), using most-recent Mv={recent.Value.move} Jp={recent.Value.jump}");
                return recent.Value;
            }
            return null;
        }

        private (int move, int jump)? TryMoveJumpMatches(
            byte[] hpPattern, int hp, int maxHp, bool broad)
        {
            if (_explorer == null) return null;
            var matches = _explorer.SearchBytesInAllMemory(
                hpPattern, maxResults: 8,
                minAddr: 0x4000000000L, maxAddr: 0x4200000000L,
                broadSearch: broad);
            ModLogger.Log($"[TryReadMoveJumpFromHeap] HP={hp}/{maxHp} broad={broad}: {matches.Count} heap matches");
            int accepted = 0;
            (int move, int jump)? first = null;
            foreach (var m in matches)
            {
                long baseAddr = (long)m.address - 0x10;
                var bytes = _explorer.Scanner.ReadBytes((nint)(baseAddr + 0x22), 2);
                if (bytes.Length != 2) continue;
                int mv = bytes[0];
                int jp = bytes[1];
                // Sanity check: valid Move is 1-10, Jump is 1-8. Rejects zero
                // slots and mis-matched structs.
                bool valid = mv >= 1 && mv <= 10 && jp >= 1 && jp <= 8;
                ModLogger.Log($"[TryReadMoveJumpFromHeap]   struct 0x{baseAddr:X} mv={mv} jp={jp} valid={valid}");
                if (!valid) continue;
                accepted++;
                if (first == null) first = (mv, jp);
            }
            if (accepted > 1)
                ModLogger.Log($"[TryReadMoveJumpFromHeap] WARN: multiple structs passed sanity check ({accepted}) — first-match wins, may be wrong unit");
            return first;
        }

        /// <summary>
        /// <summary>
        /// Read a lightweight post-action snapshot from the condensed struct.
        /// Much faster than a full scan — just reads position + HP/MP from
        /// known static addresses. Returns null if any read fails.
        ///
        /// Position comes from the live grid-cursor address. For battle_move
        /// this is correct (cursor returns to the active unit at its new
        /// tile). For battle_attack / battle_ability this is the TARGET
        /// tile, NOT the caster — those callers should use the explicit-pos
        /// overload below to avoid mixing target coords with caster HP in
        /// the response payload.
        /// </summary>
        public PostActionState? ReadPostActionState()
        {
            var pos = ReadGridPos();
            if (pos.x < 0 || pos.y < 0) return null;
            return ReadPostActionState(pos.x, pos.y);
        }

        /// <summary>
        /// Variant that takes explicit X/Y. Use this for battle_attack /
        /// battle_ability where the cursor sits on the target tile after
        /// the action — the active unit (caster) didn't move, so pass the
        /// caster's start position.
        /// </summary>
        public PostActionState? ReadPostActionState(int posX, int posY)
        {
            try
            {
                if (posX < 0 || posY < 0) return null;
                var reads = _explorer.ReadMultiple(new[]
                {
                    ((nint)(AddrCondensedBase + 0x0C), 2), // HP
                    ((nint)(AddrCondensedBase + 0x10), 2), // MaxHP
                    ((nint)(AddrCondensedBase + 0x12), 2), // MP
                    ((nint)(AddrCondensedBase + 0x16), 2), // MaxMP
                });
                int condHp = (int)reads[0], condMaxHp = (int)reads[1];
                // CondensedBase reflects the CURSOR-hovered unit, not the
                // caster. For battle_ability/attack callers passing explicit
                // caster X/Y, the cursor often sits on the TARGET tile —
                // returning condensed HP would surface the target's HP
                // labelled with the caster's tile (e.g. Wilham's HP=528 for
                // Ramza's Shout). Cross-check with the static array slot
                // at the caster's position; prefer static when it
                // disagrees beyond half-MaxHp (same dual-read pattern as
                // ReadLiveHp X-Potion fix). Live-flagged 2026-04-25.
                int staticHp = ReadStaticArrayHpAt(posX, posY);
                int staticMaxHp = ReadStaticArrayMaxHpAt(posX, posY);
                int hp = condHp;
                int maxHp = condMaxHp;
                if (staticHp >= 0 && staticMaxHp > 0)
                {
                    bool maxHpDiffers = staticMaxHp != condMaxHp;
                    bool hpDiffersBeyondHalf = condMaxHp > 0
                        && System.Math.Abs(condHp - staticHp) > condMaxHp / 2;
                    if (maxHpDiffers || hpDiffersBeyondHalf)
                    {
                        hp = staticHp;
                        maxHp = staticMaxHp;
                    }
                }
                return new PostActionState
                {
                    X = posX,
                    Y = posY,
                    Hp = hp,
                    MaxHp = maxHp,
                    Mp = (int)reads[2],
                    MaxMp = (int)reads[3],
                };
            }
            catch
            {
                return null;
            }
        }

        /// Navigate to Move (index 0) in the action menu without trusting memory cursor.
        /// Menu has 5 items (Move/Abilities/Wait/Status/AutoBattle) and wraps.
        /// Press Down 4x to reach bottom (index 4) from anywhere, then Up 4x to reach Move (0).
        /// </summary>
        private void NavigateToMove()
        {
            // Go to bottom first
            for (int i = 0; i < 4; i++)
            {
                SendKey(VK_DOWN);
                Thread.Sleep(100);
            }
            // Then go to top (Move = index 0)
            for (int i = 0; i < 4; i++)
            {
                SendKey(VK_UP);
                Thread.Sleep(100);
            }
        }

        private void NavigateMenuCursor(int current, int target)
        {
            int delta = target - current;
            int vk = delta > 0 ? VK_DOWN : VK_UP;
            for (int i = 0; i < Math.Abs(delta); i++)
            {
                SendKey(vk);
                // 150ms → 80ms per press. SendKey already does a 25ms
                // key-down hold, so the menu sees ~105ms per press.
                // 5-item menu × 80ms = 400ms vs old 750ms on worst case.
                Thread.Sleep(80);
            }
            // Verify cursor arrived. 100ms → 50ms settle — the cursor
            // counter updates immediately after the last KEYUP posts.
            Thread.Sleep(50);
            var check = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
            int actual = check != null ? (int)check.Value.value : -1;
            if (actual != target)
                ModLogger.Log($"[NavigateMenu] WARN: cursor at {actual}, expected {target}");
        }

        private bool WaitForScreen(string screenName, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(5);
                var screen = _detectScreen();
                if (screen != null && string.Equals(screen.Name, screenName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Retry-read of detection specifically looking for one of the "player
        /// is in control" battle states (BattleMyTurn or BattleActing). Returns
        /// as soon as detection reports one. Returns the last non-null read on
        /// timeout. Outputs <paramref name="waitedMs"/> so callers can include
        /// it in failure diagnostics.
        ///
        /// Rationale (S56): detection can flicker to BattleEnemiesTurn,
        /// BattleAttacking, or other transient states for a few hundred ms
        /// during move-animation tails. In execute_turn bundled flows this
        /// racetracks the next sub-step's state gate. Single retries make it
        /// robust without hiding genuine screen transitions.
        /// </summary>
        private DetectedScreen? WaitForTurnState(int timeoutMs, out long waitedMs)
        {
            var sw = Stopwatch.StartNew();
            DetectedScreen? last = null;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                last = _detectScreen();
                if (last != null && (last.Name == "BattleMyTurn" || last.Name == "BattleActing"))
                {
                    waitedMs = sw.ElapsedMilliseconds;
                    return last;
                }
                Thread.Sleep(40);
            }
            waitedMs = sw.ElapsedMilliseconds;
            return last;
        }

        /// <summary>
        /// Post-action settle: wait for the state to resolve to ANY "action
        /// complete" screen — our turn is back, another unit's turn started,
        /// or the battle ended. Used by battle_ability / battle_attack after
        /// the cast/swing to avoid paying a 2000ms timeout when the next
        /// state happens to be Victory/Defeat/EnemyTurn instead of MyTurn.
        /// </summary>
        private DetectedScreen? WaitForActionResolved(int timeoutMs, out long waitedMs)
        {
            var sw = Stopwatch.StartNew();
            DetectedScreen? last = null;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                last = _detectScreen();
                if (last != null && IsPostActionResolved(last.Name))
                {
                    waitedMs = sw.ElapsedMilliseconds;
                    return last;
                }
                Thread.Sleep(40);
            }
            waitedMs = sw.ElapsedMilliseconds;
            return last;
        }

        private static bool IsPostActionResolved(string? name) =>
            name is "BattleMyTurn" or "BattleActing"
                 or "BattleVictory" or "BattleDesertion" or "GameOver"
                 or "BattleAlliesTurn" or "BattleEnemiesTurn";

        private CommandResponse AdvanceDialogue(CommandResponse response)
        {
            // Send Enter to advance cutscene text
            SendKey(VK_ENTER);
            Thread.Sleep(300);
            var screen = _detectScreen();
            response.Status = "completed";
            response.Screen = screen;
            return response;
        }

        private CommandResponse SaveGame(CommandResponse response)
        {
            // Open the in-game Save slot picker:
            //   (any non-battle state) → WorldMap → PartyMenu(Units)
            //   → Q to Options tab → Up×5 resets OptionsIndex to Save
            //   → Enter → SaveSlotPicker (user then picks slot manually)
            //
            // SaveSlotPicker state tracking was added session 25. The actual
            // slot selection + overwrite confirmation is intentionally left
            // to the caller — Claude decides which slot and whether to
            // overwrite. This action just parks the game on the picker.
            //
            // State guard: refuses from any Battle*/Encounter/Cutscene state
            // because the escape storm would mis-handle those transitions.

            var current = _detectScreen();
            if (current == null)
            {
                response.Status = "failed";
                response.Error = "Could not detect current screen";
                return response;
            }

            string currentName = current.Name;
            if (ScreenNamePredicates.IsBattleState(currentName)
                || currentName == "EncounterDialog"
                || currentName == "Cutscene"
                || currentName == "BattleSequence"
                || currentName == "GameOver")
            {
                response.Status = "failed";
                response.Error = $"Cannot save from {currentName}. Resolve the battle/encounter/cutscene first.";
                return response;
            }

            // Step 1: get to a known entry point. Two valid entry states:
            //   (a) WorldMap — Escape-on-WorldMap opens PartyMenuUnits cleanly.
            //   (b) PartyMenu* (any tab) — already open; can Q directly.
            // Anything else: Escape repeatedly with 2-consecutive-read confirm
            // (session 24 gotcha: Escape-on-WorldMap opens PartyMenu, so blind
            // escape loops toggle — need to stop the INSTANT we see WorldMap
            // with confidence).
            bool startedOnPartyMenu = currentName.StartsWith("PartyMenu");
            if (!startedOnPartyMenu && currentName != "WorldMap")
            {
                int consecutiveWorldMap = 0;
                for (int i = 0; i < 10; i++)
                {
                    var check = _detectScreen();
                    if (check?.Name == "WorldMap")
                    {
                        consecutiveWorldMap++;
                        if (consecutiveWorldMap >= 2) break;
                        Thread.Sleep(150);
                        continue;
                    }
                    consecutiveWorldMap = 0;
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                }
                var afterEscape = _detectScreen();
                if (afterEscape?.Name != "WorldMap")
                {
                    response.Status = "failed";
                    response.Error = $"Could not reach WorldMap from {currentName} (landed on {afterEscape?.Name ?? "?"})";
                    return response;
                }
                // Fall-through to the WorldMap path below.
            }

            // Step 2: land on PartyMenu. From WorldMap one Escape opens the
            // menu. If we're already on a PartyMenu tab, skip this step.
            if (!startedOnPartyMenu)
            {
                SendKey(VK_ESCAPE);
                Thread.Sleep(800);
            }

            // Step 3: ensure we're on the Options tab. Q cycles tabs
            // left: Units → Options (wraps), Inventory → Units → Options
            // (two Qs), Chronicle → Inventory (one Q), Options → Chronicle
            // (one Q — wrong way). Safest: check the SM tab and emit the
            // right count. When SM doesn't know (just entered), assume
            // Units and send one Q.
            int qCount = 1; // default: Units → Options via wrap
            if (ScreenMachine != null)
            {
                qCount = ScreenMachine.Tab switch
                {
                    PartyTab.Units => 1,       // Units → Options (wrap left)
                    PartyTab.Inventory => 2,   // Inventory → Units → Options
                    PartyTab.Chronicle => 3,   // Chronicle → Inventory → Units → Options
                    PartyTab.Options => 0,     // already there
                    _ => 1
                };
            }
            for (int i = 0; i < qCount; i++)
            {
                SendKey(VK_Q);
                Thread.Sleep(300);
            }

            // Step 4: reset OptionsIndex to 0 (Save). Up×5 guarantees top
            // regardless of where the cursor was last parked.
            for (int i = 0; i < 5; i++)
            {
                SendKey(VK_UP);
                Thread.Sleep(100);
            }

            // Step 5: Enter → SaveSlotPicker.
            SendKey(VK_ENTER);
            Thread.Sleep(500);

            var final = _detectScreen();
            response.Screen = final;
            response.Status = "completed";
            response.Info = final?.Name == "SaveSlotPicker"
                ? "Save slot picker opened. Navigate with up/down, Enter to save to the highlighted slot."
                : $"Opened save flow; final screen={final?.Name ?? "?"}";
            return response;
        }

        private CommandResponse LoadGame(CommandResponse response)
        {
            response.Status = "failed";
            response.Error = "Not implemented yet";
            return response;
        }

        private CommandResponse BattleRetry(CommandResponse response, bool formation)
        {
            var screen = _detectScreen();
            if (screen == null)
            {
                response.Status = "failed";
                response.Error = "Could not detect screen";
                return response;
            }

            // Path 1: GameOver screen — menu has Retry(0), Load(1), Return to World Map(2), Return to Title(3).
            // Retry is the default (top) option. Press Up×3 to guarantee top, then Enter.
            if (screen.Name == "GameOver")
            {
                for (int i = 0; i < 3; i++)
                {
                    SendKey(VK_UP);
                    Thread.Sleep(100);
                }
                SendKey(VK_ENTER);
                Thread.Sleep(1000);

                if (formation)
                {
                    // "Retry with formation" — press Down once to select the formation option
                    // (if the game offers it after selecting Retry). Some FFT versions
                    // show a "Retry" vs "Retry (Formation)" sub-choice.
                    SendKey(VK_DOWN);
                    Thread.Sleep(100);
                    SendKey(VK_ENTER);
                    Thread.Sleep(1000);
                }

                response.Status = "completed";
                response.Info = "Retry from GameOver";
                return response;
            }

            // Path 2: Mid-battle retry via pause menu (Tab → Retry).
            // Pause menu: Resume(0), Retry(1), Quit(2) — or similar layout.
            if (ScreenNamePredicates.IsBattleState(screen.Name))
            {
                // If not already paused, open the pause menu
                if (screen.Name != "BattlePaused")
                {
                    SendKey(VK_TAB);
                    Thread.Sleep(500);
                    screen = _detectScreen();
                    if (screen?.Name != "BattlePaused")
                    {
                        response.Status = "failed";
                        response.Error = $"Failed to open pause menu (current: {screen?.Name ?? "null"})";
                        return response;
                    }
                }

                // Navigate to Retry — press Down once from Resume(0) to Retry(1)
                SendKey(VK_DOWN);
                Thread.Sleep(150);
                SendKey(VK_ENTER);
                Thread.Sleep(500);

                // Confirm retry (game may show "Are you sure?" dialog)
                SendKey(VK_ENTER);
                Thread.Sleep(1000);

                if (formation)
                {
                    SendKey(VK_DOWN);
                    Thread.Sleep(100);
                    SendKey(VK_ENTER);
                    Thread.Sleep(1000);
                }

                response.Status = "completed";
                response.Info = "Retry from pause menu";
                return response;
            }

            response.Status = "failed";
            response.Error = $"Cannot retry from {screen.Name} — need GameOver or Battle screen";
            return response;
        }

        private CommandResponse Buy(CommandResponse response, CommandRequest command)
        {
            response.Status = "failed";
            response.Error = "Not implemented yet";
            return response;
        }

        private CommandResponse Sell(CommandResponse response, CommandRequest command)
        {
            response.Status = "failed";
            response.Error = "Not implemented yet";
            return response;
        }

        private CommandResponse ChangeJob(CommandResponse response, CommandRequest command)
        {
            response.Status = "failed";
            response.Error = "Not implemented yet";
            return response;
        }
    }
}
