using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// `execute_turn` returns a TURN HANDOFF banner and the wait info, but
    /// no recap of what the agent's action actually DID. Live-flagged
    /// 2026-04-25 playtest: agent cast Hasteja and could only verify by
    /// HP-diffing prior `screen` output mentally; could not tell whether
    /// Phoenix Down landed.
    ///
    /// OutcomeRecapRenderer compresses a UnitScanDiff event list into a
    /// one-line `[OUTCOME] X took 42 dmg, Y +Haste, Z KO'd` summary that
    /// rides at the head of execute_turn's Info field.
    /// </summary>
    public class OutcomeRecapRendererTests
    {
        private static UnitScanDiff.ChangeEvent Damaged(string name, int from, int to, string team = "ENEMY")
            => new(name, team, OldXY: (5, 5), NewXY: (5, 5),
                OldHp: from, NewHp: to,
                StatusesGained: null, StatusesLost: null, Kind: "damaged");

        private static UnitScanDiff.ChangeEvent Healed(string name, int from, int to, string team = "PLAYER")
            => new(name, team, OldXY: (5, 5), NewXY: (5, 5),
                OldHp: from, NewHp: to,
                StatusesGained: null, StatusesLost: null, Kind: "healed");

        private static UnitScanDiff.ChangeEvent Ko(string name, string team = "ENEMY")
            => new(name, team, OldXY: (5, 5), NewXY: (5, 5),
                OldHp: 100, NewHp: 0,
                StatusesGained: null, StatusesLost: null, Kind: "ko");

        private static UnitScanDiff.ChangeEvent StatusGain(string name, params string[] gained)
            => new(name, "PLAYER", OldXY: (5, 5), NewXY: (5, 5),
                OldHp: 100, NewHp: 100,
                StatusesGained: new List<string>(gained), StatusesLost: null,
                Kind: "status");

        private static UnitScanDiff.ChangeEvent Moved(string name, int oldX, int oldY, int newX, int newY, string team = "ENEMY")
            => new(name, team, OldXY: (oldX, oldY), NewXY: (newX, newY),
                OldHp: 100, NewHp: 100,
                StatusesGained: null, StatusesLost: null, Kind: "moved");

        [Fact]
        public void EmptyEvents_ReturnsNull()
        {
            Assert.Null(OutcomeRecapRenderer.Render(new List<UnitScanDiff.ChangeEvent>()));
        }

        [Fact]
        public void NullEvents_ReturnsNull()
        {
            Assert.Null(OutcomeRecapRenderer.Render(null));
        }

        [Fact]
        public void SingleEnemyDamage_RendersUnderEnemiesSection()
        {
            // Damage to an enemy goes under [OUTCOME enemies] so the
            // agent can tell their attack landed (vs the prior single-
            // [OUTCOME] format that mixed enemy ambient buffs with the
            // player's intended action effects). 2026-04-26 P3 split.
            var events = new List<UnitScanDiff.ChangeEvent> { Damaged("Goblin", 100, 58) };
            Assert.Equal("[OUTCOME enemies] Goblin -42 HP", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void SinglePlayerHeal_RendersUnderYoursSection()
        {
            var events = new List<UnitScanDiff.ChangeEvent> { Healed("Ramza", 200, 350) };
            Assert.Equal("[OUTCOME yours] Ramza +150 HP", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void EnemyKo_RendersUnderEnemiesSection()
        {
            var events = new List<UnitScanDiff.ChangeEvent> { Ko("Skeleton") };
            Assert.Equal("[OUTCOME enemies] Skeleton KO'd", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void PlayerStatusGain_RendersUnderYoursSection()
        {
            var events = new List<UnitScanDiff.ChangeEvent> { StatusGain("Wilham", "Haste", "Regen") };
            Assert.Equal("[OUTCOME yours] Wilham +Haste,Regen", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void MultiplePlayerEffects_JoinsWithSlashUnderYours()
        {
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                StatusGain("Kenrick", "Haste"),
                StatusGain("Wilham", "Haste"),
            };
            Assert.Equal("[OUTCOME yours] Kenrick +Haste / Wilham +Haste",
                OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void MovedEvents_FilteredOut_OnlyEnemyDamageRenders()
        {
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Moved("Time Mage", 1, 4, 5, 4),
                Damaged("Goblin", 100, 58),
            };
            Assert.Equal("[OUTCOME enemies] Goblin -42 HP", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void OnlyMoves_ReturnsNull()
        {
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Moved("Time Mage", 1, 4, 5, 4),
            };
            Assert.Null(OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void DamageAndKo_BothEnemies_RenderUnderEnemies()
        {
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Damaged("Archer", 80, 30),
                Ko("Skeleton"),
            };
            Assert.Equal("[OUTCOME enemies] Archer -50 HP / Skeleton KO'd",
                OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void MixedTeams_RenderInTwoSections()
        {
            // The agent's case: Ramza attacks Knight, Wilham takes counter,
            // enemy Steel buff fires during animation. Splitting by team
            // makes "what your action did" visually distinct from "what
            // the enemy did to you."
            var enemyStatus = new UnitScanDiff.ChangeEvent(
                "Archer", "ENEMY", OldXY: (5, 5), NewXY: (5, 5),
                OldHp: 100, NewHp: 100,
                StatusesGained: new List<string> { "Defending" }, StatusesLost: null,
                Kind: "status");
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Damaged("Knight", 531, 468),                       // your attack landed
                Damaged("Wilham", 528, 492, team: "PLAYER"),       // ally counter-damage
                enemyStatus,                                        // enemy ambient buff
            };
            // Order: yours first, enemies second
            Assert.Equal(
                "[OUTCOME yours] Wilham -36 HP | [OUTCOME enemies] Knight -63 HP / Archer +Defending",
                OutcomeRecapRenderer.Render(events));
        }
    }
}
