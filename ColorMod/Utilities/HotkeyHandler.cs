using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FFTColorMod.Utilities;

public class HotkeyHandler
{
    private const int VK_F1 = 0x70;
    private const int VK_F2 = 0x71;
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
        Console.WriteLine("[FFT Color Mod] Hotkey monitoring started");
    }

    public void StopMonitoring()
    {
        _cancellationTokenSource?.Cancel();
        _monitorTask?.Wait(TimeSpan.FromSeconds(1));
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _monitorTask = null;
    }

    private void MonitorHotkeys(CancellationToken cancellationToken)
    {
        Console.WriteLine("[FFT Color Mod] Starting hotkey monitoring loop...");
        bool wasF1Pressed = false;
        bool wasF2Pressed = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                short f1State = GetAsyncKeyState(VK_F1);
                bool isF1Pressed = (f1State & 0x8000) != 0;

                short f2State = GetAsyncKeyState(VK_F2);
                bool isF2Pressed = (f2State & 0x8000) != 0;

                if (isF1Pressed && !wasF1Pressed)
                {
                    Console.WriteLine("[FFT Color Mod] F1 pressed - cycling colors backward");
                    _onHotkeyPressed(VK_F1);
                }

                if (isF2Pressed && !wasF2Pressed)
                {
                    Console.WriteLine("[FFT Color Mod] F2 pressed - cycling colors forward");
                    _onHotkeyPressed(VK_F2);
                }

                wasF1Pressed = isF1Pressed;
                wasF2Pressed = isF2Pressed;

                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FFT Color Mod] Error in hotkey loop: {ex.Message}");
            }
        }
        Console.WriteLine("[FFT Color Mod] Hotkey monitoring stopped");
    }
}