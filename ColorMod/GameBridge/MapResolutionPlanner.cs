namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Session 48: pure decision logic for picking which map-resolution strategy to
    /// try first during battle setup. The dispatch-layer (NavigationActions) calls
    /// these in a fallthrough chain. This helper exists so the priority order is
    /// testable without mocking MemoryExplorer / MapLoader.
    ///
    /// Priority (session 48 2026-04-19):
    ///   0. Scenario struct on heap — authoritative; the game's own pointer to
    ///      the currently-loaded map. Fixes the random-encounter-at-wrong-loc
    ///      bug where screen.location stuck at the travel target.
    ///   1. Random-encounter map lookup — keyed by locId, known correct for
    ///      battleground random encounters when the scenario struct isn't findable.
    ///   2. Story-battle map lookup — keyed by locId, for named story battles.
    ///   3. Fingerprint-based detection — compare live tile heights to every
    ///      candidate map, pick best match. Last-resort.
    /// </summary>
    public static class MapResolutionPlanner
    {
        public enum Strategy
        {
            ScenarioStruct,
            RandomEncounter,
            StoryBattle,
            Fingerprint,
            None,
        }

        public class Inputs
        {
            public int ScenarioMapId { get; set; } = -1;
            public int LocId { get; set; } = -1;
            public bool HasRandomEncounterMap { get; set; }
            public bool HasStoryBattleMap { get; set; }
            public bool HasFingerprint { get; set; }
        }

        /// <summary>
        /// Returns the ordered list of strategies to try given the inputs.
        /// Empty list means "no viable strategy"; caller should bail.
        /// </summary>
        public static System.Collections.Generic.List<Strategy> Plan(Inputs inputs)
        {
            var order = new System.Collections.Generic.List<Strategy>();

            if (inputs.ScenarioMapId >= 1 && inputs.ScenarioMapId <= 127)
                order.Add(Strategy.ScenarioStruct);

            if (inputs.LocId >= 0 && inputs.LocId <= 42)
            {
                if (inputs.HasRandomEncounterMap) order.Add(Strategy.RandomEncounter);
                if (inputs.HasStoryBattleMap) order.Add(Strategy.StoryBattle);
            }

            if (inputs.HasFingerprint) order.Add(Strategy.Fingerprint);

            return order;
        }
    }
}
