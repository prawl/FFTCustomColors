using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class MapResolutionPlannerTests
    {
        [Fact]
        public void ScenarioStructComesFirst()
        {
            var inputs = new MapResolutionPlanner.Inputs
            {
                ScenarioMapId = 86,
                LocId = 28,
                HasRandomEncounterMap = true,
                HasStoryBattleMap = false,
                HasFingerprint = true,
            };
            var order = MapResolutionPlanner.Plan(inputs);
            Assert.Equal(MapResolutionPlanner.Strategy.ScenarioStruct, order[0]);
        }

        [Fact]
        public void ZeklausToDugeuraBugRegression()
        {
            // Session 48 scenario: travel target Zeklaus (loc 28), encounter fires
            // at Dugeura Pass (map 86). Scenario struct reports 86 authoritatively.
            // Without the scenario-struct branch, we'd have loaded the random-
            // encounter map for loc 28 which is Zeklaus (map 76) — wrong.
            var inputs = new MapResolutionPlanner.Inputs
            {
                ScenarioMapId = 86,
                LocId = 28,
                HasRandomEncounterMap = true,
                HasStoryBattleMap = false,
                HasFingerprint = true,
            };
            var order = MapResolutionPlanner.Plan(inputs);
            Assert.Equal(MapResolutionPlanner.Strategy.ScenarioStruct, order[0]);
            Assert.Equal(MapResolutionPlanner.Strategy.RandomEncounter, order[1]);
        }

        [Fact]
        public void ScenarioStructOutOfRangeSkipped()
        {
            var inputs = new MapResolutionPlanner.Inputs
            {
                ScenarioMapId = 0,  // invalid
                LocId = 28,
                HasRandomEncounterMap = true,
                HasStoryBattleMap = false,
                HasFingerprint = false,
            };
            var order = MapResolutionPlanner.Plan(inputs);
            Assert.Single(order);
            Assert.Equal(MapResolutionPlanner.Strategy.RandomEncounter, order[0]);
        }

        [Fact]
        public void ScenarioStructTooHighSkipped()
        {
            var inputs = new MapResolutionPlanner.Inputs
            {
                ScenarioMapId = 200,  // out of 1..127 range
                LocId = 6,
                HasStoryBattleMap = true,
            };
            var order = MapResolutionPlanner.Plan(inputs);
            Assert.Single(order);
            Assert.Equal(MapResolutionPlanner.Strategy.StoryBattle, order[0]);
        }

        [Fact]
        public void LocIdOutOfRangeSkipsRandomAndStory()
        {
            var inputs = new MapResolutionPlanner.Inputs
            {
                ScenarioMapId = -1,
                LocId = 99,  // out of 0..42 range
                HasRandomEncounterMap = true,
                HasStoryBattleMap = true,
                HasFingerprint = true,
            };
            var order = MapResolutionPlanner.Plan(inputs);
            Assert.Single(order);
            Assert.Equal(MapResolutionPlanner.Strategy.Fingerprint, order[0]);
        }

        [Fact]
        public void NoViableStrategyReturnsEmpty()
        {
            var inputs = new MapResolutionPlanner.Inputs
            {
                ScenarioMapId = -1,
                LocId = -1,
                HasRandomEncounterMap = false,
                HasStoryBattleMap = false,
                HasFingerprint = false,
            };
            var order = MapResolutionPlanner.Plan(inputs);
            Assert.Empty(order);
        }

        [Fact]
        public void StoryBattleAvailableScenarioStructFirst()
        {
            var inputs = new MapResolutionPlanner.Inputs
            {
                ScenarioMapId = 4,
                LocId = 6,   // Gariland
                HasRandomEncounterMap = false,
                HasStoryBattleMap = true,
                HasFingerprint = true,
            };
            var order = MapResolutionPlanner.Plan(inputs);
            Assert.Equal(MapResolutionPlanner.Strategy.ScenarioStruct, order[0]);
            Assert.Equal(MapResolutionPlanner.Strategy.StoryBattle, order[1]);
            Assert.Equal(MapResolutionPlanner.Strategy.Fingerprint, order[2]);
        }

        [Fact]
        public void FallbackChainComplete()
        {
            var inputs = new MapResolutionPlanner.Inputs
            {
                ScenarioMapId = 86,
                LocId = 28,
                HasRandomEncounterMap = true,
                HasStoryBattleMap = true,
                HasFingerprint = true,
            };
            var order = MapResolutionPlanner.Plan(inputs);
            Assert.Equal(4, order.Count);
            Assert.Equal(MapResolutionPlanner.Strategy.ScenarioStruct, order[0]);
            Assert.Equal(MapResolutionPlanner.Strategy.RandomEncounter, order[1]);
            Assert.Equal(MapResolutionPlanner.Strategy.StoryBattle, order[2]);
            Assert.Equal(MapResolutionPlanner.Strategy.Fingerprint, order[3]);
        }
    }
}
