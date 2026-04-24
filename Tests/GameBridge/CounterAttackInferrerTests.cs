using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure inferrer: given a list of UnitScanDiff.ChangeEvent from one battle_wait
    /// window + the active player's name, synthesize "> {Player} countered {Enemy}
    /// for N dmg" narration lines when the pattern appears.
    ///
    /// Pattern: within a single event window, the active player took damage
    /// AND at least one enemy died. We infer the enemy's HP drop came from
    /// the player's counter-attack. If the enemy just lost HP (not killed),
    /// we still infer a counter but tag it "(no KO)". Without an active-unit
    /// byte we can't confirm attribution, so this is a best-effort inference.
    ///
    /// Returns rendered "> ..." lines ready to append to the narrator output.
    /// Never suppresses existing events — only adds synthesized ones.
    /// </summary>
    public class CounterAttackInferrerTests
    {
        private static UnitScanDiff.ChangeEvent Evt(
            string label, string kind, string team = "ENEMY",
            int? oldHp = null, int? newHp = null,
            (int, int)? oldXY = null, (int, int)? newXY = null)
        {
            return new UnitScanDiff.ChangeEvent(
                Label: label, Team: team,
                OldXY: oldXY, NewXY: newXY,
                OldHp: oldHp, NewHp: newHp,
                StatusesGained: null, StatusesLost: null,
                Kind: kind);
        }

        [Fact]
        public void Empty_ReturnsEmpty()
        {
            var lines = CounterAttackInferrer.Infer(new List<UnitScanDiff.ChangeEvent>(), "Ramza");
            Assert.Empty(lines);
        }

        [Fact]
        public void PlayerDamagedOnly_NoCounterInferred()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 400),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Empty(lines);
        }

        [Fact]
        public void EnemyDiedOnly_NoCounterInferred()
        {
            // An enemy died but the player took no damage — could be many things
            // (ally kill, bomb timer, self-destruct, etc). Don't infer counter.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Skeleton", "ko", oldHp: 50, newHp: 0),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Empty(lines);
        }

        [Fact]
        public void PlayerDamagedAndEnemyKilled_InfersCounterKo()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 719, newHp: 623),
                Evt("Skeleton", "ko", oldHp: 432, newHp: 0),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza countered Skeleton for 432 dmg — Skeleton died", lines[0]);
        }

        [Fact]
        public void PlayerDamagedAndEnemyDamagedNotKilled_InfersCounterHit()
        {
            // Enemy took damage but survived (Ramza's counter wasn't enough to KO).
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 719, newHp: 623),
                Evt("Skeleton", "damaged", oldHp: 620, newHp: 188),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza countered Skeleton for 432 dmg", lines[0]);
        }

        [Fact]
        public void PlayerDamagedAndMultipleEnemiesKilled_InfersMultipleCounters()
        {
            // Two enemies walked adjacent to Ramza and both got counter-KO'd.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 719, newHp: 539),
                Evt("Skeleton", "ko", oldHp: 300, newHp: 0),
                Evt("Bomb", "ko", oldHp: 250, newHp: 0),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Equal(2, lines.Count);
            Assert.Contains("> Ramza countered Skeleton for 300 dmg — Skeleton died", lines);
            Assert.Contains("> Ramza countered Bomb for 250 dmg — Bomb died", lines);
        }

        [Fact]
        public void ActivePlayerNotInEventList_ReturnsEmpty()
        {
            // The "active player" Claude passes doesn't appear in the diff —
            // maybe an ally was doing the countering. Don't claim Ramza did it.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Agrias", "damaged", team: "PLAYER", oldHp: 400, newHp: 300),
                Evt("Skeleton", "ko", oldHp: 100, newHp: 0),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Empty(lines);
        }

        [Fact]
        public void EmptyPlayerName_ReturnsEmpty()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 719, newHp: 623),
                Evt("Skeleton", "ko", oldHp: 50, newHp: 0),
            };
            var lines = CounterAttackInferrer.Infer(events, "");
            Assert.Empty(lines);
        }

        [Fact]
        public void EnemyWithoutHpValues_IsSkipped()
        {
            // Defensive: an enemy with kind="ko" but no hp fields shouldn't blow up.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 719, newHp: 623),
                Evt("Skeleton", "ko"),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Empty(lines);
        }

        [Fact]
        public void PlayerHealedNotDamaged_NoCounterInferred()
        {
            // Player HP increased (Regen tick, ally heal) → not a hit → no counter.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "healed", team: "PLAYER", oldHp: 500, newHp: 580),
                Evt("Skeleton", "ko", oldHp: 50, newHp: 0),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Empty(lines);
        }
    }
}
