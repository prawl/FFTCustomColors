namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure logic for deciding when to persist the world map location to disk.
    /// Uses the RAW location from memory (before battle override), not the display location.
    /// Only saves on WorldMap or EncounterDialog screens to avoid flickering during travel.
    /// </summary>
    public static class LocationSaveLogic
    {
        public static bool ShouldSave(int rawLocation, string screenName, int lastSavedLocation)
        {
            if (rawLocation < 0 || rawLocation > 42)
                return false;

            if (rawLocation == lastSavedLocation)
                return false;

            return screenName == "WorldMap" || screenName == "EncounterDialog";
        }
    }
}
