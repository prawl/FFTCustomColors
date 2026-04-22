using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    // Pure helper: consume a sequence of session-log rows and emit a
    // per-action-type latency summary. Keeps the stats math testable
    // and the bridge wrapper thin.

    public class SessionStatRow
    {
        public string Action { get; set; } = "";
        public int? LatencyMs { get; set; }
        public string Status { get; set; } = "";
    }

    public class SessionStatEntry
    {
        public string Action { get; set; } = "";
        public int Count { get; set; }
        public int Failed { get; set; }
        public int MedianMs { get; set; }
        public int P95Ms { get; set; }
        public int MaxMs { get; set; }
    }

    public class SessionStatsReport
    {
        public int TotalRows { get; set; }
        public int FailedRows { get; set; }
        public int SlowRows { get; set; }
        public List<SessionStatEntry> Actions { get; set; } = new();
    }

    public static class SessionStatsCalculator
    {
        public static SessionStatsReport Compute(
            IEnumerable<SessionStatRow> rows,
            int slowThresholdMs = 2000)
        {
            var list = rows?.ToList() ?? new List<SessionStatRow>();
            var report = new SessionStatsReport
            {
                TotalRows = list.Count,
                FailedRows = list.Count(r => r.Status != "completed"),
                SlowRows = list.Count(r => r.LatencyMs.HasValue && r.LatencyMs.Value >= slowThresholdMs),
            };

            var grouped = list.GroupBy(r => r.Action ?? "");
            foreach (var g in grouped)
            {
                var latencies = g.Where(r => r.LatencyMs.HasValue)
                                 .Select(r => r.LatencyMs!.Value)
                                 .OrderBy(v => v)
                                 .ToList();
                var entry = new SessionStatEntry
                {
                    Action = g.Key,
                    Count = g.Count(),
                    Failed = g.Count(r => r.Status != "completed"),
                    MedianMs = Percentile(latencies, 0.5, middleStrategy: MiddleStrategy.LowerMid),
                    P95Ms = Percentile(latencies, 0.95, middleStrategy: MiddleStrategy.Ceiling),
                    MaxMs = latencies.Count > 0 ? latencies[^1] : 0,
                };
                report.Actions.Add(entry);
            }

            report.Actions = report.Actions
                .OrderByDescending(a => a.MaxMs)
                .ToList();

            return report;
        }

        private enum MiddleStrategy { LowerMid, Ceiling }

        private static int Percentile(List<int> sorted, double p, MiddleStrategy middleStrategy)
        {
            if (sorted.Count == 0) return 0;
            if (sorted.Count == 1) return sorted[0];

            if (middleStrategy == MiddleStrategy.LowerMid)
            {
                // Lower midpoint: sorted[count/2 - 1] for even count,
                // sorted[count/2] for odd. Simple and deterministic.
                int idx = (sorted.Count % 2 == 0)
                    ? (sorted.Count / 2 - 1)
                    : (sorted.Count / 2);
                return sorted[idx];
            }

            // Ceiling: index = ceil(p * (count - 1)).
            int ceilIdx = (int)System.Math.Ceiling(p * (sorted.Count - 1));
            if (ceilIdx >= sorted.Count) ceilIdx = sorted.Count - 1;
            return sorted[ceilIdx];
        }
    }
}
