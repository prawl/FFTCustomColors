using FFTColorMod.Interfaces;

namespace FFTColorMod.Utilities
{
    /// <summary>
    /// A no-op implementation of IHotkeyHandler for use in tests.
    /// This prevents actual hotkey monitoring from starting during test execution.
    /// </summary>
    public class NullHotkeyHandler : IHotkeyHandler
    {
        public void StartMonitoring()
        {
            // No-op - don't start monitoring in tests
        }

        public void StopMonitoring()
        {
            // No-op - nothing to stop
        }

        public void ProcessKey(int vKey)
        {
            // No-op - ignore key presses in tests
        }
    }
}