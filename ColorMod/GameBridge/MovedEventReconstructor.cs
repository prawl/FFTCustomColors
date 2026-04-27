using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Recombine same-team <c>removed</c> + <c>added</c> event pairs back
    /// into single <c>moved</c> events when both endpoints lack stable
    /// identity (position-fallback labels of the form <c>(unit@x,y)</c>).
    ///
    /// <para>2026-04-26 Mandalia repro: 4 enemies on the field all named
    /// <c>[ENEMY]</c> with no class fingerprint or roster nameId. When
    /// one moves (8,5)→(6,4), <see cref="UnitScanDiff"/> falls back to
    /// position-derived keys, sees the old position empty + new position
    /// occupied, and emits separate <c>removed</c>/<c>added</c> events.
    /// <see cref="PhantomKoCoalescer"/> can't dedupe — labels are
    /// <c>(unit@8,5)</c> vs <c>(unit@6,4)</c> which differ.</para>
    ///
    /// <para>Pairing rule: same Team, identical OldHp/NewHp, both labels
    /// in <c>(unit@x,y)</c> shape. Real KO+spawn within one batch is
    /// rare AND wouldn't share an HP fingerprint, so collateral risk is
    /// low. Named units pass through unchanged — <see cref="UnitScanDiff"/>
    /// already handles them via name-based keys.</para>
    /// </summary>
    public static class MovedEventReconstructor
    {
        public static List<UnitScanDiff.ChangeEvent> Reconstruct(
            IReadOnlyList<UnitScanDiff.ChangeEvent> incoming)
        {
            var result = new List<UnitScanDiff.ChangeEvent>(incoming.Count);
            var consumed = new HashSet<int>();

            for (int i = 0; i < incoming.Count; i++)
            {
                if (consumed.Contains(i)) continue;
                var e = incoming[i];

                // Only consider position-fallback labels for pairing —
                // named units already have stable identities upstream.
                if (e.Kind != "removed" || !IsPositionFallback(e.Label))
                {
                    result.Add(e);
                    continue;
                }

                // Look ahead for a matching `added` partner.
                int partner = -1;
                for (int j = i + 1; j < incoming.Count; j++)
                {
                    if (consumed.Contains(j)) continue;
                    var c = incoming[j];
                    if (c.Kind != "added") continue;
                    if (!IsPositionFallback(c.Label)) continue;
                    if (c.Team != e.Team) continue;
                    if (e.OldHp != c.NewHp) continue;
                    partner = j;
                    break;
                }

                if (partner < 0)
                {
                    result.Add(e);
                    continue;
                }

                var p = incoming[partner];
                consumed.Add(partner);
                result.Add(new UnitScanDiff.ChangeEvent(
                    Label: e.Label,
                    Team: e.Team,
                    OldXY: e.OldXY,
                    NewXY: p.NewXY,
                    OldHp: null,
                    NewHp: null,
                    StatusesGained: null,
                    StatusesLost: null,
                    Kind: "moved"));
            }

            return result;
        }

        private static bool IsPositionFallback(string? label)
        {
            return !string.IsNullOrEmpty(label)
                && label!.StartsWith("(unit@")
                && label.EndsWith(")");
        }
    }
}
