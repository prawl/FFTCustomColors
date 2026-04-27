using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// 2026-04-26 Mandalia playtest: Ramza levelled up mid-battle. The
// scanned identity (Level/Brave/Faith) shifted with the level-up, but
// the roster slot's level lagged for a frame, so RosterMatcher.Match
// returned NameId=0 for the active unit. Result: unit.Job stayed at
// the C# default (0 = Squire / Mettle), so the bridge offered Mettle
// abilities for what the player still saw as a Knight, and the menu
// nav got desynchronised.
//
// Per-NameId cache: stash the last successful match keyed by the
// active unit's stable NameId (read from activeUnit memory, NOT from
// roster matching), and reuse it when a fresh match misses. Cleared
// on battle boundary like the other per-battle caches.
public class RosterMatchCacheTests
{
    [Fact]
    public void Get_Empty_ReturnsNull()
    {
        var cache = new RosterMatchCache();
        Assert.Null(cache.Get(nameId: 1));
    }

    [Fact]
    public void Put_ThenGet_ReturnsMatch()
    {
        var cache = new RosterMatchCache();
        var match = new RosterMatchResult
        {
            NameId = 1, Job = 5, Brave = 70, Faith = 50,
            Secondary = 6, SlotIndex = 0,
        };
        cache.Put(nameId: 1, match);

        var hit = cache.Get(1);
        Assert.NotNull(hit);
        Assert.Equal(5, hit!.Value.Job);
        Assert.Equal(6, hit.Value.Secondary);
        Assert.Equal(70, hit.Value.Brave);
    }

    [Fact]
    public void Put_DifferentNameIds_IsolatedEntries()
    {
        var cache = new RosterMatchCache();
        cache.Put(1, new RosterMatchResult { NameId = 1, Job = 5 });
        cache.Put(2, new RosterMatchResult { NameId = 2, Job = 8 });

        Assert.Equal(5, cache.Get(1)!.Value.Job);
        Assert.Equal(8, cache.Get(2)!.Value.Job);
    }

    [Fact]
    public void Put_SameNameId_OverwritesPrevious()
    {
        // Job change mid-battle (Ramza Squire → Knight) updates the cache.
        var cache = new RosterMatchCache();
        cache.Put(1, new RosterMatchResult { NameId = 1, Job = 0 });
        cache.Put(1, new RosterMatchResult { NameId = 1, Job = 5 });

        Assert.Equal(5, cache.Get(1)!.Value.Job);
    }

    [Fact]
    public void Put_InvalidNameId_Ignored()
    {
        // NameId 0 = sentinel for "no match"; never cache it.
        var cache = new RosterMatchCache();
        cache.Put(0, new RosterMatchResult { NameId = 0, Job = 5 });
        Assert.Null(cache.Get(0));
    }

    [Fact]
    public void Put_NameIdMismatch_Ignored()
    {
        // Defensive: cache key must agree with match payload's NameId,
        // otherwise the entry is corrupted and we'd return wrong identity.
        var cache = new RosterMatchCache();
        cache.Put(1, new RosterMatchResult { NameId = 2, Job = 5 });
        Assert.Null(cache.Get(1));
        Assert.Null(cache.Get(2));
    }

    [Fact]
    public void Clear_EmptiesAllEntries()
    {
        // Battle boundary clears (roster slots can shuffle across battles).
        var cache = new RosterMatchCache();
        cache.Put(1, new RosterMatchResult { NameId = 1, Job = 5 });
        cache.Put(2, new RosterMatchResult { NameId = 2, Job = 8 });

        cache.Clear();

        Assert.Null(cache.Get(1));
        Assert.Null(cache.Get(2));
    }
}
