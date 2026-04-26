using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Suppress phantom move events that exactly reverse a recent emit
    /// for the same unit. Mid-animation scan races sometimes catch a
    /// unit at a transient interpolated position then back at the real
    /// one, producing log lines like:
    ///   `> Knight moved (2,5) → (8,10)`
    ///   `> Knight moved (8,10) → (2,5)`
    /// where Knight could not actually have moved 8 tiles. The first
    /// emit gets through (we don't know yet it'll round-trip); the
    /// second is dropped if it cancels the first within the configured
    /// window. Live-flagged 2026-04-25 P2.
    /// </summary>
    public class MoveArtifactCoalescer
    {
        private record EmittedMove((int x, int y) Old, (int x, int y) New, DateTime At);
        private readonly Dictionary<string, EmittedMove> _recent = new();
        private readonly TimeSpan _window;

        public MoveArtifactCoalescer(TimeSpan window)
        {
            _window = window;
        }

        // Implausibly-large moves (Manhattan > this) are filtered as
        // identity-mismatch / scan-attribution artifacts. No FFT class
        // has Move + abilities exceeding ~8 tiles in a single turn (Move
        // stats max ~6, jumps max ~3). Live-flagged 2026-04-26 P3:
        // `> Lloyd moved (8,8) → (3,5)` (Manhattan=8) when Lloyd was
        // actually still at (8,8) — the post-snap mis-labeled some
        // enemy as Lloyd.
        private const int MaxPlausibleMoveDistance = 8;

        public List<UnitScanDiff.ChangeEvent> Filter(
            IReadOnlyList<UnitScanDiff.ChangeEvent> incoming, DateTime now)
        {
            var result = new List<UnitScanDiff.ChangeEvent>();
            foreach (var e in incoming)
            {
                if (e.Kind != "moved" || !e.OldXY.HasValue || !e.NewXY.HasValue)
                {
                    result.Add(e);
                    continue;
                }
                int dist = System.Math.Abs(e.OldXY.Value.x - e.NewXY.Value.x)
                    + System.Math.Abs(e.OldXY.Value.y - e.NewXY.Value.y);
                if (dist > MaxPlausibleMoveDistance)
                {
                    // Implausible — likely identity-mismatch in scan diff.
                    // Suppress without recording (so a follow-up real
                    // move for this unit emits cleanly).
                    continue;
                }
                string key = $"{e.Team}:{e.Label}";
                if (_recent.TryGetValue(key, out var recent)
                    && (now - recent.At) <= _window
                    && recent.Old == e.NewXY.Value
                    && recent.New == e.OldXY.Value)
                {
                    // Round-trip — suppress this event AND clear the
                    // recent entry so a third move (true direction)
                    // starts fresh.
                    _recent.Remove(key);
                    continue;
                }
                _recent[key] = new EmittedMove(e.OldXY.Value, e.NewXY.Value, now);
                result.Add(e);
            }
            return result;
        }

        public void ResetForNewBattle() => _recent.Clear();
    }
}
