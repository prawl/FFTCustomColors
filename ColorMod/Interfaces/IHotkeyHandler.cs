using System;

namespace FFTColorMod.Interfaces
{
    public interface IHotkeyHandler
    {
        void StartMonitoring();
        void StopMonitoring();
        void ProcessKey(int vKey);
    }
}