using System;
using System.Runtime.InteropServices;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// The result of trying to apply a DPI-unaware thread context: which variant stuck,
    /// or None if the platform does not support the API and the scope was a no-op.
    /// </summary>
    public enum DpiScopeResult
    {
        None,
        Unaware,
        UnawareGdiScaled,
    }

    /// <summary>
    /// The configuration UI lays out every container in fixed pixels, but its fonts scale
    /// with the process DPI, so at a display scale above 100% labels and controls clip.
    /// This scope makes the calling thread DPI-unaware for its lifetime so Windows scales
    /// the finished window as a bitmap instead, keeping the fixed-pixel layout intact.
    /// </summary>
    public sealed class DpiAwarenessScope : IDisposable
    {
        public static readonly IntPtr DPI_AWARENESS_CONTEXT_UNAWARE = new IntPtr(-1);
        public static readonly IntPtr DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED = new IntPtr(-5);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        private readonly Func<IntPtr, IntPtr> _setContext;
        private IntPtr _previousContext;
        private bool _disposed;

        public DpiScopeResult AppliedContext { get; }

        public DpiAwarenessScope() : this(SetThreadDpiAwarenessContext)
        {
        }

        public DpiAwarenessScope(Func<IntPtr, IntPtr> setContext)
        {
            _setContext = setContext;
            AppliedContext = DpiScopeResult.None;

            try
            {
                var previous = setContext(DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED);
                if (previous != IntPtr.Zero)
                {
                    _previousContext = previous;
                    AppliedContext = DpiScopeResult.UnawareGdiScaled;
                    return;
                }

                // GDI-scaled variant is unsupported (Win10 1607-1803); fall back to plain unaware.
                previous = setContext(DPI_AWARENESS_CONTEXT_UNAWARE);
                if (previous != IntPtr.Zero)
                {
                    _previousContext = previous;
                    AppliedContext = DpiScopeResult.Unaware;
                }
            }
            catch (EntryPointNotFoundException)
            {
                // Pre-Win10-1607: the API does not exist. Stay a no-op.
            }
            catch (DllNotFoundException)
            {
                // Exotic host without user32.dll. Stay a no-op.
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (AppliedContext == DpiScopeResult.None || _previousContext == IntPtr.Zero)
                return;

            try
            {
                _setContext(_previousContext);
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }
        }
    }
}
