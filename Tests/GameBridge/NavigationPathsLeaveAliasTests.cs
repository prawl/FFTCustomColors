using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// 'Leave' is a UX alias for 'Back' on exit-only screens (TavernRumors,
    /// TavernErrands, equipment/ability pickers, picker sub-screens). Users
    /// reflexively type `execute_action Leave` on any shop-adjacent screen;
    /// without the alias the bridge fails with "No path 'Leave'...". Since
    /// every one of these screens exits the same way (Escape), exposing
    /// 'Leave' as a pass-through to 'Back' is a pure UX compat shim.
    ///
    /// The alias is added via post-processing in GetPaths so every screen
    /// that defines 'Back' automatically exposes 'Leave' too, without
    /// touching each individual path helper.
    /// </summary>
    public class NavigationPathsLeaveAliasTests
    {
        private static DetectedScreen MakeScreen(string name) => new() { Name = name };

        [Theory]
        [InlineData("TavernRumors")]
        [InlineData("TavernErrands")]
        [InlineData("EquippableWeapons")]
        [InlineData("EquippableShields")]
        [InlineData("EquippableHeadware")]
        [InlineData("EquippableCombatGarb")]
        [InlineData("EquippableAccessories")]
        [InlineData("ReactionAbilities")]
        [InlineData("SupportAbilities")]
        [InlineData("MovementAbilities")]
        [InlineData("SecondaryAbilities")]
        public void Leave_Alias_Present_When_Back_Exists(string screenName)
        {
            var paths = NavigationPaths.GetPaths(MakeScreen(screenName));
            Assert.NotNull(paths);

            // If the screen defines 'Back', it should also expose 'Leave' as
            // a compat alias pointing at the same PathEntry. Note: not every
            // screen in this list uses 'Back' vs 'Cancel' — this test covers
            // the subset that does.
            if (paths!.TryGetValue("Back", out var backEntry))
            {
                Assert.True(paths.ContainsKey("Leave"),
                    $"Screen '{screenName}' has 'Back' but not 'Leave' alias");
                Assert.Same(backEntry, paths["Leave"]);
            }
        }

        [Fact]
        public void TavernRumors_Leave_IsEscape()
        {
            // The handoff-documented case: `execute_action Leave` on
            // TavernRumors used to silently fail because the path was named
            // 'Back'. Alias makes both names reach the same Escape keypress.
            var paths = NavigationPaths.GetPaths(MakeScreen("TavernRumors"));
            Assert.NotNull(paths);
            Assert.True(paths!.ContainsKey("Leave"));

            var leave = paths["Leave"];
            Assert.NotNull(leave.Keys);
            Assert.Single(leave.Keys!);
            Assert.Equal(0x1B, leave.Keys![0].Vk); // VK_ESCAPE
        }

        [Fact]
        public void TavernErrands_Leave_IsEscape()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("TavernErrands"));
            Assert.NotNull(paths);
            Assert.True(paths!.ContainsKey("Leave"));

            var leave = paths["Leave"];
            Assert.NotNull(leave.Keys);
            Assert.Single(leave.Keys!);
            Assert.Equal(0x1B, leave.Keys![0].Vk);
        }

        [Fact]
        public void ExistingLeave_NotOverwritten_By_Alias()
        {
            // Outfitter has its own 'Leave' (Escape + Enter for the farewell
            // dialog). The auto-alias must NOT clobber that — if the screen
            // already defines 'Leave', keep it.
            var paths = NavigationPaths.GetPaths(MakeScreen("Outfitter"));
            Assert.NotNull(paths);

            var leave = paths!["Leave"];
            Assert.NotNull(leave.Keys);
            // Outfitter's Leave is 2 keys (Escape + Enter), not 1.
            Assert.Equal(2, leave.Keys!.Length);
        }
    }
}
