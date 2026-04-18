using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for the logic that decides WHEN to save the world map location to disk.
    ///
    /// Bug: During battle, screen.Location gets overridden from 255 to _lastWorldMapLocation.
    /// The save condition checks screen.Location != _lastWorldMapLocation, which is always
    /// false after the override — so the file never updates.
    ///
    /// The save decision should use the RAW location (before override), not the display location.
    /// </summary>
    public class LocationSaveLogicTests
    {
        [Fact]
        public void ShouldSave_WorldMap_NewLocation_ReturnsTrue()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 26, screenName: "WorldMap", lastSavedLocation: -1);
            Assert.True(result);
        }

        [Fact]
        public void ShouldSave_WorldMap_SameLocation_ReturnsFalse()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 26, screenName: "WorldMap", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_WorldMap_DifferentLocation_ReturnsTrue()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 26, screenName: "WorldMap", lastSavedLocation: 30);
            Assert.True(result);
        }

        [Fact]
        public void ShouldSave_EncounterDialog_ReturnsTrue()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 26, screenName: "EncounterDialog", lastSavedLocation: 30);
            Assert.True(result);
        }

        [Fact]
        public void ShouldSave_Battle_ReturnsFalse()
        {
            // During battle, rawLocation is 255 — should NOT save
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 255, screenName: "BattleMyTurn", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_TravelList_ReturnsFalse()
        {
            // TravelList — location may be flickering during travel animation
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 30, screenName: "TravelList", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_TitleScreen_ReturnsFalse()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 255, screenName: "TitleScreen", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_InvalidLocation_ReturnsFalse()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: -1, screenName: "WorldMap", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_LocationOutOfRange_ReturnsFalse()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 99, screenName: "WorldMap", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_EncounterDialog_SameLocation_ReturnsFalse()
        {
            // Already saved this location
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 26, screenName: "EncounterDialog", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_BattleMyTurn_WithValidRawLocation_ReturnsFalse()
        {
            // Even if rawLocation happens to be valid during battle (e.g. location
            // didn't flip to 255 yet), don't save during battle screens
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 26, screenName: "BattleMyTurn", lastSavedLocation: 30);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_WorldMap_StaleRawLocation_UsesHover()
        {
            // rawLocation is stale (stuck at Orbonne=18 from previous travel),
            // but hover shows the real position (Siedge Weald=26).
            // On WorldMap, hover is the authoritative location.
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 18, hover: 26, screenName: "WorldMap", lastSavedLocation: 18);
            Assert.True(result);
        }

        [Fact]
        public void ShouldSave_WorldMap_HoverMatchesSaved_ReturnsFalse()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 18, hover: 26, screenName: "WorldMap", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_EncounterDialog_UsesRawLocation_NotHover()
        {
            // During encounter, rawLocation is the encounter location, hover may differ
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 26, hover: 10, screenName: "EncounterDialog", lastSavedLocation: 18);
            Assert.True(result);
        }

        [Fact]
        public void GetEffectiveLocation_WorldMap_ReturnsHover()
        {
            int loc = LocationSaveLogic.GetEffectiveLocation(
                rawLocation: 18, hover: 26, screenName: "WorldMap");
            Assert.Equal(26, loc);
        }

        [Fact]
        public void GetEffectiveLocation_EncounterDialog_ReturnsRaw()
        {
            int loc = LocationSaveLogic.GetEffectiveLocation(
                rawLocation: 26, hover: 10, screenName: "EncounterDialog");
            Assert.Equal(26, loc);
        }

        [Fact]
        public void GetEffectiveLocation_Battle_ReturnsRaw()
        {
            int loc = LocationSaveLogic.GetEffectiveLocation(
                rawLocation: 255, hover: 18, screenName: "BattleMyTurn");
            Assert.Equal(255, loc);
        }

        // Session 35: coverage for screens + edge inputs that weren't pinned.

        [Fact]
        public void ShouldSave_BattleSequence_AllowedScreen()
        {
            // BattleSequence (multi-stage minimap) is in the save allow-list
            // alongside WorldMap / EncounterDialog. Pin it.
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 18, screenName: "BattleSequence", lastSavedLocation: 26);
            Assert.True(result);
        }

        [Fact]
        public void ShouldSave_BattleSequence_SameLocation_ReturnsFalse()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 18, screenName: "BattleSequence", lastSavedLocation: 18);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_BattleVictory_NotInAllowList()
        {
            // Victory / Desertion advance to WorldMap automatically. The save
            // should happen from WorldMap, not from these transient screens.
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 18, screenName: "BattleVictory", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_BattleDesertion_NotInAllowList()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 18, screenName: "BattleDesertion", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_PartyMenuUnits_NotInAllowList()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 18, screenName: "PartyMenuUnits", lastSavedLocation: 26);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_WorldMap_HoverOutOfRange_FallsBackToRaw()
        {
            // hover=99 is out of [0, 42]; GetEffectiveLocation should fall back
            // to rawLocation. ShouldSave then gates on the raw value.
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 26, hover: 99, screenName: "WorldMap", lastSavedLocation: 18);
            Assert.True(result);
        }

        [Fact]
        public void ShouldSave_WorldMap_HoverNegative_FallsBackToRaw()
        {
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 26, hover: -1, screenName: "WorldMap", lastSavedLocation: 18);
            Assert.True(result);
        }

        [Fact]
        public void ShouldSave_LocationAtBoundary42_Allowed()
        {
            // 42 is the inclusive upper bound per GetEffectiveLocation logic.
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 42, screenName: "WorldMap", lastSavedLocation: -1);
            Assert.True(result);
        }

        [Fact]
        public void ShouldSave_LocationAtBoundary43_Rejected()
        {
            // Just past the inclusive upper bound.
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 43, screenName: "WorldMap", lastSavedLocation: -1);
            Assert.False(result);
        }

        [Fact]
        public void ShouldSave_LocationZero_Allowed()
        {
            // 0 (Lesalia) is a valid location id.
            var result = LocationSaveLogic.ShouldSave(
                rawLocation: 0, screenName: "WorldMap", lastSavedLocation: -1);
            Assert.True(result);
        }

        [Fact]
        public void GetEffectiveLocation_WorldMap_HoverAtBoundary42_Returned()
        {
            int loc = LocationSaveLogic.GetEffectiveLocation(
                rawLocation: 18, hover: 42, screenName: "WorldMap");
            Assert.Equal(42, loc);
        }

        [Fact]
        public void GetEffectiveLocation_WorldMap_HoverAtBoundary43_FallsBack()
        {
            int loc = LocationSaveLogic.GetEffectiveLocation(
                rawLocation: 18, hover: 43, screenName: "WorldMap");
            Assert.Equal(18, loc);
        }

        [Fact]
        public void GetEffectiveLocation_TitleScreen_ReturnsRaw()
        {
            // Any non-WorldMap screen returns raw as-is (hover is ignored).
            int loc = LocationSaveLogic.GetEffectiveLocation(
                rawLocation: 255, hover: 10, screenName: "TitleScreen");
            Assert.Equal(255, loc);
        }
    }
}
