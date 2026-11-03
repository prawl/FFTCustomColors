using System;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>Routes the F1 hotkey to the color scheme callbacks.</summary>
    public class HotkeyService
    {
        private const int VK_F1 = 0x70;

        private readonly Action _openConfigUI;

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
            _openConfigUI = openConfigUI;
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
            ModLogger.Log(LogVerb.Ui, "Opening configuration UI (F1)");

            // Always call the open config UI action
            _openConfigUI();

            // Also invoke any event handlers if present (for tests)
            ConfigUIRequested?.Invoke();
        }
    }
}
