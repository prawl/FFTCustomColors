using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Detect a player unit's crystallization (3-turn deathCounter
    /// expiry → "Treasure" or "Crystal" status appears) and emit a
    /// loud banner so the agent doesn't miss the permanent unit loss
    /// in a long enemy-turn dump. Mirrors the TURN HANDOFF banner
    /// shape. Live-flagged 2026-04-26 P3 playtest.
    /// </summary>
    public static class UnitLostBannerClassifier
    {
        public static string? BuildBanner(IReadOnlyList<UnitScanDiff.ChangeEvent>? events)
        {
            if (events == null || events.Count == 0) return null;
            var lostNames = new List<string>();
            foreach (var e in events)
            {
                if (e.Team != "PLAYER" && e.Team != "ALLY") continue;
                if (e.StatusesGained == null || e.StatusesGained.Count == 0) continue;
                if (!e.StatusesGained.Contains("Treasure") && !e.StatusesGained.Contains("Crystal"))
                    continue;
                lostNames.Add(e.Label);
            }
            if (lostNames.Count == 0) return null;
            string body = string.Join(" + ",
                lostNames.Select(n => $"{n} crystallized"));
            return $"=== UNIT LOST: {body} (permanent for this battle) ===";
        }
    }
}
