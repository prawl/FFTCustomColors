using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure diff planner for enemy/ally-turn play-by-play reporting.
    /// Session 51 shipment of `project_enemy_turn_report_design.md`.
    ///
    /// Given two snapshots of the unit list (before/after a turn), emits
    /// a sequence of change events describing what moved, what took
    /// damage, what got status-inflicted, etc. Identity-matched by a
    /// stable key (name, falling back to roster nameId, falling back to
    /// pre-snapshot position).
    /// </summary>
    public static class UnitScanDiff
    {
        public record UnitSnap(
            string? Name,
            int RosterNameId,
            int Team,
            int GridX,
            int GridY,
            int Hp,
            int MaxHp,
            List<string>? Statuses,
            byte[]? ClassFingerprint = null
        );

        public record ChangeEvent(
            string Label,       // "Archer", "Ramza", "(Skeleton #2)" etc.
            string Team,        // "PLAYER", "ENEMY", "ALLY"
            (int x, int y)? OldXY,
            (int x, int y)? NewXY,
            int? OldHp,
            int? NewHp,
            List<string>? StatusesGained,
            List<string>? StatusesLost,
            string Kind         // "moved", "damaged", "healed", "ko", "revived", "status", "removed", "added", "noop"
        );

        /// <summary>
        /// Build base identity key for matching before/after. Combines
        /// the strongest available identifiers — name (or roster nameId),
        /// AND the 11-byte class fingerprint when present. The fingerprint
        /// keeps same-named enemies (e.g. Agrias vs a knight enemy both
        /// surfacing as "Knight") distinguishable when their jobs differ.
        /// Falls back to xy only if literally nothing else is available.
        /// When two units share the same base key (e.g. 2 Black Mages
        /// with identical fingerprints), <see cref="KeyInList"/> layers
        /// a scan-order rank on top.
        /// </summary>
        public static string Key(UnitSnap u)
        {
            string head;
            if (!string.IsNullOrEmpty(u.Name)) head = $"name:{u.Name}";
            else if (u.RosterNameId > 0) head = $"roster:{u.RosterNameId}";
            else if (u.ClassFingerprint != null && u.ClassFingerprint.Length > 0)
                return $"fp:{FpHex(u.ClassFingerprint)}";
            else return $"xy:{u.GridX},{u.GridY}";

            if (u.ClassFingerprint != null && u.ClassFingerprint.Length > 0)
                return $"{head}|fp:{FpHex(u.ClassFingerprint)}";
            return head;
        }

        private static string FpHex(byte[] fp)
        {
            var sb = new System.Text.StringBuilder(fp.Length * 2);
            foreach (var b in fp) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        /// <summary>
        /// Identity key for a unit in the context of a scan list. If
        /// the base <see cref="Key(UnitSnap)"/> is unique within the
        /// list, returns the base key unchanged. If multiple units
        /// share the same base key (duplicate-name enemies with
        /// identical fingerprints — e.g. two Black Mages with the same
        /// class), appends "#N" where N is the 0-based scan-order rank
        /// so each one gets a stable per-scan identifier. Assumes scan
        /// order is stable between the before/after snapshots being
        /// compared (which is true for same-battle repeat scans).
        /// </summary>
        public static string KeyInList(UnitSnap u, IReadOnlyList<UnitSnap> all)
        {
            string baseK = Key(u);
            int rank = 0;
            foreach (var other in all)
            {
                if (ReferenceEquals(other, u)) break;
                if (Key(other) == baseK) rank++;
            }
            return rank == 0 ? baseK : $"{baseK}#{rank}";
        }

        private static string TeamLabel(int t) => t == 0 ? "PLAYER" : t == 2 ? "ALLY" : "ENEMY";

        private static List<string> ListDiff(List<string>? from, List<string>? to)
        {
            var result = new List<string>();
            if (to == null) return result;
            var fromSet = new HashSet<string>(from ?? new List<string>());
            foreach (var s in to)
                if (!fromSet.Contains(s)) result.Add(s);
            return result;
        }

        /// <summary>
        /// Compare two snapshots, emit ordered change events. Enumerates
        /// in "before" order, then appends events for units present only
        /// in "after" (spawned/revealed).
        /// </summary>
        public static List<ChangeEvent> Compare(
            IReadOnlyList<UnitSnap> before,
            IReadOnlyList<UnitSnap> after)
        {
            var events = new List<ChangeEvent>();
            if (before == null || after == null) return events;

            // Build lookup maps keyed by rank-aware identity so
            // duplicate-name units (e.g. two Black Mages with identical
            // fingerprints) stay distinguishable.
            var afterByKey = new Dictionary<string, UnitSnap>();
            foreach (var a in after) afterByKey[KeyInList(a, after)] = a;
            var beforeKeys = new HashSet<string>();

            foreach (var b in before)
            {
                string k = KeyInList(b, before);
                beforeKeys.Add(k);
                if (!afterByKey.TryGetValue(k, out var a))
                {
                    // unit disappeared (dead + removed? crystallized?)
                    events.Add(new ChangeEvent(
                        Label: b.Name ?? $"(unit@{b.GridX},{b.GridY})",
                        Team: TeamLabel(b.Team),
                        OldXY: (b.GridX, b.GridY),
                        NewXY: null,
                        OldHp: b.Hp,
                        NewHp: null,
                        StatusesGained: null,
                        StatusesLost: null,
                        Kind: "removed"));
                    continue;
                }

                bool moved = b.GridX != a.GridX || b.GridY != a.GridY;
                bool hpChanged = b.Hp != a.Hp;
                bool ko = b.Hp > 0 && a.Hp == 0;
                bool revived = b.Hp == 0 && a.Hp > 0;
                var gained = ListDiff(b.Statuses, a.Statuses);
                var lost = ListDiff(a.Statuses, b.Statuses);
                bool statusChanged = gained.Count > 0 || lost.Count > 0;

                if (!moved && !hpChanged && !statusChanged) continue;

                string kind = ko ? "ko"
                    : revived ? "revived"
                    : (hpChanged && a.Hp < b.Hp) ? "damaged"
                    : (hpChanged && a.Hp > b.Hp) ? "healed"
                    : moved ? "moved"
                    : "status";

                events.Add(new ChangeEvent(
                    Label: a.Name ?? $"(unit@{a.GridX},{a.GridY})",
                    Team: TeamLabel(a.Team),
                    OldXY: moved ? ((int x, int y)?)(b.GridX, b.GridY) : null,
                    NewXY: moved ? ((int x, int y)?)(a.GridX, a.GridY) : null,
                    OldHp: hpChanged ? (int?)b.Hp : null,
                    NewHp: hpChanged ? (int?)a.Hp : null,
                    StatusesGained: gained.Count > 0 ? gained : null,
                    StatusesLost: lost.Count > 0 ? lost : null,
                    Kind: kind));
            }

            foreach (var a in after)
            {
                string k = KeyInList(a, after);
                if (beforeKeys.Contains(k)) continue;
                events.Add(new ChangeEvent(
                    Label: a.Name ?? $"(unit@{a.GridX},{a.GridY})",
                    Team: TeamLabel(a.Team),
                    OldXY: null,
                    NewXY: (a.GridX, a.GridY),
                    OldHp: null,
                    NewHp: a.Hp,
                    StatusesGained: null,
                    StatusesLost: null,
                    Kind: "added"));
            }

            return events;
        }

        /// <summary>
        /// One-line render for shell output. Example:
        /// "Ramza (8,10)→(7,10) HP 719→649 (-70) [+Poison]"
        /// </summary>
        public static string RenderEvent(ChangeEvent e)
        {
            var parts = new List<string>();
            parts.Add($"{e.Label}");
            if (e.OldXY.HasValue && e.NewXY.HasValue)
                parts.Add($"({e.OldXY.Value.x},{e.OldXY.Value.y})→({e.NewXY.Value.x},{e.NewXY.Value.y})");
            else if (e.NewXY.HasValue && e.Kind == "added")
                parts.Add($"appeared@({e.NewXY.Value.x},{e.NewXY.Value.y})");
            else if (e.OldXY.HasValue && e.Kind == "removed")
                parts.Add($"gone from ({e.OldXY.Value.x},{e.OldXY.Value.y})");

            if (e.OldHp.HasValue && e.NewHp.HasValue)
            {
                int delta = e.NewHp.Value - e.OldHp.Value;
                string sign = delta >= 0 ? "+" : "";
                parts.Add($"HP {e.OldHp.Value}→{e.NewHp.Value} ({sign}{delta})");
            }
            if (e.StatusesGained != null && e.StatusesGained.Count > 0)
                parts.Add($"[+{string.Join(",", e.StatusesGained)}]");
            if (e.StatusesLost != null && e.StatusesLost.Count > 0)
                parts.Add($"[-{string.Join(",", e.StatusesLost)}]");
            if (e.Kind == "ko") parts.Add("[KO]");
            else if (e.Kind == "revived") parts.Add("[REVIVED]");

            return string.Join(" ", parts);
        }
    }
}
