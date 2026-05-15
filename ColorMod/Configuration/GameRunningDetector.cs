using System;
using System.Diagnostics;
using System.Linq;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Detects whether one of FFT's supported executables is currently running.
    /// Used to gate the Reloaded-II "Configure Mod" button — applying themes when
    /// the game isn't live just produces a stale config that won't be visible
    /// until the next launch, which surprises users.
    /// </summary>
    public static class GameRunningDetector
    {
        /// <summary>
        /// Process names (no .exe) for FFT executables we mod.
        /// Mirrors the "SupportedAppId" list in ModConfig.json.
        /// </summary>
        public static readonly string[] SupportedProcessNames = new[]
        {
            "fft_enhanced",
            "fft_classic"
        };

        /// <summary>
        /// Returns true if any of the supported FFT executables is currently
        /// running on the system.
        /// </summary>
        public static bool IsGameRunning()
        {
            return IsGameRunning(name => Process.GetProcessesByName(name).Length > 0);
        }

        /// <summary>
        /// Test seam: pass a custom probe to decide if a given process name is live.
        /// </summary>
        public static bool IsGameRunning(Func<string, bool> processProbe)
        {
            if (processProbe == null) return false;
            return SupportedProcessNames.Any(processProbe);
        }
    }
}
