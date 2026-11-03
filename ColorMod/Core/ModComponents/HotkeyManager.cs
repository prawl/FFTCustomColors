using System;
using System.Collections.Generic;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Core.ModComponents
{
    /// <summary>
    /// Manages hotkey processing and actions.
    /// </summary>
    public class HotkeyManager
    {
        private readonly Dictionary<int, Action> _hotkeyActions;
        private readonly Action _openConfigUI;

        // Virtual key codes for hotkeys
        private const int VK_F1 = 0x70; // F1

        public HotkeyManager(
            IInputSimulator? inputSimulator,
            ThemeManager? themeManager,
            Action openConfigUI,
            Action resetColors)
        {
            _openConfigUI = openConfigUI ?? throw new ArgumentNullException(nameof(openConfigUI));
            ArgumentNullException.ThrowIfNull(resetColors);

            // Initialize hotkey actions
            _hotkeyActions = new Dictionary<int, Action>
            {
                [VK_F1] = () => _openConfigUI()  // F1 opens configuration UI
            };
        }

        /// <summary>
        /// Process a hotkey press
        /// </summary>
        public void ProcessHotkeyPress(int vkCode)
        {
            ModLogger.LogDebug(LogVerb.Startup, $"ProcessHotkeyPress: vkCode={vkCode:X}");

            if (_hotkeyActions.TryGetValue(vkCode, out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    ModLogger.LogError(LogVerb.Startup, $"Error processing hotkey {vkCode:X}: {ex.Message}");
                }
            }
            else
            {
                ModLogger.LogDebug(LogVerb.Startup, $"No action registered for vkCode {vkCode:X}");
            }
        }

        /// <summary>
        /// Register a custom hotkey action
        /// </summary>
        public void RegisterHotkey(int vkCode, Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _hotkeyActions[vkCode] = action;
        }

        /// <summary>
        /// Unregister a hotkey
        /// </summary>
        public void UnregisterHotkey(int vkCode)
        {
            _hotkeyActions.Remove(vkCode);
        }

        /// <summary>
        /// Check if a hotkey is registered
        /// </summary>
        public bool IsHotkeyRegistered(int vkCode)
        {
            return _hotkeyActions.ContainsKey(vkCode);
        }
    }
}
