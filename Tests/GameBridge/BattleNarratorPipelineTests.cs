using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Integration: exercises the full pre-snap → UnitScanDiff.Compare → BattleNarratorRenderer
    /// pipeline that BattleWait uses to produce enemy-turn narration. Fake UnitSnap lists
    /// stand in for live memory reads; we assert the narrator output matches what Claude
    /// will see appended to response.Info.
    /// </summary>
    public class BattleNarratorPipelineTests
    {
        private static UnitScanDiff.UnitSnap Unit(string name, int team, int x, int y, int hp, int maxHp, List<string>? statuses = null)
            => new(Name: name, RosterNameId: 0, Team: team, GridX: x, GridY: y, Hp: hp, MaxHp: maxHp, Statuses: statuses);

        [Fact]
        public void NoChanges_BetweenSnapshots_ProducesNoLines()
        {
            var pre = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 719, 719),
                Unit("Skeleton", 1, 7, 2, 620, 620),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 719, 719),
                Unit("Skeleton", 1, 7, 2, 620, 620),
            };
            var events = UnitScanDiff.Compare(pre, post);
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Empty(lines);
        }

        [Fact]
        public void EnemyMovesAndHitsPlayer_ProducesMovedAndDamagedLines()
        {
            var pre = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 719, 719),
                Unit("Grenade", 1, 8, 2, 383, 383),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 619, 719),
                Unit("Grenade", 1, 5, 2, 383, 383),
            };
            var events = UnitScanDiff.Compare(pre, post);
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Equal(2, lines.Count);
            // Order: iterated over "before" list, so Ramza first then Grenade
            Assert.Equal("> Ramza took 100 damage (HP 719→619)", lines[0]);
            Assert.Equal("> Grenade moved (8,2) → (5,2)", lines[1]);
        }

        [Fact]
        public void CounterKoScenario_EnemyDiesDuringEnemyTurn()
        {
            // Enemy walked adjacent to Ramza and got counter-KO'd: Ramza took some
            // damage, enemy HP went to 0.
            var pre = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 719, 719),
                Unit("Skeleton", 1, 4, 1, 47, 620),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 700, 719),
                Unit("Skeleton", 1, 3, 1, 0, 620),
            };
            var events = UnitScanDiff.Compare(pre, post);
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Equal(2, lines.Count);
            Assert.Equal("> Ramza took 19 damage (HP 719→700)", lines[0]);
            // "ko" wins over "moved" in the diff engine, so the skeleton's death
            // is surfaced rather than its final position.
            Assert.Equal("> Skeleton died", lines[1]);
        }

        [Fact]
        public void RegenTickOnAlivePlayer_SurfaceHeal()
        {
            // Regen ticks mid-turn — Ramza recovered HP with no ally heal
            var pre = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 519, 719),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 609, 719),
            };
            var events = UnitScanDiff.Compare(pre, post);
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza recovered 90 HP (HP 519→609)", lines[0]);
        }

        [Fact]
        public void StatusInflictedDuringEnemyTurn_SurfacesStatus()
        {
            var pre = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 719, 719, new List<string> { "Regen", "Protect" }),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 719, 719, new List<string> { "Regen", "Protect", "Poison", "Slow" }),
            };
            var events = UnitScanDiff.Compare(pre, post);
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Equal("> Ramza gained Poison, Slow", lines[0]);
        }

        [Fact]
        public void FiveEnemiesActEachTurn_AllRenderedInOrder()
        {
            var pre = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 719, 719),
                Unit("Grenade", 1, 8, 2, 383, 383),
                Unit("Bomb", 1, 5, 5, 467, 467),
                Unit("Skeleton1", 1, 7, 2, 620, 620),
                Unit("Skeleton2", 1, 6, 4, 432, 432),
            };
            var post = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 619, 719),
                Unit("Grenade", 1, 5, 2, 383, 383),
                Unit("Bomb", 1, 3, 3, 467, 467),
                Unit("Skeleton1", 1, 4, 2, 620, 620),
                Unit("Skeleton2", 1, 4, 4, 432, 432),
            };
            var events = UnitScanDiff.Compare(pre, post);
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Equal(5, lines.Count);
            Assert.Contains("> Ramza took 100 damage", lines[0]);
            Assert.Contains("> Grenade moved", lines[1]);
            Assert.Contains("> Bomb moved", lines[2]);
            Assert.Contains("> Skeleton1 moved", lines[3]);
            Assert.Contains("> Skeleton2 moved", lines[4]);
        }

        [Fact]
        public void NullSnapshot_ShortCircuitsPipeline()
        {
            // Pre-snap capture failure path: when CaptureCurrentUnitSnapshot
            // returns null, BattleWait skips the whole narrator block.
            //
            // 2026-04-26: with the post-snap empty (1 pre unit → 0 post),
            // every pre unit's removal looks like a death. The renderer
            // now surfaces "removed with OldHp>0" as a death line (fixes
            // the playtest-#3 bug where a real Skeleton KO produced no
            // narrator entry). For THIS hypothetical test scenario that's
            // working as designed: if the post-snap really was empty we'd
            // want to know something went wrong. In production this code
            // path is guarded upstream (CaptureCurrentUnitSnapshot returns
            // null → BattleWait short-circuits before Compare runs), so
            // this test now pins the renderer's surface contract rather
            // than the silent-fallback behavior.
            var pre = new List<UnitScanDiff.UnitSnap> {
                Unit("Ramza", 0, 2, 1, 719, 719),
            };
            var post = new List<UnitScanDiff.UnitSnap>(); // empty, no post-wait data
            var events = UnitScanDiff.Compare(pre, post);
            var lines = BattleNarratorRenderer.Render(events, "Ramza");
            Assert.Single(lines);
            Assert.Contains("Ramza", lines[0]);
            Assert.Contains("died", lines[0]);
        }
    }
}
