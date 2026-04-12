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
    }
}
