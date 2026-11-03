using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FFTColorCustomizer.Interfaces;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>Polls the F1 key on a background task and fires the callback.</summary>
    public class HotkeyHandler : IHotkeyHandler
    {
        private const int VK_F1 = 0x70;
        private readonly Action<int> _onHotkeyPressed;
        private Task? _monitorTask;
        private CancellationTokenSource? _cancellationTokenSource;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public HotkeyHandler(Action<int> onHotkeyPressed)
        {
            _onHotkeyPressed = onHotkeyPressed;
        }

        public void StartMonitoring()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorHotkeys(_cancellationTokenSource.Token));
            ModLogger.Log(LogVerb.Startup, "Hotkey monitoring started");
        }

        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(1));
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _monitorTask = null;
        }

        public void ProcessKey(int vKey)
        {
            // Minimal implementation, just call the callback
            _onHotkeyPressed(vKey);
        }

        public static bool IsKeyMonitored(int vKey)
        {
            return vKey == VK_F1;
        }

        private void MonitorHotkeys(CancellationToken cancellationToken)
        {
            ModLogger.LogDebug(LogVerb.Startup, "Starting hotkey monitoring loop...");
            bool wasF1Pressed = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    short f1State = GetAsyncKeyState(VK_F1);
                    bool isF1Pressed = (f1State & 0x8000) != 0;

                    if (isF1Pressed && !wasF1Pressed)
                    {
                        ModLogger.LogDebug(LogVerb.Ui, "F1 pressed - opening configuration");
                        _onHotkeyPressed(VK_F1);
                    }

                    wasF1Pressed = isF1Pressed;

                    Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    ModLogger.LogError(LogVerb.Startup, $"The hotkey loop failed: {ex.Message}");
                }
            }
            ModLogger.LogDebug(LogVerb.Startup, "Hotkey monitoring stopped");
        }
    }
}
