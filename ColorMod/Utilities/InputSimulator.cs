using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

namespace FFTColorMod.Utilities
{
    public interface IInputSimulator
    {
        bool SendKeyPress(int vkCode);
        bool SimulateMenuRefresh();
    }

    public class InputSimulator : IInputSimulator
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int VK_RETURN = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;

        public virtual bool SendKeyPress(int vkCode)
        {
            Console.WriteLine($"[InputSimulator] SendKeyPress called with vkCode: 0x{vkCode:X2}");

            // Try PostMessage first (works better with games)
            IntPtr gameWindow = GetForegroundWindow();
            if (gameWindow != IntPtr.Zero)
            {
                Console.WriteLine($"[InputSimulator] Found game window: {gameWindow}");

                // Check if it's the FFT process
                uint processId;
                GetWindowThreadProcessId(gameWindow, out processId);
                var currentProcess = Process.GetCurrentProcess();

                if (processId == currentProcess.Id)
                {
                    Console.WriteLine("[InputSimulator] Using PostMessage for FFT window");

                    // Send key down
                    bool downResult = PostMessage(gameWindow, WM_KEYDOWN, new IntPtr(vkCode), IntPtr.Zero);
                    Thread.Sleep(50); // Delay between down and up to ensure key press is registered

                    // Send key up
                    bool upResult = PostMessage(gameWindow, WM_KEYUP, new IntPtr(vkCode), IntPtr.Zero);

                    Console.WriteLine($"[InputSimulator] PostMessage results - Down: {downResult}, Up: {upResult}");
                    return downResult && upResult;
                }
            }

            // Fall back to SendInput if PostMessage doesn't work
            Console.WriteLine("[InputSimulator] Falling back to SendInput");
            var inputs = new INPUT[2];

            // Key down
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vkCode,
                        dwFlags = 0
                    }
                }
            };

            // Key up
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vkCode,
                        dwFlags = KEYEVENTF_KEYUP
                    }
                }
            };

            uint result = SendInput(2, inputs, Marshal.SizeOf<INPUT>());
            Console.WriteLine($"[InputSimulator] SendInput returned: {result} (expected 2)");
            return result == 2;
        }

        public bool SimulateMenuRefresh()
        {
            Console.WriteLine("[InputSimulator] SimulateMenuRefresh called");

            // Send Enter key
            Console.WriteLine("[InputSimulator] Sending Enter key...");
            bool enterResult = SendKeyPress(VK_RETURN);
            Console.WriteLine($"[InputSimulator] Enter key result: {enterResult}");
            if (!enterResult) return false;

            // Longer delay to ensure menu fully transitions
            Console.WriteLine("[InputSimulator] Waiting 500ms for menu transition...");
            Thread.Sleep(500);

            // Send Escape key
            Console.WriteLine("[InputSimulator] Sending Escape key...");
            bool escapeResult = SendKeyPress(VK_ESCAPE);
            Console.WriteLine($"[InputSimulator] Escape key result: {escapeResult}");

            return escapeResult;
        }
    }
}