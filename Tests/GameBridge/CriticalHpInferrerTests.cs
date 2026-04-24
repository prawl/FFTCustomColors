using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class CriticalHpInferrerTests
    {
        private static UnitScanDiff.ChangeEvent Damaged(
            string label, int oldHp, int newHp, string team = "PLAYER")
            => new(Label: label, Team: team,
                   OldXY: null, NewXY: null,
                   OldHp: oldHp, NewHp: newHp,
                   StatusesGained: null, StatusesLost: null,
                   Kind: "damaged");

        private static UnitScanDiff.UnitSnap Snap(
            string name, int team, int hp, int maxHp)
            => new(Name: name, RosterNameId: 0, Team: team,
                   GridX: 0, GridY: 0, Hp: hp, MaxHp: maxHp, Statuses: null);

        [Fact]
        public void EmptyEvents_ReturnsEmpty()
        {
            var lines = CriticalHpInferrer.Infer(
                new List<UnitScanDiff.ChangeEvent>(),
                new List<UnitScanDiff.UnitSnap>());
            Assert.Empty(lines);
        }

        [Fact]
        public void PlayerCrossesCriticalThreshold_EmitsLine()
        {
            // Ramza 719 MaxHp → threshold = 239. Dropped from 400 to 180 — crossed.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 400, newHp: 180),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 180, maxHp: 719),
            };
            var lines = CriticalHpInferrer.Infer(events, post);
            Assert.Single(lines);
            Assert.Equal("> Ramza reached critical HP (400→180/719)", lines[0]);
        }

        [Fact]
        public void PlayerStaysAboveCritical_NoLine()
        {
            // 400 → 300, threshold 239 — still above, not critical.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 400, newHp: 300),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 300, maxHp: 719),
            };
            Assert.Empty(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void PlayerAlreadyCriticalBeforeHit_NoDuplicateLine()
        {
            // 200 → 100, threshold 239 — was already critical coming in.
            // Don't re-surface — the narrator would have already flagged
            // the earlier crossing; re-firing on every subsequent hit is
            // noisy.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 200, newHp: 100),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 100, maxHp: 719),
            };
            Assert.Empty(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void PlayerDroppedToZero_NotReportedAsCritical()
        {
            // KO is its own narrator line — don't double-report as critical.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 400, newHp: 0),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 0, maxHp: 719),
            };
            Assert.Empty(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void EnemyCrossesCriticalThreshold_NoLine()
        {
            // Only PLAYER units get the critical-HP heads-up.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Skeleton", oldHp: 400, newHp: 100, team: "ENEMY"),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Skeleton", team: 1, hp: 100, maxHp: 620),
            };
            Assert.Empty(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void MissingMaxHp_NoLine()
        {
            // Defensive: if the post-snap lookup fails (unit not found,
            // MaxHp reads 0), skip — can't compute threshold without MaxHp.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 400, newHp: 180),
            };
            var post = new List<UnitScanDiff.UnitSnap>();
            Assert.Empty(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void MultiplePlayersCrossing_EmitsMultipleLines()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 400, newHp: 180),
                Damaged("Agrias", oldHp: 350, newHp: 120),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 180, maxHp: 719),
                Snap("Agrias", team: 0, hp: 120, maxHp: 400),
            };
            var lines = CriticalHpInferrer.Infer(events, post);
            Assert.Equal(2, lines.Count);
            Assert.Contains("Ramza reached critical HP", lines[0]);
            Assert.Contains("Agrias reached critical HP", lines[1]);
        }

        [Fact]
        public void HpLandsExactlyOnThreshold_EmitsLine()
        {
            // MaxHp=720 → threshold=240 (integer div). Dropping to exactly
            // 240 should count as critical (<=threshold is the rule).
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 500, newHp: 240),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 240, maxHp: 720),
            };
            var lines = CriticalHpInferrer.Infer(events, post);
            Assert.Single(lines);
        }

        [Fact]
        public void PreHpExactlyAtThreshold_NoLine()
        {
            // Pre-HP at exactly threshold means we were already critical
            // coming in — the rule requires strict above-threshold start.
            // MaxHp=720 → threshold=240. 240→100 shouldn't re-fire.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 240, newHp: 100),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 100, maxHp: 720),
            };
            Assert.Empty(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void TinyMaxHp_StillFiresOnCrossing()
        {
            // MaxHp=6, threshold=2. From 3 (above) → 1 (critical). Rule
            // should still fire even with integer-math on tiny HP pools.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 3, newHp: 1),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 1, maxHp: 6),
            };
            Assert.Single(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void MaxHpOne_ThresholdZero_NeverCritical()
        {
            // MaxHp=1 edge case: threshold = 1/3 = 0. Rule requires
            // newHp<=threshold (<=0), which the nowCritical > 0 guard
            // rejects. KO is reported via Kind=ko, not critical.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 1, newHp: 1),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 1, maxHp: 1),
            };
            Assert.Empty(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void MultipleDamageEventsOnSameUnit_FiresOnceWhenCrossing()
        {
            // If a mid-chunk chunked scan emits two damaged events for
            // the same unit (e.g. multiple attacks landed in the poll
            // window), the FIRST crossing fires. The second event with
            // already-critical pre-HP is silently skipped by the
            // "wasAboveCritical" guard.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 400, newHp: 200),  // crosses 239 threshold
                Damaged("Ramza", oldHp: 200, newHp: 50),   // already critical — skip
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 50, maxHp: 719),
            };
            Assert.Single(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void NegativeNewHp_NotReportedAsCritical()
        {
            // Defensive: if a diff emits a negative newHp (shouldn't
            // happen but guard against transient reads), the nowCritical
            // `newHp > 0` check keeps us silent rather than reporting a
            // bogus critical line.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Damaged("Ramza", oldHp: 400, newHp: -1),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: -1, maxHp: 719),
            };
            Assert.Empty(CriticalHpInferrer.Infer(events, post));
        }

        [Fact]
        public void HealedEventNotConsidered_NoLine()
        {
            // Kind="healed" — not a damage event. Never fires critical line.
            var events = new List<UnitScanDiff.ChangeEvent> {
                new(Label: "Ramza", Team: "PLAYER", OldXY: null, NewXY: null,
                    OldHp: 100, NewHp: 200, StatusesGained: null, StatusesLost: null,
                    Kind: "healed"),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Snap("Ramza", team: 0, hp: 200, maxHp: 719),
            };
            Assert.Empty(CriticalHpInferrer.Infer(events, post));
        }
    }
}
