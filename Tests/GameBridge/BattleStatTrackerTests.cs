using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleStatTrackerTests
    {
        [Fact]
        public void StartAndEndBattle_TracksWin()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Zeklaus Desert");
            tracker.OnDamageDealt("Ramza", "Goblin", 100, "Throw Stone");
            tracker.OnKill("Ramza", "Goblin");
            tracker.EndBattle(won: true);

            Assert.Equal(1, tracker.Lifetime.TotalBattles);
            Assert.Equal(1, tracker.Lifetime.BattlesWon);
            Assert.Equal(0, tracker.Lifetime.BattlesLost);
        }

        [Fact]
        public void DamageDealt_TrackedForBothAttackerAndTarget()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Test");
            tracker.OnDamageDealt("Ramza", "Goblin", 150, "Rush");

            Assert.Equal(150, tracker.CurrentBattle!.Units["Ramza"].DamageDealt);
            Assert.Equal(150, tracker.CurrentBattle.Units["Goblin"].DamageReceived);
        }

        [Fact]
        public void Healing_Tracked()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Test");
            tracker.OnHeal("Ramza", "Kenrick", 150, "X-Potion");

            Assert.Equal(150, tracker.CurrentBattle!.Units["Ramza"].HealingDealt);
        }

        [Fact]
        public void KillsAndKOs_Tracked()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Test");
            tracker.OnKill("Ramza", "Goblin");
            tracker.OnKill("Ramza", "Grenade");

            Assert.Equal(2, tracker.CurrentBattle!.Units["Ramza"].Kills);
            Assert.Equal(1, tracker.CurrentBattle.Units["Goblin"].TimesKOd);
        }

        [Fact]
        public void TilesMoved_Accumulated()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Test");
            tracker.OnMove("Ramza", 3);
            tracker.OnMove("Ramza", 4);

            Assert.Equal(7, tracker.CurrentBattle!.Units["Ramza"].TilesMoved);
        }

        [Fact]
        public void AbilityUsage_Counted()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Test");
            tracker.OnAbilityUsed("Ramza", "Throw Stone");
            tracker.OnAbilityUsed("Ramza", "Throw Stone");
            tracker.OnAbilityUsed("Ramza", "Focus");

            Assert.Equal(2, tracker.CurrentBattle!.Units["Ramza"].AbilityUsage["Throw Stone"]);
            Assert.Equal(1, tracker.CurrentBattle.Units["Ramza"].AbilityUsage["Focus"]);
        }

        [Fact]
        public void LowestHp_TracksMinimum()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Test");
            tracker.OnHpLow("Ramza", 200, 719);
            tracker.OnHpLow("Ramza", 46, 719);
            tracker.OnHpLow("Ramza", 300, 719); // higher, should not override

            Assert.Equal(46, tracker.CurrentBattle!.Units["Ramza"].LowestHp);
        }

        [Fact]
        public void MvpSelection_HighestScoreWins()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Test");
            tracker.OnDamageDealt("Ramza", "A", 500);
            tracker.OnKill("Ramza", "A");
            tracker.OnDamageDealt("Kenrick", "B", 1200);
            tracker.OnKill("Kenrick", "B");
            tracker.OnKill("Kenrick", "C");
            tracker.EndBattle(won: true);

            // Kenrick: 2 kills * 300 + 1200 dmg = 1800
            // Ramza: 1 kill * 300 + 500 dmg = 800
            Assert.Equal("Kenrick", tracker.CurrentBattle!.Mvp);
        }

        [Fact]
        public void LifetimeStats_AccumulateAcrossBattles()
        {
            var tracker = new BattleStatTracker();

            tracker.StartBattle("Battle 1");
            tracker.OnDamageDealt("Ramza", "A", 500);
            tracker.OnKill("Ramza", "A");
            tracker.EndBattle(won: true);

            tracker.StartBattle("Battle 2");
            tracker.OnDamageDealt("Ramza", "B", 300);
            tracker.EndBattle(won: true);

            Assert.Equal(2, tracker.Lifetime.Units["Ramza"].TotalBattles);
            Assert.Equal(800, tracker.Lifetime.Units["Ramza"].TotalDamageDealt);
            Assert.Equal(1, tracker.Lifetime.Units["Ramza"].TotalKills);
        }

        [Fact]
        public void RenderBattleSummary_ContainsMvpAndUnits()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Zeklaus Desert");
            tracker.OnDamageDealt("Ramza", "Goblin", 400, "Throw Stone");
            tracker.OnKill("Ramza", "Goblin");
            tracker.OnDamageDealt("Kenrick", "Grenade", 200);
            tracker.EndBattle(won: true);

            var summary = tracker.RenderBattleSummary();
            Assert.Contains("BATTLE COMPLETE", summary);
            Assert.Contains("Zeklaus Desert", summary);
            Assert.Contains("MVP: Ramza", summary);
            Assert.Contains("Kenrick", summary);
        }

        [Fact]
        public void RenderLifetimeSummary_ContainsAllUnits()
        {
            var tracker = new BattleStatTracker();
            tracker.StartBattle("Test");
            tracker.OnDamageDealt("Ramza", "A", 1000);
            tracker.OnDamageDealt("Lloyd", "B", 500);
            tracker.EndBattle(won: true);

            var summary = tracker.RenderLifetimeSummary();
            Assert.Contains("LIFETIME STATS", summary);
            Assert.Contains("Ramza", summary);
            Assert.Contains("Lloyd", summary);
            Assert.Contains("1000 dmg", summary);
        }
    }
}
