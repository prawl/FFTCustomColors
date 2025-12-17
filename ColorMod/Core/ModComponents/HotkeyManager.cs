using System;
using System.Collections.Generic;
using FFTColorMod.Services;
using FFTColorMod.Utilities;

namespace FFTColorMod.Core.ModComponents
{
    /// <summary>
    /// Manages hotkey processing and actions
    /// </summary>
    public class HotkeyManager
    {
        private readonly IInputSimulator? _inputSimulator;
        private readonly Dictionary<int, Action> _hotkeyActions;
        private readonly ThemeManager? _themeManager;
        private readonly Action _openConfigUI;
        private readonly Action _resetColors;

        // Virtual key codes for hotkeys
        private const int VK_F1 = 0x70; // F1
        private const int VK_F2 = 0x71; // F2
        private const int VK_F3 = 0x72; // F3
        private const int VK_F4 = 0x73; // F4
        private const int VK_F5 = 0x74; // F5
        private const int VK_F6 = 0x75; // F6
        private const int VK_F7 = 0x76; // F7
        private const int VK_F8 = 0x77; // F8
        private const int VK_F9 = 0x78; // F9
        private const int VK_F10 = 0x79; // F10
        private const int VK_F11 = 0x7A; // F11
        private const int VK_F12 = 0x7B; // F12

        public HotkeyManager(
            IInputSimulator? inputSimulator,
            ThemeManager? themeManager,
            Action openConfigUI,
            Action resetColors)
        {
            _inputSimulator = inputSimulator;
            _themeManager = themeManager;
            _openConfigUI = openConfigUI ?? throw new ArgumentNullException(nameof(openConfigUI));
            _resetColors = resetColors ?? throw new ArgumentNullException(nameof(resetColors));

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
            ModLogger.Log($"ProcessHotkeyPress: vkCode={vkCode:X}");

            if (_hotkeyActions.TryGetValue(vkCode, out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"Error processing hotkey {vkCode:X}: {ex.Message}");
                }
            }
            else
            {
                ModLogger.Log($"No action registered for vkCode {vkCode:X}");
            }
        }

        private void CycleMustadioTheme()
        {
            if (_themeManager != null)
            {
                var storyManager = _themeManager.GetStoryCharacterManager();
                if (storyManager != null)
                {
                    var nextTheme = storyManager.CycleTheme("Mustadio");
                    ModLogger.Log($"Mustadio theme cycled to: {nextTheme}");
                }
            }
        }

        private void CycleOrlandeauTheme()
        {
            if (_themeManager != null)
            {
                _themeManager.CycleOrlandeauTheme();
            }
        }

        private void CycleAgriasTheme()
        {
            if (_themeManager != null)
            {
                _themeManager.CycleAgriasTheme();
            }
        }

        private void CycleCloudTheme()
        {
            if (_themeManager != null)
            {
                _themeManager.CycleCloudTheme();
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