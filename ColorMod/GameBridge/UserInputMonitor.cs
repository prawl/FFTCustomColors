using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Polls the OS for navigation key transitions the user types directly
    /// into the game window (i.e. NOT sent via the bridge), and forwards
    /// them into the <see cref="ScreenStateMachine"/> so SM-tracked state
    /// (cursor rows, tab positions, etc.) stays aligned with reality.
    ///
    /// Without this, any cursor state we track in the SM desynchronises the
    /// moment the user takes manual control — pressing Down on the keyboard
    /// advances the game but leaves the SM cursor where it was. Addresses
    /// the session-46 concern "my inputs break the state".
    ///
    /// Mechanism:
    /// 1. Background thread polls <see cref="GetAsyncKeyState"/> every 20ms
    ///    for tracked nav keys.
    /// 2. On rising edge (not-pressed → pressed) AND game window is
    ///    foreground, forward to SM.
    /// 3. Keys that were recently sent by the bridge itself (timestamp
    ///    within 150ms) are skipped to avoid double-counting.
    /// </summary>
    public class UserInputMonitor
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private readonly ScreenStateMachine _sm;
        private readonly IntPtr _gameWindow;
        private readonly int[] _trackedKeys = new[]
        {
            0x0D, // Enter
            0x1B, // Escape
            0x20, // Space
            0x25, // Left
            0x26, // Up
            0x27, // Right
            0x28, // Down
            0x51, // Q (unit cycle)
            0x45, // E (unit cycle)
            0x52, // R (EqA effects toggle)
            0x41, // A (picker tab back)
            0x44, // D (picker tab forward)
            0x42, // B (hold-B for DismissUnit / exit BattleSequence)
            0x59, // Y (JobChange confirmation)
            0x54, // T (auto-battle toggle)
            0x58, // X
        };

        /// <summary>
        /// Timestamps of recent bridge-sent key presses. Used to skip
        /// forwarding a key to the SM when the bridge just sent it (the
        /// SM already received it via the direct OnKeyPressed call).
        /// </summary>
        private readonly Dictionary<int, DateTime> _bridgeSentTimestamps = new();
        private readonly object _lock = new();
        private const int BridgeDedupMs = 150;

        private Task? _monitorTask;
        private CancellationTokenSource? _cts;

        public UserInputMonitor(ScreenStateMachine sm, IntPtr gameWindow)
        {
            _sm = sm;
            _gameWindow = gameWindow;
        }

        /// <summary>
        /// Call right before sending a key via the bridge. Records the
        /// timestamp so the poller skips it (we already updated SM).
        /// </summary>
        public void MarkBridgeSent(int vk)
        {
            lock (_lock)
            {
                _bridgeSentTimestamps[vk] = DateTime.UtcNow;
            }
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
            ModLogger.Log("[UserInputMonitor] started");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(1));
            _cts?.Dispose();
            _cts = null;
            _monitorTask = null;
        }

        private void MonitorLoop(CancellationToken ct)
        {
            var prev = new Dictionary<int, bool>();
            foreach (var vk in _trackedKeys) prev[vk] = false;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Only track keys when the game window has focus.
                    // Without this, pressing Enter in any other app (e.g.
                    // typing in the terminal) would fire SM transitions.
                    bool gameFocused = GetForegroundWindow() == _gameWindow;

                    foreach (var vk in _trackedKeys)
                    {
                        bool down = (GetAsyncKeyState(vk) & 0x8000) != 0;
                        bool wasDown = prev[vk];
                        prev[vk] = down;

                        if (!down || wasDown) continue;
                        if (!gameFocused) continue;

                        // Rising edge AND game has focus. Decide: bridge or user?
                        bool isBridgeSent;
                        lock (_lock)
                        {
                            isBridgeSent = _bridgeSentTimestamps.TryGetValue(vk, out var t)
                                && (DateTime.UtcNow - t).TotalMilliseconds < BridgeDedupMs;
                        }
                        if (isBridgeSent) continue;

                        // User-driven key. Feed to SM.
                        _sm.OnKeyPressed(vk);
                        _sm.OnKeyPressedForDetectedScreen(vk);
                        ModLogger.LogDebug($"[UserInputMonitor] user key 0x{vk:X2} → SM");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"[UserInputMonitor] {ex.Message}");
                }

                Thread.Sleep(20);
            }
        }
    }
}
