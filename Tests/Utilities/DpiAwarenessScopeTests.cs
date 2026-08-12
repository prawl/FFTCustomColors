using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.Utilities
{
    public class DpiAwarenessScopeTests
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        private static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [Fact]
        public void Constructor_SetsThreadContextToAnUnawareVariant()
        {
            // The xUnit host process is already DPI-unaware, so GetThreadDpiAwarenessContext()
            // equals DPI_AWARENESS_CONTEXT_UNAWARE before the scope even runs. Pin the thread to
            // a non-unaware context first so the assertion below is a real observation of the
            // constructor's effect, not a pre-existing coincidence.
            var pinPrevious = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            Assert.True(pinPrevious != IntPtr.Zero, "precondition: could not pin the test thread to PerMonitorV2; the vacuity guard cannot run");

            try
            {
                using (var scope = new DpiAwarenessScope())
                {
                    Assert.NotEqual(DpiScopeResult.None, scope.AppliedContext);
                    Assert.True(
                        AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), DpiAwarenessScope.DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED) ||
                        AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), DpiAwarenessScope.DPI_AWARENESS_CONTEXT_UNAWARE),
                        "thread DPI awareness context should be one of the unaware variants while the scope is active");
                }
            }
            finally
            {
                SetThreadDpiAwarenessContext(pinPrevious);
            }
        }

        [Fact]
        public void Dispose_RestoresPreviousContext()
        {
            var before = GetThreadDpiAwarenessContext();

            var scope = new DpiAwarenessScope();
            scope.Dispose();

            Assert.True(AreDpiAwarenessContextsEqual(before, GetThreadDpiAwarenessContext()));
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var before = GetThreadDpiAwarenessContext();

            var scope = new DpiAwarenessScope();
            scope.Dispose();
            var exception = Record.Exception(() => scope.Dispose());

            Assert.Null(exception);
            Assert.True(AreDpiAwarenessContextsEqual(before, GetThreadDpiAwarenessContext()));
        }

        [Fact]
        public void MissingApi_YieldsNoneAndNeverThrows()
        {
            var scope = new DpiAwarenessScope(_ => throw new EntryPointNotFoundException());

            Assert.Equal(DpiScopeResult.None, scope.AppliedContext);
            var exception = Record.Exception(() => scope.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public void GdiScaledRejected_FallsBackToUnaware_AndDisposeRestoresThroughSeam()
        {
            var calls = new List<IntPtr>();
            IntPtr SetContext(IntPtr context)
            {
                calls.Add(context);
                if (context == DpiAwarenessScope.DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED)
                    return IntPtr.Zero;
                if (context == DpiAwarenessScope.DPI_AWARENESS_CONTEXT_UNAWARE)
                    return new IntPtr(1234);
                return IntPtr.Zero;
            }

            var scope = new DpiAwarenessScope(SetContext);
            Assert.Equal(DpiScopeResult.Unaware, scope.AppliedContext);

            scope.Dispose();

            Assert.Equal(new IntPtr(1234), calls[^1]);
        }
    }
}
