using System.Runtime.CompilerServices;
using ModGates.Logging;
using Xunit;

// ModLogger.Instance is one process wide static, so a parallel class could swap it mid assertion.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FFTColorCustomizer.Tests.Helpers
{
    /// <summary>Runs once when the test assembly loads and swaps in the logger that swallows everything.</summary>
    internal static class LoggingTestDefaults
    {
        [ModuleInitializer]
        internal static void UseNullLoggerForTheWholeTestRun() => ModLogger.UseNullLogger();
    }
}
