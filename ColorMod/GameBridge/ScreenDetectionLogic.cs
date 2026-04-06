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

            // Battle detection: unit slots populated = in battle.
            // Don't let flickering battleMode/ui flags pull us out of battle.
            // Previously: clearlyOnWorldMap = rawValidLocation && party==0 && battleMode==0
            // This caused false negatives during attack animations when battleMode flickers to 0.
            // Fix: only consider "clearly on world map" if unit slots are NOT populated.
            bool unitSlotsPopulated = slot0 == 255 && slot9 == 0xFFFFFFFF;
            bool clearlyOnWorldMap = rawValidLocation && !unitSlotsPopulated && party == 0 && battleMode == 0;
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
