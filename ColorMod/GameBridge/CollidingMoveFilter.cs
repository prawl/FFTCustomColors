using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Drop `moved` events whose destination tile is occupied by a
    /// DIFFERENT-named unit in the post-snap. Defends against the
    /// rank-based identity collision that <see cref="UnitScanDiff.KeyInList"/>
    /// produces for duplicate-name enemies (3 Skeletons all sharing the
    /// same `Key`/fingerprint, distinguished only by scan-order rank).
    /// When scan order shifts between before/after snaps, the rank-based
    /// matching can attribute one Skeleton's move to another's
    /// destination — including impossible destinations like a tile held
    /// by Ramza.
    ///
    /// <para>Live-flagged 2026-04-26 at Siedge Weald: narrator emitted
    /// `&gt; Skeleton moved (3,7) → (3,3)` while Ramza was at (3,3).
    /// This filter drops that phantom.</para>
    ///
    /// <para>Same-name destinations pass through — the move could
    /// genuinely be that named unit relocating; we can't distinguish
    /// without per-unit identity, but at least it's not impossible.</para>
    /// </summary>
    public static class CollidingMoveFilter
    {
        public static List<UnitScanDiff.ChangeEvent> Filter(
            IReadOnlyList<UnitScanDiff.ChangeEvent> incoming,
            IReadOnlyList<UnitScanDiff.UnitSnap> postSnap)
        {
            // Index post-snap unit positions by their (x, y) for cheap
            // lookups. Keep a list of names per tile in case multiple
            // units somehow share a tile (shouldn't in normal play but
            // be defensive).
            var occupants = new Dictionary<(int x, int y), List<string>>();
            foreach (var u in postSnap)
            {
                if (string.IsNullOrEmpty(u.Name)) continue;
                var key = (u.GridX, u.GridY);
                if (!occupants.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    occupants[key] = list;
                }
                list.Add(u.Name!);
            }

            var result = new List<UnitScanDiff.ChangeEvent>(incoming.Count);
            foreach (var e in incoming)
            {
                if (e.Kind == "moved" && e.NewXY.HasValue
                    && occupants.TryGetValue(e.NewXY.Value, out var names))
                {
                    // If ANY occupant of the destination has a DIFFERENT
                    // name from this event's label, the attribution is
                    // wrong — drop the event.
                    bool anyDifferent = false;
                    foreach (var n in names)
                    {
                        if (n != e.Label) { anyDifferent = true; break; }
                    }
                    if (anyDifferent) continue;
                }
                result.Add(e);
            }
            return result;
        }
    }
}
