using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FFTColorMod.Utilities
{
public class HotkeyHandler
{
    private const int VK_F1 = 0x70;
    private const int VK_F2 = 0x71;
    private const int VK_C = 0x43;
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
        return vKey == VK_F1 || vKey == VK_F2 || vKey == VK_C;
    }

    private void MonitorHotkeys(CancellationToken cancellationToken)
    {
        ModLogger.Log("Starting hotkey monitoring loop...");
        bool wasF1Pressed = false;
        bool wasF2Pressed = false;
        bool wasCPressed = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                short f1State = GetAsyncKeyState(VK_F1);
                bool isF1Pressed = (f1State & 0x8000) != 0;

                short f2State = GetAsyncKeyState(VK_F2);
                bool isF2Pressed = (f2State & 0x8000) != 0;

                short cState = GetAsyncKeyState(VK_C);
                bool isCPressed = (cState & 0x8000) != 0;

                if (isF1Pressed && !wasF1Pressed)
                {
                    ModLogger.Log("F1 pressed - cycling colors backward");
                    _onHotkeyPressed(VK_F1);
                }

                if (isF2Pressed && !wasF2Pressed)
                {
                    ModLogger.Log("F2 pressed - cycling colors forward");
                    _onHotkeyPressed(VK_F2);
                }

                if (isCPressed && !wasCPressed)
                {
                    ModLogger.Log("C pressed - opening configuration");
                    _onHotkeyPressed(VK_C);
                }

                wasF1Pressed = isF1Pressed;
                wasF2Pressed = isF2Pressed;
                wasCPressed = isCPressed;

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