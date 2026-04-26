using System;
using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Mid-animation scan races emit phantom move pairs like
    ///   `> Knight moved (2,5) → (8,10)`
    ///   `> Knight moved (8,10) → (2,5)`
    /// where the unit appeared to teleport across the map and back inside
    /// one turn. The diff is a scan artifact — Knight didn't actually
    /// move there. Live-flagged 2026-04-25 P2.
    ///
    /// MoveArtifactCoalescer tracks the most recent emitted move per
    /// unit; a subsequent move event that exactly reverses it within a
    /// short window (default ~3s, the duration of a typical enemy turn
    /// cycle) is suppressed.
    /// </summary>
    public class MoveArtifactCoalescerTests
    {
        private static UnitScanDiff.ChangeEvent Move(string label, int oldX, int oldY, int newX, int newY, string team = "ENEMY")
            => new(label, team, OldXY: (oldX, oldY), NewXY: (newX, newY),
                OldHp: null, NewHp: null,
                StatusesGained: null, StatusesLost: null, Kind: "moved");

        private static UnitScanDiff.ChangeEvent Damaged(string label, int oldHp, int newHp)
            => new(label, "ENEMY", OldXY: null, NewXY: null,
                OldHp: oldHp, NewHp: newHp,
                StatusesGained: null, StatusesLost: null, Kind: "damaged");

        [Fact]
        public void NewMove_PassesThrough()
        {
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var events = new List<UnitScanDiff.ChangeEvent> { Move("Knight", 2, 5, 4, 5) };
            var filtered = c.Filter(events, DateTime.UtcNow);
            Assert.Single(filtered);
            Assert.Equal("Knight", filtered[0].Label);
        }

        [Fact]
        public void SecondMoveSameDirection_PassesThrough()
        {
            // Knight goes 2,5 → 4,5 → 6,5 in two batches. Both are real
            // legs of a continuing movement, neither is a round-trip.
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var t0 = DateTime.UtcNow;
            c.Filter(new List<UnitScanDiff.ChangeEvent> { Move("Knight", 2, 5, 4, 5) }, t0);
            var second = c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Knight", 4, 5, 6, 5) }, t0.AddMilliseconds(500));
            Assert.Single(second);
        }

        [Fact]
        public void RoundTrip_SuppressesSecondMove()
        {
            // Knight 2,5 → 8,10 (artifact), then 8,10 → 2,5 (back).
            // The second move exactly reverses the first within window.
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var t0 = DateTime.UtcNow;
            var first = c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Knight", 4, 5, 7, 8) }, t0);
            Assert.Single(first); // first move emits — we don't know yet it's an artifact
            var second = c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Knight", 7, 8, 4, 5) }, t0.AddMilliseconds(500));
            Assert.Empty(second); // second suppressed as round-trip
        }

        [Fact]
        public void RoundTripBeyondWindow_NotSuppressed()
        {
            // If too much time has passed, treat as a real bounce-back move.
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var t0 = DateTime.UtcNow;
            c.Filter(new List<UnitScanDiff.ChangeEvent> { Move("Knight", 4, 5, 7, 8) }, t0);
            var late = c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Knight", 7, 8, 4, 5) }, t0.AddSeconds(10));
            Assert.Single(late);
        }

        [Fact]
        public void DifferentUnits_NotConflated()
        {
            // Knight A→B, then Wilham B→A — different units, no round-trip.
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var t0 = DateTime.UtcNow;
            c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Knight", 4, 5, 7, 8) }, t0);
            var second = c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Wilham", 7, 8, 4, 5, team: "PLAYER") }, t0.AddMilliseconds(500));
            Assert.Single(second);
        }

        [Fact]
        public void ImplausiblyLongMove_SuppressedAsArtifact()
        {
            // Manhattan 8 (8,8)→(3,5) — Lloyd has Move 4. Misattribution
            // from scan diff (some enemy got labeled Lloyd in post-snap).
            // Use Manhattan 9 to ensure suppress.
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Move("Lloyd", 8, 8, 0, 5, team: "PLAYER"),  // Manhattan 11
            };
            var filtered = c.Filter(events, DateTime.UtcNow);
            Assert.Empty(filtered);
        }

        [Fact]
        public void PlausibleLongMove_StillEmits()
        {
            // Manhattan 8 is the upper bound (Move 6 + Jump 2) — emit.
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Move("Knight", 0, 0, 4, 4),  // Manhattan 8 — borderline plausible
            };
            var filtered = c.Filter(events, DateTime.UtcNow);
            Assert.Single(filtered);
        }

        [Fact]
        public void NonMoveEvents_PassThroughUnchanged()
        {
            // Damage / status / KO events don't get coalesced — only moves.
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Damaged("Knight", 100, 50),
                Move("Knight", 2, 5, 4, 5),
            };
            var filtered = c.Filter(events, DateTime.UtcNow);
            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void RoundTripWithSameUnitDifferentTeam_KeyedSeparately()
        {
            // Edge case: two units named "Knight" on different teams. Tracked
            // independently so a player's Knight returning home doesn't
            // suppress an enemy Knight's actual move.
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var t0 = DateTime.UtcNow;
            c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Knight", 2, 5, 8, 10, team: "ENEMY") }, t0);
            var second = c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Knight", 7, 8, 4, 5, team: "PLAYER") }, t0.AddMilliseconds(500));
            Assert.Single(second);
        }

        [Fact]
        public void Reset_ClearsTrackedState()
        {
            // New battle starts — past round-trips no longer apply.
            var c = new MoveArtifactCoalescer(TimeSpan.FromSeconds(3));
            var t0 = DateTime.UtcNow;
            c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Knight", 4, 5, 7, 8) }, t0);
            c.ResetForNewBattle();
            // Now (8,10) → (2,5) shouldn't be considered a round-trip.
            var after = c.Filter(new List<UnitScanDiff.ChangeEvent>
                { Move("Knight", 7, 8, 4, 5) }, t0.AddMilliseconds(500));
            Assert.Single(after);
        }
    }
}
