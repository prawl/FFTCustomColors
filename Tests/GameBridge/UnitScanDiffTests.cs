using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="UnitScanDiff.Compare"/> — pure planner that
    /// diffs two scan snapshots and emits change events for enemy-turn
    /// play-by-play reporting (session 51 shipping of `project_enemy_turn_report_design.md`).
    /// </summary>
    public class UnitScanDiffTests
    {
        private static UnitScanDiff.UnitSnap Snap(
            string? name, int team, int x, int y, int hp, int maxHp,
            int rosterNameId = 0, List<string>? statuses = null)
            => new(name, rosterNameId, team, x, y, hp, maxHp, statuses);

        [Fact]
        public void Compare_EmptySnapshots_ReturnsEmpty()
        {
            var events = UnitScanDiff.Compare(
                new List<UnitScanDiff.UnitSnap>(),
                new List<UnitScanDiff.UnitSnap>());
            Assert.Empty(events);
        }

        [Fact]
        public void Compare_NoChanges_ReturnsEmpty()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", team: 0, x: 8, y: 10, hp: 719, maxHp: 719),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", team: 0, x: 8, y: 10, hp: 719, maxHp: 719),
            };
            var events = UnitScanDiff.Compare(before, after);
            Assert.Empty(events);
        }

        [Fact]
        public void Compare_MoveOnly_EmitsMovedEvent()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 7, 10, 719, 719),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("moved", e.Kind);
            Assert.Equal("Ramza", e.Label);
            Assert.Equal((8, 10), e.OldXY);
            Assert.Equal((7, 10), e.NewXY);
            Assert.Null(e.OldHp);
            Assert.Null(e.NewHp);
        }

        [Fact]
        public void Compare_DamageOnly_EmitsDamagedEvent()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 649, 719),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("damaged", e.Kind);
            Assert.Equal(719, e.OldHp);
            Assert.Equal(649, e.NewHp);
            Assert.Null(e.OldXY);
        }

        [Fact]
        public void Compare_Heal_EmitsHealedEvent()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Kenrick", 0, 9, 9, 100, 437),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Kenrick", 0, 9, 9, 437, 437),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("healed", e.Kind);
        }

        [Fact]
        public void Compare_KO_EmitsKoEvent()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Wilham", 0, 10, 10, 40, 477),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Wilham", 0, 10, 10, 0, 477),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("ko", e.Kind);
            Assert.Equal(0, e.NewHp);
        }

        [Fact]
        public void Compare_Revive_EmitsRevivedEvent()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Wilham", 0, 10, 10, 0, 477),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Wilham", 0, 10, 10, 477, 477),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("revived", e.Kind);
        }

        [Fact]
        public void Compare_MoveAndDamage_SingleEvent_KindDamaged()
        {
            // Both moved and took damage — surface damage as primary signal.
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 7, 10, 649, 719),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("damaged", e.Kind);
            Assert.Equal((8, 10), e.OldXY);
            Assert.Equal((7, 10), e.NewXY);
            Assert.Equal(719, e.OldHp);
            Assert.Equal(649, e.NewHp);
        }

        [Fact]
        public void Compare_StatusGained_EmitsStatusEvent()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719, statuses: new List<string>()),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719, statuses: new List<string> { "Poison" }),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("status", e.Kind);
            Assert.Equal(new[] { "Poison" }, e.StatusesGained);
            Assert.Null(e.StatusesLost);
        }

        [Fact]
        public void Compare_StatusLost_EmitsStatusEvent()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719, statuses: new List<string> { "Poison", "Haste" }),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719, statuses: new List<string> { "Haste" }),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("status", e.Kind);
            Assert.Equal(new[] { "Poison" }, e.StatusesLost);
            Assert.Null(e.StatusesGained);
        }

        [Fact]
        public void Compare_UnitRemoved_EmitsRemovedEvent()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719),
                Snap(null, team: 1, x: 1, y: 2, hp: 50, maxHp: 50),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("removed", e.Kind);
            Assert.Equal("ENEMY", e.Team);
        }

        [Fact]
        public void Compare_UnitAdded_EmitsAddedEvent()
        {
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Ramza", 0, 8, 10, 719, 719),
                Snap("Delita", 2, 5, 5, 300, 300, rosterNameId: 2),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("added", e.Kind);
            Assert.Equal("Delita", e.Label);
            Assert.Equal("ALLY", e.Team);
        }

        [Fact]
        public void Compare_EnemyWithNullName_MatchesByRosterNameId()
        {
            // Enemies often have Name=null; ensure we still match by
            // rosterNameId so the same enemy's movement isn't split into
            // remove + add events.
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap(null, team: 1, x: 1, y: 5, hp: 100, maxHp: 100, rosterNameId: 99),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap(null, team: 1, x: 2, y: 5, hp: 100, maxHp: 100, rosterNameId: 99),
            };
            var events = UnitScanDiff.Compare(before, after);
            var e = Assert.Single(events);
            Assert.Equal("moved", e.Kind);
            Assert.Equal((1, 5), e.OldXY);
            Assert.Equal((2, 5), e.NewXY);
        }

        [Fact]
        public void RenderEvent_MoveAndDamage_FormatsExpectedString()
        {
            var e = new UnitScanDiff.ChangeEvent(
                Label: "Ramza",
                Team: "PLAYER",
                OldXY: (8, 10),
                NewXY: (7, 10),
                OldHp: 719,
                NewHp: 649,
                StatusesGained: null,
                StatusesLost: null,
                Kind: "damaged");
            var s = UnitScanDiff.RenderEvent(e);
            Assert.Contains("Ramza", s);
            Assert.Contains("(8,10)→(7,10)", s);
            Assert.Contains("HP 719→649", s);
            Assert.Contains("-70", s);
        }

        [Fact]
        public void RenderEvent_StatusGained_IncludesBracketedStatusList()
        {
            var e = new UnitScanDiff.ChangeEvent(
                Label: "Ramza",
                Team: "PLAYER",
                OldXY: null, NewXY: null, OldHp: null, NewHp: null,
                StatusesGained: new List<string> { "Poison" },
                StatusesLost: null,
                Kind: "status");
            var s = UnitScanDiff.RenderEvent(e);
            Assert.Contains("[+Poison]", s);
        }
    }
}
