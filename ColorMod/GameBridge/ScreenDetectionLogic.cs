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
            int encA, int encB, bool isPartySubScreen)
        {
            bool rawValidLocation = rawLocation >= 0 && rawLocation <= 42;

            // Battle detection: unit slots populated AND not clearly on world map.
            // Unit slots (0xFF) persist after leaving battle, so we need clearlyOnWorldMap
            // to override. During attack animations rawLocation=255 (not valid), so
            // clearlyOnWorldMap stays false and we correctly stay in battle.
            bool unitSlotsPopulated = slot0 == 255 && slot9 == 0xFFFFFFFF;
            bool clearlyOnWorldMap = rawValidLocation && party == 0 && battleMode == 0;
            bool inBattle = unitSlotsPopulated && !clearlyOnWorldMap;

            if (inBattle && paused == 1 && battleMode == 0 && gameOverFlag == 1)
                return "GameOver";
            if (inBattle && paused == 1)
                return "Battle_Paused";
            if (inBattle && (moveMode == 255 || battleMode == 2) && battleActed == 0)
                return "Battle_Moving";
            if (inBattle && (moveMode == 255 || battleMode == 2) && battleActed == 1)
                return "Battle_Targeting";
            if (inBattle && battleTeam == 0 && battleActed == 0 && battleMoved == 0)
                return "Battle_MyTurn";
            if (inBattle && battleTeam == 0 && (battleActed == 1 || battleMoved == 1))
                return "Battle_Acting";
            if (inBattle)
                return "Battle";
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
    }
}
