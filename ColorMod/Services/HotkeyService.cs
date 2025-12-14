using System;
using FFTColorMod.Utilities;

namespace FFTColorMod.Services
{
    public class HotkeyService
    {
        private const int VK_F1 = 0x70;
        private const int VK_F2 = 0x71;
        private const int VK_F3 = 0x72;
        private const int VK_C = 0x43;

        private readonly Action<string> _setColorScheme;
        private readonly Func<string> _cycleNextScheme;
        private readonly Func<string> _cyclePreviousScheme;
        private readonly Action _cycleOrlandeauTheme;
        private readonly Action _cycleAgriasTheme;
        private readonly Action _openConfigUI;
        private readonly IInputSimulator? _inputSimulator;

        public event Action? ConfigUIRequested;

        public HotkeyService(
            Action<string> setColorScheme,
            Func<string> cycleNextScheme,
            Func<string> cyclePreviousScheme,
            Action cycleOrlandeauTheme,
            Action cycleAgriasTheme,
            Action openConfigUI,
            IInputSimulator? inputSimulator)
        {
            _setColorScheme = setColorScheme;
            _cycleNextScheme = cycleNextScheme;
            _cyclePreviousScheme = cyclePreviousScheme;
            _cycleOrlandeauTheme = cycleOrlandeauTheme;
            _cycleAgriasTheme = cycleAgriasTheme;
            _openConfigUI = openConfigUI;
            _inputSimulator = inputSimulator;
        }

        public void ProcessHotkeyPress(int vkCode)
        {
            switch (vkCode)
            {
                case VK_F1:
                    HandleF1Press();
                    break;
                case VK_F2:
                    HandleF2Press();
                    break;
                case VK_F3:
                    HandleF3Press();
                    break;
                case VK_C:
                    HandleCPress();
                    break;
            }
        }

        private void HandleF1Press()
        {
            // Cycle to previous color
            string previousColor = _cyclePreviousScheme();
            ModLogger.Log($"Cycling backward to {previousColor}");
            _setColorScheme(previousColor);
            SimulateMenuRefresh();
        }

        private void HandleF2Press()
        {
            // Cycle forward through color schemes
            string nextColor = _cycleNextScheme();
            Console.WriteLine("================================================");
            Console.WriteLine($"    GENERIC THEME CHANGED TO: {nextColor}");
            Console.WriteLine("================================================");
            _setColorScheme(nextColor);

            // Also cycle story character themes
            _cycleOrlandeauTheme();
            _cycleAgriasTheme();

            SimulateMenuRefresh();
        }

        private void HandleF3Press()
        {
            ModLogger.Log("Opening configuration UI (F3)");
            _openConfigUI();
        }

        private void HandleCPress()
        {
            ModLogger.Log("Opening configuration UI (C)");

            // If there are test event handlers, just invoke them
            if (ConfigUIRequested != null)
            {
                ConfigUIRequested.Invoke();
            }
            else
            {
                _openConfigUI();
            }
        }

        private void SimulateMenuRefresh()
        {
            if (_inputSimulator != null)
            {
                ModLogger.Log("Calling SimulateMenuRefresh...");
                bool result = _inputSimulator.SimulateMenuRefresh();
                ModLogger.Log($"SimulateMenuRefresh returned: {result}");
            }
            else
            {
                ModLogger.LogWarning("InputSimulator is null!");
            }
        }
    }
}