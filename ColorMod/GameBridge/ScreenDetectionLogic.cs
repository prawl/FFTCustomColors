using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure logic for screen detection — no memory reads, no side effects.
    /// Extracted from CommandWatcher.DetectScreen for testability.
    /// </summary>
    public static class ScreenDetectionLogic
    {
        public static string Detect(
            int party, int ui, int rawLocation,
            long slot0, long slot9,
            int battleMode, int moveMode, int paused, int gameOverFlag,
            int battleTeam, int battleActed, int battleMoved,
            int encA, int encB, bool isPartySubScreen, int eventId = 0,
            int submenuFlag = 0, int menuCursor = -1)
        {
            bool rawValidLocation = rawLocation >= 0 && rawLocation <= 42;

            // Battle detection: unit slots populated AND not clearly on world map.
            // Unit slots (0xFF) persist after leaving battle, so we need clearlyOnWorldMap
            // to override. During attack animations rawLocation=255 (not valid), so
            // clearlyOnWorldMap stays false and we correctly stay in battle.
            // When browsing ability lists, slot0 can change from 255 to a non-FF value,
            // but slot9=0xFFFFFFFF + battleMode=3 confirms we're still in battle.
            bool unitSlotsPopulated = slot0 == 255 && slot9 == 0xFFFFFFFF;
            // battleMode values:
            //   1 = cast-time magick targeting (Black/White/Time/Mystic/Summon/etc.)
            //   2 = move tile selection
            //   3 = action menu / ability browsing
            //   4 = instant targeting (basic Attack AND Throw/Items/Iaido/Aim/etc.)
            //
            // The engine treats battleMode=4 as "pick a tile with a cursor, confirm
            // to execute immediately." Both basic Attack and many skillset abilities
            // (Throw Shuriken, Potion, Ashura, Aim+N) route through this path.
            // From Claude's POV they're the same screen: move cursor, press F.
            //
            // battleMode=1 is reserved for magicks that have a cast-time charge —
            // Fire, Haste, Cura, Moogle, etc. Same targeting UX but internally queued.
            //
            // We split the state name (Battle_Attacking vs Battle_Casting) to let
            // downstream logic reason about "am I in a queued cast?" but the valid
            // paths and cursor interactions are identical.
            bool battleModeActive = slot9 == 0xFFFFFFFF && (battleMode == 1 || battleMode == 2 || battleMode == 3 || battleMode == 4);
            bool clearlyOnWorldMap = rawValidLocation && party == 0 && battleMode == 0;
            // After battle ends, stale flags persist (acted=1, moved=1, slot0=255) but
            // battleMode resets to 0 and location goes to 255. Not a real battle.
            // Distinguished from mid-battle flickering (acted=0) by the acted/moved flags.
            bool postBattleTransition = battleMode == 0 && !rawValidLocation
                                        && (battleActed == 1 || battleMoved == 1);
            bool inBattle = (unitSlotsPopulated || battleModeActive) && !clearlyOnWorldMap && !postBattleTransition;

            // Formation screen: battle-like flags (slot9=0xFFFFFFFF, battleMode=1)
            // but no units populated yet (slot0=0xFFFFFFFF instead of 0x000000FF).
            // During ability browsing slot0 can be other values (e.g. 150), so we check
            // specifically for the 0xFFFFFFFF terminator, not just "not 255".
            if (inBattle && slot0 == 0xFFFFFFFF && battleMode == 1)
                return "Battle_Formation";

            // Post-battle screens: stale unit slots + battleMode=0 + acted/moved flags
            // still set from the last turn. These appear after the final enemy is defeated.
            // Victory: encA != encB (encounter values diverge as battle ends). Auto-advances.
            // Desertion: encA == encB, warns about units near Brave/Faith thresholds. Needs Enter.
            // Note: these use unitSlotsPopulated directly, not inBattle, because clearlyOnWorldMap
            // would suppress inBattle (location is valid + battleMode=0 during post-battle).
            // Desertion can appear with paused=0 OR paused=1 and location=valid OR location=255.
            // MUST require rawValidLocation to distinguish from stale post-battle WorldMap
            // (where location=255 and all other flags are identical to Desertion).
            bool postBattle = unitSlotsPopulated && battleMode == 0 && gameOverFlag == 0
                              && (battleActed == 1 || battleMoved == 1) && rawValidLocation;
            bool postBattlePaused = unitSlotsPopulated && battleMode == 0 && paused == 1
                              && gameOverFlag == 1 && (battleActed == 1 || battleMoved == 1);
            if (postBattle && encA != encB)
                return "Battle_Victory";
            if ((postBattle || postBattlePaused) && encA == encB && submenuFlag == 1)
                return "Battle_Desertion";

            // Mid-battle dialogue: characters talking during an active battle.
            // eventId > 0 means a dialogue event is playing. battleMode=0 means no
            // action menu is open (distinguishes from eventId=401 that fires during
            // normal ability browsing with battleMode=3).
            if (inBattle && eventId > 0 && eventId != 0xFFFF && battleMode == 0
                && paused == 0 && gameOverFlag == 0
                && !(battleActed == 1 || battleMoved == 1)) // exclude post-battle transition
                return "Battle_Dialogue";

            // GameOver: paused=1, battleMode=0, gameOverFlag=1, acted=0 (player didn't finish turn).
            // Desertion warning can also have paused=1 + gameOverFlag=1 + battleMode=0, but
            // it has acted=1/moved=1 (battle was completed). Check acted to distinguish.
            if (inBattle && paused == 1 && battleMode == 0 && gameOverFlag == 1
                && battleActed == 0 && battleMoved == 0)
                return "GameOver";
            // Status screen: paused=1 + menuCursor=3. Must check before Battle_Paused.
            if (inBattle && paused == 1 && menuCursor == 3)
                return "Battle_Status";
            if (inBattle && paused == 1)
                return "Battle_Paused";
            // Attacking: instant-targeting — basic Attack, Throw, Items, Iaido, Aim.
            if (inBattle && battleMode == 4)
                return "Battle_Attacking";
            // Casting: cast-time magick target selection — Fire, Cure, Haste, etc.
            if (inBattle && battleMode == 1)
                return "Battle_Casting";
            // Waiting/Facing: battleMode=2 + menuCursor=2 (Wait) — post-action facing selection
            if (inBattle && battleMode == 2 && menuCursor == 2)
                return "Battle_Waiting";
            // Moving: battleMode=2 (selecting movement tile)
            if (inBattle && battleMode == 2)
                return "Battle_Moving";
            // Auto-Battle submenu: menuCursor=4 + submenuFlag=1. Must check before Battle_Abilities/Acting.
            if (inBattle && submenuFlag == 1 && battleMode == 3 && menuCursor == 4)
                return "Battle_AutoBattle";
            // Abilities submenu: submenuFlag=1 + battleMode=3 + acted/moved flags set by entering submenu.
            // menuCursor must be 1 (Abilities) to distinguish from stale submenuFlag after ability use —
            // when returning to action menu after acting, submenuFlag stays 1 but cursor resets to 0 (Move).
            if (inBattle && submenuFlag == 1 && battleMode == 3 && battleTeam == 0
                && (battleActed == 1 || battleMoved == 1) && menuCursor == 1)
                return "Battle_Abilities";
            if (inBattle && battleTeam == 0 && battleActed == 0 && battleMoved == 0)
                return "Battle_MyTurn";
            if (inBattle && battleTeam == 2 && battleActed == 0 && battleMoved == 0)
                return "Battle_AlliesTurn";
            if (inBattle && battleTeam == 1 && battleActed == 0 && battleMoved == 0)
                return "Battle_EnemiesTurn";
            if (inBattle && battleTeam == 0 && (battleActed == 1 || battleMoved == 1))
                return "Battle_Acting";
            if (inBattle)
                return "Battle";
            // eventId == 0xFFFF is the uninitialized sentinel (seen on freshly
            // launched game at title screen), not a real cutscene event.
            if (eventId > 0 && eventId != 0xFFFF && (rawLocation == 255 || rawLocation < 0))
                return "Cutscene";
            if (rawLocation == 255 || rawLocation < 0)
                return "TitleScreen";
            if (encA != encB)
                return "EncounterDialog";
            if (isPartySubScreen)
                return "PartySubScreen";
            if (party == 1)
                return "PartyMenu";
            if (party == 0 && ui == 1)
                return "TravelList";
            if (party == 0 && ui == 0)
                return "WorldMap";
            return "Unknown";
        }

        /// <summary>
        /// Converts a selected ability/skillset name into its screen state name.
        /// E.g. "Attack" → "Battle_Attack", "White Magicks" → "Battle_WhiteMagicks"
        /// </summary>
        public static string GetAbilityScreenName(string abilityName)
        {
            var words = abilityName.Split(' ');
            var pascal = string.Concat(words.Select(w =>
                char.ToUpper(w[0]) + w.Substring(1)));
            return $"Battle_{pascal}";
        }
    }
}
