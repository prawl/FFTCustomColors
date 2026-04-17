using System.IO;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// HpMpCache: disk-backed per-unit HP/MaxHp/Mp/MaxMp store keyed by roster
    /// slot index + equipment signature. Caches values observed from the
    /// per-hover HoveredUnitArray so we can surface HP/MP for ALL roster units
    /// (not just the ~4 near the cursor), falling back to last-known-good when
    /// a unit isn't in the hovered window.
    ///
    /// Equipment signature invalidation: if the unit's 7 equipment u16s change,
    /// the cached HP/MP may be stale (new gear = new bonuses), so a cache read
    /// for that slot returns null until the next observation.
    /// </summary>
    public class HpMpCacheTests
    {
        private static string MakeTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"HpMpCacheTest_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static int[] Eq(params int[] ids)
        {
            var a = new int[7];
            for (int i = 0; i < a.Length && i < ids.Length; i++) a[i] = ids[i];
            return a;
        }

        [Fact]
        public void Get_ReturnsNullWhenSlotUnset()
        {
            var dir = MakeTempDir();
            var cache = new HpMpCache(dir);
            Assert.Null(cache.Get(0, Eq(1, 2, 3, 4, 5, 6, 7)));
        }

        [Fact]
        public void Set_WritesAndGetReadsBack_WhenEquipmentMatches()
        {
            var dir = MakeTempDir();
            var cache = new HpMpCache(dir);
            var eq = Eq(143, 156, 218, 36, 0xFFFF, 0xFFFF, 10);
            cache.Set(0, eq, hp: 500, maxHp: 719, mp: 100, maxMp: 138);

            var got = cache.Get(0, eq);
            Assert.NotNull(got);
            Assert.Equal(500, got!.Hp);
            Assert.Equal(719, got.MaxHp);
            Assert.Equal(100, got.Mp);
            Assert.Equal(138, got.MaxMp);
        }

        [Fact]
        public void Get_ReturnsNullWhenEquipmentChanged()
        {
            // Equipment change invalidates cached MaxHp/MaxMp (HP bonuses
            // differ). Caller should omit HP/MP until next live observation.
            var dir = MakeTempDir();
            var cache = new HpMpCache(dir);
            var eqOld = Eq(143, 156, 218, 36, 0, 0, 10);
            cache.Set(0, eqOld, hp: 719, maxHp: 719, mp: 138, maxMp: 138);

            var eqNew = Eq(144, 156, 218, 36, 0, 0, 10);  // swapped helm
            Assert.Null(cache.Get(0, eqNew));
        }

        [Fact]
        public void Set_OverwritesPreviousEntry()
        {
            var dir = MakeTempDir();
            var cache = new HpMpCache(dir);
            var eq = Eq(1, 2, 3, 4, 5, 6, 7);
            cache.Set(0, eq, hp: 100, maxHp: 200, mp: 50, maxMp: 100);
            cache.Set(0, eq, hp: 150, maxHp: 200, mp: 75, maxMp: 100);

            var got = cache.Get(0, eq);
            Assert.Equal(150, got!.Hp);
            Assert.Equal(75, got.Mp);
        }

        [Fact]
        public void MultipleSlots_AreIsolated()
        {
            var dir = MakeTempDir();
            var cache = new HpMpCache(dir);
            var eq0 = Eq(1, 2, 3, 4, 5, 6, 7);
            var eq1 = Eq(10, 11, 12, 13, 14, 15, 16);

            cache.Set(0, eq0, hp: 100, maxHp: 100, mp: 10, maxMp: 10);
            cache.Set(1, eq1, hp: 200, maxHp: 200, mp: 20, maxMp: 20);

            Assert.Equal(100, cache.Get(0, eq0)!.MaxHp);
            Assert.Equal(200, cache.Get(1, eq1)!.MaxHp);
        }

        [Fact]
        public void Persists_AcrossInstances()
        {
            // Disk-backed: a second instance reading the same bridge dir
            // sees entries written by an earlier instance. Mirrors
            // ModStateFlags persistence.
            var dir = MakeTempDir();
            var a = new HpMpCache(dir);
            var eq = Eq(143, 156, 218, 36, 0, 0, 10);
            a.Set(0, eq, hp: 500, maxHp: 719, mp: 100, maxMp: 138);

            var b = new HpMpCache(dir);
            var got = b.Get(0, eq);
            Assert.NotNull(got);
            Assert.Equal(719, got!.MaxHp);
            Assert.Equal(138, got.MaxMp);
        }
    }
}
