using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Suppress phantom-KO event clusters in narrator batches. When a
    /// transient empty/partial scan during a wait poll drops a unit (or
    /// produces a duplicate at HP=0), <see cref="UnitScanDiff.Compare"/>
    /// can emit BOTH a damaged-to-zero/ko event AND an added/joined event
    /// for the same unit name in the same batch. Both are wrong — the
    /// unit was alive throughout.
    ///
    /// <para>Heuristic: same Label + Team appearing in {ko OR damagedTo0 OR
    /// statusGainedDead} AND in {added} within one batch is a phantom.
    /// Drop every event for that label.</para>
    ///
    /// <para>Live-flagged 2026-04-26: Time Mage took 345 damage to HP=0 +
    /// gained Dead + lost Haste, immediately followed by joined at (3,4),
    /// despite being alive throughout. The "joined" right after the "ko"
    /// is the tell.</para>
    /// </summary>
    public static class PhantomKoCoalescer
    {
        public static List<UnitScanDiff.ChangeEvent> Filter(
            IReadOnlyList<UnitScanDiff.ChangeEvent> incoming)
        {
            // Identify labels that have BOTH a death-event AND an added-event.
            var deathLabels = new HashSet<(string label, string team)>();
            var addedLabels = new HashSet<(string label, string team)>();
            foreach (var e in incoming)
            {
                bool isDeath = e.Kind == "ko"
                    || (e.Kind == "damaged" && e.NewHp.HasValue && e.NewHp.Value == 0)
                    // "removed" with OldHp>0 is the other phantom shape:
                    // the unit was alive in `before` but missing in `after`
                    // due to a transient bad scan; usually paired with an
                    // "added" for the same name in the next batch.
                    || (e.Kind == "removed" && e.OldHp.HasValue && e.OldHp.Value > 0);
                if (isDeath) deathLabels.Add((e.Label, e.Team));
                if (e.Kind == "added") addedLabels.Add((e.Label, e.Team));
            }

            var phantomLabels = new HashSet<(string, string)>();
            foreach (var key in deathLabels)
            {
                if (addedLabels.Contains(key)) phantomLabels.Add(key);
            }

            if (phantomLabels.Count == 0)
            {
                // Materialize the result list to a stable concrete type so
                // callers always get a writable List back.
                var passthrough = new List<UnitScanDiff.ChangeEvent>(incoming.Count);
                passthrough.AddRange(incoming);
                return passthrough;
            }

            // Drop every event for the phantom labels (death, joined, AND
            // status flips like gained Dead / lost Haste — they're all
            // artifacts of the same transient bad scan).
            var result = new List<UnitScanDiff.ChangeEvent>(incoming.Count);
            foreach (var e in incoming)
            {
                if (phantomLabels.Contains((e.Label, e.Team))) continue;
                result.Add(e);
            }
            return result;
        }
    }
}
