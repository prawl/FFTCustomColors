using System.Runtime.CompilerServices;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Tests.Helpers
{
    /// <summary>
    /// Assembly-wide test default for the static ModLogger, the lesson imported from the
    /// sibling mods' port: xUnit runs test classes in parallel, and any class left writing
    /// through the shared ModLogger.Instance sprays other classes' production logging into
    /// whatever logger a test installed (a CI-only red in the sibling repo). Defaulting the
    /// whole assembly to NullLogger means a test only sees logging it explicitly wired, and
    /// any test that installs a capture logger must lock its writes and assert on snapshots.
    /// </summary>
    internal static class LoggingTestDefaults
    {
        [ModuleInitializer]
        internal static void UseNullLoggerForTheWholeTestRun()
        {
            ModLogger.Instance = NullLogger.Instance;
        }
    }
}
