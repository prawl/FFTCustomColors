using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Drop battle-unit-state entries whose MaxHp doesn't appear in
    /// the current static-array snapshot's active-slot MaxHp set.
    /// Defends against stale residue from prior battles polluting
    /// the emitted <c>state.units[]</c> when <see cref="BattleTracker"/>'s
    /// battle-exit detection misfires.
    ///
    /// <para>2026-04-26 Mandalia Plain repro: scan response listed 17
    /// units (4 actual + 13 residue) including Lv25 entries on a Ch1
    /// map and ghost units stacked at (4,6) with positionKnown=false.</para>
    ///
    /// <para>The active unit (IsActive=true) is always kept regardless
    /// of MaxHp membership — its slot may be transiently absent from
    /// the snapshot during certain animations, and losing the player's
    /// own data would be worse than letting one residue entry through.</para>
    ///
    /// <para>Pass-through if the caller can't build an active set
    /// (null) — first-frame defensive behavior, don't strip data we
    /// can't validate.</para>
    /// </summary>
    public static class StaleBattleUnitFilter
    {
        public static List<BattleUnitState> Filter(
            IReadOnlyList<BattleUnitState> units,
            IReadOnlySet<int>? activeMaxHps)
        {
            var result = new List<BattleUnitState>(units.Count);
            if (activeMaxHps == null)
            {
                result.AddRange(units);
                return result;
            }
            foreach (var u in units)
            {
                if (u.IsActive || activeMaxHps.Contains(u.MaxHp))
                    result.Add(u);
            }
            return result;
        }
    }
}
