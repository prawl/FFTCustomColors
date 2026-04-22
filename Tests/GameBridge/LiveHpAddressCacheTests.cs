using System;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// ReadLiveHp does a broad SearchBytesInAllMemory over ~500MB of process
// memory on EVERY battle_attack (~100-200ms). Most attacks in a row
// hit the same target — the readonly-region live HP addresses are
// stable within a battle. Cache the addresses per (maxHp, level) key;
// re-validate on hit with a 2-byte HP read; fall through to full
// search only on miss or stale.
//
// Cache is battle-scoped — addresses can relocate across battles,
// so the tracker invalidates on new-battle transition.
public class LiveHpAddressCacheTests
{
    [Fact]
    public void GetCachedAddresses_EmptyCache_ReturnsNull()
    {
        var cache = new LiveHpAddressCache();
        Assert.Null(cache.GetCachedAddresses(maxHp: 120, level: 5));
    }

    [Fact]
    public void Remember_ThenGet_ReturnsSameAddress()
    {
        var cache = new LiveHpAddressCache();
        cache.Remember(maxHp: 120, level: 5, address: (IntPtr)0x141234560);

        var hit = cache.GetCachedAddresses(maxHp: 120, level: 5);
        Assert.NotNull(hit);
        Assert.Contains((IntPtr)0x141234560, hit!);
    }

    [Fact]
    public void Remember_DifferentKey_IsolatedEntry()
    {
        var cache = new LiveHpAddressCache();
        cache.Remember(maxHp: 120, level: 5, address: (IntPtr)0x141234560);
        cache.Remember(maxHp: 200, level: 5, address: (IntPtr)0x141ABCDEF);

        Assert.Contains((IntPtr)0x141234560, cache.GetCachedAddresses(120, 5)!);
        Assert.Contains((IntPtr)0x141ABCDEF, cache.GetCachedAddresses(200, 5)!);
        Assert.DoesNotContain((IntPtr)0x141234560, cache.GetCachedAddresses(200, 5)!);
    }

    [Fact]
    public void Remember_SameKey_AccumulatesAddresses()
    {
        // Multiple readonly copies exist per unit — cache collects them
        // so revalidation can try each.
        var cache = new LiveHpAddressCache();
        cache.Remember(120, 5, (IntPtr)0x141234560);
        cache.Remember(120, 5, (IntPtr)0x15A000000);

        var hit = cache.GetCachedAddresses(120, 5)!;
        Assert.Equal(2, hit.Count);
        Assert.Contains((IntPtr)0x141234560, hit);
        Assert.Contains((IntPtr)0x15A000000, hit);
    }

    [Fact]
    public void Remember_DuplicateAddress_NotRecorded()
    {
        var cache = new LiveHpAddressCache();
        cache.Remember(120, 5, (IntPtr)0x141234560);
        cache.Remember(120, 5, (IntPtr)0x141234560);

        var hit = cache.GetCachedAddresses(120, 5)!;
        Assert.Single(hit);
    }

    [Fact]
    public void Invalidate_SpecificKey_OnlyClearsThatEntry()
    {
        var cache = new LiveHpAddressCache();
        cache.Remember(120, 5, (IntPtr)0x141234560);
        cache.Remember(200, 5, (IntPtr)0x141ABCDEF);

        cache.Invalidate(maxHp: 120, level: 5);

        Assert.Null(cache.GetCachedAddresses(120, 5));
        Assert.NotNull(cache.GetCachedAddresses(200, 5));
    }

    [Fact]
    public void Clear_EmptiesAllEntries()
    {
        // Called on battle-boundary transitions — all heap addresses
        // invalidate when a new battle starts.
        var cache = new LiveHpAddressCache();
        cache.Remember(120, 5, (IntPtr)0x141234560);
        cache.Remember(200, 5, (IntPtr)0x141ABCDEF);

        cache.Clear();

        Assert.Null(cache.GetCachedAddresses(120, 5));
        Assert.Null(cache.GetCachedAddresses(200, 5));
    }

    [Fact]
    public void Level_ZeroUsedAsWildcardKey_WhenLevelNotAvailable()
    {
        // Callers with no level info (targetLevel=0 path) share a single
        // keyed slot so their entries don't conflict with level-keyed ones.
        var cache = new LiveHpAddressCache();
        cache.Remember(120, level: 0, (IntPtr)0x141000000);
        cache.Remember(120, level: 5, (IntPtr)0x141234560);

        Assert.Contains((IntPtr)0x141000000, cache.GetCachedAddresses(120, 0)!);
        Assert.Contains((IntPtr)0x141234560, cache.GetCachedAddresses(120, 5)!);
        Assert.DoesNotContain((IntPtr)0x141000000, cache.GetCachedAddresses(120, 5)!);
    }
}
