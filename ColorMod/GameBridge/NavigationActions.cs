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
        public BattleTracker? BattleTracker { get; set; }
        public MapLoader? _mapLoader;
        public Func<string[]>? GetAbilitiesSubmenuItems { get; set; }
        public Func<string, string[]>? GetAbilityListForSkillset { get; set; }
        private IntPtr _gameWindow;

        // --- DirectInput hook for faking held C key ---
        private static volatile bool _injectCKey = false;
        private static bool _diHookInstalled = false;

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

        private const int KEY_DELAY = 150;

        public NavigationActions(IInputSimulator input, MemoryExplorer explorer, Func<DetectedScreen?> detectScreen)
        {
            _input = input;
            _explorer = explorer;
            _detectScreen = detectScreen;
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
                        return BattleWait(response);

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

        private CommandResponse BattleWait(CommandResponse response)
        {
            var screen = _detectScreen();
            if (screen == null || !BattleWaitLogic.CanStartBattleWait(screen.Name))
            {
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
                Thread.Sleep(300);
            }
            else
            {
                // Normal path: navigate action menu to Wait
                Thread.Sleep(300);

                var cursorResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
                int cursor = cursorResult != null ? (int)cursorResult.Value.value : screen.MenuCursor;
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

                var (faceDx, faceDy) = FacingStrategy.ComputeOptimalFacing(allyPos, enemyPositions);

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

            // Hold Ctrl to fast-forward enemy turns
            Thread.Sleep(500);
            SendInputKeyDown(VK_CONTROL);
            _input.SendKeyDownToWindow(_gameWindow, VK_CONTROL);
            ModLogger.Log("[BattleWait] Holding Ctrl for fast-forward");

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

                    // If we hit Battle_Paused due to stale flag, send Escape to clear
                    if (current.Name == "Battle_Paused")
                    {
                        SendKey(VK_ESCAPE);
                        Thread.Sleep(300);
                        continue;
                    }

                    // Friendly unit's turn — we're done
                    if (current.Name == "Battle_MyTurn")
                    {
                        response.Error = $"Friendly turn after {sw.ElapsedMilliseconds}ms";
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
                // Always release Ctrl
                SendInputKeyUp(VK_CONTROL);
                _input.SendKeyUpToWindow(_gameWindow, VK_CONTROL);
                ModLogger.Log("[BattleWait] Released Ctrl");
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
            if (screen == null || (screen.Name != "Battle_MyTurn" && screen.Name != "Battle_Acting"))
            {
                response.Status = "failed";
                response.Error = $"Not on Battle_MyTurn/Battle_Acting (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Step 1: Navigate menu to Abilities (index 1)
            var cursorResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
            int cursor = cursorResult != null ? (int)cursorResult.Value.value : screen.MenuCursor;
            NavigateMenuCursor(cursor, 1);
            SendKey(VK_ENTER); // Open Abilities submenu
            Thread.Sleep(500);

            // Step 2: Select Attack (top item in submenu)
            SendKey(VK_ENTER);
            Thread.Sleep(500);

            // Verify we're in targeting mode
            screen = _detectScreen();
            if (screen == null || screen.Name != "Battle_Attacking")
            {
                response.Status = "failed";
                response.Error = $"Failed to enter targeting mode (current: {screen?.Name ?? "null"})";
                // Try to back out
                SendKey(VK_ESCAPE);
                Thread.Sleep(300);
                SendKey(VK_ESCAPE);
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
                response.Error = $"Attacked ({targetX},{targetY}) — cursor was already on target";
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
                else
                {
                    response.Status = "failed";
                    response.Error = "Could not detect rotation — cursor didn't move";
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                    return response;
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
                SendKey(VK_ESCAPE);
                Thread.Sleep(300);
                response.Status = "failed";
                return response;
            }

            // Step 7: Read target HP and team before confirming (cursor is on target)
            const long AddrCondensedHp = 0x14077D2AC; // TurnQueueBase + TqHp, uint16 LE
            const long AddrCondensedTeam = 0x14077D2A2; // TurnQueueBase + team, uint16 LE
            var preHpResult = _explorer.ReadAbsolute((nint)AddrCondensedHp, 2);
            var preTeamResult = _explorer.ReadAbsolute((nint)AddrCondensedTeam, 2);
            int preAttackHp = preHpResult != null ? (int)preHpResult.Value.value : -1;
            int targetTeam = preTeamResult != null ? (int)preTeamResult.Value.value : -1;
            ModLogger.Log($"[BattleAttack] Pre-attack: target HP={preAttackHp}, team={targetTeam}");

            // Step 8: Confirm attack — Enter (select target) + Enter (confirm "Target this tile?")
            SendKey(VK_ENTER);
            Thread.Sleep(500);
            SendKey(VK_ENTER);

            // Step 9: Poll for HP change while struct still shows the target (same team)
            int postAttackHp = preAttackHp;
            if (preAttackHp > 0)
            {
                for (int poll = 0; poll < 60; poll++) // 60 * 100ms = 6s max
                {
                    Thread.Sleep(100);
                    var teamRead = _explorer.ReadAbsolute((nint)AddrCondensedTeam, 2);
                    int currentTeam = teamRead != null ? (int)teamRead.Value.value : -1;

                    // Struct switched away from target — stop polling
                    if (currentTeam != targetTeam)
                    {
                        ModLogger.Log($"[BattleAttack] Struct switched to team {currentTeam} after {poll * 100}ms");
                        break;
                    }

                    var hpRead = _explorer.ReadAbsolute((nint)AddrCondensedHp, 2);
                    if (hpRead != null)
                    {
                        int currentHp = (int)hpRead.Value.value;
                        if (currentHp != preAttackHp)
                        {
                            postAttackHp = currentHp;
                            ModLogger.Log($"[BattleAttack] HP changed: {preAttackHp} -> {currentHp} after {poll * 100}ms");
                            break;
                        }
                    }
                }
            }

            // Step 10: Evaluate attack result
            var attackResult = AttackVerification.Evaluate(preAttackHp, postAttackHp);
            string resultStr = attackResult.Hit
                ? (attackResult.Killed ? $"KILLED! {attackResult.Damage} damage" : $"HIT! {attackResult.Damage} damage ({attackResult.HpAfter} HP remaining)")
                : "MISSED (no HP change detected)";

            response.Status = "completed";
            response.Error = $"Attacked ({targetX},{targetY}) from ({startPos.x},{startPos.y}) — {resultStr}";
            ModLogger.Log($"[BattleAttack] {response.Error}");
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
            if (screen == null || (screen.Name != "Battle_MyTurn" && screen.Name != "Battle_Acting"))
            {
                response.Status = "failed";
                response.Error = $"Not on Battle_MyTurn/Battle_Acting (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Step 1: Navigate to Abilities in action menu (index 1)
            var cursorResult = _explorer.ReadAbsolute((nint)0x1407FC620, 1);
            int cursor = cursorResult != null ? (int)cursorResult.Value.value : screen.MenuCursor;
            NavigateMenuCursor(cursor, 1);
            SendKey(VK_ENTER);
            Thread.Sleep(500);

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

            // Step 3: Enter the Abilities submenu and navigate to the correct skillset
            screen = _detectScreen();
            // The menu may not have entered the submenu yet if the previous DetectScreen
            // didn't trigger SyncBattleMenuTracker. Press Enter on Abilities if still on main menu.
            if (screen?.Name == "Battle_MyTurn")
            {
                // Menu cursor should be on Abilities already from Step 1
                SendKey(VK_ENTER);
                Thread.Sleep(500);
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

            // Navigate: submenu starts on the last cursor position (may not be Attack).
            // Press Up enough times to guarantee we're at Attack (index 0), then Down to target.
            for (int i = 0; i < submenuItems.Length; i++)
            {
                SendKey(VK_UP);
                Thread.Sleep(150);
            }
            for (int i = 0; i < skillsetIdx; i++)
            {
                SendKey(VK_DOWN);
                Thread.Sleep(150);
            }

            // Step 3: Enter the skillset
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

            // Navigate: go to top first, then down to target
            int listSize = learnedAbilities?.Length ?? 20;
            for (int i = 0; i < listSize; i++)
            {
                SendKey(VK_UP);
                Thread.Sleep(150);
            }
            for (int i = 0; i < abilityIndex; i++)
            {
                SendKey(VK_DOWN);
                Thread.Sleep(150);
            }

            // Step 5: Select the ability
            SendKey(VK_ENTER);
            Thread.Sleep(500);

            // Step 6: Handle targeting
            if (loc.isSelfTarget)
            {
                // Self-targeting abilities: confirm immediately
                // The game may show a confirmation or just apply
                SendKey(VK_ENTER);
                Thread.Sleep(300);
                response.Status = "completed";
                response.Info = $"Used {abilityName} (self-target)";
                return response;
            }

            // Targeted ability: we should now be in targeting mode (Battle_Attacking)
            screen = _detectScreen();
            if (screen == null || screen.Name != "Battle_Attacking")
            {
                // Some abilities go straight to targeting without battleMode=4
                // Check if we're still in a battle state
                if (screen?.Name?.StartsWith("Battle") != true)
                {
                    response.Status = "failed";
                    response.Error = $"Failed to enter targeting mode for {abilityName} (current: {screen?.Name ?? "null"})";
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                    SendKey(VK_ESCAPE);
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
                response.Info = $"Used {abilityName} on ({targetX},{targetY}) — cursor was already on target";
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
                }
                else
                {
                    response.Status = "failed";
                    response.Error = $"Could not detect rotation for {abilityName} targeting";
                    SendKey(VK_ESCAPE);
                    Thread.Sleep(300);
                    return response;
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
            response.Info = $"Used {abilityName} on ({targetX},{targetY})";
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
                        if (targetScreen.Equals("PartyMenu", StringComparison.OrdinalIgnoreCase))
                        {
                            SendKey(VK_ESCAPE);
                            WaitForScreen("PartyMenu", 2000);
                            progressed = true;
                        }
                        else if (targetScreen.Equals("TravelList", StringComparison.OrdinalIgnoreCase))
                        {
                            SendKey(VK_T);
                            WaitForScreen("TravelList", 2000);
                            progressed = true;
                        }
                        break;

                    case "PartyMenu":
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

                    case "Battle_Paused":
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

            var screen = _detectScreen();
            if (screen == null || (screen.Name != "WorldMap" && screen.Name != "TravelList"))
            {
                // Stale-state bypass: after battle_flee returns, screen detection can stay stuck
                // at Battle_MyTurn because unit slot memory (0x14077CA30/54) persists until the
                // game reallocates it. The combination of:
                //   - battleMode == 0     (no active battle)
                //   - rawLocation valid   (distinguishes from attack-animation flicker where it's 255)
                //   - party == 0          (not in pause/party menu)
                // means we're genuinely on the world map with stale slot memory.
                // See memory/feedback_flee_stale_state.md.
                bool looksStaleWorldMap = false;
                if (screen?.Name != null && screen.Name.StartsWith("Battle"))
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

            // 4.5. Hold Ctrl to speed up movement
            SendInputKeyDown(VK_CONTROL);
            _input.SendKeyDownToWindow(_gameWindow, VK_CONTROL);
            ModLogger.Log("[Travel] Holding Ctrl for fast-forward");

            // 5-9. Poll until arrival or encounter
            var sw = Stopwatch.StartNew();
            int stableWorldMapCount = 0;
            try
            {
                while (sw.ElapsedMilliseconds < 30000) // 30s max travel time
                {
                    Thread.Sleep(200);

                    // Check encounter via memory directly (faster than DetectScreen)
                    var encA = _explorer.ReadAbsolute((nint)0x140900824, 1);
                    var encB = _explorer.ReadAbsolute((nint)0x140900828, 1);
                    if (encA != null && encB != null && encA.Value.value != encB.Value.value)
                    {
                        // 6. Encounter detected — release Ctrl and report
                        SendInputKeyUp(VK_CONTROL);
                        _input.SendKeyUpToWindow(_gameWindow, VK_CONTROL);
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
                // 10. Always release Ctrl
                SendInputKeyUp(VK_CONTROL);
                _input.SendKeyUpToWindow(_gameWindow, VK_CONTROL);
                ModLogger.Log("[Travel] Released Ctrl");
            }

            return response;
        }

        /// <summary>
        /// confirm_attack: Press F twice (select target + confirm), then poll until
        /// the battle reaches a terminal state (Battle_MyTurn, Battle, GameOver, Battle_Paused).
        /// The attack animation can last several seconds with transient Battle_Acting/Moving states.
        /// </summary>
        private CommandResponse ConfirmAttack(CommandResponse response)
        {
            var screen = _detectScreen();
            if (screen == null || screen.Name != "Battle_Attacking")
            {
                response.Status = "failed";
                response.Error = $"Not on Battle_Attacking screen (current: {screen?.Name ?? "null"})";
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
                if (screen.Name == "Battle_MyTurn" ||
                    screen.Name == "Battle" ||
                    screen.Name == "GameOver" ||
                    screen.Name == "Battle_Paused")
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
            if (screen == null || !screen.Name.StartsWith("Battle"))
            {
                response.Status = "failed";
                response.Error = $"Not in battle (current: {screen?.Name ?? "null"})";
                return response;
            }

            var units = CollectUnitPositionsFull();

            // Build comprehensive summary
            var lines = new List<string>();
            lines.Add($"units={units.Count}");
            foreach (var u in units)
            {
                string teamName = u.Team == 0 ? "PLAYER" : u.Team == 2 ? "ALLY" : "ENEMY";
                string nameStr = u.Name != null ? $" {u.Name}" : "";
                var statuses = StatusDecoder.Decode(u.StatusBytes);
                string statusStr = statuses.Count > 0 ? $" [{string.Join(",", statuses)}]" : "";
                lines.Add($"[{teamName}]{nameStr} ({u.GridX},{u.GridY}) Lv{u.Level} HP={u.Hp}/{u.MaxHp} MP={u.Mp}/{u.MaxMp} PA={u.PA} MA={u.MA} Mv={u.Move} Jmp={u.Jump} Job={u.Job} Br={u.Brave} Fa={u.Faith} CT={u.CT} Exp={u.Exp}{statusStr}");
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
            if (screen == null || !screen.Name.StartsWith("Battle"))
            {
                response.Status = "failed";
                response.Error = $"Not in battle (current: {screen?.Name ?? "null"})";
                return response;
            }

            // 1. Scan units
            var units = CollectUnitPositionsFull();
            var ally = units.FirstOrDefault(u => u.Team == 0);
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
                JobId = ally.Job,
                JobName = ally.JobNameOverride ?? GameStateReporter.GetJobName(ally.Job),
                Brave = ally.Brave,
                Faith = ally.Faith,
                Move = ally.Move,
                Jump = ally.Jump,
                PA = ally.PA,
                MA = ally.MA,
            };
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
                    abilities = FilterAbilitiesBySkillsets(u).Select(a => new AbilityEntry
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
                    // Facing: null for now. Movement delta is unreliable because the game's
                    // Wait AI picks a facing direction independent of movement. Need a stable
                    // memory address for unit facing direction to populate this. See TODO.md.
                    Facing = null,
                    SecondaryAbility = u.SecondaryAbility,
                    LifeState = StatusDecoder.GetLifeState(u.StatusBytes) is var ls && ls != "alive" ? ls
                        : (u.Hp <= 0 && u.MaxHp > 0 ? "dead" : null),
                    Statuses = StatusDecoder.Decode(u.StatusBytes) is var s && s.Count > 0 ? s : null,
                    Abilities = abilities,
                });
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

            // 6. Compute valid tiles if map is loaded
            var validPaths = new Dictionary<string, PathEntry>();
            if (_mapLoader?.CurrentMap != null)
            {
                var map = _mapLoader.CurrentMap;
                // BFS using map data
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
                        if (occupiedPositions.Contains((nx, ny))) continue; // all units block movement
                        double nh = GetDisplayHeight(nx, ny);
                        if (nh < 0 || ch < 0) continue;
                        if (Math.Abs(nh - ch) > jumpStat) continue;
                        int nc = cost + 1;
                        if (nc > moveStat) continue;
                        var key = (nx, ny);
                        if (!visited.ContainsKey(key) || visited[key] > nc)
                        {
                            visited[key] = nc;
                            queue.Enqueue((nx, ny, nc));
                        }
                    }
                }

                // Remove starting tile
                visited.Remove((ally.GridX, ally.GridY));

                // Self-correction: if BFS found 0 tiles, the map is probably wrong
                if (visited.Count == 0 && moveStat > 0)
                {
                    ModLogger.Log($"[ScanMove] BFS returned 0 tiles on MAP{map.MapNumber:D3} — rejecting and retrying");
                    _mapLoader.RejectCurrentMap();

                    // Retry detection
                    var allPositions2 = units.Select(u => (u.GridX, u.GridY)).ToList();
                    var heightResult2 = _explorer.ReadAbsolute((nint)0x140C6492C, 4);
                    double allyHeight2 = heightResult2 != null ? (double)heightResult2.Value.value / 10.0 : -1;
                    int retryMap = _mapLoader.DetectMap(allPositions2, ally.GridX, ally.GridY, allyHeight2);
                    if (retryMap >= 0)
                    {
                        _mapLoader.LoadMap(retryMap);
                        lines.Add($"MAP{map.MapNumber:D3} rejected (0 tiles), retried → MAP{retryMap:D3}");
                        // Recompute BFS with new map — recurse by falling through to else branch
                    }
                    else
                    {
                        lines.Add($"MAP{map.MapNumber:D3} rejected (0 tiles), no alternative found");
                    }
                }

                // Build tile list as structured array
                var tileList = visited
                    .OrderBy(kv => kv.Value)
                    .Select(kv => new Utilities.TilePosition
                    {
                        X = kv.Key.Item1,
                        Y = kv.Key.Item2,
                        H = GetDisplayHeight(kv.Key.Item1, kv.Key.Item2)
                    })
                    .ToList();

                // Cache for battle_move validation
                _lastValidMoveTiles = new HashSet<(int, int)>(visited.Keys);

                validPaths["ValidMoveTiles"] = new PathEntry
                {
                    Desc = $"{tileList.Count} tiles from ({ally.GridX},{ally.GridY}) Mv={moveStat} Jmp={jumpStat} enemies={enemyPositions.Count}",
                    Tiles = tileList
                };

                lines.Add($"validTiles={tileList.Count} move={moveStat} jump={jumpStat} enemies={enemyPositions.Count}");
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
            if (screen == null || screen.Name != "Battle_MyTurn")
            {
                response.Status = "failed";
                response.Error = $"Not on Battle_MyTurn (current: {screen?.Name ?? "null"})";
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
                    if (check != null && check.Name == "Battle_MyTurn") break;
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
            if (preWait != null && preWait.Name == "Battle_MyTurn")
            {
                int waitCursor = preWait.MenuCursor;
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
                        if (current.Name == "Battle_Paused") { SendKey(VK_ESCAPE); Thread.Sleep(300); continue; }
                        if (current.Name == "Battle_MyTurn") { response.Error += $" | Next turn after {sw2.ElapsedMilliseconds}ms"; break; }
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
            if (!MoveValidator.IsValidTile(targetX, targetY, _lastValidMoveTiles))
            {
                response.Status = "failed";
                response.Error = MoveValidator.GetInvalidTileError(targetX, targetY, _lastValidMoveTiles);
                return response;
            }

            // Enter Move mode if on Battle_MyTurn
            var screen = _detectScreen();
            if (screen != null && screen.Name == "Battle_MyTurn")
            {
                NavigateToMove();
                SendKey(VK_ENTER);
                Thread.Sleep(500);
                screen = _detectScreen();
            }

            if (screen == null || screen.Name != "Battle_Moving")
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

            // Poll up to 5s for Battle_MyTurn or Battle_Acting (move confirmed, back on action menu).
            // Long-distance moves (4+ tiles) have walking animations that can exceed 3s.
            bool confirmed = false;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 5000)
            {
                var check = _detectScreen();
                // After F confirms, wait until we leave Battle_Moving (tile selection).
                // Any other battle screen means the move completed (action menu, acting, etc.)
                if (check != null && check.Name != "Battle_Moving" && check.Name!.StartsWith("Battle"))
                { confirmed = true; break; }
                Thread.Sleep(100);
            }

            response.Status = confirmed ? "completed" : "failed";
            response.Error = $"({startPos.x},{startPos.y})->({finalPos.x},{finalPos.y}) {(confirmed ? "CONFIRMED" : "NOT CONFIRMED")}";
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
                // Enter Move mode from Battle_MyTurn
                var screen = _detectScreen();
                if (screen != null && screen.Name == "Battle_MyTurn")
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
                if (postScreen != null && postScreen.Name == "Battle_MyTurn")
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
            if (screen == null || !screen.Name.StartsWith("Battle"))
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
            if (screen != null && screen.Name == "Battle_MyTurn")
            {
                NavigateToMove();
                SendKey(VK_ENTER);
                Thread.Sleep(500);
                screen = _detectScreen();
            }

            if (screen == null || screen.Name != "Battle_Moving")
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
                if (screen != null && screen.Name == "Battle_MyTurn")
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
        /// <summary>Rich unit data collected during C+Up scan.</summary>
        internal class ScannedUnit
        {
            public int GridX, GridY;
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
            public byte[]? ClassFingerprint;  // 11 bytes at heap struct +0x69 (see ClassFingerprintLookup)
            public string? JobNameOverride;   // Resolved class name from fingerprint (fallback for enemies)
        }

        /// <summary>
        /// Last empirically detected Right arrow delta from grid navigation.
        /// Used by BattleWait to determine facing rotation without reading camera address.
        /// Set during battle_move/battle_attack/auto_move whenever rotation detection runs.
        /// </summary>
        private (int dx, int dy)? _lastDetectedRightDelta;

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
                if (u.Team != 0 && u.Hp > 0)
                    result.Add((u.GridX, u.GridY));
            }
            return result;
        }

        /// <summary>Get active unit (first ally) from last scan.</summary>
        internal ScannedUnit? GetActiveAlly()
        {
            return _lastScannedUnits?.FirstOrDefault(u => u.Team == 0);
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
            var seen = new HashSet<(int, int)>();
            int maxUnits = 12;

            // Dismiss action menu first — C+Up only works on open field
            SendKey(VK_ESCAPE);
            Thread.Sleep(500);

            // Hold C via SendInput with SCANCODE flag (required for DirectInput games)
            // Also hold via keybd_event and PostMessage for belt-and-suspenders
            SendInputKeyDown(VK_C);
            _input.SendKeyDownToWindow(_gameWindow, VK_C);
            PostMessage(_gameWindow, 0x0100, (IntPtr)VK_C, IntPtr.Zero);
            Thread.Sleep(500);

            // Read candidate unit count from 0x140900650 — use as upper bound
            int expectedCount = 0;
            try {
                var countResult = _explorer.ReadAbsolute((nint)0x140900650, 1);
                expectedCount = countResult != null ? (int)countResult.Value.value : 0;
            } catch { }
            if (expectedCount <= 0 || expectedCount > maxUnits) expectedCount = maxUnits;
            ModLogger.Log($"[CollectPositions] Expected unit count: {expectedCount}");

            // Read the active unit FIRST (cursor starts on them before any Up press)
            Thread.Sleep(200);
            {
                var pos0 = ReadGridPos();
                if (pos0.x >= 0 && pos0.y >= 0)
                {
                    // Read active unit data from condensed struct (reliable) and UI buffer (Move/Jump only).
                    // Job/Brave/Faith are NOT read from UI buffer — it's stale after C+Up cycling.
                    // They'll be populated from roster matching below.
                    var reads0 = _explorer.ReadMultiple(new (nint, int)[]
                    {
                        ((nint)(AddrCondensedBase + 0x00), 2), // 0: level
                        ((nint)(AddrCondensedBase + 0x02), 2), // 1: team
                        ((nint)(AddrCondensedBase + 0x04), 2), // 2: nameId
                        ((nint)(AddrCondensedBase + 0x06), 1), // 3: Speed (base)
                        ((nint)(AddrCondensedBase + 0x08), 2), // 4: exp
                        ((nint)(AddrCondensedBase + 0x0A), 2), // 5: CT
                        ((nint)(AddrCondensedBase + 0x0C), 2), // 6: HP
                        ((nint)(AddrCondensedBase + 0x10), 2), // 7: maxHP
                        ((nint)(AddrCondensedBase + 0x12), 2), // 8: MP
                        ((nint)(AddrCondensedBase + 0x16), 2), // 9: maxMP
                        ((nint)(AddrCondensedBase + 0x18), 2), // 10: PA
                        ((nint)(AddrCondensedBase + 0x1A), 2), // 11: MA
                        ((nint)(AddrUIBuffer + 0x24), 2),      // 12: Move (UI buffer — only reliable before C+Up)
                        ((nint)(AddrUIBuffer + 0x26), 2),      // 13: Jump
                    });
                    var u0 = new ScannedUnit
                    {
                        GridX = pos0.x, GridY = pos0.y,
                        Level = (int)reads0[0], Team = (int)reads0[1], NameId = (int)reads0[2],
                        Speed = (int)reads0[3],
                        Exp = (int)reads0[4], CT = (int)reads0[5], Hp = (int)reads0[6],
                        MaxHp = (int)reads0[7], Mp = (int)reads0[8], MaxMp = (int)reads0[9],
                        PA = (int)reads0[10], MA = (int)reads0[11], Move = (int)reads0[12],
                        Jump = (int)reads0[13],
                        // Job, Brave, Faith populated from roster matching below
                    };
                    // Read learned abilities for active unit only (condensed struct list doesn't update during C+Up)
                    var abilityBytes = _explorer.Scanner.ReadBytes((nint)(AddrCondensedBase + 0x28), 64);
                    if (abilityBytes.Length > 0)
                    {
                        var learnedIds = ActionAbilityLookup.ParseLearnedIdsFromBytes(abilityBytes);
                        u0.LearnedAbilities = ActionAbilityLookup.ResolveLearnedAbilities(learnedIds);
                    }

                    seen.Add((pos0.x, pos0.y));
                    units.Add(u0);
                    ModLogger.Log($"[CollectPositions] Active unit: ({pos0.x},{pos0.y}) t{u0.Team} lv{u0.Level} hp={u0.Hp}/{u0.MaxHp} abilities={u0.LearnedAbilities.Count}");
                }
            }

            for (int i = 0; i < maxUnits; i++)
            {
                // Re-assert C held
                SendInputKeyDown(VK_C);
                Thread.Sleep(50);

                // Press Up via PostMessage
                _input.SendKeyPressToWindow(_gameWindow, VK_UP);
                Thread.Sleep(500); // give game time to fully update grid pos + condensed struct (was 250ms → 350ms → 500ms)

                // Read grid position
                var pos = ReadGridPos();
                if (pos.x < 0 || pos.y < 0) continue;

                // Deduplicate by position
                bool isNew = seen.Add((pos.x, pos.y));

                if (isNew)
                {
                    // Read unit data, then verify position didn't change during the read (race detection).
                    var reads = _explorer.ReadMultiple(new (nint, int)[]
                    {
                        ((nint)(AddrCondensedBase + 0x00), 2), // 0: level
                        ((nint)(AddrCondensedBase + 0x02), 2), // 1: team
                        ((nint)(AddrCondensedBase + 0x04), 2), // 2: nameId
                        ((nint)(AddrCondensedBase + 0x06), 1), // 3: Speed (base)
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
                        ((nint)(AddrUIBuffer + 0x2A), 2),      // 14: Job
                        ((nint)(AddrUIBuffer + 0x2C), 2),      // 15: Brave
                        ((nint)(AddrUIBuffer + 0x2E), 2),      // 16: Faith
                    });

                    // Verify grid position didn't change during the read.
                    // If it did, the condensed struct data belongs to a different unit — skip this read.
                    var posAfter = ReadGridPos();
                    if (posAfter.x != pos.x || posAfter.y != pos.y)
                    {
                        ModLogger.Log($"[CollectPositions] Position race at ({pos.x},{pos.y})→({posAfter.x},{posAfter.y}), skipping stale read");
                        seen.Remove((pos.x, pos.y));
                        continue;
                    }

                    var unit = new ScannedUnit
                    {
                        GridX = pos.x,
                        GridY = pos.y,
                        Level = (int)reads[0],
                        Team = (int)reads[1],
                        NameId = (int)reads[2],
                        Speed = (int)reads[3],
                        Exp = (int)reads[4],
                        CT = (int)reads[5],
                        Hp = (int)reads[6],
                        MaxHp = (int)reads[7],
                        Mp = (int)reads[8],
                        MaxMp = (int)reads[9],
                        PA = (int)reads[10],
                        MA = (int)reads[11],
                        Move = (int)reads[12],
                        Jump = (int)reads[13],
                        Job = (int)reads[14],
                        Brave = (int)reads[15],
                        Faith = (int)reads[16],
                    };

                    units.Add(unit);
                    ModLogger.Log($"[CollectPositions] Unit {units.Count}: ({pos.x},{pos.y}) t{unit.Team} lv{unit.Level} hp={unit.Hp}/{unit.MaxHp}");
                }

                // Stop when we've cycled back to a unit we already saw AND we've
                // pressed Up enough times to have seen everyone. Fast units appear
                // multiple times in the Combat Timeline before slower units appear,
                // so we can't stop at the first duplicate — we must keep cycling
                // until we've completed a full loop.
                if (!isNew && units.Count >= 2 && i >= units.Count)
                {
                    ModLogger.Log($"[CollectPositions] Full cycle after {i + 1} presses, {units.Count} unique units");
                    break;
                }
            }

            // Release C everywhere
            SendInputKeyUp(VK_C);
            _input.SendKeyUpToWindow(_gameWindow, VK_C);
            PostMessage(_gameWindow, 0x0101, (IntPtr)VK_C, IntPtr.Zero);
            Thread.Sleep(200);

            // Re-open action menu
            SendKey(VK_F);
            Thread.Sleep(300);

            // Read status bytes + identity from static battle array
            // Layout per slot (stride 0x200):
            //   +0x0C from array base: exp(byte) level(byte) origBrave(byte) brave(byte) origFaith(byte) faith(byte) flag(byte) 00 HP(u16) maxHP(u16)
            //   +0x45 from array base: 5 status bytes
            const long AddrStatArrayBase = 0x140893C00;  // Start of battle array (unit 0)
            const long StatOffStatPattern = 0x20C;        // exp/level offset from array base
            const long StatOffHp = 0x214;                 // HP offset from array base
            const long StatOffMaxHp = 0x216;              // MaxHP offset from array base
            const long StatOffStatus = 0x245;             // Status bytes offset from array base
            const int StatusStride = 0x200;
            const int MaxSlots = 21;

            try
            {
                // Build batch read for all slot HP+MaxHP+origBrave+origFaith values
                var slotReads = new (nint, int)[MaxSlots * 4];
                for (int s = 0; s < MaxSlots; s++)
                {
                    long slotBase = AddrStatArrayBase + s * StatusStride;
                    slotReads[s * 4] = ((nint)(slotBase + StatOffHp), 2);       // HP
                    slotReads[s * 4 + 1] = ((nint)(slotBase + StatOffMaxHp), 2); // MaxHP
                    slotReads[s * 4 + 2] = ((nint)(slotBase + StatOffStatPattern), 1); // exp (byte)
                    slotReads[s * 4 + 3] = ((nint)(slotBase + StatOffStatPattern + 1), 1); // level (byte)
                }
                var slotValues = _explorer.ReadMultiple(slotReads);

                // Match each scanned unit to a slot by HP+MaxHP
                foreach (var unit in units)
                {
                    for (int s = 0; s < MaxSlots; s++)
                    {
                        int slotHp = (int)slotValues[s * 4];
                        int slotMaxHp = (int)slotValues[s * 4 + 1];
                        if (slotMaxHp == unit.MaxHp && slotHp == unit.Hp)
                        {
                            // Read status bytes
                            var statusBytes = _explorer.Scanner.ReadBytes((nint)(AddrStatArrayBase + s * StatusStride + StatOffStatus), 5);
                            if (statusBytes.Length == 5)
                            {
                                unit.StatusBytes = statusBytes;
                                var decoded = StatusDecoder.Decode(statusBytes);
                                if (decoded.Count > 0)
                                    ModLogger.Log($"[CollectPositions] Unit ({unit.GridX},{unit.GridY}) statuses: [{string.Join(",", decoded)}]");
                            }

                            // Read origBrave/origFaith for roster matching (bytes at statPattern+2 and +4)
                            var identBytes = _explorer.Scanner.ReadBytes((nint)(AddrStatArrayBase + s * StatusStride + StatOffStatPattern + 2), 4);
                            if (identBytes.Length == 4)
                            {
                                unit.Brave = identBytes[0];  // origBrave
                                unit.Faith = identBytes[2];  // origFaith
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Log($"[CollectPositions] Status/identity read failed: {ex.Message}");
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
                        unit.Name = UnitNameLookup.GetName(m.NameId);
                        unit.Job = m.Job;
                        unit.Brave = m.Brave;
                        unit.Faith = m.Faith;
                        unit.SecondaryAbility = m.Secondary;
                        // For story characters, the roster's job field at +0x02 equals
                        // their nameId rather than a real job ID (e.g. Marach job=26
                        // which PSX maps to Dragoon). StoryCharacterJob dict provides
                        // the canonical job name for these characters.
                        var storyJob = CharacterData.GetStoryJob(m.NameId);
                        if (storyJob != null)
                        {
                            unit.JobNameOverride = storyJob;
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
                        ModLogger.Log($"[CollectPositions] No heap match for ({unit.GridX},{unit.GridY}) hp={unit.Hp}/{unit.MaxHp}");
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
                        ModLogger.Log($"[CollectPositions] All heap matches had zero fingerprint for ({unit.GridX},{unit.GridY}) hp={unit.Hp}/{unit.MaxHp}");
                        continue;
                    }

                    unit.ClassFingerprint = fpBytes;
                    var jobName = ClassFingerprintLookup.GetJobName(fpBytes, team: unit.Team);
                    if (jobName != null)
                    {
                        unit.JobNameOverride = jobName;
                        ModLogger.Log($"[CollectPositions] Fingerprint match ({unit.GridX},{unit.GridY}) → {jobName}");
                    }
                    else
                    {
                        var fpKey = ClassFingerprintLookup.ToKey(fpBytes);
                        ModLogger.Log($"[CollectPositions] Unknown fingerprint ({unit.GridX},{unit.GridY}): {fpKey} hp={unit.Hp}/{unit.MaxHp} lv={unit.Level}");
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

        // --- Helpers ---

        private void SendKey(int vk)
        {
            _input.SendKeyPressToWindow(_gameWindow, vk);
            Thread.Sleep(KEY_DELAY);
        }

        /// <summary>
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
            response.Status = "failed";
            response.Error = "Not implemented yet";
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
            response.Status = "failed";
            response.Error = "Not implemented yet";
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
