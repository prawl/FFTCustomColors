using System;

namespace FFTColorCustomizer.Interfaces
{
    public interface IHotkeyHandler
    {
        void StartMonitoring();
        void StopMonitoring();
        void ProcessKey(int vKey);
    }
}
