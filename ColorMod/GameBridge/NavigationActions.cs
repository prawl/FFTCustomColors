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

                    case "navigate":
                        return Navigate(response, command.To ?? "");

                    case "travel":
                        return Travel(response, command.LocationId);

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

                    case "move_grid":
                        return MoveGrid(response, command);

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
        private CommandResponse BattleWait(CommandResponse response)
        {
            var screen = _detectScreen();
            if (screen == null || screen.Name != "Battle_MyTurn")
            {
                response.Status = "failed";
                response.Error = $"Not on Battle_MyTurn screen (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Navigate cursor to Wait (position 2)
            int cursor = screen.MenuCursor;
            int target = 2; // Wait
            NavigateMenuCursor(cursor, target);

            // Press Enter to select Wait
            SendKey(VK_ENTER);
            Thread.Sleep(300);

            // Press Enter to confirm facing direction
            SendKey(VK_ENTER);

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
        ///      hover matches target.
        /// </summary>
        private CommandResponse Travel(CommandResponse response, int locationId)
        {
            if (locationId < 0)
            {
                response.Status = "failed";
                response.Error = "locationId is required and must be >= 0";
                return response;
            }

            // First navigate to WorldMap
            var screen = _detectScreen();
            if (screen == null)
            {
                response.Status = "failed";
                response.Error = "Could not detect current screen";
                return response;
            }

            if (screen.Name != "WorldMap")
            {
                var navResponse = Navigate(response, "WorldMap");
                if (navResponse.Status != "completed")
                    return navResponse;
            }

            // Open travel list
            SendKey(VK_T);
            if (!WaitForScreen("TravelList", 2000))
            {
                response.Status = "failed";
                response.Error = "Failed to open travel list";
                return response;
            }

            // Scan all 3 tabs. For each tab:
            //   - E switches tab (resets cursor to top of that tab)
            //   - Enter selects the highlighted item (closes list, updates hover)
            //   - If hover matches, done
            //   - If not, T reopens (remembers tab + cursor), Down advances, repeat
            //   - Track hover values to detect wrap-around (list is circular)
            //
            // Key behaviors (proven manually):
            //   - E switches tab and resets cursor to first item
            //   - Enter selects current item, updates hover, closes list
            //   - T reopens on same tab with cursor on last-selected item
            //   - Down advances cursor one position
            //   - Hover address only updates on Enter, not while scrolling

            for (int tab = 0; tab < 3; tab++)
            {
                // Switch to next tab (resets cursor to top)
                SendKey(VK_E);
                Thread.Sleep(300);

                // Select first item on this tab
                SendKey(VK_ENTER);
                Thread.Sleep(300);

                screen = _detectScreen();
                if (screen != null && screen.Hover == locationId)
                {
                    response.Status = "completed";
                    return response;
                }

                // Remember first hover to detect wrap-around
                int firstHover = screen?.Hover ?? -1;

                // Scan remaining items on this tab
                for (int item = 0; item < 25; item++)
                {
                    // Reopen list (cursor on last-selected item)
                    SendKey(VK_T);
                    Thread.Sleep(500);

                    // Advance to next item
                    SendKey(VK_DOWN);
                    Thread.Sleep(150);

                    // Select it
                    SendKey(VK_ENTER);
                    Thread.Sleep(300);

                    screen = _detectScreen();
                    if (screen != null && screen.Hover == locationId)
                    {
                        response.Status = "completed";
                        return response;
                    }

                    // If we wrapped back to the first item, this tab is exhausted
                    if (screen != null && screen.Hover == firstHover)
                        break;
                }

                // Reopen list so E can switch to next tab
                SendKey(VK_T);
                Thread.Sleep(500);
            }

            // Close the travel list before reporting failure
            SendKey(VK_ESCAPE);
            Thread.Sleep(300);

            response.Status = "failed";
            response.Error = $"Could not find location {locationId} in any travel list tab";
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
            if (screen == null || screen.Name != "Battle_Targeting")
            {
                response.Status = "failed";
                response.Error = $"Not on Battle_Targeting screen (current: {screen?.Name ?? "null"})";
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
                string teamName = u.Team == 0 ? "ALLY" : "ENEMY";
                lines.Add($"[{teamName}] ({u.GridX},{u.GridY}) Lv{u.Level} HP={u.Hp}/{u.MaxHp} MP={u.Mp}/{u.MaxMp} PA={u.PA} MA={u.MA} Mv={u.Move} Jmp={u.Jump} Job={u.Job} Br={u.Brave} Fa={u.Faith} CT={u.CT} Exp={u.Exp} NameId={u.NameId}");
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

            // 3. Get enemy positions
            var enemyPositions = GetEnemyPositions();

            // 4. Build unit summary
            var lines = new List<string>();
            lines.Add($"units={units.Count}");
            foreach (var u in units)
            {
                string teamName = u.Team == 0 ? "ALLY" : "ENEMY";
                lines.Add($"[{teamName}] ({u.GridX},{u.GridY}) Lv{u.Level} HP={u.Hp}/{u.MaxHp}");
            }

            // 5. Auto-detect map if not loaded
            if (_mapLoader != null && _mapLoader.CurrentMap == null)
            {
                // Try 1: Location ID lookup (fast, reliable for known locations)
                var currentScreen = _detectScreen();
                int locId = currentScreen?.Location ?? -1;
                // If location is 255 (battle), try reading persisted location from disk
                if (locId < 0 || locId > 42)
                {
                    try
                    {
                        var lastLocPath = System.IO.Path.Combine(_mapLoader.MapDataDir, "last_location.txt");
                        if (System.IO.File.Exists(lastLocPath))
                            locId = int.Parse(System.IO.File.ReadAllText(lastLocPath).Trim());
                    }
                    catch { }
                }
                if (locId >= 0 && locId <= 42)
                {
                    var locMap = _mapLoader.LoadMapForLocation(locId);
                    if (locMap != null)
                        lines.Add($"MAP{locMap.MapNumber:D3} (location {locId} lookup)");
                }

                // Try 2: Fingerprint detection (unit positions + height)
                if (_mapLoader.CurrentMap == null)
                {
                    var heightResult = _explorer.ReadAbsolute((nint)0x140C6492C, 4);
                    double allyHeight = heightResult != null ? (double)heightResult.Value.value / 10.0 : -1;

                    var allPositions = units.Select(u => (u.GridX, u.GridY)).ToList();
                    int detected = _mapLoader.DetectMap(allPositions, ally.GridX, ally.GridY, allyHeight);
                    if (detected >= 0)
                    {
                        _mapLoader.LoadMap(detected);
                        lines.Add($"MAP{detected:D3} (fingerprint, allyH={allyHeight})");
                    }
                    else
                    {
                        lines.Add($"MAP DETECTION FAILED (loc={locId}, allyH={allyHeight}, {units.Count} units)");
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
                        if (enemyPositions.Contains((nx, ny))) continue;
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

                // Build tile list as "x,y" entries in validPaths
                var tileStrings = visited
                    .OrderBy(kv => kv.Value)
                    .Select(kv => $"{kv.Key.Item1},{kv.Key.Item2}")
                    .ToList();

                validPaths["ValidMoveTiles"] = new PathEntry
                {
                    Desc = $"{tileStrings.Count} tiles from ({ally.GridX},{ally.GridY}) Mv={moveStat} Jmp={jumpStat} enemies={enemyPositions.Count}",
                    Action = string.Join(" ", tileStrings)
                };

                lines.Add($"validTiles={tileStrings.Count} move={moveStat} jump={jumpStat} enemies={enemyPositions.Count}");
            }
            else
            {
                lines.Add("NO MAP — detection failed and no manual set_map");
            }

            response.Status = "completed";
            response.Error = string.Join(" | ", lines);
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
                NavigateMenuCursor(_detectScreen()?.MenuCursor ?? 0, 0);
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

            // Enter Move mode if on Battle_MyTurn
            var screen = _detectScreen();
            if (screen != null && screen.Name == "Battle_MyTurn")
            {
                NavigateMenuCursor(screen.MenuCursor, 0);
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

            // Poll up to 3s for Battle_MyTurn
            bool confirmed = false;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3000)
            {
                var check = _detectScreen();
                if (check != null && check.Name == "Battle_MyTurn") { confirmed = true; break; }
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
                    NavigateMenuCursor(screen.MenuCursor, 0); // Navigate to Move
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
                int cursor = screen.MenuCursor;
                NavigateMenuCursor(cursor, 0);
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
            public int NameId;
            public int Exp;
            public int CT;
            public int Hp, MaxHp;
            public int Mp, MaxMp;
            public int PA, MA;
            public int Move, Jump;
            public int Job;
            public int Brave, Faith;
        }

        /// <summary>Last scan results cached for BFS enemy blocking.</summary>
        private List<ScannedUnit>? _lastScannedUnits;

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
                    var reads0 = _explorer.ReadMultiple(new (nint, int)[]
                    {
                        ((nint)(AddrCondensedBase + 0x00), 2),
                        ((nint)(AddrCondensedBase + 0x02), 2),
                        ((nint)(AddrCondensedBase + 0x04), 2),
                        ((nint)(AddrCondensedBase + 0x08), 2),
                        ((nint)(AddrCondensedBase + 0x0A), 2),
                        ((nint)(AddrCondensedBase + 0x0C), 2),
                        ((nint)(AddrCondensedBase + 0x10), 2),
                        ((nint)(AddrCondensedBase + 0x12), 2),
                        ((nint)(AddrCondensedBase + 0x16), 2),
                        ((nint)(AddrCondensedBase + 0x18), 2),
                        ((nint)(AddrCondensedBase + 0x1A), 2),
                        ((nint)(AddrUIBuffer + 0x24), 2),
                        ((nint)(AddrUIBuffer + 0x26), 2),
                        ((nint)(AddrUIBuffer + 0x2A), 2),
                        ((nint)(AddrUIBuffer + 0x2C), 2),
                        ((nint)(AddrUIBuffer + 0x2E), 2),
                    });
                    var u0 = new ScannedUnit
                    {
                        GridX = pos0.x, GridY = pos0.y,
                        Level = (int)reads0[0], Team = (int)reads0[1], NameId = (int)reads0[2],
                        Exp = (int)reads0[3], CT = (int)reads0[4], Hp = (int)reads0[5],
                        MaxHp = (int)reads0[6], Mp = (int)reads0[7], MaxMp = (int)reads0[8],
                        PA = (int)reads0[9], MA = (int)reads0[10], Move = (int)reads0[11],
                        Jump = (int)reads0[12], Job = (int)reads0[13], Brave = (int)reads0[14],
                        Faith = (int)reads0[15],
                    };
                    seen.Add((pos0.x, pos0.y));
                    units.Add(u0);
                    ModLogger.Log($"[CollectPositions] Active unit: ({pos0.x},{pos0.y}) t{u0.Team} lv{u0.Level} hp={u0.Hp}/{u0.MaxHp}");
                }
            }

            for (int i = 0; i < maxUnits; i++)
            {
                // Re-assert C held
                SendInputKeyDown(VK_C);
                Thread.Sleep(50);

                // Press Up via PostMessage
                _input.SendKeyPressToWindow(_gameWindow, VK_UP);
                Thread.Sleep(250);

                // Read grid position
                var pos = ReadGridPos();
                if (pos.x < 0 || pos.y < 0) continue;

                // Deduplicate by position
                bool isNew = seen.Add((pos.x, pos.y));

                if (isNew)
                {
                    // Read ALL unit data from condensed struct + UI buffer
                    var reads = _explorer.ReadMultiple(new (nint, int)[]
                    {
                        ((nint)(AddrCondensedBase + 0x00), 2), // 0: level
                        ((nint)(AddrCondensedBase + 0x02), 2), // 1: team
                        ((nint)(AddrCondensedBase + 0x04), 2), // 2: nameId
                        ((nint)(AddrCondensedBase + 0x08), 2), // 3: exp
                        ((nint)(AddrCondensedBase + 0x0A), 2), // 4: CT
                        ((nint)(AddrCondensedBase + 0x0C), 2), // 5: HP
                        ((nint)(AddrCondensedBase + 0x10), 2), // 6: maxHP
                        ((nint)(AddrCondensedBase + 0x12), 2), // 7: MP
                        ((nint)(AddrCondensedBase + 0x16), 2), // 8: maxMP
                        ((nint)(AddrCondensedBase + 0x18), 2), // 9: PA
                        ((nint)(AddrCondensedBase + 0x1A), 2), // 10: MA
                        ((nint)(AddrUIBuffer + 0x24), 2),      // 11: Move
                        ((nint)(AddrUIBuffer + 0x26), 2),      // 12: Jump
                        ((nint)(AddrUIBuffer + 0x2A), 2),      // 13: Job
                        ((nint)(AddrUIBuffer + 0x2C), 2),      // 14: Brave
                        ((nint)(AddrUIBuffer + 0x2E), 2),      // 15: Faith
                    });

                    var unit = new ScannedUnit
                    {
                        GridX = pos.x,
                        GridY = pos.y,
                        Level = (int)reads[0],
                        Team = (int)reads[1],
                        NameId = (int)reads[2],
                        Exp = (int)reads[3],
                        CT = (int)reads[4],
                        Hp = (int)reads[5],
                        MaxHp = (int)reads[6],
                        Mp = (int)reads[7],
                        MaxMp = (int)reads[8],
                        PA = (int)reads[9],
                        MA = (int)reads[10],
                        Move = (int)reads[11],
                        Jump = (int)reads[12],
                        Job = (int)reads[13],
                        Brave = (int)reads[14],
                        Faith = (int)reads[15],
                    };

                    units.Add(unit);
                    ModLogger.Log($"[CollectPositions] Unit {units.Count}: ({pos.x},{pos.y}) t{unit.Team} lv{unit.Level} hp={unit.Hp}/{unit.MaxHp} job={unit.Job}");
                }

                // Only stop after we've pressed Up at least expectedCount times
                // This ensures fast units appearing multiple times in turn order
                // don't cause us to stop before seeing all unique units
                if (i >= expectedCount && !isNew)
                {
                    ModLogger.Log($"[CollectPositions] Duplicate after {i + 1} presses (>= {expectedCount}), {units.Count} unique units");
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
    }
}
