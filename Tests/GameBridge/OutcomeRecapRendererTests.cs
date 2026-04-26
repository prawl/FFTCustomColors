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
        public void SingleDamage_RendersDamageLine()
        {
            var events = new List<UnitScanDiff.ChangeEvent> { Damaged("Goblin", 100, 58) };
            Assert.Equal("[OUTCOME] Goblin -42 HP", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void SingleHeal_RendersHealLine()
        {
            var events = new List<UnitScanDiff.ChangeEvent> { Healed("Ramza", 200, 350) };
            Assert.Equal("[OUTCOME] Ramza +150 HP", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void SingleKo_RendersKoMarker()
        {
            var events = new List<UnitScanDiff.ChangeEvent> { Ko("Skeleton") };
            Assert.Equal("[OUTCOME] Skeleton KO'd", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void StatusGain_RendersStatusList()
        {
            var events = new List<UnitScanDiff.ChangeEvent> { StatusGain("Wilham", "Haste", "Regen") };
            Assert.Equal("[OUTCOME] Wilham +Haste,Regen", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void MultipleEffects_JoinsWithSlash()
        {
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                StatusGain("Kenrick", "Haste"),
                StatusGain("Wilham", "Haste"),
            };
            Assert.Equal("[OUTCOME] Kenrick +Haste / Wilham +Haste",
                OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void MovedEvents_FilteredOut()
        {
            // Movement is rendered separately by the narrator '> X moved' lines.
            // Outcome recap focuses on action effects (HP, status, KO).
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Moved("Time Mage", 1, 4, 5, 4),
                Damaged("Goblin", 100, 58),
            };
            Assert.Equal("[OUTCOME] Goblin -42 HP", OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void OnlyMoves_ReturnsNull()
        {
            // No action-effects → no recap (don't surface a noisy banner).
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Moved("Time Mage", 1, 4, 5, 4),
            };
            Assert.Null(OutcomeRecapRenderer.Render(events));
        }

        [Fact]
        public void DamageAndKo_BothRender()
        {
            var events = new List<UnitScanDiff.ChangeEvent>
            {
                Damaged("Archer", 80, 30),
                Ko("Skeleton"),
            };
            Assert.Equal("[OUTCOME] Archer -50 HP / Skeleton KO'd",
                OutcomeRecapRenderer.Render(events));
        }
    }
}
