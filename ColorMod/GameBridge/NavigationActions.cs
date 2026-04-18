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
        public MapLoader? _mapLoader;
        public Func<string[]>? GetAbilitiesSubmenuItems { get; set; }
        public Func<string, string[]>? GetAbilityListForSkillset { get; set; }
        private IntPtr _gameWindow;

        // --- DirectInput hook for faking held C key ---
        private static volatile bool _injectCKey = false;
        private static bool _diHookInstalled = false;

        // After battle_move or battle_ability, the game auto-advances cursor to
        // Abilities (index 1) but memory at 0x1407FC620 still reads 0.
        // This flag triggers cursor correction in battle_wait and battle_ability.
        private bool _menuCursorStale = false;

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
        /// convention. Accepts N/S/E/W (case-insensitive) and full names. Returns
        /// null when input is null/empty/unrecognized (caller falls back to auto-pick).
        /// </summary>
        public static (int dx, int dy)? ParseFacingDirection(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var s = input.Trim().ToUpperInvariant();
            return s switch
            {
                "N" or "NORTH" => (0, 1),
                "S" or "SOUTH" => (0, -1),
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

            var screen = _detectScreen();

            // Turn-ending abilities (Jump) end the turn immediately — no Wait needed.
            // If we're already on another unit's turn, return success.
            if (screen != null && (screen.Name == "BattleEnemiesTurn"
                || screen.Name == "BattleAlliesTurn"))
            {
                response.Status = "completed";
                response.Info = "Turn already ended (ability ended turn automatically)";
                return response;
            }

            if (screen == null || !BattleWaitLogic.CanStartBattleWait(screen.Name))
            {
                _menuCursorStale = false;
                response.Status = "failed";
                response.Error = $"Cannot battle_wait from screen (current: {screen?.Name ?? "null"})";
                return response;
            }

            bool skipMenu = BattleWaitLogic.ShouldSkipMenuNavigation(screen.Name);

            if (skipMenu)
            {
                // After Move+Act, game already transitioned to facing screen.
                // Skip menu navigation entirely — we're already where we need to be.
                ModLogger.Log($"[BattleWait] Auto-facing detected (screen={screen.Name}), skipping menu navigation");
                _menuCursorStale = false;
                Thread.Sleep(300);
            }
            else
            {
                // Normal path: navigate action menu to Wait
                Thread.Sleep(300);

                var cursorResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
                int rawCursor = cursorResult != null ? (int)cursorResult.Value.value : screen.MenuCursor;
                bool hasMoved = screen.BattleMoved == 1 || _menuCursorStale;
                bool hasActed = screen.BattleActed == 1;
                int cursor = BattleAbilityNavigation.EffectiveMenuCursor(rawCursor, moved: hasMoved, acted: hasActed);
                if (cursor != rawCursor)
                    ModLogger.Log($"[BattleWait] Cursor correction: raw={rawCursor} → effective={cursor} (moved={hasMoved}, acted={hasActed})");
                _menuCursorStale = false; // consumed
                int target = 2; // Wait
                ModLogger.Log($"[BattleWait] Cursor at {cursor}, navigating to {target}");
                NavigateMenuCursor(cursor, target);

                Thread.Sleep(150);
                var verifyResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
                int actual = verifyResult != null ? (int)verifyResult.Value.value : -1;
                if (actual != target)
                {
                    ModLogger.Log($"[BattleWait] RETRY: cursor at {actual}, expected {target}. Retrying navigation.");
                    NavigateMenuCursor(actual, target);
                    Thread.Sleep(150);
                }

                // Press Enter to select Wait — enters the facing screen.
                SendKey(VK_ENTER);
                Thread.Sleep(500);
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

            // Confirm facing (game says "press F to confirm")
            SendKey(VK_F);

            // NOTE: Ctrl fast-forward disabled — holding Ctrl during enemy turns
            // doesn't visibly speed up animations in the IC remaster. Tested both
            // continuous hold and pulse approaches (2026-04-12). May need a different
            // key or game setting. The game still processes turns at normal speed.
            Thread.Sleep(500);
            ModLogger.Log("[BattleWait] Waiting for enemy turns (Ctrl fast-forward disabled)");

            // Poll until it's a friendly unit's turn again (or game over/timeout)
            var sw = Stopwatch.StartNew();
            string lastScreen = "";
            try
            {
                while (sw.ElapsedMilliseconds < 120000) // 2 minute max
                {
                    Thread.Sleep(300);

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

                    // Friendly unit's turn — we're done
                    if (current.Name == "BattleMyTurn")
                    {
                        response.Info = $"Friendly turn after {sw.ElapsedMilliseconds}ms";
                        break;
                    }

                    // Game over
                    if (current.Name == "GameOver")
                    {
                        response.Error = "Game Over";
                        break;
                    }
                }

                if (sw.ElapsedMilliseconds >= 120000)
                    response.Error = "Timeout waiting for friendly turn (120s)";
            }
            finally
            {
                ModLogger.Log("[BattleWait] Turn wait complete");
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

            response.Status = "completed";
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

            var screen = _detectScreen();
            if (screen == null || (screen.Name != "BattleMyTurn" && screen.Name != "BattleActing"))
            {
                response.Status = "failed";
                response.Error = $"Not on BattleMyTurn/BattleActing (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Step 1: Navigate menu to Abilities (always index 1).
            // Menu is stable: Move/ResetMove(0) Abilities(1) Wait(2) Status(3) AutoBattle(4)
            // Trust the raw memory cursor — EffectiveMenuCursor corrections cause more bugs.
            var cursorResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
            int cursor = cursorResult != null ? (int)cursorResult.Value.value : screen.MenuCursor;
            NavigateMenuCursor(cursor, 1);
            SendKey(VK_ENTER); // Open Abilities submenu
            Thread.Sleep(500);

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
                Thread.Sleep(300);
            }

            // Now select Attack (top item in submenu)
            SendKey(VK_ENTER);
            Thread.Sleep(500);

            // Verify we're in targeting mode
            screen = _detectScreen();
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
                Thread.Sleep(500);
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
            Thread.Sleep(500);
            SendKey(VK_ENTER);

            // Step 9: Wait for animation, then read live HP from readonly memory.
            // The static array is stale mid-turn, but SearchBytesAllRegions finds
            // live copies in readonly regions (0x141xxx, 0x15Axxx) that update immediately.
            response.Status = "completed";
            Thread.Sleep(2000); // wait for attack animation
            int postHp = ReadLiveHp(targetMaxHp, preHp, targetLevel);
            ModLogger.Log($"[BattleAttack] Post-attack: live HP={postHp} (was {preHp})");

            string dmgStr = projDamage > 0 ? $" ({projDamage} dmg, {projHitPct}% hit)" : "";
            if (postHp >= 0 && postHp != preHp)
            {
                if (postHp <= 0)
                    response.Info = $"Attacked ({targetX},{targetY}) from ({startPos.x},{startPos.y}) — KO'd!{dmgStr} ({preHp}→0/{targetMaxHp})";
                else
                    response.Info = $"Attacked ({targetX},{targetY}) from ({startPos.x},{startPos.y}) — HIT{dmgStr} ({preHp}→{postHp}/{targetMaxHp})";
            }
            else if (postHp == preHp)
            {
                response.Info = $"Attacked ({targetX},{targetY}) from ({startPos.x},{startPos.y}) — MISSED!{dmgStr}";
            }
            else
            {
                response.Info = $"Attacked ({targetX},{targetY}) from ({startPos.x},{startPos.y}){dmgStr}";
            }
            ModLogger.Log($"[BattleAttack] {response.Info}");
            return response;
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

            var screen = _detectScreen();
            if (screen == null || (screen.Name != "BattleMyTurn" && screen.Name != "BattleActing"))
            {
                response.Status = "failed";
                response.Error = $"Not on BattleMyTurn/BattleActing (current: {screen?.Name ?? "null"})";
                return response;
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
            Thread.Sleep(1000); // Wait for submenu to fully load — 500ms was too fast

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

            if (!loc.isSelfTarget && (targetX < 0 || targetY < 0))
            {
                response.Status = "failed";
                response.Error = $"Ability '{abilityName}' requires a target (locationId=x, unitIndex=y)";
                SendKey(VK_ESCAPE);
                return response;
            }

            // Step 3: Verify we're in the Abilities submenu. Step 1 already pressed Enter
            // on Abilities — if screen detection is slow, we might still read BattleMyTurn
            // or BattleActing. Do NOT press Enter again here (that would select Attack
            // from the submenu, which is the bug that caused Throw Stone → Attack targeting).
            screen = _detectScreen();
            if (screen?.Name == "BattleMyTurn" || screen?.Name == "BattleActing")
            {
                // Still on action menu — the Enter from Step 1 didn't register.
                // Wait longer and retry.
                Thread.Sleep(500);
                screen = _detectScreen();
                if (screen?.Name == "BattleMyTurn" || screen?.Name == "BattleActing")
                {
                    // Try Enter one more time
                    var retryRead = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
                    int retryCursor = retryRead != null ? (int)retryRead.Value.value : 1;
                    NavigateMenuCursor(retryCursor, 1);
                    SendKey(VK_ENTER);
                    Thread.Sleep(1000);
                }
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

            // Wait for the Abilities submenu to fully appear before navigating.
            ModLogger.Log($"[BattleAbility] Navigating submenu: skillsetIdx={skillsetIdx} for '{loc.skillsetName}' in [{string.Join(", ", submenuItems)}]");
            for (int wait = 0; wait < 20; wait++)
            {
                var subScreen = _detectScreen();
                if (subScreen?.Name == "BattleAbilities")
                {
                    ModLogger.Log($"[BattleAbility] Submenu detected after {wait * 150}ms");
                    break;
                }
                Thread.Sleep(150);
            }
            Thread.Sleep(500); // extra settle time after submenu appears

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
                Thread.Sleep(400);
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

            // Enter the skillset
            Thread.Sleep(300);
            SendKey(VK_ENTER);
            Thread.Sleep(500);

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

            // Reset ability-list cursor to the top before counting Downs. The ability
            // list REMEMBERS the previously-selected position within a turn (e.g. after
            // Escape from Haste at index 4, re-entering Time Magicks still has cursor
            // at index 4). Pressing Down×abilityIndex from that position lands on the
            // wrong ability (off-by-prev-selection). Up wraps forward past any start
            // position: listSize+1 Ups guarantees we end at index 0. Cheap (~0.5s) and
            // deterministic. See feedback_submenu_sticky_cursor.md.
            int listSize = learnedAbilities?.Length ?? 16; // Fallback: no skillset >16
            int resetUps = listSize + 1;
            ModLogger.Log($"[BattleAbility] Reset: Up×{resetUps} to force cursor to index 0");
            // Blind fire-and-forget. Historical note: session 31 tried counter-delta
            // verification here to mirror the Down-loop pattern, but the cursor
            // counter at 0x140C0EB20 reports NEGATIVE deltas on Up-wrap (observed
            // live on Lloyd's 12-entry Jump list). Retrying against the wrong-sign
            // delta produces an explosive retry storm. Wrap-reset is guaranteed
            // correct after listSize+1 blind Ups — no verification needed.
            for (int i = 0; i < resetUps; i++)
            {
                SendKey(VK_UP);
                Thread.Sleep(100);
            }
            Thread.Sleep(200);

            // Navigate to the target ability. The cursor is now at index 0.
            // Use counter-delta to verify each keypress registered.
            ModLogger.Log($"[BattleAbility] Nav: Down×{abilityIndex} (listSize={learnedAbilities?.Length ?? -1})");
            var abilityCounterBefore = _explorer.ReadAbsolute((nint)0x140C0EB20, 2);
            int abilityBaseline = abilityCounterBefore != null ? (int)abilityCounterBefore.Value.value : -1;

            int pressesConfirmed = 0;
            for (int i = 0; i < abilityIndex; i++)
            {
                SendKey(VK_DOWN);
                Thread.Sleep(150);
                pressesConfirmed++;

                // Verify every 3 presses or on the last press
                if (abilityBaseline >= 0 && (pressesConfirmed % 3 == 0 || i == abilityIndex - 1))
                {
                    var counterCheck = _explorer.ReadAbsolute((nint)0x140C0EB20, 2);
                    int counterNow = counterCheck != null ? (int)counterCheck.Value.value : -1;
                    int delta = counterNow - abilityBaseline;
                    if (delta != pressesConfirmed && counterNow >= 0)
                    {
                        int missed = pressesConfirmed - delta;
                        ModLogger.Log($"[BattleAbility] Counter mismatch: expected {pressesConfirmed}, got delta {delta}. Retrying {missed} presses.");
                        for (int r = 0; r < missed; r++)
                        {
                            SendKey(VK_DOWN);
                            Thread.Sleep(200);
                        }
                        // Re-read baseline for next verification batch
                        abilityBaseline = counterNow - delta + pressesConfirmed + missed;
                    }
                }
            }

            // Step 5: Select the ability
            SendKey(VK_ENTER);
            Thread.Sleep(500);

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
                Thread.Sleep(300);
                response.Status = "completed";
                response.Info = $"{verb} {abilityName} (self-target){ctSuffix}{autoEndSuffix}";
                return response;
            }

            if (loc.isSelfTarget)
            {
                // Self-radius abilities (Chakra, Cyclone, Purification): game shows AoE
                // preview centered on caster. Need to wait for the preview to appear,
                // then confirm twice (select target + confirm cast).
                Thread.Sleep(500); // wait for AoE preview to render
                SendKey(VK_ENTER);
                Thread.Sleep(500);
                SendKey(VK_ENTER); // confirm cast
                Thread.Sleep(300);
                response.Status = "completed";
                response.Info = $"{verb} {abilityName} (self-radius AoE){ctSuffix}{autoEndSuffix}";
                return response;
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

            // Navigate cursor to target tile (reuse BattleAttack's targeting logic)
            var startPos = ReadGridPos();
            ModLogger.Log($"[BattleAbility] Targeting {abilityName}, cursor at ({startPos.x},{startPos.y}), target ({targetX},{targetY})");

            int deltaX = targetX - startPos.x;
            int deltaY = targetY - startPos.y;

            if (deltaX == 0 && deltaY == 0)
            {
                SendKey(VK_ENTER);
                Thread.Sleep(500);
                SendKey(VK_ENTER); // confirm target
                Thread.Sleep(500);
                SendKey(VK_ENTER); // Unit/Tile dialog (selects "Unit" default; harmless if no dialog)
                Thread.Sleep(300);
                response.Status = "completed";
                response.Info = $"{verb} {abilityName} on ({targetX},{targetY}) — cursor was already on target{ctSuffix}{autoEndSuffix}";
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
                response.Error = $"Cursor miss: at ({finalPos.x},{finalPos.y}) expected ({targetX},{targetY})";
                SendKey(VK_ESCAPE);
                Thread.Sleep(300);
                response.Status = "failed";
                return response;
            }

            // Confirm target + Unit/Tile dialog
            SendKey(VK_ENTER);
            Thread.Sleep(500);
            SendKey(VK_ENTER); // confirm target
            Thread.Sleep(500);
            SendKey(VK_ENTER); // Unit/Tile dialog (selects "Unit" default; harmless if no dialog)
            Thread.Sleep(300);

            response.Status = "completed";
            response.Info = $"{verb} {abilityName} on ({targetX},{targetY}){ctSuffix}{autoEndSuffix}";
            return response;
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

            // 2. Get move/jump - use overrides if provided, otherwise from scan
            int moveStat = command.LocationId > 0 ? command.LocationId : ally.Move;
            int jumpStat = command.UnitIndex > 0 ? command.UnitIndex : ally.Jump;

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
            foreach (var posUnit in units)
            {
                if (posUnit.Hp <= 0 && posUnit.MaxHp > 0)
                    deadByPos[(posUnit.GridX, posUnit.GridY)] = posUnit;
                else
                    aliveByPos[(posUnit.GridX, posUnit.GridY)] = posUnit;
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

                    abilities = FilterAbilitiesBySkillsets(u).Select(a =>
                    {
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
                        // Uses aliveByPos for normal abilities, deadByPos for revival.
                        var tileIndex = AbilityTargetCalculator.IsRevivalAbility(a)
                            ? deadByPos : aliveByPos;

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
                    Statuses = StatusDecoder.Decode(u.StatusBytes) is var s && s.Count > 0 ? s : null,
                    Abilities = abilities,
                    Reaction = u.ReactionAbility,
                    Support = u.SupportAbility,
                    Movement = u.MovementAbility,
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

                // Try 1: Random encounter map lookup (highest priority — known correct)
                if (locId >= 0 && locId <= 42)
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
                var enemySet = new HashSet<(int, int)>();
                var allySet = new HashSet<(int, int)>();
                foreach (var u in units)
                {
                    if (u == ally) continue;
                    if (u.Hp <= 0) continue;
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
                    string occupant = occupantUnit == null ? "empty"
                        : occupantUnit.Team == 0 ? "ally" : "enemy";
                    var tile = new AttackTileInfo { X = tx, Y = ty, Arrow = arrowName, Occupant = occupant };
                    if (occupantUnit != null)
                    {
                        tile.Hp = occupantUnit.Hp;
                        tile.MaxHp = occupantUnit.MaxHp;
                        tile.JobName = occupantUnit.JobNameOverride
                            ?? (occupantUnit.Team == 0 ? GameStateReporter.GetJobName(occupantUnit.Job) : null);
                        if (occupant == "enemy" && !string.IsNullOrEmpty(occupantUnit.Facing))
                        {
                            tile.Arc = BackstabArcCalculator.ComputeArc(
                                ally.GridX, ally.GridY, occupantUnit.GridX, occupantUnit.GridY, occupantUnit.Facing);
                        }
                    }
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
                var facingResult = FacingStrategy.ComputeOptimalFacingDetailed(allyPos, livingEnemies);
                string faceName = (facingResult.Dx, facingResult.Dy) switch
                {
                    (1, 0) => "East", (-1, 0) => "West",
                    (0, 1) => "North", (0, -1) => "South",
                    _ => $"({facingResult.Dx},{facingResult.Dy})"
                };
                validPaths["RecommendedFacing"] = new PathEntry
                {
                    Desc = $"Face {faceName} — {facingResult.Front} front, {facingResult.Side} side, {facingResult.Back} back",
                    Facing = new FacingInfo
                    {
                        Dx = facingResult.Dx,
                        Dy = facingResult.Dy,
                        Direction = faceName,
                        Front = facingResult.Front,
                        Side = facingResult.Side,
                        Back = facingResult.Back
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
                Thread.Sleep(500);
                screen = _detectScreen();
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
                // Already at target — confirm
                _input.SendKeyPressToWindow(_gameWindow, VK_F);
                Thread.Sleep(500);
                response.Status = "completed";
                response.Error = $"Already at ({targetX},{targetY})";
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
                response.Status = "failed";
                response.Error = $"Could not detect rotation from ({startPos.x},{startPos.y})";
                SendKey(VK_ESCAPE); // Exit move mode
                Thread.Sleep(300);
                return response;
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
            Thread.Sleep(500);

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
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 8000)
            {
                var check = _detectScreen();
                polls++;
                if (check?.Name != null) lastScreenSeen = check.Name;
                if (check != null && check.Name != "BattleMoving"
                    && check.Name != "BattleFormation"
                    && check.Name != "BattleCasting"
                    && ScreenNamePredicates.IsBattleState(check.Name))
                {
                    ModLogger.Log($"[MoveGrid] confirmed via screen.Name={check.Name} after {sw.ElapsedMilliseconds}ms ({polls} polls)");
                    confirmed = true; break;
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

            // Verify the unit actually moved by reading postAction position.
            // If the game rejected the move (invalid tile), it returns to BattleMyTurn
            // without changing position — the "confirmed" check above can't distinguish
            // this from a successful move.
            var postCheck = ReadPostActionState();
            if (postCheck != null && postCheck.X == startPos.x && postCheck.Y == startPos.y
                && (startPos.x != targetX || startPos.y != targetY))
            {
                response.Status = "failed";
                response.Error = $"({startPos.x},{startPos.y})->({finalPos.x},{finalPos.y}) REJECTED — unit still at start position";
                return response;
            }

            response.Status = "completed";
            response.Info = $"({startPos.x},{startPos.y})->({finalPos.x},{finalPos.y}) CONFIRMED";
            _menuCursorStale = true;
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
                var statuses = StatusDecoder.Decode(u.StatusBytes);
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
                    LifeState = u.Hp <= 0 && u.MaxHp > 0 ? "dead" : null,
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

        /// <summary>Last computed valid move tiles from scan_move BFS. Used by battle_move to validate targets.</summary>
        private HashSet<(int x, int y)>? _lastValidMoveTiles;


        /// <summary>Get enemy grid positions from last scan for BFS blocking.</summary>
        public HashSet<(int, int)> GetEnemyPositions()
        {
            var result = new HashSet<(int, int)>();
            if (_lastScannedUnits == null) return result;
            foreach (var u in _lastScannedUnits)
            {
                // Only block enemy units (team=1), not neutrals/NPCs (team=2)
                if (u.Team == 1 && u.Hp > 0)
                    result.Add((u.GridX, u.GridY));
            }
            return result;
        }

        /// <summary>Get positions of allied units (team=0, alive, not active) for BFS traversal cost.</summary>
        public HashSet<(int, int)> GetAllyPositions()
        {
            var result = new HashSet<(int, int)>();
            if (_lastScannedUnits == null) return result;
            foreach (var u in _lastScannedUnits)
            {
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

                    // +0x12 = 1 for units actively in this battle, 0 for stale/empty slots
                    if (inBattle == 0) continue;

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
                            // Fallback to UIBuffer (may be wrong but better than nothing)
                            unit.Move = (int)activeReads[12];
                            unit.Jump = (int)activeReads[13];
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
                        if (m.NameId <= 0) continue;

                        var unit = playerUnits[i];
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
                        var storyJob = CharacterData.GetStoryJob(m.NameId);
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
                        var cached = _unitNameCache.Get(unit.GridX, unit.GridY);
                        if (cached != null)
                        {
                            unit.JobNameOverride = cached;
                            ModLogger.Log($"[CollectPositions] Cache hit ({unit.GridX},{unit.GridY}) → {cached} (no heap match)");
                        }
                        else
                        {
                            ModLogger.Log($"[CollectPositions] No heap match for ({unit.GridX},{unit.GridY}) hp={unit.Hp}/{unit.MaxHp}");
                        }
                        continue;
                    }

                    // Try each heap match until we find one with a non-zero fingerprint.
                    // Some matches land on stale/dead unit slots where +0x69 is all zeros,
                    // which isn't a valid class signature — fall through to the next match.
                    byte[]? fpBytes = null;
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
                        fpBytes = candidateBytes;
                        break;
                    }
                    if (fpBytes == null)
                    {
                        var cached2 = _unitNameCache.Get(unit.GridX, unit.GridY);
                        if (cached2 != null)
                        {
                            unit.JobNameOverride = cached2;
                            ModLogger.Log($"[CollectPositions] Cache hit ({unit.GridX},{unit.GridY}) → {cached2} (zero fingerprint)");
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
                        _unitNameCache.Set(unit.GridX, unit.GridY, jobName);
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
        /// and start battle. Places 4 units (Enter×2 each) then commences
        /// (Space + Enter). Polls until a battle state appears.
        /// </summary>
        private CommandResponse AutoPlaceUnits(CommandResponse response)
        {
            // Wait for formation screen to fully load (detection is unreliable
            // for 3-6 seconds after Fight)
            Thread.Sleep(4000);

            // Place 4 units: each unit is Enter (select tile) + Enter (confirm)
            for (int unit = 0; unit < 4; unit++)
            {
                SendKey(VK_ENTER);
                Thread.Sleep(200);
                SendKey(VK_ENTER);
                Thread.Sleep(400);
            }

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
                if (screen != null)
                {
                    string name = screen.Name;
                    if (name == "BattleMyTurn" || name == "BattleAlliesTurn" ||
                        name == "BattleEnemiesTurn" || name == "BattleActing")
                    {
                        response.Screen = screen;
                        response.Status = "completed";
                        response.Info = $"Battle started ({name})";
                        return response;
                    }
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
            var hpPattern = new byte[]
            {
                (byte)(hp & 0xFF), (byte)(hp >> 8),
                (byte)(maxHp & 0xFF), (byte)(maxHp >> 8),
            };
            var matches = _explorer.SearchBytesInAllMemory(
                hpPattern, maxResults: 8, minAddr: 0x4000000000L, maxAddr: 0x4200000000L);
            ModLogger.Log($"[TryReadMoveJumpFromHeap] HP={hp}/{maxHp}: {matches.Count} heap matches");
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
        /// </summary>
        public PostActionState? ReadPostActionState()
        {
            try
            {
                var reads = _explorer.ReadMultiple(new[]
                {
                    ((nint)(AddrCondensedBase + 0x0C), 2), // HP
                    ((nint)(AddrCondensedBase + 0x10), 2), // MaxHP
                    ((nint)(AddrCondensedBase + 0x12), 2), // MP
                    ((nint)(AddrCondensedBase + 0x16), 2), // MaxMP
                });
                var pos = ReadGridPos();
                if (pos.x < 0 || pos.y < 0) return null;
                return new PostActionState
                {
                    X = pos.x,
                    Y = pos.y,
                    Hp = (int)reads[0],
                    MaxHp = (int)reads[1],
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
                Thread.Sleep(150);
            }
            // Verify cursor arrived
            Thread.Sleep(100);
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
