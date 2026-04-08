using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Predicts turn order by simulating CT accumulation.
    /// Each tick, every unit gains Speed to their CT. When CT >= 100, that unit acts.
    /// After acting, CT resets to 0. This mirrors FFT's actual turn system.
    /// Uses base Speed (may differ slightly from effective Speed with equipment).
    /// </summary>
    public static class TurnOrderPredictor
    {
        public static List<TurnOrderEntry> Predict(
            List<(string? name, string team, int ct, int speed)> units,
            int maxTurns = 9)
        {
            var result = new List<TurnOrderEntry>();
            if (units.Count == 0) return result;

            // Filter out units with no speed (dead, stopped, etc.)
            var active = units.Where(u => u.speed > 0).ToList();
            if (active.Count == 0) return result;

            // Simulate CT accumulation
            var simCTs = active.Select(u => (double)u.ct).ToArray();

            for (int tick = 0; tick < 1000 && result.Count < maxTurns; tick++)
            {
                // Add Speed to all units' CT
                for (int i = 0; i < active.Count; i++)
                    simCTs[i] += active[i].speed;

                // Check who crossed 100 this tick (highest CT first for tie-breaking)
                var acting = new List<(int index, double ct)>();
                for (int i = 0; i < active.Count; i++)
                {
                    if (simCTs[i] >= 100)
                        acting.Add((i, simCTs[i]));
                }

                // Sort by CT descending (higher CT = acted first this tick)
                acting.Sort((a, b) => b.ct.CompareTo(a.ct));

                foreach (var (idx, ct) in acting)
                {
                    if (result.Count >= maxTurns) break;

                    result.Add(new TurnOrderEntry
                    {
                        Name = active[idx].name,
                        Team = active[idx].team,
                        CT = (int)ct,
                    });

                    simCTs[idx] -= 100; // Reset after acting
                }
            }

            return result;
        }
    }
}
