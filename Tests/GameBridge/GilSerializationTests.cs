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
        // Outfitter/Tavern/WarriorsGuild/PoachersDen/SaveGame (shop interiors),
        // OutfitterBuy/OutfitterSell/OutfitterFitting (sub-actions).
        // Everything else (battle states, cutscenes, title, etc.): omitted.

        [Theory]
        [InlineData("WorldMap")]
        [InlineData("PartyMenuUnits")]
        [InlineData("LocationMenu")]
        [InlineData("Outfitter")]
        [InlineData("Tavern")]
        [InlineData("WarriorsGuild")]
        [InlineData("PoachersDen")]
        [InlineData("SaveGame")]
        [InlineData("OutfitterBuy")]
        [InlineData("OutfitterSell")]
        [InlineData("OutfitterFitting")]
        public void ShopGilPolicy_ShouldShowGil_OnShopAdjacentScreens(string screenName)
        {
            Assert.True(ShopGilPolicy.ShouldShowGil(screenName),
                $"{screenName} should expose gil — it's a purchase-decision screen.");
        }

        [Theory]
        [InlineData("BattleMyTurn")]
        [InlineData("BattleMoving")]
        [InlineData("BattleAttacking")]
        [InlineData("BattleFormation")]
        [InlineData("BattleVictory")]
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
                Name = "OutfitterBuy",
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
                Name = "OutfitterBuy",
                ShopListCursorIndex = 0
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"shopListCursorIndex\":0", json);
        }

        [Fact]
        public void DetectedScreen_StockItems_SerializesWithCategoryKeys()
        {
            // Session 55: stockItems populates on OutfitterBuy with the
            // full catalog of registered categories. Shape is a dict
            // keyed by category name ("Weapons"/"Shields"/...) mapping
            // to the ordered stock list. JSON shape is stable because
            // callers (shell, Claude tooling) iterate the dict.
            var screen = new DetectedScreen
            {
                Name = "OutfitterBuy",
                StockItems = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ShopStockItem>>
                {
                    ["Weapons"] = new()
                    {
                        new ShopStockItem { Id = 60, Name = "White Staff", Type = "Staff", BuyPrice = 400 }
                    }
                }
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"stockItems\"", json);
            Assert.Contains("\"Weapons\"", json);
            Assert.Contains("\"White Staff\"", json);
            // ShopStockItem ships with default PascalCase property names
            // (matches the existing shopStock field from session 54).
            // Callers depend on that shape — don't silently change it.
            Assert.Contains("\"BuyPrice\":400", json);
        }

        [Fact]
        public void DetectedScreen_StockItems_OmittedWhenNull()
        {
            // Non-shop screens must not carry a stockItems field at
            // all — keeps the JSON response lean and makes the
            // "decodable stock available" signal clearer when the
            // field IS present.
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                StockItems = null
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.DoesNotContain("stockItems", json);
        }
    }
}
