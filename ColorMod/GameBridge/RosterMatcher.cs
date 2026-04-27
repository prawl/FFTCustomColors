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
        public int Team; // scanned team (may be stale/wrong)
    }

    public struct RosterMatchResult
    {
        public int NameId;
        public int Job;
        public int Brave;
        public int Faith;
        public int Secondary;
        /// <summary>Index of the matched roster slot (0-based). -1 if no match.</summary>
        public int SlotIndex;
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
            for (int u = 0; u < results.Length; u++) results[u].SlotIndex = -1;
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
                            SlotIndex = s,
                        };
                        break;
                    }
                }
            }

            // Pass 2: match units with unknown Brave/Faith (active unit)
            // to the remaining unclaimed slot at the same level.
            // Only apply fuzzy level-only matching to units scanned as team=0 (trusted active unit).
            //
            // 2026-04-26 PM iter2 tightening: multiple team=0+brave=0+
            // faith=0 candidates is an ambiguous signal — could be the
            // real active player + a phantom enemy that read team=0/
            // brave=0/faith=0 from bad memory. Mis-attributing to the
            // player slot caused narrator to emit events like
            // "Ramza moved (8,10)→(9,11)" while real Ramza was at (1,6).
            // Only fuzzy-match when there's exactly ONE candidate.
            int pass2Candidates = 0;
            for (int u = 0; u < scannedUnits.Length; u++)
            {
                if (scannedUnits[u].Brave == 0
                    && scannedUnits[u].Faith == 0
                    && scannedUnits[u].Team == 0
                    && results[u].NameId == 0)
                {
                    pass2Candidates++;
                }
            }
            if (pass2Candidates > 1) return results;

            for (int u = 0; u < scannedUnits.Length; u++)
            {
                if (scannedUnits[u].Brave != 0 || scannedUnits[u].Faith != 0)
                    continue; // already matched
                if (results[u].NameId > 0)
                    continue; // somehow already matched
                if (scannedUnits[u].Team != 0)
                    continue; // level-only fuzzy match is unsafe for non-player units

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
                            SlotIndex = s,
                        };
                        break;
                    }
                }
            }

            return results;
        }
    }
}
