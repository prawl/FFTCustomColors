using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class RosterMatchingTests
    {
        [Fact]
        public void ActiveUnit_WithZeroBraveFaith_ShouldNotMatchFirstSlot()
        {
            // The active unit has Brave=0, Faith=0 (not yet read from roster).
            // It should NOT blindly match the first level-99 slot (Ramza).
            // It should match the slot that no other unit claimed.
            var rosterSlots = new[]
            {
                new RosterSlot { NameId = 401, Level = 99, Brave = 94, Faith = 75, Job = 3, Secondary = 6 },   // Ramza
                new RosterSlot { NameId = 100, Level = 99, Brave = 85, Faith = 86, Job = 74, Secondary = 0 },  // Squire
                new RosterSlot { NameId = 200, Level = 99, Brave = 80, Faith = 50, Job = 77, Secondary = 0 },  // Archer (Lloyd)
                new RosterSlot { NameId = 300, Level = 99, Brave = 70, Faith = 60, Job = 78, Secondary = 0 },  // Monk
            };

            var scannedUnits = new[]
            {
                new ScannedUnitIdentity { Level = 99, Brave = 0, Faith = 0, Hp = 475 },     // Active unit (Lloyd) — Brave/Faith unknown
                new ScannedUnitIdentity { Level = 99, Brave = 94, Faith = 75, Hp = 719 },    // Ramza
                new ScannedUnitIdentity { Level = 99, Brave = 85, Faith = 86, Hp = 496 },    // Squire
                new ScannedUnitIdentity { Level = 99, Brave = 70, Faith = 60, Hp = 533 },    // Monk
            };

            var matches = RosterMatcher.Match(scannedUnits, rosterSlots);

            // Active unit (index 0) should match Lloyd (Archer), not Ramza
            Assert.Equal(200, matches[0].NameId);
            Assert.Equal(77, matches[0].Job);
            Assert.Equal(80, matches[0].Brave);
            Assert.Equal(50, matches[0].Faith);

            // Ramza should still match Ramza
            Assert.Equal(401, matches[1].NameId);
        }

        [Fact]
        public void AllUnitsWithBraveFaith_ShouldMatchCorrectly()
        {
            var rosterSlots = new[]
            {
                new RosterSlot { NameId = 401, Level = 99, Brave = 94, Faith = 75, Job = 3, Secondary = 6 },
                new RosterSlot { NameId = 100, Level = 99, Brave = 85, Faith = 86, Job = 74, Secondary = 0 },
                new RosterSlot { NameId = 200, Level = 99, Brave = 80, Faith = 50, Job = 77, Secondary = 0 },
            };

            var scannedUnits = new[]
            {
                new ScannedUnitIdentity { Level = 99, Brave = 80, Faith = 50, Hp = 475 },
                new ScannedUnitIdentity { Level = 99, Brave = 94, Faith = 75, Hp = 719 },
                new ScannedUnitIdentity { Level = 99, Brave = 85, Faith = 86, Hp = 496 },
            };

            var matches = RosterMatcher.Match(scannedUnits, rosterSlots);

            Assert.Equal(200, matches[0].NameId); // Archer
            Assert.Equal(401, matches[1].NameId); // Ramza
            Assert.Equal(100, matches[2].NameId); // Squire
        }

        [Fact]
        public void NoMatch_ReturnsEmptySlot()
        {
            var rosterSlots = new[]
            {
                new RosterSlot { NameId = 401, Level = 50, Brave = 94, Faith = 75, Job = 3, Secondary = 0 },
            };

            var scannedUnits = new[]
            {
                new ScannedUnitIdentity { Level = 99, Brave = 80, Faith = 50, Hp = 475 },
            };

            var matches = RosterMatcher.Match(scannedUnits, rosterSlots);

            Assert.Equal(0, matches[0].NameId); // no match
        }

        [Fact]
        public void DuplicateBraveFaith_UsesClaimedSlotTracking()
        {
            // Two units with identical level/brave/faith — shouldn't double-claim
            var rosterSlots = new[]
            {
                new RosterSlot { NameId = 100, Level = 99, Brave = 80, Faith = 50, Job = 74, Secondary = 0 },
                new RosterSlot { NameId = 200, Level = 99, Brave = 80, Faith = 50, Job = 77, Secondary = 0 },
            };

            var scannedUnits = new[]
            {
                new ScannedUnitIdentity { Level = 99, Brave = 80, Faith = 50, Hp = 475 },
                new ScannedUnitIdentity { Level = 99, Brave = 80, Faith = 50, Hp = 496 },
            };

            var matches = RosterMatcher.Match(scannedUnits, rosterSlots);

            // Both should match, each to a different slot
            Assert.NotEqual(matches[0].NameId, matches[1].NameId);
            Assert.True(matches[0].NameId > 0);
            Assert.True(matches[1].NameId > 0);
        }
    }
}
