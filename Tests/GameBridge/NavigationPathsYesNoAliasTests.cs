using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// UX aliases for Yes/No confirmation screens. The game's yes/no modal
    /// has Yes on top — `CursorUp` moves the cursor there and `Select`
    /// commits; the player can also press Escape to cancel (equivalent to
    /// selecting No). Users reflexively type `Yes` / `No` on these screens,
    /// so both names map to the right keypress sequence.
    ///
    /// Yes  = CursorUp + Select (always land on the top choice, then commit)
    /// No   = Cancel (Escape shortcuts "No" on every FFT yes/no prompt)
    ///
    /// Same pattern philosophy as the Leave→Back alias shipped in session 47.
    /// </summary>
    public class NavigationPathsYesNoAliasTests
    {
        private static DetectedScreen Screen(string name) => new() { Name = name };

        [Theory]
        [InlineData("BattleCrystalMoveConfirm")]
        [InlineData("BattleAbilityAcquireConfirm")]
        [InlineData("ShopConfirmDialog")]
        public void YesNoScreens_ExposeYesAlias(string screenName)
        {
            var paths = NavigationPaths.GetPaths(Screen(screenName));
            Assert.NotNull(paths);
            Assert.Contains("Yes", paths!.Keys);
        }

        [Theory]
        [InlineData("BattleCrystalMoveConfirm")]
        [InlineData("BattleAbilityAcquireConfirm")]
        [InlineData("ShopConfirmDialog")]
        public void YesNoScreens_ExposeNoAlias(string screenName)
        {
            var paths = NavigationPaths.GetPaths(Screen(screenName));
            Assert.NotNull(paths);
            Assert.Contains("No", paths!.Keys);
        }

        [Fact]
        public void BattleCrystalMoveConfirm_Yes_CommitsViaUpThenEnter()
        {
            // Yes = land on top (CursorUp) + Select (Enter). Two-key sequence
            // guarantees the cursor is on Yes regardless of default position.
            var paths = NavigationPaths.GetPaths(Screen("BattleCrystalMoveConfirm"));
            var yes = paths!["Yes"];
            Assert.NotNull(yes.Keys);
            Assert.Equal(2, yes.Keys!.Length);
            Assert.Equal(0x26, yes.Keys[0].Vk); // VK_UP
            Assert.Equal(0x0D, yes.Keys[1].Vk); // VK_ENTER
        }

        [Fact]
        public void BattleCrystalMoveConfirm_No_IsEscape()
        {
            var paths = NavigationPaths.GetPaths(Screen("BattleCrystalMoveConfirm"));
            var no = paths!["No"];
            Assert.NotNull(no.Keys);
            Assert.Single(no.Keys!);
            Assert.Equal(0x1B, no.Keys![0].Vk); // VK_ESCAPE
        }

        [Fact]
        public void ShopConfirmDialog_Yes_CommitsPurchase()
        {
            // The purchase confirm is horizontal (Yes=Left, No=Right), NOT
            // vertical like the crystal modals. Yes = CursorLeft + Enter to
            // guarantee cursor placement. Cancel stays Escape.
            var paths = NavigationPaths.GetPaths(Screen("ShopConfirmDialog"));
            var yes = paths!["Yes"];
            Assert.NotNull(yes.Keys);
            Assert.Equal(2, yes.Keys!.Length);
            Assert.Equal(0x25, yes.Keys[0].Vk); // VK_LEFT (Yes is on the left)
            Assert.Equal(0x0D, yes.Keys[1].Vk); // VK_ENTER
        }

        [Fact]
        public void ShopConfirmDialog_No_IsEscape()
        {
            var paths = NavigationPaths.GetPaths(Screen("ShopConfirmDialog"));
            var no = paths!["No"];
            Assert.NotNull(no.Keys);
            Assert.Single(no.Keys!);
            Assert.Equal(0x1B, no.Keys![0].Vk);
        }

        [Fact]
        public void NonYesNoScreens_DoNotLeakYesNoAliases()
        {
            // WorldMap, BattleMyTurn, PartyMenuUnits shouldn't have Yes/No
            // — those aliases are yes/no-modal-specific.
            var paths = NavigationPaths.GetPaths(Screen("BattleMyTurn"));
            Assert.NotNull(paths);
            Assert.DoesNotContain("Yes", paths!.Keys);
            Assert.DoesNotContain("No", paths.Keys);
        }
    }
}
