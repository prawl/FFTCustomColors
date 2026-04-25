using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class UnitNameCacheTests
    {
        [Fact]
        public void Cache_StoresNameByPosition()
        {
            var cache = new UnitNameCache();
            cache.Set(5, 4, "Skeleton");

            Assert.Equal("Skeleton", cache.Get(5, 4));
        }

        [Fact]
        public void Cache_ReturnsNullForUnknownPosition()
        {
            var cache = new UnitNameCache();

            Assert.Null(cache.Get(5, 4));
        }

        [Fact]
        public void Cache_UpdatesWhenUnitMoves()
        {
            var cache = new UnitNameCache();
            cache.Set(5, 4, "Skeleton");
            cache.Move(5, 4, 6, 5);

            Assert.Null(cache.Get(5, 4));
            Assert.Equal("Skeleton", cache.Get(6, 5));
        }

        [Fact]
        public void Cache_ClearsAll()
        {
            var cache = new UnitNameCache();
            cache.Set(5, 4, "Skeleton");
            cache.Set(7, 2, "Black Goblin");
            cache.Clear();

            Assert.Null(cache.Get(5, 4));
            Assert.Null(cache.Get(7, 2));
        }

        [Fact]
        public void Cache_OverwritesExistingName()
        {
            var cache = new UnitNameCache();
            cache.Set(5, 4, "Skeleton");
            cache.Set(5, 4, "Revenant");

            Assert.Equal("Revenant", cache.Get(5, 4));
        }

        [Fact]
        public void StatsKey_LookupAfterMove_RecoversName()
        {
            // Live-flagged 2026-04-25 playtest: enemy moved between scans
            // and the (x,y)-keyed cache missed at the new tile, leaving
            // them as `(?)`. Stats-keyed fallback (maxHp, level, team)
            // recovers the name.
            var cache = new UnitNameCache();
            cache.Set(x: 5, y: 4, maxHp: 426, level: 95, team: 1, name: "Black Goblin");

            // Unit moved to a new tile, position lookup misses
            Assert.Null(cache.Get(8, 7));
            // Stats lookup finds it
            Assert.Equal("Black Goblin", cache.GetByStats(maxHp: 426, level: 95, team: 1));
        }

        [Fact]
        public void StatsKey_LookupAfterDeath_StillResolves()
        {
            // Dead unit's heap fingerprint search fails (struct deallocated/
            // zeroed). Stats key (which uses MaxHp not Hp) still matches
            // the original fingerprint cached when the unit was alive.
            var cache = new UnitNameCache();
            cache.Set(x: 5, y: 4, maxHp: 426, level: 95, team: 1, name: "Black Goblin");

            // Unit died — same maxHp, level, team
            Assert.Equal("Black Goblin", cache.GetByStats(maxHp: 426, level: 95, team: 1));
        }

        [Fact]
        public void StatsKey_DifferentTeams_AreDistinct()
        {
            // Same MaxHp + Level on different teams (player vs enemy with
            // matching stats) should not collide.
            var cache = new UnitNameCache();
            cache.Set(x: 5, y: 4, maxHp: 200, level: 30, team: 0, name: "Knight");
            cache.Set(x: 8, y: 2, maxHp: 200, level: 30, team: 1, name: "Black Knight");

            Assert.Equal("Knight", cache.GetByStats(200, 30, 0));
            Assert.Equal("Black Knight", cache.GetByStats(200, 30, 1));
        }

        [Fact]
        public void StatsKey_RejectsZeroOrNegativeMaxHp()
        {
            // Set with maxHp=0 should not pollute the stats cache (would
            // collide on any future zeroed read).
            var cache = new UnitNameCache();
            cache.Set(x: 5, y: 4, maxHp: 0, level: 0, team: 1, name: "ShouldNotCache");

            Assert.Null(cache.GetByStats(0, 0, 1));
        }

        [Fact]
        public void Clear_AlsoClearsStatsKey()
        {
            var cache = new UnitNameCache();
            cache.Set(x: 5, y: 4, maxHp: 426, level: 95, team: 1, name: "Black Goblin");
            cache.Clear();

            Assert.Null(cache.GetByStats(426, 95, 1));
        }
    }
}
