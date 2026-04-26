using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class PhantomKoCoalescerTests
    {
        private static UnitScanDiff.ChangeEvent KoEvent(string name, string team = "ENEMY", int? oldHp = 345)
            => new(
                Label: name, Team: team,
                OldXY: (3, 4), NewXY: (3, 4),
                OldHp: oldHp, NewHp: 0,
                StatusesGained: null, StatusesLost: null,
                Kind: "ko");

        private static UnitScanDiff.ChangeEvent DamagedEvent(string name, string team = "ENEMY", int oldHp = 345, int newHp = 0)
            => new(
                Label: name, Team: team,
                OldXY: (3, 4), NewXY: (3, 4),
                OldHp: oldHp, NewHp: newHp,
                StatusesGained: null, StatusesLost: null,
                Kind: "damaged");

        private static UnitScanDiff.ChangeEvent AddedEvent(string name, string team = "ENEMY")
            => new(
                Label: name, Team: team,
                OldXY: null, NewXY: (3, 4),
                OldHp: null, NewHp: 345,
                StatusesGained: null, StatusesLost: null,
                Kind: "added");

        private static UnitScanDiff.ChangeEvent StatusEvent(string name, string team, List<string>? gained, List<string>? lost)
            => new(
                Label: name, Team: team,
                OldXY: (3, 4), NewXY: (3, 4),
                OldHp: null, NewHp: null,
                StatusesGained: gained, StatusesLost: lost,
                Kind: "status");

        [Fact]
        public void EmptyInput_ReturnsEmpty()
        {
            var result = PhantomKoCoalescer.Filter(new List<UnitScanDiff.ChangeEvent>());
            Assert.Empty(result);
        }

        [Fact]
        public void NoPhantom_PassesAllEventsThrough()
        {
            // Real combat: a unit took damage, no joined event for the same unit.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                DamagedEvent("Knight", oldHp: 100, newHp: 60),
            };
            Assert.Single(PhantomKoCoalescer.Filter(events));
        }

        [Fact]
        public void RealKoWithoutJoined_PassesThrough()
        {
            // KO without a same-unit joined: not phantom — keep it.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                KoEvent("Knight"),
            };
            Assert.Single(PhantomKoCoalescer.Filter(events));
        }

        [Fact]
        public void PhantomKoPlusJoined_BothSuppressed()
        {
            // Live-flagged 2026-04-26: Time Mage took 345 damage to HP=0 +
            // Dead status + joined at (3,4) all in one batch despite being
            // alive throughout. Same unit appearing in both ko/damaged AND
            // added in a single batch is the phantom-scan signature.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                DamagedEvent("Time Mage", oldHp: 345, newHp: 0),
                AddedEvent("Time Mage"),
            };
            Assert.Empty(PhantomKoCoalescer.Filter(events));
        }

        [Fact]
        public void PhantomKoPlusJoined_AlsoSuppressesStatusGainedDead()
        {
            // The full bug: damage→0 + status gained "Dead" + status lost
            // "Haste" + joined. All four for the same unit name = phantom.
            // Drop the damage, drop the joined, drop the death-status flip.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                DamagedEvent("Time Mage", oldHp: 345, newHp: 0),
                StatusEvent("Time Mage", "ENEMY",
                    gained: new List<string> { "Dead" },
                    lost: new List<string> { "Haste" }),
                AddedEvent("Time Mage"),
            };
            Assert.Empty(PhantomKoCoalescer.Filter(events));
        }

        [Fact]
        public void PhantomKoEvent_AlsoSuppressedWithJoined()
        {
            // "ko" kind (not just "damaged") also coalesces.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                KoEvent("Skeleton"),
                AddedEvent("Skeleton"),
            };
            Assert.Empty(PhantomKoCoalescer.Filter(events));
        }

        [Fact]
        public void DifferentUnitNames_DoNotCoalesce()
        {
            // KO of one unit + joined of a different unit is real and
            // common (e.g. enemy died, ally guest joined).
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                KoEvent("Skeleton"),
                AddedEvent("Tietra", team: "ALLY"),
            };
            Assert.Equal(2, PhantomKoCoalescer.Filter(events).Count);
        }

        [Fact]
        public void DamagedNonZero_PlusJoined_DoesNotCoalesce()
        {
            // Only HP→0 damaged events are coalesced with joined. A
            // damaged-but-alive unit + joined of same name (rare but
            // possible: clone/spawn) should not be suppressed.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                DamagedEvent("Knight", oldHp: 100, newHp: 60),
                AddedEvent("Knight"),
            };
            Assert.Equal(2, PhantomKoCoalescer.Filter(events).Count);
        }

        [Fact]
        public void OtherEventsPreserved_AroundPhantomCoalesce()
        {
            // Unrelated events bracketing a phantom KO+join: keep the
            // unrelated, drop the phantom.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                DamagedEvent("Knight", oldHp: 100, newHp: 60),
                DamagedEvent("Time Mage", oldHp: 345, newHp: 0),
                AddedEvent("Time Mage"),
                StatusEvent("Wizard", "ENEMY",
                    gained: new List<string> { "Slow" }, lost: null),
            };
            var result = PhantomKoCoalescer.Filter(events);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, e => e.Label == "Knight");
            Assert.Contains(result, e => e.Label == "Wizard");
        }

        private static UnitScanDiff.ChangeEvent RemovedEvent(string name, string team = "ENEMY", int? oldHp = 345)
            => new(
                Label: name, Team: team,
                OldXY: (3, 4), NewXY: null,
                OldHp: oldHp, NewHp: null,
                StatusesGained: null, StatusesLost: null,
                Kind: "removed");

        [Fact]
        public void RemovedAlive_PlusAdded_CoalescedAsPhantom()
        {
            // The other phantom shape: a transient bad scan made the unit
            // disappear (kind="removed" with OldHp>0) and the next scan
            // re-introduced them (kind="added"). Same root cause as the
            // damaged-to-zero phantom; same fix: drop both.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                RemovedEvent("Skeleton", oldHp: 680),
                AddedEvent("Skeleton"),
            };
            Assert.Empty(PhantomKoCoalescer.Filter(events));
        }

        [Fact]
        public void RemovedAlive_WithoutAdded_PassesThrough()
        {
            // Real disappearance (crystallized, treasure, etc.) — keep.
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                RemovedEvent("Skeleton", oldHp: 680),
            };
            Assert.Single(PhantomKoCoalescer.Filter(events));
        }
    }
}
