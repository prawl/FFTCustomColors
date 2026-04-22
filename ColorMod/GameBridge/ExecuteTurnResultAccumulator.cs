using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Minimal unit snapshot for kill-diff computation. Matched by Name;
    /// disappeared-from-scan is treated as kill if HP was positive before.
    /// </summary>
    public record UnitSnapshot(string Name, string? JobName, int Team, int Hp, int MaxHp);

    public record KilledUnitInfo(string Name, string? JobName, int Team);

    /// <summary>
    /// Accumulates per-sub-step state across an execute_turn bundle so
    /// the returned response can expose deltas (HP / position / kills)
    /// across the full turn rather than only the last sub-step's raw
    /// PostAction. Pure helper — callers seed with the pre-bundle state
    /// and record each step's PostAction.
    /// </summary>
    public class ExecuteTurnResultAccumulator
    {
        public PostActionState? InitialPostAction { get; private set; }
        public PostActionState? FinalPostAction { get; private set; }

        /// <summary>HP delta = final - initial. Null if either endpoint missing.</summary>
        public int? HpDelta
            => (InitialPostAction != null && FinalPostAction != null)
                ? FinalPostAction.Hp - InitialPostAction.Hp
                : (int?)null;

        /// <summary>Active-unit X before the bundle started. Null if unseeded.</summary>
        public int? PreMoveX => InitialPostAction?.X;
        public int? PreMoveY => InitialPostAction?.Y;

        /// <summary>Active-unit X after the bundle's last step. Null if no steps.</summary>
        public int? PostMoveX => FinalPostAction?.X;
        public int? PostMoveY => FinalPostAction?.Y;

        public void Seed(PostActionState? initial)
        {
            InitialPostAction = initial;
        }

        public void RecordStep(string action, PostActionState? postAction)
        {
            if (postAction != null)
                FinalPostAction = postAction;
        }

        public List<KilledUnitInfo> KilledUnits { get; } = new();

        /// <summary>
        /// Diff a pre-bundle vs post-bundle unit scan. Any unit that was
        /// alive (HP > 0) before and is either HP &lt;= 0 after OR missing
        /// from the after-scan entirely counts as a kill. Crystallized /
        /// despawned units drop out of the scan naturally.
        /// </summary>
        public void RecordScanDiff(
            IEnumerable<UnitSnapshot> before,
            IEnumerable<UnitSnapshot> after)
        {
            if (before == null) return;
            var afterByName = after == null
                ? new Dictionary<string, UnitSnapshot>()
                : after.GroupBy(u => u.Name).ToDictionary(g => g.Key, g => g.First());

            foreach (var pre in before)
            {
                if (pre.Hp <= 0) continue; // already dead — no new kill
                if (afterByName.TryGetValue(pre.Name, out var post))
                {
                    if (post.Hp <= 0)
                        KilledUnits.Add(new KilledUnitInfo(pre.Name, pre.JobName, pre.Team));
                }
                else
                {
                    // Disappeared from scan — treat as kill (crystallized/despawned).
                    KilledUnits.Add(new KilledUnitInfo(pre.Name, pre.JobName, pre.Team));
                }
            }
        }
    }
}
