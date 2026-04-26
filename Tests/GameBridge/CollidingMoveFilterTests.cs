using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Live-flagged 2026-04-26 playtest at Siedge Weald: with 3 same-name
    /// Skeletons on the field, the narrator reported `> Skeleton moved
    /// (3,7) → (3,3)` despite Ramza being at (3,3). That destination is
    /// occupied — a Skeleton can't have moved there. The diff's
    /// rank-based identity matching gets confused for duplicate-name
    /// enemies and emits the wrong source/destination.
    ///
    /// Defense: drop any `moved` event whose destination tile is held by
    /// a DIFFERENT unit in the post-snap. We can't always identify which
    /// physical Skeleton actually moved where, but we can at least
    /// suppress the obviously-impossible attributions.
    /// </summary>
    public class CollidingMoveFilterTests
    {
        private static UnitScanDiff.UnitSnap Snap(string? name, int x, int y, int team = 1, int hp = 100, int maxHp = 100)
            => new(Name: name, RosterNameId: 0, Team: team, GridX: x, GridY: y, Hp: hp, MaxHp: maxHp, Statuses: null);

        private static UnitScanDiff.ChangeEvent MovedEvent(string label, int oldX, int oldY, int newX, int newY, string team = "ENEMY")
            => new(
                Label: label, Team: team,
                OldXY: (oldX, oldY), NewXY: (newX, newY),
                OldHp: null, NewHp: null,
                StatusesGained: null, StatusesLost: null,
                Kind: "moved");

        private static UnitScanDiff.ChangeEvent DamagedEvent(string label, int hp, int newHp)
            => new(
                Label: label, Team: "ENEMY",
                OldXY: (3, 4), NewXY: (3, 4),
                OldHp: hp, NewHp: newHp,
                StatusesGained: null, StatusesLost: null,
                Kind: "damaged");

        [Fact]
        public void EmptyInput_ReturnsEmpty()
        {
            var result = CollidingMoveFilter.Filter(
                new List<UnitScanDiff.ChangeEvent>(),
                new List<UnitScanDiff.UnitSnap>());
            Assert.Empty(result);
        }

        [Fact]
        public void MoveToOccupiedTile_DropsEvent()
        {
            // The bug: Skeleton "moved" to (3,3), but Ramza is at (3,3) in
            // the post-snap. Drop the phantom.
            var events = new List<UnitScanDiff.ChangeEvent> {
                MovedEvent("Skeleton", 3, 7, 3, 3),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", 3, 3, team: 0),
                Snap("Skeleton", 4, 5),
            };
            Assert.Empty(CollidingMoveFilter.Filter(events, post));
        }

        [Fact]
        public void MoveToVacantTile_PassesThrough()
        {
            // Real move: destination not occupied by anyone in post-snap.
            var events = new List<UnitScanDiff.ChangeEvent> {
                MovedEvent("Skeleton", 3, 7, 4, 5),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", 3, 3, team: 0),
                Snap("Skeleton", 4, 5),  // moved here legitimately
            };
            Assert.Single(CollidingMoveFilter.Filter(events, post));
        }

        [Fact]
        public void MoveToSameLabel_PassesThrough()
        {
            // The destination matches a unit with the SAME name (e.g. another
            // Skeleton). This is identity-ambiguous but not obviously wrong;
            // pass through and let other filters handle.
            var events = new List<UnitScanDiff.ChangeEvent> {
                MovedEvent("Skeleton", 3, 7, 4, 5),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Skeleton", 4, 5),  // could be the same unit (real move)
            };
            Assert.Single(CollidingMoveFilter.Filter(events, post));
        }

        [Fact]
        public void NonMoveEvents_PassThrough()
        {
            // damaged / healed / ko events aren't affected by this filter.
            var events = new List<UnitScanDiff.ChangeEvent> {
                DamagedEvent("Skeleton", 100, 60),
            };
            var post = new List<UnitScanDiff.UnitSnap>();
            Assert.Single(CollidingMoveFilter.Filter(events, post));
        }

        [Fact]
        public void DropsOnlyCollidingMove_OtherEventsKept()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                DamagedEvent("Goblin", 50, 30),
                MovedEvent("Skeleton", 3, 7, 3, 3),  // colliding — drop
                MovedEvent("Knight", 5, 5, 6, 5),    // legit — keep
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", 3, 3, team: 0),
                Snap("Knight", 6, 5),
            };
            var result = CollidingMoveFilter.Filter(events, post);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, e => e.Label == "Goblin" && e.Kind == "damaged");
            Assert.Contains(result, e => e.Label == "Knight" && e.Kind == "moved");
        }

        [Fact]
        public void NoNewXY_PassThrough()
        {
            // Defensive: a "moved" with no NewXY is malformed but shouldn't crash.
            var events = new List<UnitScanDiff.ChangeEvent> {
                new(
                    Label: "Skeleton", Team: "ENEMY",
                    OldXY: (3, 7), NewXY: null,
                    OldHp: null, NewHp: null,
                    StatusesGained: null, StatusesLost: null,
                    Kind: "moved"),
            };
            var post = new List<UnitScanDiff.UnitSnap>();
            Assert.Single(CollidingMoveFilter.Filter(events, post));
        }
    }
}
