using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private IntPtr _gameWindow;

        // VK codes
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
        /// battle_wait: Navigate to Wait in action menu, confirm, confirm facing.
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

            // Wait for the turn to fully resolve — moveMode and action flags
            // may flicker during the animation. Poll until stable.
            Thread.Sleep(500);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3000)
            {
                Thread.Sleep(100);
                // Wait for moveMode to clear (it flickers during facing confirm)
                var val = _explorer.ReadAbsolute((nint)0x14077CA5C, 1);
                if (val != null && val.Value.value == 0)
                    break;
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

        // Tile list addresses
        private const long AddrTileList = 0x140C66315;
        private const long AddrCursorIndex = 0x140C64E7C;

        /// <summary>
        /// move_to: Navigate the movement cursor to a tile and confirm.
        /// Accepts target as:
        ///   - "nearest_enemy": finds the valid tile closest to nearest living enemy
        ///   - "tile": uses locationId as tile index into the tile list
        /// Strategy: press cursor keys, read cursor position after each,
        /// check if we're on the target tile, confirm when matched.
        /// </summary>
        private CommandResponse MoveTo(CommandResponse response, string target, int tileIndex)
        {
            var screen = _detectScreen();

            // Enter Move mode if on action menu
            if (screen != null && screen.Name == "Battle_MyTurn")
            {
                int cursor = screen.MenuCursor;
                NavigateMenuCursor(cursor, 0); // Navigate to Move (position 0)
                SendKey(VK_ENTER);
                Thread.Sleep(300);
                screen = _detectScreen();
            }

            if (screen == null || screen.Name != "Battle_Moving")
            {
                response.Status = "failed";
                response.Error = $"Not in Move mode (current: {screen?.Name ?? "null"})";
                return response;
            }

            // Read tile list
            var tiles = ReadTileList();
            if (tiles == null || tiles.Count == 0)
            {
                response.Status = "failed";
                response.Error = "Could not read tile list";
                return response;
            }

            int targetX, targetY;

            if (target == "tile" && tileIndex >= 0 && tileIndex < tiles.Count)
            {
                targetX = tiles[tileIndex].x;
                targetY = tiles[tileIndex].y;
            }
            else
            {
                // Default: find tile closest to nearest living enemy
                // Read battle state for enemy positions
                var battleState = BattleTracker?.Update();
                if (battleState?.Units == null || battleState.Units.Count == 0)
                {
                    response.Status = "failed";
                    response.Error = "No battle state available";
                    return response;
                }

                // Find nearest living enemy with known position
                var enemies = battleState.Units
                    .Where(u => u.Team != 0 && u.Hp > 0 && u.PositionKnown)
                    .ToList();

                if (enemies.Count == 0)
                {
                    response.Status = "failed";
                    response.Error = "No living enemies with known positions";
                    return response;
                }

                // Find the tile closest to any enemy
                int bestDist = int.MaxValue;
                targetX = tiles[0].x;
                targetY = tiles[0].y;

                foreach (var tile in tiles)
                {
                    foreach (var enemy in enemies)
                    {
                        int dist = Math.Abs(tile.x - enemy.X) + Math.Abs(tile.y - enemy.Y);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            targetX = tile.x;
                            targetY = tile.y;
                        }
                    }
                }
            }

            // Try to navigate to the target tile.
            // Press each direction and check if cursor lands on target.
            // Try up to 20 key presses total.
            int[] directions = { VK_DOWN, VK_UP, VK_LEFT, VK_RIGHT };
            string[] dirNames = { "Down", "Up", "Left", "Right" };

            for (int attempt = 0; attempt < 30; attempt++)
            {
                // Read current cursor position
                var cursorPos = ReadCursorTile();
                if (cursorPos.x == targetX && cursorPos.y == targetY)
                {
                    // On target — confirm move
                    SendKey(VK_F);
                    Thread.Sleep(300);

                    // Wait for move animation
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < 5000)
                    {
                        Thread.Sleep(100);
                        screen = _detectScreen();
                        if (screen != null && screen.Name != "Battle_Moving")
                            break;
                    }

                    response.Status = "completed";
                    return response;
                }

                // Try each direction to get closer
                int bestDir = -1;
                int bestDist = Math.Abs(cursorPos.x - targetX) + Math.Abs(cursorPos.y - targetY);

                for (int d = 0; d < 4; d++)
                {
                    // Press direction
                    _input.SendKeyPressToWindow(_gameWindow, directions[d]);
                    Thread.Sleep(KEY_DELAY);

                    // Read new position
                    var newPos = ReadCursorTile();
                    int newDist = Math.Abs(newPos.x - targetX) + Math.Abs(newPos.y - targetY);

                    if (newPos.x == targetX && newPos.y == targetY)
                    {
                        // Found target — confirm move
                        SendKey(VK_F);
                        Thread.Sleep(300);

                        var sw2 = System.Diagnostics.Stopwatch.StartNew();
                        while (sw2.ElapsedMilliseconds < 5000)
                        {
                            Thread.Sleep(100);
                            screen = _detectScreen();
                            if (screen != null && screen.Name != "Battle_Moving")
                                break;
                        }

                        response.Status = "completed";
                        return response;
                    }

                    if (newDist < bestDist)
                    {
                        bestDist = newDist;
                        bestDir = d;
                    }

                    // Undo — press opposite direction
                    int opposite = d ^ 1; // Down↔Up, Left↔Right
                    _input.SendKeyPressToWindow(_gameWindow, directions[opposite]);
                    Thread.Sleep(KEY_DELAY);
                }

                // Move in the best direction found
                if (bestDir >= 0)
                {
                    _input.SendKeyPressToWindow(_gameWindow, directions[bestDir]);
                    Thread.Sleep(KEY_DELAY);
                }
                else
                {
                    // No improvement possible — stuck
                    break;
                }
            }

            response.Status = "failed";
            response.Error = $"Could not reach tile ({targetX},{targetY}) after 30 attempts";
            return response;
        }

        /// <summary>
        /// Reads the current cursor tile position from cursor index + tile list.
        /// </summary>
        private (int x, int y) ReadCursorTile()
        {
            var idxResult = _explorer.ReadAbsolute((nint)AddrCursorIndex, 1);
            int idx = idxResult != null ? (int)idxResult.Value.value : 0;

            var tileData = _explorer.Scanner.ReadBytes((nint)AddrTileList, 350);
            int offset = idx * 7;
            if (offset + 6 < tileData.Length && tileData[offset + 3] != 0)
                return (tileData[offset], tileData[offset + 1]);

            return (-1, -1);
        }

        /// <summary>
        /// Reads the full tile list (valid movement tiles).
        /// </summary>
        private List<(int x, int y)>? ReadTileList()
        {
            var tileData = _explorer.Scanner.ReadBytes((nint)AddrTileList, 350);
            if (tileData.Length < 7) return null;

            var tiles = new List<(int x, int y)>();
            var seen = new HashSet<(int, int)>();
            for (int i = 0; i < tileData.Length - 6; i += 7)
            {
                int x = tileData[i];
                int y = tileData[i + 1];
                int flag = tileData[i + 3];
                if (flag == 0 || x > 30 || y > 30) break;
                if (seen.Add((x, y)))
                    tiles.Add((x, y));
            }
            return tiles;
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
                SendKey(vk);
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
