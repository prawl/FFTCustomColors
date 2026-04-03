using System;
using System.Diagnostics;
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
        private IntPtr _gameWindow;

        // VK codes
        private const int VK_ENTER = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_LEFT = 0x25;
        private const int VK_RIGHT = 0x27;
        private const int VK_TAB = 0x09;
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
