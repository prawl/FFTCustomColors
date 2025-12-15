using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FFTColorMod.Utilities
{
public class HotkeyHandler
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
        ModLogger.Log("Hotkey monitoring started");
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
        // Minimal implementation - just call the callback
        _onHotkeyPressed(vKey);
    }

    public static bool IsKeyMonitored(int vKey)
    {
        return vKey == VK_F1;
    }

    private void MonitorHotkeys(CancellationToken cancellationToken)
    {
        ModLogger.Log("Starting hotkey monitoring loop...");
        bool wasF1Pressed = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                short f1State = GetAsyncKeyState(VK_F1);
                bool isF1Pressed = (f1State & 0x8000) != 0;

                if (isF1Pressed && !wasF1Pressed)
                {
                    ModLogger.Log("F1 pressed - opening configuration");
                    _onHotkeyPressed(VK_F1);
                }

                wasF1Pressed = isF1Pressed;

                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"in hotkey loop: {ex.Message}");
            }
        }
        ModLogger.Log("Hotkey monitoring stopped");
    }
}
}