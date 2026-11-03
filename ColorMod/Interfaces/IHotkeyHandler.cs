using System;

namespace FFTColorCustomizer.Interfaces
{
    /// <summary>Starts and stops hotkey monitoring and receives key presses.</summary>
    public interface IHotkeyHandler
    {
        void StartMonitoring();
        void StopMonitoring();
        void ProcessKey(int vKey);
    }
}
