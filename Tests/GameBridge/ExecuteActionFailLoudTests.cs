using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// execute_action must fail LOUD on unknown action names. The bridge
    /// returns status="failed" with an error message naming the unknown
    /// action AND listing available actions on the current screen. This
    /// test characterises the lookup slice CommandWatcher.ExecuteValidPath
    /// uses, ensuring future refactors keep the fail-loud contract.
    ///
    /// Slice under test:
    ///   var paths = NavigationPaths.GetPaths(screen);
    ///   if (paths == null || !paths.TryGetValue(pathName, out _)) { error }
    /// </summary>
    public class ExecuteActionFailLoudTests
    {
        private static DetectedScreen MakeScreen(string name) => new() { Name = name };

        [Fact]
        public void UnknownAction_OnTavernRumors_IsNotInPathsDictionary()
        {
            // The handoff-documented bug case: `execute_action Leave` silently
            // dropped. Now 'Leave' is a valid alias; use a genuinely unknown
            // name to verify the fail-loud path.
            var paths = NavigationPaths.GetPaths(MakeScreen("TavernRumors"));
            Assert.NotNull(paths);
            Assert.False(paths!.ContainsKey("DoTheThing"));
        }

        [Fact]
        public void AvailableActions_AreEnumerable_ForErrorFormatting()
        {
            // The error message format is:
            //   $"No path '{pathName}' on screen '{screen.Name}'. Available: {available}"
            // where `available = string.Join(", ", paths.Keys)`. This test
            // pins the enumeration shape so the error message stays useful.
            var paths = NavigationPaths.GetPaths(MakeScreen("TavernRumors"));
            Assert.NotNull(paths);

            var joined = string.Join(", ", paths!.Keys);
            Assert.Contains("ScrollUp", joined);
            Assert.Contains("ScrollDown", joined);
            Assert.Contains("Back", joined);
            Assert.Contains("Leave", joined); // alias from session 47
        }

        [Fact]
        public void UnknownScreen_ReturnsNullPaths()
        {
            // The 'paths == null' branch fires when the screen name has no
            // switch case. ExecuteValidPath reports "Available: none" in
            // this case.
            var paths = NavigationPaths.GetPaths(MakeScreen("DefinitelyNotARealScreen"));
            Assert.Null(paths);
        }

        [Fact]
        public void LocationMenu_Leave_ExistsAlready_NotShadowedByAlias()
        {
            // LocationMenu defines its own 'Leave' via GetLocationMenuPaths.
            // The post-processing 'Leave←Back' alias must not fire here.
            var paths = NavigationPaths.GetPaths(MakeScreen("LocationMenu"));
            Assert.NotNull(paths);
            Assert.Contains("Leave", paths!.Keys);
        }
    }
}
