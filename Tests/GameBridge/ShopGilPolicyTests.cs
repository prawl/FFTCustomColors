using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure function: given a screen name, decide whether to surface the player's
    /// gil on the response. Shown on shop/purchase-adjacent screens; hidden during
    /// combat + cutscenes to keep responses lean.
    /// </summary>
    public class ShopGilPolicyTests
    {
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
        public void ShouldShowGil_ReturnsTrueForShopAdjacentScreens(string screen)
        {
            Assert.True(ShopGilPolicy.ShouldShowGil(screen));
        }

        [Theory]
        [InlineData("BattleMyTurn")]
        [InlineData("BattleMoving")]
        [InlineData("BattleAbilities")]
        [InlineData("BattleWaiting")]
        [InlineData("BattleEnemiesTurn")]
        [InlineData("BattleAlliesTurn")]
        [InlineData("BattleFormation")]
        [InlineData("BattleVictory")]
        [InlineData("BattleDesertion")]
        [InlineData("Cutscene")]
        [InlineData("BattleDialogue")]
        [InlineData("EncounterDialog")]
        [InlineData("CharacterStatus")]
        [InlineData("EquipmentAndAbilities")]
        [InlineData("JobSelection")]
        [InlineData("TitleScreen")]
        [InlineData("TravelList")]
        [InlineData("TavernRumors")]
        [InlineData("TavernErrands")]
        public void ShouldShowGil_ReturnsFalseForNonShopScreens(string screen)
        {
            Assert.False(ShopGilPolicy.ShouldShowGil(screen));
        }

        [Fact]
        public void ShouldShowGil_UnknownScreen_ReturnsFalse()
        {
            Assert.False(ShopGilPolicy.ShouldShowGil("SomeNewScreenWeHaventMappedYet"));
        }

        [Fact]
        public void ShouldShowGil_EmptyScreen_ReturnsFalse()
        {
            Assert.False(ShopGilPolicy.ShouldShowGil(""));
        }

        [Fact]
        public void ShouldShowGil_CaseSensitive()
        {
            // Screen names use CamelCase and are matched exactly. Lowercase variants
            // should not match — this pins that contract so a future case-insensitive
            // refactor is a visible breaking change.
            Assert.True(ShopGilPolicy.ShouldShowGil("WorldMap"));
            Assert.False(ShopGilPolicy.ShouldShowGil("worldmap"));
            Assert.False(ShopGilPolicy.ShouldShowGil("WORLDMAP"));
        }
    }
}
