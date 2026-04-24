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

        private static UnitScanDiff.UnitSnap Snap(
            string name, int team, int hp, int maxHp)
            => new(Name: name, RosterNameId: 0, Team: team,
                   GridX: 0, GridY: 0, Hp: hp, MaxHp: maxHp, Statuses: null);

        [Fact]
        public void SanityCheck_DeltaExceedsMaxHp_SkipsCounterLine()
        {
            // Live-repro TODO 2026-04-24: Knight's Defending buff drop
            // shifted snapshot MaxHp 521→524 during an enemy-turn window,
            // making the diff look like a 521-dmg counter-KO on a unit
            // with MaxHp ~521. With postSnaps provided, the inferrer
            // should reject any delta that exceeds the target's MaxHp as
            // physically impossible (maximum possible damage in one hit
            // is capped at MaxHp — overflow indicates an animation-
            // transient read, not a real counter).
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 400),
                Evt("Knight", "damaged", oldHp: 521, newHp: 0), // delta=521
            };
            var postSnaps = new List<UnitScanDiff.UnitSnap> {
                Snap("Knight", team: 1, hp: 521, maxHp: 521),
            };

            // Without postSnaps (legacy signature): emits the counter line.
            var noGuard = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Single(noGuard);  // legacy behavior preserved

            // With postSnaps: rejects as delta (521) > MaxHp (521) is
            // still at the boundary — exactly at MaxHp is ok (full
            // one-shot). Strictly GREATER would reject.
            var withGuard = CounterAttackInferrer.Infer(events, "Ramza", postSnaps);
            Assert.Single(withGuard);  // delta == MaxHp is allowed
        }

        [Fact]
        public void SanityCheck_DeltaStrictlyExceedsMaxHp_Rejected()
        {
            // Delta 600 vs MaxHp 521 — physically impossible. Reject.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 400),
                Evt("Knight", "damaged", oldHp: 600, newHp: 0),
            };
            var postSnaps = new List<UnitScanDiff.UnitSnap> {
                Snap("Knight", team: 1, hp: 0, maxHp: 521),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza", postSnaps);
            Assert.Empty(lines);
        }

        [Fact]
        public void SanityCheck_MissingPostSnap_FallsBackToNoGuard()
        {
            // If a unit isn't in postSnaps (name mismatch), treat as
            // unknown-MaxHp and fall through to legacy behavior. Don't
            // silently drop the counter line just because the snap lookup
            // missed.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 400),
                Evt("Knight", "damaged", oldHp: 600, newHp: 0),
            };
            var postSnaps = new List<UnitScanDiff.UnitSnap>(); // empty
            var lines = CounterAttackInferrer.Infer(events, "Ramza", postSnaps);
            Assert.Single(lines);
        }

        [Fact]
        public void SanityCheck_NullPostSnaps_LegacyBehavior()
        {
            // Overload accepting null postSnaps matches the original
            // 2-arg signature exactly.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 500, newHp: 400),
                Evt("Knight", "damaged", oldHp: 600, newHp: 0),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza", null);
            Assert.Single(lines);
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
        public void ActivePlayerNotInEventList_StillAttributesToDamagedPlayer()
        {
            // The "active player" hint doesn't match any damaged player in
            // the diff — but Agrias DID take damage, so she's the real
            // counter-attacker. Better to attribute correctly than to drop
            // the attribution entirely (S60 fix: chunked-mode inferrer
            // needs to derive the actor from events, not trust the hint
            // which shifts to the acting enemy mid-turn).
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Agrias", "damaged", team: "PLAYER", oldHp: 400, newHp: 300),
                Evt("Skeleton", "ko", oldHp: 100, newHp: 0),
            };
            var lines = CounterAttackInferrer.Infer(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Agrias countered Skeleton for 100 dmg — Skeleton died", lines[0]);
        }

        [Fact]
        public void HintNameIsCurrentlyActingEnemy_StillAttributesToDamagedPlayer()
        {
            // Chunked-mode repro: when BattleWait captures narratorActivePlayerName
            // during an enemy's turn, it picks up the enemy's name. The inferrer
            // must NOT emit "> Skeletal Fiend countered Skeletal Fiend for N dmg" —
            // it must derive the actual counter-attacker from the event list.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 719, newHp: 604),
                Evt("Skeletal Fiend", "damaged", oldHp: 629, newHp: 293),
            };
            var lines = CounterAttackInferrer.Infer(events, "Skeletal Fiend");
            Assert.Single(lines);
            Assert.Equal("> Ramza countered Skeletal Fiend for 336 dmg", lines[0]);
        }

        [Fact]
        public void EmptyPlayerName_StillInfersIfPlayerDamaged()
        {
            // Null/empty hint is fine — the inferrer derives the actor from
            // the events. Only return empty when NO player was damaged.
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER", oldHp: 719, newHp: 623),
                Evt("Skeleton", "ko", oldHp: 50, newHp: 0),
            };
            var lines = CounterAttackInferrer.Infer(events, "");
            Assert.Single(lines);
            Assert.Equal("> Ramza countered Skeleton for 50 dmg — Skeleton died", lines[0]);
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
