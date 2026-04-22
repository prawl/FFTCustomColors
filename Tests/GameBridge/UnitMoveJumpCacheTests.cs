using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// S58 live-observed: heap Move/Jump search missed for EVERY unit in a
// battle (possibly because structs relocated outside the hardcoded
// 0x40..0x42 billion range). Result: `scan_move` reported Mv=0 Jmp=0
// for all units, BFS returned 0 valid tiles, every battle_move failed.
//
// Cache the last successful heap read per unit (keyed by MaxHp since
// that's what the search pattern uses). On miss, fall back to the
// cached value instead of honest Mv=0. Cache clears on battle boundary
// (same as LiveHpAddressCache). Wrong-slot collision risk is low: two
// units with identical MaxHp on the same map is rare.
public class UnitMoveJumpCacheTests
{
    [Fact]
    public void Get_Empty_ReturnsNull()
    {
        var cache = new UnitMoveJumpCache();
        Assert.Null(cache.Get(maxHp: 628));
    }

    [Fact]
    public void Put_ThenGet_ReturnsSameValues()
    {
        var cache = new UnitMoveJumpCache();
        cache.Put(maxHp: 628, move: 5, jump: 4);

        var hit = cache.Get(628);
        Assert.NotNull(hit);
        Assert.Equal(5, hit!.Value.move);
        Assert.Equal(4, hit.Value.jump);
    }

    [Fact]
    public void Put_DifferentMaxHp_IsolatedEntries()
    {
        var cache = new UnitMoveJumpCache();
        cache.Put(628, 5, 4);
        cache.Put(437, 3, 3);

        Assert.Equal((5, 4), cache.Get(628)!.Value);
        Assert.Equal((3, 3), cache.Get(437)!.Value);
    }

    [Fact]
    public void Put_SameMaxHp_OverwritesPrevious()
    {
        // Buffed Move (+3 movement ability) replaces the base read.
        var cache = new UnitMoveJumpCache();
        cache.Put(719, 4, 3);
        cache.Put(719, 7, 3); // Ramza's Mv=7 with Movement+3

        Assert.Equal((7, 3), cache.Get(719)!.Value);
    }

    [Fact]
    public void Clear_EmptiesAllEntries()
    {
        // Called on StartBattle — heap structs relocate across battles.
        var cache = new UnitMoveJumpCache();
        cache.Put(628, 5, 4);
        cache.Put(437, 3, 3);

        cache.Clear();

        Assert.Null(cache.Get(628));
        Assert.Null(cache.Get(437));
    }

    [Fact]
    public void Put_InvalidRange_Ignored()
    {
        // Sanity guard — caller already validates 1..10 move / 1..8 jump,
        // but reject pathological values to keep cache clean.
        var cache = new UnitMoveJumpCache();
        cache.Put(628, move: 0, jump: 4);
        cache.Put(628, move: 5, jump: 99);
        cache.Put(628, move: 99, jump: 3);

        Assert.Null(cache.Get(628));
    }
}
