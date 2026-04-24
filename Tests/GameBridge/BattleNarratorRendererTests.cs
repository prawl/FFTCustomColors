using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure renderer for enemy-turn narration appended to `battle_wait` response.Info.
    /// Takes the UnitScanDiff.ChangeEvent list from comparing pre-wait and post-wait
    /// unit snapshots + the active player's name. Returns a list of formatted
    /// "> ..." lines suitable for multi-line info rendering.
    ///
    /// Kinds handled:
    ///   moved     → "> Grenade moved (8,2) → (5,2)"
    ///   damaged   → "> Ramza took 100 damage (HP 623→523)"
    ///   healed    → "> Ramza recovered 30 HP (HP 500→530)"
    ///   ko        → "> Skeleton died"
    ///   revived   → "> Ramza revived (HP 0→50)"
    ///   status    → "> Ramza gained Poison, Slow" / "> Ramza lost Shell"
    ///   added/removed → skipped (not meaningful between turns)
    ///
    /// Truncates at 8 lines and appends "> ... (+N more)" if more exist — keeps
    /// the battle_wait response scannable when an enemy turn is busy.
    /// </summary>
    public class BattleNarratorRendererTests
    {
        private static UnitScanDiff.ChangeEvent Evt(
            string label, string kind,
            (int, int)? oldXY = null, (int, int)? newXY = null,
            int? oldHp = null, int? newHp = null,
            List<string>? gained = null, List<string>? lost = null,
            string team = "ENEMY")
        {
            return new UnitScanDiff.ChangeEvent(
                Label: label, Team: team,
                OldXY: oldXY, NewXY: newXY,
                OldHp: oldHp, NewHp: newHp,
                StatusesGained: gained, StatusesLost: lost,
                Kind: kind);
        }

        [Fact]
        public void EmptyEvents_ReturnsEmptyList()
        {
            var lines = BattleNarratorRenderer.Render(new List<UnitScanDiff.ChangeEvent>(), "Ramza");
            Assert.Empty(lines);
        }

        [Fact]
        public void MovedEvent_FormatsArrow()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Grenade", "moved", oldXY: (8, 2), newXY: (5, 2))
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Grenade moved (8,2) → (5,2)", lines[0]);
        }

        [Fact]
        public void DamagedEvent_FormatsTookDamage()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", oldHp: 623, newHp: 523, team: "PLAYER")
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza took 100 damage (HP 623→523)", lines[0]);
        }

        [Fact]
        public void HealedEvent_FormatsRecovered()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "healed", oldHp: 500, newHp: 530, team: "PLAYER")
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza recovered 30 HP (HP 500→530)", lines[0]);
        }

        [Fact]
        public void KoEvent_FormatsDied()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Skeleton", "ko", oldHp: 432, newHp: 0)
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Skeleton died", lines[0]);
        }

        [Fact]
        public void RevivedEvent_FormatsRevived()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "revived", oldHp: 0, newHp: 50, team: "PLAYER")
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza revived (HP 0→50)", lines[0]);
        }

        [Fact]
        public void StatusGained_FormatsGained()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "status", gained: new List<string> { "Poison", "Slow" }, team: "PLAYER")
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza gained Poison, Slow", lines[0]);
        }

        [Fact]
        public void StatusLost_FormatsLost()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "status", lost: new List<string> { "Shell" }, team: "PLAYER")
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza lost Shell", lines[0]);
        }

        [Fact]
        public void StatusGainedAndLost_EmitsBothLines()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "status",
                    gained: new List<string> { "Poison" },
                    lost: new List<string> { "Shell" },
                    team: "PLAYER")
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Equal(2, lines.Count);
            Assert.Equal("> Ramza gained Poison", lines[0]);
            Assert.Equal("> Ramza lost Shell", lines[1]);
        }

        [Fact]
        public void AddedAndRemovedEvents_AreSkipped()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ghost", "added", newXY: (3, 3), newHp: 400),
                Evt("Vanisher", "removed", oldXY: (5, 5), oldHp: 100),
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Empty(lines);
        }

        [Fact]
        public void TruncatesAt8Lines_AppendsMoreSummary()
        {
            var events = new List<UnitScanDiff.ChangeEvent>();
            for (int i = 0; i < 10; i++)
                events.Add(Evt($"Goblin{i}", "moved", oldXY: (i, 0), newXY: (i + 1, 0)));
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Equal(9, lines.Count); // 8 events + summary
            Assert.Equal("> Goblin0 moved (0,0) → (1,0)", lines[0]);
            Assert.Equal("> Goblin7 moved (7,0) → (8,0)", lines[7]);
            Assert.Equal("> ... (+2 more)", lines[8]);
        }

        [Fact]
        public void MultipleMixedEvents_PreserveOrder()
        {
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Grenade", "moved", oldXY: (8, 2), newXY: (5, 2)),
                Evt("Ramza", "damaged", oldHp: 623, newHp: 523, team: "PLAYER"),
                Evt("Skeleton", "ko", oldHp: 47, newHp: 0),
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Equal(3, lines.Count);
            Assert.Equal("> Grenade moved (8,2) → (5,2)", lines[0]);
            Assert.Equal("> Ramza took 100 damage (HP 623→523)", lines[1]);
            Assert.Equal("> Skeleton died", lines[2]);
        }

        [Fact]
        public void DamagedWithoutHpFields_OmitsHpSuffix()
        {
            // Defensive: ChangeEvent.Kind="damaged" but hp values missing for some reason
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Ramza", "damaged", team: "PLAYER")
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza took damage", lines[0]);
        }

        [Fact]
        public void MovedEventWithoutCoordinates_IsSkipped()
        {
            // Defensive: Kind="moved" but OldXY/NewXY missing
            var events = new List<UnitScanDiff.ChangeEvent> {
                Evt("Phantom", "moved")
            };
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Empty(lines);
        }
    }
}
