using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// ValidPaths coverage for the PartyMenu sub-screens defined in TODO §10.6.
    /// Detection from memory not yet implemented — these tests confirm the
    /// NavigationPaths dispatcher already recognises each new state name so
    /// that once detection lands the ValidPaths are already wired up.
    /// </summary>
    public class NavigationPathsPartyMenuTests
    {
        private const int VK_ENTER = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_SPACE = 0x20;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_LEFT = 0x25;
        private const int VK_RIGHT = 0x27;
        private const int VK_TAB = 0x09;
        private const int VK_Q = 0x51;
        private const int VK_E = 0x45;
        private const int VK_R = 0x52;
        private const int VK_1 = 0x31;

        private static DetectedScreen MakeScreen(string name) => new() { Name = name };

        // --- PartyMenu top-level tabs -------------------------------------------------

        [Theory]
        [InlineData("PartyMenuInventory")]
        [InlineData("PartyMenuChronicle")]
        [InlineData("PartyMenuOptions")]
        public void PartyMenuTabs_HaveTabSwitchAndWorldMapEscape(string screenName)
        {
            var paths = NavigationPaths.GetPaths(MakeScreen(screenName));

            Assert.NotNull(paths);
            Assert.Contains("PrevTab", paths!.Keys);
            Assert.Contains("NextTab", paths.Keys);
            Assert.Contains("WorldMap", paths.Keys);

            var prev = paths["PrevTab"].Keys;
            Assert.NotNull(prev);
            Assert.Equal(VK_Q, prev![0].Vk);

            var next = paths["NextTab"].Keys;
            Assert.NotNull(next);
            Assert.Equal(VK_E, next![0].Vk);

            // WorldMap exit waits for the right screen.
            Assert.Equal("WorldMap", paths["WorldMap"].WaitForScreen);
        }

        [Theory]
        [InlineData("PartyMenu")]
        [InlineData("PartyMenuInventory")]
        [InlineData("PartyMenuChronicle")]
        [InlineData("PartyMenuOptions")]
        public void PartyMenuAllTabs_ExposeOpenAliases(string screenName)
        {
            // Every tab knows how to reach every OTHER tab in one named
            // ValidPath (OpenUnits / OpenInventory / OpenChronicle / OpenOptions).
            // The alias on the current tab is a no-op (empty Keys).
            var paths = NavigationPaths.GetPaths(MakeScreen(screenName));
            Assert.NotNull(paths);
            Assert.Contains("OpenUnits", paths!.Keys);
            Assert.Contains("OpenInventory", paths.Keys);
            Assert.Contains("OpenChronicle", paths.Keys);
            Assert.Contains("OpenOptions", paths.Keys);
        }

        [Theory]
        [InlineData("PartyMenu",          "OpenUnits")]
        [InlineData("PartyMenuInventory", "OpenInventory")]
        [InlineData("PartyMenuChronicle", "OpenChronicle")]
        [InlineData("PartyMenuOptions",   "OpenOptions")]
        public void PartyMenu_OpenAliasForCurrentTabIsNoOp(string screenName, string aliasName)
        {
            var paths = NavigationPaths.GetPaths(MakeScreen(screenName));
            Assert.NotNull(paths);
            var alias = paths![aliasName];
            Assert.NotNull(alias.Keys);
            Assert.Empty(alias.Keys!);
        }

        // ReturnToWorldMap should appear on every PartyMenu-tree screen.
        // Each gets a slot-appropriate Escape count (1 for PartyMenu+tabs,
        // 2 for CharacterStatus, 3 for inner Equipment/Job/Combat/Dialog,
        // 4 for pickers, 5 for JobChangeConfirmation). All wait for
        // "WorldMap" and use a 200ms gap so close animations don't eat keys.
        [Theory]
        [InlineData("PartyMenu", 1)]
        [InlineData("PartyMenuInventory", 1)]
        [InlineData("PartyMenuChronicle", 1)]
        [InlineData("PartyMenuOptions", 1)]
        [InlineData("CharacterStatus", 2)]
        // CharacterDialog handled separately — Escape is a no-op on flavor
        // dialogs (Enter advances them), so its ReturnToWorldMap leads with
        // Enter then Escapes. See CharacterDialog_ReturnToWorldMap_LeadsWithEnter.
        [InlineData("DismissUnit", 3)]
        [InlineData("CombatSets", 3)]
        [InlineData("EquipmentAndAbilities", 3)]
        [InlineData("JobSelection", 3)]
        [InlineData("JobActionMenu", 4)]
        [InlineData("JobChangeConfirmation", 5)]
        [InlineData("EquipmentItemList", 4)]
        [InlineData("EquippableWeapons", 4)]
        [InlineData("EquippableShields", 4)]
        [InlineData("EquippableHeadware", 4)]
        [InlineData("EquippableCombatGarb", 4)]
        [InlineData("EquippableAccessories", 4)]
        [InlineData("ActionAbilities", 4)]
        [InlineData("SecondaryAbilities", 4)]
        [InlineData("ReactionAbilities", 4)]
        [InlineData("SupportAbilities", 4)]
        [InlineData("MovementAbilities", 4)]
        public void PartyTree_ExposesReturnToWorldMap(string screenName, int expectedEscapes)
        {
            var paths = NavigationPaths.GetPaths(MakeScreen(screenName));
            Assert.NotNull(paths);
            Assert.Contains("ReturnToWorldMap", paths!.Keys);

            var rwm = paths["ReturnToWorldMap"];
            Assert.NotNull(rwm.Keys);
            Assert.Equal(expectedEscapes, rwm.Keys!.Length);
            foreach (var k in rwm.Keys!) Assert.Equal(VK_ESCAPE, k.Vk);
            Assert.Equal("WorldMap", rwm.WaitForScreen);
            // 200ms between presses so close animations don't eat keys.
            Assert.True(rwm.DelayBetweenMs >= 200, $"delay {rwm.DelayBetweenMs}ms < 200ms minimum");
        }

        [Fact]
        public void CharacterDialog_ReturnToWorldMap_LeadsWithEnter()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("CharacterDialog"));
            Assert.NotNull(paths);
            Assert.Contains("ReturnToWorldMap", paths!.Keys);

            var rwm = paths["ReturnToWorldMap"];
            Assert.NotNull(rwm.Keys);
            Assert.Equal(3, rwm.Keys!.Length);
            // Enter dismisses the flavor dialog (Escape is a no-op on dialogs),
            // then 2 Escapes climb out CharacterStatus → PartyMenu → WorldMap.
            Assert.Equal(VK_ENTER, rwm.Keys[0].Vk);
            Assert.Equal(VK_ESCAPE, rwm.Keys[1].Vk);
            Assert.Equal(VK_ESCAPE, rwm.Keys[2].Vk);
            Assert.Equal("WorldMap", rwm.WaitForScreen);
            Assert.True(rwm.DelayBetweenMs >= 200);
        }

        [Fact]
        public void PartyMenuInventory_ExposesListAndPageNavigation()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("PartyMenuInventory"));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("ChangePage", paths.Keys);

            Assert.Equal(VK_TAB, paths["ChangePage"].Keys![0].Vk);
        }

        [Fact]
        public void PartyMenuChronicle_ExposesFourWayGridAndSelect()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("PartyMenuChronicle"));

            Assert.NotNull(paths);
            Assert.Contains("CursorUp", paths!.Keys);
            Assert.Contains("CursorDown", paths.Keys);
            Assert.Contains("CursorLeft", paths.Keys);
            Assert.Contains("CursorRight", paths.Keys);
            Assert.Contains("Select", paths.Keys);
        }

        [Fact]
        public void PartyMenuOptions_ExposesVerticalListAndSelect()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("PartyMenuOptions"));

            Assert.NotNull(paths);
            Assert.Contains("CursorUp", paths!.Keys);
            Assert.Contains("CursorDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            // Horizontal nav is not meaningful in a vertical list.
            Assert.DoesNotContain("CursorLeft", paths.Keys);
            Assert.DoesNotContain("CursorRight", paths.Keys);
        }

        // --- CharacterDialog / DismissUnit / CombatSets -------------------------------

        [Fact]
        public void CharacterDialog_HasOnlyAdvance_NoCancel()
        {
            // In this game, Escape does NOT close flavor-text dialogs — only
            // Enter advances them. We deliberately do NOT expose a Close path.
            var paths = NavigationPaths.GetPaths(MakeScreen("CharacterDialog"));

            Assert.NotNull(paths);
            Assert.Contains("Advance", paths!.Keys);
            Assert.DoesNotContain("Close", paths.Keys);
            Assert.DoesNotContain("Cancel", paths.Keys);
            Assert.Equal(VK_ENTER, paths["Advance"].Keys![0].Vk);
        }

        [Fact]
        public void DismissUnit_ExposesLeftRightSelectCancel_CursorDefaultsBack()
        {
            // Cursor defaults to Back/Cancel, so a blind Enter is safe.
            // Left/Right toggle between Back and Confirm.
            var paths = NavigationPaths.GetPaths(MakeScreen("DismissUnit"));

            Assert.NotNull(paths);
            Assert.Contains("CursorLeft", paths!.Keys);
            Assert.Contains("CursorRight", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);

            // The Select description must call out which option is default
            // so Claude doesn't accidentally dismiss a unit by pressing Enter.
            Assert.Contains("Back", paths["Select"].Desc);
            Assert.Contains("Confirm", paths["Select"].Desc);

            // Cancel returns to CharacterStatus.
            Assert.Equal("CharacterStatus", paths["Cancel"].WaitForScreen);
        }

        [Fact]
        public void CombatSets_ExposesScrollSelectBack()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("CombatSets"));

            Assert.NotNull(paths);
            Assert.Contains("CursorUp", paths!.Keys);
            Assert.Contains("CursorDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Back", paths.Keys);
            Assert.Equal("CharacterStatus", paths["Back"].WaitForScreen);
        }

        // --- EquipmentAndAbilities + Equippable<Type> ---------------------------------

        [Fact]
        public void EquipmentAndAbilities_ExposesColumnNavAndEffectsToggle()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("EquipmentAndAbilities"));

            Assert.NotNull(paths);
            Assert.Contains("CursorUp", paths!.Keys);
            Assert.Contains("CursorDown", paths.Keys);
            Assert.Contains("CursorLeft", paths.Keys);
            Assert.Contains("CursorRight", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("ToggleEffectsView", paths.Keys);
            Assert.Contains("Back", paths.Keys);

            Assert.Equal(VK_R, paths["ToggleEffectsView"].Keys![0].Vk);
            Assert.Equal("CharacterStatus", paths["Back"].WaitForScreen);
        }

        [Theory]
        [InlineData("EquippableWeapons")]
        [InlineData("EquippableShields")]
        [InlineData("EquippableHeadware")]
        [InlineData("EquippableCombatGarb")]
        [InlineData("EquippableAccessories")]
        public void EquippableSlots_ExposeItemPickerPaths(string screenName)
        {
            var paths = NavigationPaths.GetPaths(MakeScreen(screenName));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("PrevPage", paths.Keys);
            Assert.Contains("NextPage", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);
            Assert.Equal("EquipmentAndAbilities", paths["Cancel"].WaitForScreen);
        }

        // --- <Slot>Abilities pickers --------------------------------------------------

        [Theory]
        [InlineData("ActionAbilities")]
        [InlineData("SecondaryAbilities")]
        [InlineData("ReactionAbilities")]
        [InlineData("SupportAbilities")]
        [InlineData("MovementAbilities")]
        public void AbilityPickers_ExposeScrollSelectCancel(string screenName)
        {
            var paths = NavigationPaths.GetPaths(MakeScreen(screenName));

            Assert.NotNull(paths);
            Assert.Contains("ScrollUp", paths!.Keys);
            Assert.Contains("ScrollDown", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);
            Assert.Equal("EquipmentAndAbilities", paths["Cancel"].WaitForScreen);
        }

        // --- JobSelection + JobActionMenu cursor atomics ------------------------------

        [Fact]
        public void JobSelection_ExposesGridNavAndSelect()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("JobSelection"));

            Assert.NotNull(paths);
            Assert.Contains("CursorUp", paths!.Keys);
            Assert.Contains("CursorDown", paths.Keys);
            Assert.Contains("CursorLeft", paths.Keys);
            Assert.Contains("CursorRight", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Back", paths.Keys);
            Assert.Equal("JobActionMenu", paths["Select"].WaitForScreen);
            Assert.Equal("CharacterStatus", paths["Back"].WaitForScreen);
        }

        [Fact]
        public void JobActionMenu_ExposesAtomicCursorMovesInAdditionToCombined()
        {
            // Existing combined LearnAbilities/ChangeJob sequences stay. New
            // atomic CursorLeft/CursorRight/Select let Claude move the cursor
            // deliberately (e.g. to inspect Change Job greyed state) without
            // committing immediately.
            var paths = NavigationPaths.GetPaths(MakeScreen("JobActionMenu"));

            Assert.NotNull(paths);
            Assert.Contains("LearnAbilities", paths!.Keys);
            Assert.Contains("ChangeJob", paths.Keys);
            Assert.Contains("CursorLeft", paths.Keys);
            Assert.Contains("CursorRight", paths.Keys);
            Assert.Contains("Select", paths.Keys);
            Assert.Contains("Cancel", paths.Keys);

            Assert.Equal(VK_LEFT, paths["CursorLeft"].Keys![0].Vk);
            Assert.Equal(VK_RIGHT, paths["CursorRight"].Keys![0].Vk);
            Assert.Single(paths["Select"].Keys!);
            Assert.Equal(VK_ENTER, paths["Select"].Keys![0].Vk);
        }

        // --- CharacterStatus additions (R/1/Space) -----------------------------------

        [Fact]
        public void CharacterStatus_ExposesViewToggles_AndDialogOpen()
        {
            var paths = NavigationPaths.GetPaths(MakeScreen("CharacterStatus"));

            Assert.NotNull(paths);
            Assert.Contains("OpenDialog", paths!.Keys);
            Assert.Contains("ToggleStatsPanel", paths.Keys);

            Assert.Equal(VK_SPACE, paths["OpenDialog"].Keys![0].Vk);
            Assert.Equal("CharacterDialog", paths["OpenDialog"].WaitForScreen);
            Assert.Equal(VK_1, paths["ToggleStatsPanel"].Keys![0].Vk);
        }
    }
}
