using System;
using FFTColorMod.Utilities;

namespace FFTColorMod.Services
{
    public class HotkeyService
    {
        private const int VK_F1 = 0x70;

        private readonly Action<string> _setColorScheme;
        private readonly Func<string> _cycleNextScheme;
        private readonly Func<string> _cyclePreviousScheme;
        private readonly Action _cycleOrlandeauTheme;
        private readonly Action _cycleAgriasTheme;
        private readonly Action _cycleCloudTheme;
        private readonly Action _openConfigUI;
        private readonly IInputSimulator? _inputSimulator;

        public event Action? ConfigUIRequested;

        public HotkeyService(
            Action<string> setColorScheme,
            Func<string> cycleNextScheme,
            Func<string> cyclePreviousScheme,
            Action cycleOrlandeauTheme,
            Action cycleAgriasTheme,
            Action cycleCloudTheme,
            Action openConfigUI,
            IInputSimulator? inputSimulator)
        {
            _setColorScheme = setColorScheme;
            _cycleNextScheme = cycleNextScheme;
            _cyclePreviousScheme = cyclePreviousScheme;
            _cycleOrlandeauTheme = cycleOrlandeauTheme;
            _cycleAgriasTheme = cycleAgriasTheme;
            _cycleCloudTheme = cycleCloudTheme;
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
            }
        }

        private void HandleF1Press()
        {
            ModLogger.Log("Opening configuration UI (F1)");

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