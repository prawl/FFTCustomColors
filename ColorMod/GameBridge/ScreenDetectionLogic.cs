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
            bool battleModeActive = slot9 == 0xFFFFFFFF && (battleMode == 2 || battleMode == 3 || battleMode == 4);
            bool clearlyOnWorldMap = rawValidLocation && party == 0 && battleMode == 0;
            bool inBattle = (unitSlotsPopulated || battleModeActive) && !clearlyOnWorldMap;

            if (inBattle && paused == 1 && battleMode == 0 && gameOverFlag == 1)
                return "GameOver";
            if (inBattle && paused == 1)
                return "Battle_Paused";
            // battleMode values: 2=move tile selection, 3=action menu/ability browsing, 4=ability targeting
            // Attacking: battleMode=4 (selecting target tile for an ability/attack)
            if (inBattle && battleMode == 4)
                return "Battle_Attacking";
            // Moving: battleMode=2 (selecting movement tile)
            if (inBattle && battleMode == 2)
                return "Battle_Moving";
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
            if (eventId > 0 && (rawLocation == 255 || rawLocation < 0))
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
