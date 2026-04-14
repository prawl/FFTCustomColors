using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// ValidPaths coverage for the shop flow (LocationMenu → ShopInterior →
    /// Outfitter_Buy / Outfitter_Sell / Outfitter_Fitting). Each screen needs
    /// a set of named actions so Claude can drive the UI without memorising
    /// key sequences.
    /// </summary>
    public class NavigationPathsShopTests
    {
        private static DetectedScreen MakeScreen(string name) =>
            new() { Name = name };

        [Fact]
        public void LocationMenu_ExposesShopEntryActions()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("LocationMenu"));

            Assert.NotNull(paths);
            Assert.Contains("EnterShop", paths!.Keys);
            Assert.Contains("Leave", paths.Keys);
            Assert.Contains("CursorUp", paths.Keys);
            Assert.Contains("CursorDown", paths.Keys);
        }

        [Fact]
        public void ShopInterior_ExposesSubActionActions()
        {
            // Inside the Outfitter/Tavern/etc. at the shop menu (cursor on
            // Buy/Sell/Fitting but not yet Entered).
            var paths = NavigationPaths.GetPaths(MakeScreen("ShopInterior"));

            Assert.NotNull(paths);
            Assert.Contains("Select", paths!.Keys);
            Assert.Contains("Leave", paths.Keys);
            Assert.Contains("CursorUp", paths.Keys);
            Assert.Contains("CursorDown", paths.Keys);
        }

        [Fact]
        public void ShopInterior_Leave_DismissesFarewellDialog()
        {
            // Leaving a shop shows a "Come back anytime" dialog that requires a
            // second Enter press to actually return to LocationMenu. Escape alone
            // just opens the dialog.
            var paths = NavigationPaths.GetPaths(MakeScreen("ShopInterior"));
            var leave = paths!["Leave"];

            Assert.NotNull(leave.Keys);
            Assert.Equal(2, leave.Keys!.Length);
            // First: Escape (opens farewell dialog). Second: Enter (dismiss).
            Assert.Equal(0x1B, leave.Keys[0].Vk);
            Assert.Equal(0x0D, leave.Keys[1].Vk);
        }

        [Fact]
        public void OutfitterBuy_ExposesItemListActions()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("Outfitter_Buy"));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);
        }

        [Fact]
        public void OutfitterSell_ExposesItemListActions()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("Outfitter_Sell"));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);
        }

        [Fact]
        public void OutfitterFitting_ExposesPickerActions()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("Outfitter_Fitting"));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);
        }
    }
}
