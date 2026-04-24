using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the reverse lookup: live-battle map-id byte (at 0x14077D83C)
    /// → world-map location id. Used when we're inside a battle and need
    /// to render `curLoc=` based on where we actually are, not on the
    /// stale last-known world-map location. Example: user travels to
    /// Lenalian Plateau (locId=29, mapId=77), then immediately walks to
    /// Siedge Weald (locId=26, mapId=74) and enters a random encounter.
    /// _lastWorldMapLocation is stuck at 29 but the live map byte reads 74.
    /// </summary>
    public class BattleMapIdToLocationTests
    {
        [Theory]
        [InlineData(85, 24)] // Mandalia Plain
        [InlineData(72, 25)] // Fovoham Windflats
        [InlineData(74, 26)] // The Siedge Weald — user's live scenario
        [InlineData(75, 27)] // Mount Bervenia
        [InlineData(76, 28)] // Zeklaus Desert
        [InlineData(77, 29)] // Lenalian Plateau
        [InlineData(78, 30)] // Tchigolith Fenlands
        [InlineData(79, 31)] // The Yuguewood
        [InlineData(80, 32)] // Araguay Woods
        [InlineData(81, 33)] // Grogh Heights
        [InlineData(82, 34)] // Beddha Sandwaste
        [InlineData(83, 35)] // Zeirchele Falls
        [InlineData(71, 36)] // Dorvauldar Marsh
        [InlineData(84, 37)] // Balias Tor
        [InlineData(86, 38)] // Dugeura Pass
        [InlineData(87, 39)] // Balias Swale
        [InlineData(88, 40)] // Finnath Creek
        [InlineData(89, 41)] // Lake Poescas
        [InlineData(90, 42)] // Mount Germinas
        public void KnownMapIds_ResolveToExpectedLocation(int mapId, int expectedLocId)
        {
            Assert.Equal(expectedLocId, BattleMapIdToLocation.TryResolve(mapId));
        }

        [Fact]
        public void UnknownMapId_ReturnsMinusOne()
        {
            Assert.Equal(-1, BattleMapIdToLocation.TryResolve(200));
            Assert.Equal(-1, BattleMapIdToLocation.TryResolve(0));
            Assert.Equal(-1, BattleMapIdToLocation.TryResolve(-5));
        }

        [Fact]
        public void StoryBattleMapIds_NotMapped_ReturnsMinusOne()
        {
            // Story battle maps (e.g. Gariland=MAP001, Orbonne=MAP000) are
            // not in the random-encounter table. Caller should fall back
            // to _lastWorldMapLocation or another resolver.
            Assert.Equal(-1, BattleMapIdToLocation.TryResolve(1));
            Assert.Equal(-1, BattleMapIdToLocation.TryResolve(0));
        }
    }
}
