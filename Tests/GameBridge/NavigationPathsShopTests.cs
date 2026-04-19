using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// ValidPaths coverage for the shop flow (LocationMenu → Outfitter/Tavern/
    /// WarriorsGuild/PoachersDen → Outfitter_Buy / Outfitter_Sell / Outfitter_Fitting).
    /// Each screen needs a set of named actions so Claude can drive the UI
    /// without memorising key sequences.
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

        [Theory]
        [InlineData("Outfitter")]
        [InlineData("Tavern")]
        // WarriorsGuild removed session 47 — it's a single-item menu (Recruit
        // only at Bervenia); CursorUp/Down are no-ops there. See
        // NavigationPathsWarriorsGuildTests for its specific surface.
        [InlineData("PoachersDen")]
        [InlineData("SaveGame")]
        public void ShopInterior_ExposesSubActionActions(string shopName)
        {
            // Inside the shop at the sub-action menu (cursor on Buy/Sell/Fitting
            // or equivalent, not yet Entered). All shop-interior screens share
            // the same helper paths.
            var paths = NavigationPaths.GetPaths(MakeScreen(shopName));

            Assert.NotNull(paths);
            Assert.Contains("Select", paths!.Keys);
            Assert.Contains("Leave", paths.Keys);
            Assert.Contains("CursorUp", paths.Keys);
            Assert.Contains("CursorDown", paths.Keys);
        }

        [Fact]
        public void Outfitter_Leave_DismissesFarewellDialog()
        {
            // Leaving a shop shows a "Come back anytime" dialog that requires a
            // second Enter press to actually return to LocationMenu. Escape alone
            // just opens the dialog.
            var paths = NavigationPaths.GetPaths(MakeScreen("Outfitter"));
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
            var paths = NavigationPaths.GetPaths(MakeScreen("OutfitterBuy"));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);
        }

        [Fact]
        public void OutfitterSell_ExposesItemListActions()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("OutfitterSell"));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);
        }

        [Fact]
        public void OutfitterFitting_ExposesPickerActions()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("OutfitterFitting"));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);
        }

        [Fact]
        public void SaveGameMenu_ExposesSlotPickerActions()
        {
            // Save slot picker scaffold — detection not yet wired up (pending
            // shopTypeIndex=4 verification at Warjilis). ValidPaths present so
            // once detection lands Claude has named actions.
            var paths = NavigationPaths.GetPaths(MakeScreen("SaveGame_Menu"));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);
        }

        [Fact]
        public void ShopConfirmDialog_ExposesYesNoActions()
        {
            // Yes/No modal shown after confirming quantity inside
            // Outfitter_Buy/Sell. ValidPaths scaffold so once the modal is
            // actually detected (pending memory scan), Claude has named
            // Confirm/Cancel actions instead of having to guess Enter vs
            // Escape semantics.
            var paths = NavigationPaths.GetPaths(MakeScreen("ShopConfirmDialog"));

            Assert.NotNull(paths);
            Assert.Contains("Confirm", paths!.Keys);
            Assert.Contains("Cancel", paths.Keys);
            Assert.Contains("CursorLeft", paths.Keys);
            Assert.Contains("CursorRight", paths.Keys);
        }
    }
}
