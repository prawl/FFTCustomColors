using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure inferrer: detect a bomb self-destruct pattern in an event window.
    ///
    /// Pattern: one enemy dies (ko) AND two or more OTHER units (any team)
    /// take damage in the same window. We attribute the area damage to the
    /// dying enemy's Self-Destruct. If the enemy died alone (no splash) or
    /// only one other unit took damage, we stay silent — the regular
    /// narrator already reports the kill + hit individually.
    ///
    /// Returns rendered "> ..." lines. Never suppresses existing events.
    /// </summary>
    public class SelfDestructInferrerTests
    {
        private static UnitScanDiff.ChangeEvent Evt(
            string label, string kind, string team = "ENEMY",
            int? oldHp = null, int? newHp = null)
        {
            return new UnitScanDiff.ChangeEvent(
                Label: label, Team: team,
                OldXY: null, NewXY: null,
                OldHp: oldHp, NewHp: newHp,
                StatusesGained: null, StatusesLost: null,
                Kind: kind);
        }

        [Fact]
        public void Empty_ReturnsEmpty()
        {
            var lines = SelfDestructInferrer.Infer(new List<UnitScanDiff.ChangeEvent>());
            Assert.Empty(lines);
        }

        [Fact]
        public void SingleEnemyDiesNoSplash_ReturnsEmpty()
        {
            // Normal kill, no self-destruct.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Bomb", "ko", oldHp: 50, newHp: 0),
            };
            var lines = SelfDestructInferrer.Infer(events);
            Assert.Empty(lines);
        }

        [Fact]
        public void EnemyDiesOneOtherDamaged_ReturnsEmpty()
        {
            // Only one other unit hit — ambiguous, could just be a regular attack
            // that happened to coincide with the enemy being finished off.
            // Require 2+ damaged for the self-destruct inference.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Bomb", "ko", oldHp: 50, newHp: 0),
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 400),
            };
            var lines = SelfDestructInferrer.Infer(events);
            Assert.Empty(lines);
        }

        [Fact]
        public void EnemyDiesTwoOthersDamaged_InfersSelfDestruct()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Bomb", "ko", oldHp: 50, newHp: 0),
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 350),
                Evt("Agrias", "damaged", team: "PLAYER", oldHp: 400, newHp: 280),
            };
            var lines = SelfDestructInferrer.Infer(events);
            Assert.Single(lines);
            Assert.Equal("> Bomb self-destructed (dealt 150 to Ramza, 120 to Agrias)", lines[0]);
        }

        [Fact]
        public void TwoBombsBothSelfDestruct_EmitsTwoLines()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Bomb1", "ko", oldHp: 50, newHp: 0),
                Evt("Bomb2", "ko", oldHp: 50, newHp: 0),
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 200),
                Evt("Agrias", "damaged", team: "PLAYER", oldHp: 400, newHp: 200),
                Evt("Beowulf", "damaged", team: "PLAYER", oldHp: 600, newHp: 400),
            };
            var lines = SelfDestructInferrer.Infer(events);
            Assert.Equal(2, lines.Count);
            Assert.Contains("> Bomb1 self-destructed", lines[0]);
            Assert.Contains("> Bomb2 self-destructed", lines[1]);
        }

        [Fact]
        public void BombDiesWithMixedTeamDamage_CountsAll()
        {
            // Self-destruct doesn't care about team — the splash hits everything
            // in range (including other enemies).
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Bomb", "ko", oldHp: 50, newHp: 0),
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 400),
                Evt("Skeleton", "damaged", team: "ENEMY", oldHp: 600, newHp: 500),
            };
            var lines = SelfDestructInferrer.Infer(events);
            Assert.Single(lines);
            Assert.Contains("Ramza", lines[0]);
            Assert.Contains("Skeleton", lines[0]);
        }

        [Fact]
        public void KoEventWithoutHpFields_IsSkipped()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Bomb", "ko"),
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 400),
                Evt("Agrias", "damaged", team: "PLAYER", oldHp: 400, newHp: 300),
            };
            var lines = SelfDestructInferrer.Infer(events);
            // Bomb ko has no hp data but the self-destruct pattern still holds —
            // we can still attribute the multi-hit to the dying bomb.
            Assert.Single(lines);
        }

        [Fact]
        public void DamagedEventsWithoutHpFields_AreSkipped()
        {
            // If the "damaged" events have no hp data, we can't compute the splash
            // amounts — skip those units from the count.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Bomb", "ko", oldHp: 50, newHp: 0),
                Evt("Ramza", "damaged", team: "PLAYER"),
                Evt("Agrias", "damaged", team: "PLAYER"),
            };
            var lines = SelfDestructInferrer.Infer(events);
            Assert.Empty(lines);
        }

        [Fact]
        public void HealedEventsNotCounted()
        {
            // Regen ticks shouldn't count toward the splash-damage tally.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Bomb", "ko", oldHp: 50, newHp: 0),
                Evt("Ramza", "healed", team: "PLAYER", oldHp: 500, newHp: 580),
                Evt("Agrias", "healed", team: "PLAYER", oldHp: 400, newHp: 480),
            };
            var lines = SelfDestructInferrer.Infer(events);
            Assert.Empty(lines);
        }
    }
}
