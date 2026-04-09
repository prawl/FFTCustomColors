using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    public struct RosterSlot
    {
        public int NameId;
        public int Level;
        public int Brave;
        public int Faith;
        public int Job;
        public int Secondary;
    }

    public struct ScannedUnitIdentity
    {
        public int Level;
        public int Brave;
        public int Faith;
        public int Hp;
    }

    public struct RosterMatchResult
    {
        public int NameId;
        public int Job;
        public int Brave;
        public int Faith;
        public int Secondary;
    }

    /// <summary>
    /// Matches scanned battle units to roster slots by level + brave + faith.
    /// Units with known Brave/Faith (non-zero) are matched first.
    /// The active unit (Brave=0, Faith=0) is matched last to the remaining unclaimed slot.
    /// </summary>
    public static class RosterMatcher
    {
        public static RosterMatchResult[] Match(ScannedUnitIdentity[] scannedUnits, RosterSlot[] rosterSlots)
        {
            var results = new RosterMatchResult[scannedUnits.Length];
            var claimedSlots = new HashSet<int>();

            // Pass 1: match units with known Brave/Faith (exact match)
            for (int u = 0; u < scannedUnits.Length; u++)
            {
                if (scannedUnits[u].Brave == 0 && scannedUnits[u].Faith == 0)
                    continue; // skip active unit for now

                for (int s = 0; s < rosterSlots.Length; s++)
                {
                    if (claimedSlots.Contains(s)) continue;
                    if (rosterSlots[s].NameId <= 0) continue;

                    if (rosterSlots[s].Level == scannedUnits[u].Level
                        && rosterSlots[s].Brave == scannedUnits[u].Brave
                        && rosterSlots[s].Faith == scannedUnits[u].Faith)
                    {
                        claimedSlots.Add(s);
                        results[u] = new RosterMatchResult
                        {
                            NameId = rosterSlots[s].NameId,
                            Job = rosterSlots[s].Job,
                            Brave = rosterSlots[s].Brave,
                            Faith = rosterSlots[s].Faith,
                            Secondary = rosterSlots[s].Secondary,
                        };
                        break;
                    }
                }
            }

            // Pass 2: match units with unknown Brave/Faith (active unit)
            // to the remaining unclaimed slot at the same level
            for (int u = 0; u < scannedUnits.Length; u++)
            {
                if (scannedUnits[u].Brave != 0 || scannedUnits[u].Faith != 0)
                    continue; // already matched
                if (results[u].NameId > 0)
                    continue; // somehow already matched

                for (int s = 0; s < rosterSlots.Length; s++)
                {
                    if (claimedSlots.Contains(s)) continue;
                    if (rosterSlots[s].NameId <= 0) continue;

                    if (rosterSlots[s].Level == scannedUnits[u].Level)
                    {
                        claimedSlots.Add(s);
                        results[u] = new RosterMatchResult
                        {
                            NameId = rosterSlots[s].NameId,
                            Job = rosterSlots[s].Job,
                            Brave = rosterSlots[s].Brave,
                            Faith = rosterSlots[s].Faith,
                            Secondary = rosterSlots[s].Secondary,
                        };
                        break;
                    }
                }
            }

            return results;
        }
    }
}
