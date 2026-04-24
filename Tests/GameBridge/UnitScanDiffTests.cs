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
            int rosterNameId = 0, List<string>? statuses = null,
            byte[]? classFingerprint = null)
            => new(name, rosterNameId, team, x, y, hp, maxHp, statuses, classFingerprint);

        private static byte[] Fp(params int[] bytes) =>
            bytes.Select(b => (byte)b).ToArray();

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
        public void Compare_HpDropToZero_WhenMaxHpAlsoShifts_IsNotClassifiedAsKo()
        {
            // Live-repro 2026-04-24 Lenalian Plateau: Knight
            // [Defending] snapshot pre-turn showed HP=521/521. Mid-enemy-turn
            // chunk snap transiently read HP=0/524 (during the Defending
            // buff-drop animation; MaxHp shifted 521→524 as the buff
            // expired). Post-turn Knight was 521/524 alive. Narrator's
            // CounterAttackInferrer treated the transient as a real KO and
            // emitted "Ramza countered Knight for 521 dmg — Knight died".
            // Fix: when HP drops to 0 AND MaxHp changed in the same window,
            // downgrade to damaged-event (or drop entirely) — MaxHp shifts
            // are a reliable tell that the HP=0 reading is an animation
            // transient, not a real KO.
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Knight", team: 1, x: 7, y: 5, hp: 521, maxHp: 521),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Knight", team: 1, x: 7, y: 5, hp: 0, maxHp: 524),
            };
            var events = UnitScanDiff.Compare(before, after);
            // Must NOT be "ko". Allowed outcomes: "damaged" (HP shifted) or
            // "noop" (the entire transition treated as unreliable). The
            // narrator gates its "died" line on Kind=="ko", so suppressing
            // the ko classification is sufficient.
            foreach (var ev in events)
                Assert.NotEqual("ko", ev.Kind);
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
        public void Compare_TwoSameNameEnemiesBothMove_EmitsTwoMovedEvents_NotAddRemove()
        {
            // Session 52 live-reproducible bug: 2 Black Mages both move on
            // an enemy turn. Old Key(u) falls back to 'xy:X,Y' for
            // anonymous enemies (Name=null, RosterNameId=0), so the pre-
            // move XY never matches the post-move XY → each unit emits
            // a spurious remove+add pair instead of a single moved event.
            // Fix: disambiguate via ClassFingerprint + scan-order rank.
            var fp = Fp(0xA0, 0xB1, 0xC2, 0xD3, 0xE4, 0xF5, 0x06, 0x17, 0x28, 0x39, 0x4A);
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap(null, team: 1, x: 3, y: 4, hp: 100, maxHp: 100, classFingerprint: fp),
                Snap(null, team: 1, x: 5, y: 6, hp: 100, maxHp: 100, classFingerprint: fp),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap(null, team: 1, x: 4, y: 4, hp: 100, maxHp: 100, classFingerprint: fp),
                Snap(null, team: 1, x: 6, y: 6, hp: 100, maxHp: 100, classFingerprint: fp),
            };
            var events = UnitScanDiff.Compare(before, after);
            Assert.Equal(2, events.Count);
            Assert.All(events, e => Assert.Equal("moved", e.Kind));
            Assert.DoesNotContain(events, e => e.Kind == "removed" || e.Kind == "added");
        }

        [Fact]
        public void Compare_TwoSameJobNameEnemiesBothMove_EmitsTwoMovedEvents()
        {
            // Covers the case where enemies get a job-name surfaced as
            // UnitSnap.Name (e.g. Name="Black Mage" for both). The bare
            // name collides in the lookup dictionary: second entry
            // overwrites first. Disambiguator must rank by scan order.
            var fp = Fp(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B);
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Black Mage", team: 1, x: 3, y: 4, hp: 100, maxHp: 100, classFingerprint: fp),
                Snap("Black Mage", team: 1, x: 5, y: 6, hp: 100, maxHp: 100, classFingerprint: fp),
            };
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Black Mage", team: 1, x: 4, y: 4, hp: 100, maxHp: 100, classFingerprint: fp),
                Snap("Black Mage", team: 1, x: 6, y: 6, hp: 100, maxHp: 100, classFingerprint: fp),
            };
            var events = UnitScanDiff.Compare(before, after);
            Assert.Equal(2, events.Count);
            Assert.All(events, e => Assert.Equal("moved", e.Kind));
        }

        [Fact]
        public void Compare_SameNameDifferentFingerprints_MatchesByFingerprint()
        {
            // Two enemies surface the same job name but actually have
            // different fingerprints (e.g. a spawned clone vs. an
            // original of the same job — uncommon but possible).
            // Fingerprint should keep identity stable when one moves.
            var fpA = Fp(0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB);
            var fpB = Fp(0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11);
            var before = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Mage", 1, 3, 4, 100, 100, classFingerprint: fpA),
                Snap("Mage", 1, 5, 6, 100, 100, classFingerprint: fpB),
            };
            // fpB moves, fpA stays — order swapped in after (shouldn't
            // matter: fingerprint-based rank must still identify each)
            var after = new List<UnitScanDiff.UnitSnap>
            {
                Snap("Mage", 1, 6, 6, 100, 100, classFingerprint: fpB),
                Snap("Mage", 1, 3, 4, 100, 100, classFingerprint: fpA),
            };
            var events = UnitScanDiff.Compare(before, after);
            var moved = Assert.Single(events);
            Assert.Equal("moved", moved.Kind);
            Assert.Equal((5, 6), moved.OldXY);
            Assert.Equal((6, 6), moved.NewXY);
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
