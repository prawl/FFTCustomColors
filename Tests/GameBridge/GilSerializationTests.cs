using System.Text.Json;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Gil is exposed on shop-adjacent and purchase-decision screens so Claude
    /// can see affordability without extra reads. Read from 0x140D39CD0
    /// (u32, static, verified stable). Hidden in battle states to keep responses
    /// lean during combat turns.
    /// </summary>
    public class GilSerializationTests
    {
        [Fact]
        public void DetectedScreen_Gil_SerializesWhenNonZero()
        {
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                Gil = 2469169
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"gil\":2469169", json);
        }

        [Fact]
        public void DetectedScreen_Gil_OmittedWhenZero()
        {
            // A zero-gil read is almost always a failed read (the game has a
            // starting pouch) — better to omit than to surface 0 and mislead.
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                Gil = 0
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.DoesNotContain("\"gil\"", json);
        }

        // ShopGilPolicy decides whether gil should be surfaced on a given screen.
        // Purchase-relevant screens: WorldMap, PartyMenu, LocationMenu,
        // ShopInterior, Outfitter_Buy, Outfitter_Sell, Outfitter_Fitting.
        // Everything else (battle states, cutscenes, title, etc.): omitted.

        [Theory]
        [InlineData("WorldMap")]
        [InlineData("PartyMenu")]
        [InlineData("LocationMenu")]
        [InlineData("ShopInterior")]
        [InlineData("Outfitter_Buy")]
        [InlineData("Outfitter_Sell")]
        [InlineData("Outfitter_Fitting")]
        public void ShopGilPolicy_ShouldShowGil_OnShopAdjacentScreens(string screenName)
        {
            Assert.True(ShopGilPolicy.ShouldShowGil(screenName),
                $"{screenName} should expose gil — it's a purchase-decision screen.");
        }

        [Theory]
        [InlineData("Battle_MyTurn")]
        [InlineData("Battle_Moving")]
        [InlineData("Battle_Attacking")]
        [InlineData("Battle_Formation")]
        [InlineData("Battle_Victory")]
        [InlineData("Cutscene")]
        [InlineData("TitleScreen")]
        [InlineData("TravelList")]
        [InlineData("EncounterDialog")]
        public void ShopGilPolicy_ShouldHideGil_OnNonShopScreens(string screenName)
        {
            Assert.False(ShopGilPolicy.ShouldShowGil(screenName),
                $"{screenName} should NOT expose gil — it's not purchase-relevant.");
        }

        [Fact]
        public void DetectedScreen_ShopListCursorIndex_SerializesWhenPresent()
        {
            // shopListCursorIndex tracks the row the player is highlighting inside
            // Outfitter_Buy/Sell/Fitting. Surfaced so Claude can later resolve
            // ui=<item name> once the stock list decoding is in place.
            var screen = new DetectedScreen
            {
                Name = "Outfitter_Buy",
                ShopListCursorIndex = 3
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"shopListCursorIndex\":3", json);
        }

        [Fact]
        public void DetectedScreen_ShopListCursorIndex_OmittedWhenNull()
        {
            // null = not applicable on this screen. Row 0 is a real value
            // and must serialize (covered by the zero-is-serialized test).
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                ShopListCursorIndex = null
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.DoesNotContain("shopListCursorIndex", json);
        }

        [Fact]
        public void DetectedScreen_ShopListCursorIndex_ZeroIsSerialized()
        {
            // Row 0 is the top of the list — a real value, must not be treated
            // as "absent".
            var screen = new DetectedScreen
            {
                Name = "Outfitter_Buy",
                ShopListCursorIndex = 0
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"shopListCursorIndex\":0", json);
        }
    }
}
