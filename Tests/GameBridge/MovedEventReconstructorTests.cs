using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// 2026-04-26 Mandalia: 4 enemies all named "[ENEMY]" (no class
// fingerprint, no roster name). When one enemy moves (8,5)→(6,4),
// UnitScanDiff falls back to position-derived keys (`xy:8,5` vs
// `xy:6,4`), sees them as DIFFERENT identities, emits one "removed"
// for (8,5) and one "added" for (6,4). PhantomKoCoalescer can't
// catch this — it dedupes by Label, but labels are
// `(unit@8,5)` and `(unit@6,4)` which differ.
//
// Strategy: pair same-team remove+add events with matching HP/MaxHp
// fingerprints back into a single moved event. Real KO+spawn in one
// batch is rare and not HP-fingerprint-matched, so the false-positive
// risk is low. Anchored to the unnamed-or-position-fallback case so
// named-unit deaths/spawns aren't accidentally rejoined.
public class MovedEventReconstructorTests
{
    private static UnitScanDiff.ChangeEvent MakeRemoved(
        string label, string team, int x, int y, int hp, int maxHp = 75) =>
        new UnitScanDiff.ChangeEvent(
            Label: label, Team: team,
            OldXY: (x, y), NewXY: null,
            OldHp: hp, NewHp: null,
            StatusesGained: null, StatusesLost: null,
            Kind: "removed");

    private static UnitScanDiff.ChangeEvent MakeAdded(
        string label, string team, int x, int y, int hp, int maxHp = 75) =>
        new UnitScanDiff.ChangeEvent(
            Label: label, Team: team,
            OldXY: null, NewXY: (x, y),
            OldHp: null, NewHp: hp,
            StatusesGained: null, StatusesLost: null,
            Kind: "added");

    [Fact]
    public void RemovePlusAdd_SameTeamSameHp_ReconstructsAsMoved()
    {
        // The Mandalia case: enemy at (8,5) hp=75 → (6,4) hp=75.
        var events = new List<UnitScanDiff.ChangeEvent>
        {
            MakeRemoved("(unit@8,5)", "ENEMY", 8, 5, 75),
            MakeAdded("(unit@6,4)", "ENEMY", 6, 4, 75),
        };

        var result = MovedEventReconstructor.Reconstruct(events);

        Assert.Single(result);
        Assert.Equal("moved", result[0].Kind);
        Assert.Equal((8, 5), result[0].OldXY);
        Assert.Equal((6, 4), result[0].NewXY);
        Assert.Equal("ENEMY", result[0].Team);
    }

    [Fact]
    public void RemovePlusAdd_DifferentTeams_DoesNotPair()
    {
        // Don't merge across teams — an enemy "death" and an ally
        // "spawn" are unrelated events.
        var events = new List<UnitScanDiff.ChangeEvent>
        {
            MakeRemoved("(unit@8,5)", "ENEMY", 8, 5, 75),
            MakeAdded("(unit@6,4)", "PLAYER", 6, 4, 75),
        };

        var result = MovedEventReconstructor.Reconstruct(events);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void RemovePlusAdd_DifferentHp_DoesNotPair()
    {
        // HP must match — different HPs imply unrelated units.
        // Real move preserves HP (at most some passive regen tick).
        var events = new List<UnitScanDiff.ChangeEvent>
        {
            MakeRemoved("(unit@8,5)", "ENEMY", 8, 5, 75),
            MakeAdded("(unit@6,4)", "ENEMY", 6, 4, 50),
        };

        var result = MovedEventReconstructor.Reconstruct(events);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void NamedUnits_Unaffected()
    {
        // Don't merge for named units — UnitScanDiff already handles
        // them correctly via name-based keys. A named "Goblin" being
        // removed AND a different named "Goblin" added means a real
        // game-state change, not a phantom move.
        var events = new List<UnitScanDiff.ChangeEvent>
        {
            MakeRemoved("Goblin", "ENEMY", 8, 5, 75),
            MakeAdded("Goblin", "ENEMY", 6, 4, 75),
        };

        var result = MovedEventReconstructor.Reconstruct(events);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MultipleRemoveAddPairs_AllReconstruct()
    {
        // 2 enemies move in the same batch — pair them up.
        var events = new List<UnitScanDiff.ChangeEvent>
        {
            MakeRemoved("(unit@8,5)", "ENEMY", 8, 5, 75),
            MakeRemoved("(unit@4,4)", "ENEMY", 4, 4, 64),
            MakeAdded("(unit@6,4)", "ENEMY", 6, 4, 75),
            MakeAdded("(unit@3,4)", "ENEMY", 3, 4, 64),
        };

        var result = MovedEventReconstructor.Reconstruct(events);

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal("moved", e.Kind));
        // First pair: hp=75 → (8,5)→(6,4)
        var move75 = result.Find(e => e.OldXY == (8, 5));
        Assert.NotNull(move75);
        Assert.Equal((6, 4), move75!.NewXY);
        // Second pair: hp=64 → (4,4)→(3,4)
        var move64 = result.Find(e => e.OldXY == (4, 4));
        Assert.NotNull(move64);
        Assert.Equal((3, 4), move64!.NewXY);
    }

    [Fact]
    public void UnpairedRemove_PassesThrough()
    {
        // Genuine death (no matching add) stays as-is.
        var events = new List<UnitScanDiff.ChangeEvent>
        {
            MakeRemoved("(unit@8,5)", "ENEMY", 8, 5, 75),
        };

        var result = MovedEventReconstructor.Reconstruct(events);

        Assert.Single(result);
        Assert.Equal("removed", result[0].Kind);
    }

    [Fact]
    public void UnpairedAdd_PassesThrough()
    {
        // Genuine spawn (no matching remove) stays as-is.
        var events = new List<UnitScanDiff.ChangeEvent>
        {
            MakeAdded("(unit@0,0)", "ENEMY", 0, 0, 75),
        };

        var result = MovedEventReconstructor.Reconstruct(events);

        Assert.Single(result);
        Assert.Equal("added", result[0].Kind);
    }

    [Fact]
    public void NonRemoveAddEvents_PassThrough()
    {
        // ko, damaged, healed, etc. shouldn't be touched.
        var events = new List<UnitScanDiff.ChangeEvent>
        {
            new UnitScanDiff.ChangeEvent(
                "Goblin", "ENEMY", null, null, 75, 0,
                null, null, "ko"),
            new UnitScanDiff.ChangeEvent(
                "Ramza", "PLAYER", (3,3), (4,3), null, null,
                null, null, "moved"),
        };

        var result = MovedEventReconstructor.Reconstruct(events);

        Assert.Equal(2, result.Count);
        Assert.Equal("ko", result[0].Kind);
        Assert.Equal("moved", result[1].Kind);
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        var result = MovedEventReconstructor.Reconstruct(
            new List<UnitScanDiff.ChangeEvent>());
        Assert.Empty(result);
    }

    [Fact]
    public void OnlyMatchesPositionFallbackLabels()
    {
        // The unit-named (e.g. "Skeleton#1") rank-suffixed labels
        // shouldn't auto-merge — they have name-based identity which
        // UnitScanDiff already handles. Only `(unit@x,y)` literal
        // position-fallback labels are merge candidates.
        var events = new List<UnitScanDiff.ChangeEvent>
        {
            MakeRemoved("Skeleton#1", "ENEMY", 8, 5, 75),
            MakeAdded("Skeleton#0", "ENEMY", 6, 4, 75),
        };

        var result = MovedEventReconstructor.Reconstruct(events);

        Assert.Equal(2, result.Count);
    }
}
