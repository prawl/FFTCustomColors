namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure logic for deciding when to persist the world map location to disk.
    /// On WorldMap, uses hover (cursor position = standing position) instead of rawLocation
    /// because rawLocation (0x14077D208) stores the last-passed-through node during travel,
    /// not the actual standing position. On EncounterDialog, rawLocation is correct.
    /// </summary>
    public static class LocationSaveLogic
    {
        /// <summary>
        /// Returns the effective location ID based on screen context.
        /// WorldMap: hover is authoritative (rawLocation is stale after travel).
        /// EncounterDialog: rawLocation is the encounter location.
        /// Other screens: rawLocation as-is.
        /// </summary>
        public static int GetEffectiveLocation(int rawLocation, int hover, string screenName)
        {
            if (screenName == "WorldMap" && hover >= 0 && hover <= 42)
                return hover;
            return rawLocation;
        }

        public static bool ShouldSave(int rawLocation, string screenName, int lastSavedLocation)
        {
            return ShouldSave(rawLocation, hover: -1, screenName, lastSavedLocation);
        }

        public static bool ShouldSave(int rawLocation, int hover, string screenName, int lastSavedLocation)
        {
            int effective = GetEffectiveLocation(rawLocation, hover, screenName);

            if (effective < 0 || effective > 42)
                return false;

            if (effective == lastSavedLocation)
                return false;

            return screenName == "WorldMap" || screenName == "EncounterDialog";
        }
    }
}
