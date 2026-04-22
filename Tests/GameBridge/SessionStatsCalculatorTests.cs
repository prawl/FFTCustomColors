using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    // Pure helper that turns a session_*.jsonl row stream into per-action
    // latency statistics (count / median / p95 / max / failed). Enables
    // a one-liner `session_stats` to surface "which action types are
    // consistently slow this session?" without manual grep.
    public class SessionStatsCalculatorTests
    {
        private static SessionStatRow Row(string action, int latency, string status = "completed")
            => new SessionStatRow { Action = action, LatencyMs = latency, Status = status };

        [Fact]
        public void EmptyInput_ReturnsEmptyReport()
        {
            var report = SessionStatsCalculator.Compute(new List<SessionStatRow>());
            Assert.Empty(report.Actions);
            Assert.Equal(0, report.TotalRows);
            Assert.Equal(0, report.FailedRows);
        }

        [Fact]
        public void SingleRow_ProducesSingleActionEntry()
        {
            var rows = new List<SessionStatRow> { Row("screen", 200) };
            var report = SessionStatsCalculator.Compute(rows);

            Assert.Single(report.Actions);
            var entry = report.Actions[0];
            Assert.Equal("screen", entry.Action);
            Assert.Equal(1, entry.Count);
            Assert.Equal(200, entry.MedianMs);
            Assert.Equal(200, entry.P95Ms);
            Assert.Equal(200, entry.MaxMs);
            Assert.Equal(0, entry.Failed);
        }

        [Fact]
        public void MultipleActions_GroupedByAction()
        {
            var rows = new List<SessionStatRow>
            {
                Row("screen", 100),
                Row("scan_move", 220),
                Row("screen", 300),
                Row("scan_move", 180),
            };
            var report = SessionStatsCalculator.Compute(rows);

            Assert.Equal(2, report.Actions.Count);
            var screen = report.Actions.Find(a => a.Action == "screen");
            var scan = report.Actions.Find(a => a.Action == "scan_move");
            Assert.NotNull(screen);
            Assert.NotNull(scan);
            Assert.Equal(2, screen!.Count);
            Assert.Equal(2, scan!.Count);
        }

        [Fact]
        public void Median_TakesMiddleValue()
        {
            var rows = new List<SessionStatRow>
            {
                Row("act", 100),
                Row("act", 200),
                Row("act", 300),
            };
            var report = SessionStatsCalculator.Compute(rows);
            Assert.Equal(200, report.Actions[0].MedianMs);
        }

        [Fact]
        public void Median_EvenCount_UsesLowerMidpoint()
        {
            // Simple deterministic pick: sorted[count / 2 - 1] for even
            // counts avoids float math and is fine for monitoring-style
            // output. sorted = [100,200,300,400], lower mid = 200.
            var rows = new List<SessionStatRow>
            {
                Row("act", 100),
                Row("act", 200),
                Row("act", 300),
                Row("act", 400),
            };
            var report = SessionStatsCalculator.Compute(rows);
            Assert.Equal(200, report.Actions[0].MedianMs);
        }

        [Fact]
        public void P95_TakesCeilingIndex()
        {
            // With 10 values 100..1000, sorted[9] = 1000 is p95 ceiling.
            var rows = new List<SessionStatRow>();
            for (int i = 1; i <= 10; i++) rows.Add(Row("act", i * 100));
            var report = SessionStatsCalculator.Compute(rows);
            Assert.Equal(1000, report.Actions[0].P95Ms);
        }

        [Fact]
        public void FailedRows_CountedPerAction()
        {
            var rows = new List<SessionStatRow>
            {
                Row("screen", 100),
                Row("screen", 110, status: "failed"),
                Row("screen", 120, status: "blocked"),
                Row("scan_move", 200),
            };
            var report = SessionStatsCalculator.Compute(rows);
            Assert.Equal(4, report.TotalRows);
            Assert.Equal(2, report.FailedRows);
            var screen = report.Actions.Find(a => a.Action == "screen");
            Assert.Equal(2, screen!.Failed);
        }

        [Fact]
        public void ActionsSorted_ByMaxLatencyDescending()
        {
            // Slowest action types surface first — the point of the report.
            var rows = new List<SessionStatRow>
            {
                Row("fast", 50),
                Row("slow", 5000),
                Row("medium", 800),
            };
            var report = SessionStatsCalculator.Compute(rows);
            Assert.Equal("slow",   report.Actions[0].Action);
            Assert.Equal("medium", report.Actions[1].Action);
            Assert.Equal("fast",   report.Actions[2].Action);
        }

        [Fact]
        public void SlowRowsOverThreshold_CountedGlobally()
        {
            var rows = new List<SessionStatRow>
            {
                Row("a", 500),
                Row("a", 1800),  // under 2000
                Row("b", 2100),  // over
                Row("b", 3500),  // over
            };
            var report = SessionStatsCalculator.Compute(rows, slowThresholdMs: 2000);
            Assert.Equal(2, report.SlowRows);
        }

        [Fact]
        public void NullLatency_SkippedForStatsButCountedInTotal()
        {
            // Row without a latency value should still count toward total rows
            // but not corrupt median/p95/max.
            var rows = new List<SessionStatRow>
            {
                new SessionStatRow { Action = "act", Status = "completed" }, // no latency
                Row("act", 200),
            };
            var report = SessionStatsCalculator.Compute(rows);
            Assert.Equal(2, report.TotalRows);
            Assert.Equal(2, report.Actions[0].Count);
            Assert.Equal(200, report.Actions[0].MedianMs);
            Assert.Equal(200, report.Actions[0].MaxMs);
        }
    }
}
