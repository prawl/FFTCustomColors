using Xunit;
using FluentAssertions;

namespace FFTColorMod.Tests
{
    public class ModLoaderIntegrationTests
    {
        [Fact]
        public void ModLoaderIntegration_Should_Initialize_With_ModLoader_Interface()
        {
            // TLDR: Test that we can create integration with mod loader
            var integration = new ModLoaderIntegration();

            integration.Should().NotBeNull();
        }

        [Fact]
        public void RegisterFileRedirect_Should_Register_Sprite_Path_Redirection()
        {
            // TLDR: Can register a sprite file path to redirect to color variant
            var integration = new ModLoaderIntegration();

            var result = integration.RegisterFileRedirect("data/sprites/ramza.spr", "FFTIVC/data/sprites_blue/ramza.spr");

            result.Should().BeTrue();
        }

        [Fact]
        public void SetColorScheme_Should_Update_Active_Color_Scheme()
        {
            // TLDR: SetColorScheme changes the current active color scheme
            var integration = new ModLoaderIntegration();

            integration.SetColorScheme(ColorScheme.OceanBlue);

            integration.CurrentColorScheme.Should().Be(ColorScheme.OceanBlue);
        }

        [Fact]
        public void ProcessHotkey_F1_Should_Set_WhiteSilver_ColorScheme()
        {
            // TLDR: F1 hotkey switches to white silver color scheme
            var integration = new ModLoaderIntegration();

            integration.ProcessHotkey(System.Windows.Forms.Keys.F1);

            integration.CurrentColorScheme.Should().Be(ColorScheme.WhiteSilver);
        }

        [Fact]
        public void ProcessHotkey_F2_Should_Set_OceanBlue_ColorScheme()
        {
            // TLDR: F2 hotkey switches to ocean blue color scheme
            var integration = new ModLoaderIntegration();

            integration.ProcessHotkey(System.Windows.Forms.Keys.F2);

            integration.CurrentColorScheme.Should().Be(ColorScheme.OceanBlue);
        }

        [Fact]
        public void ProcessHotkey_F3_Should_Set_DeepPurple_ColorScheme()
        {
            // TLDR: F3 hotkey switches to deep purple color scheme
            var integration = new ModLoaderIntegration();

            integration.ProcessHotkey(System.Windows.Forms.Keys.F3);

            integration.CurrentColorScheme.Should().Be(ColorScheme.DeepPurple);
        }

        [Fact]
        public void ProcessHotkey_F4_Should_Set_Original_ColorScheme()
        {
            // TLDR: F4 hotkey switches to original color scheme
            var integration = new ModLoaderIntegration();
            integration.SetColorScheme(ColorScheme.OceanBlue); // Start with non-original

            integration.ProcessHotkey(System.Windows.Forms.Keys.F4);

            integration.CurrentColorScheme.Should().Be(ColorScheme.Original);
        }

        [Fact]
        public void ProcessHotkey_F7_Should_Be_Unassigned()
        {
            // TLDR: F7 is not assigned to any color scheme
            var integration = new ModLoaderIntegration();
            integration.SetColorScheme(ColorScheme.OceanBlue);

            integration.ProcessHotkey(System.Windows.Forms.Keys.F7);

            integration.CurrentColorScheme.Should().Be(ColorScheme.OceanBlue); // Should remain unchanged
        }

        [Fact]
        public void ProcessHotkey_F8_Should_Be_Unassigned()
        {
            // TLDR: F8 is not assigned to any color scheme
            var integration = new ModLoaderIntegration();
            integration.SetColorScheme(ColorScheme.OceanBlue);

            integration.ProcessHotkey(System.Windows.Forms.Keys.F8);

            integration.CurrentColorScheme.Should().Be(ColorScheme.OceanBlue); // Should remain unchanged
        }

        [Fact]
        public void ProcessHotkey_F9_Should_Trigger_Palette_Rescan()
        {
            // TLDR: F9 hotkey triggers palette rescan
            var integration = new ModLoaderIntegration();
            var initialRescanCount = integration.RescanCount;

            integration.ProcessHotkey(System.Windows.Forms.Keys.F9);

            integration.RescanCount.Should().Be(initialRescanCount + 1);
        }

        [Fact]
        public void ProcessHotkey_Should_Ignore_Unassigned_Keys()
        {
            // TLDR: Unassigned keys don't change color scheme
            var integration = new ModLoaderIntegration();
            integration.SetColorScheme(ColorScheme.OceanBlue);

            integration.ProcessHotkey(System.Windows.Forms.Keys.F10);

            integration.CurrentColorScheme.Should().Be(ColorScheme.OceanBlue);
        }

        [Fact]
        public void SetColorScheme_Should_Update_FileRedirector()
        {
            // TLDR: Setting color scheme updates connected FileRedirector
            var integration = new ModLoaderIntegration();

            integration.SetColorScheme(ColorScheme.WhiteSilver);

            integration.FileRedirector.Should().NotBeNull();
            integration.FileRedirector.ActiveScheme.Should().Be(ColorScheme.WhiteSilver);
        }

        [Fact]
        public void RegisterFileRedirect_Should_Create_Mapping_For_Active_Color()
        {
            // TLDR: Registering redirects creates appropriate color variant mapping
            var integration = new ModLoaderIntegration();
            integration.SetColorScheme(ColorScheme.OceanBlue);
            var originalPath = "sprites/ramza.pac";

            var result = integration.RegisterFileRedirect(originalPath, null);

            result.Should().BeTrue();
            integration.GetRedirectedPath(originalPath).Should().Be("sprites/ramza_ocean_blue.pac");
        }

        [Fact]
        public void ProcessHotkey_Should_Save_Color_Preference()
        {
            // TLDR: Processing a hotkey should save the color preference to persistent storage
            // Arrange
            var tempPath = System.IO.Path.GetTempFileName();
            var integration = new ModLoaderIntegration();
            integration.SetPreferencesPath(tempPath);

            // Act - Process F2 hotkey (Ocean Blue color scheme)
            integration.ProcessHotkey(System.Windows.Forms.Keys.F2);

            // Assert - Verify preference was saved
            integration.CurrentColorScheme.Should().Be(ColorScheme.OceanBlue);

            // Create new integration instance to verify persistence
            var newIntegration = new ModLoaderIntegration();
            newIntegration.SetPreferencesPath(tempPath);
            newIntegration.LoadPreferences();

            newIntegration.CurrentColorScheme.Should().Be(ColorScheme.OceanBlue);

            // Cleanup
            System.IO.File.Delete(tempPath);
        }

        [Fact]
        public void ColorPreferences_Should_Persist_Across_Game_Contexts()
        {
            // TLDR: Color preferences should persist across battles, cutscenes, and formation screen
            // Arrange
            var tempPath = System.IO.Path.GetTempFileName();

            // Simulate initial game load - set blue color
            var battleContext = new ModLoaderIntegration();
            battleContext.SetPreferencesPath(tempPath);
            battleContext.ProcessHotkey(System.Windows.Forms.Keys.F1); // White Silver

            // Act - Simulate transition to cutscene (new context load)
            var cutsceneContext = new ModLoaderIntegration();
            cutsceneContext.SetPreferencesPath(tempPath);
            cutsceneContext.LoadPreferences();

            // Simulate transition to formation screen (another context load)
            var formationContext = new ModLoaderIntegration();
            formationContext.SetPreferencesPath(tempPath);
            formationContext.LoadPreferences();

            // Simulate returning to battle (yet another context load)
            var battleContext2 = new ModLoaderIntegration();
            battleContext2.SetPreferencesPath(tempPath);
            battleContext2.LoadPreferences();

            // Assert - All contexts should have the same color scheme
            cutsceneContext.CurrentColorScheme.Should().Be(ColorScheme.WhiteSilver);
            formationContext.CurrentColorScheme.Should().Be(ColorScheme.WhiteSilver);
            battleContext2.CurrentColorScheme.Should().Be(ColorScheme.WhiteSilver);

            // Cleanup
            System.IO.File.Delete(tempPath);
        }

        [Fact]
        public void ModLoaderIntegration_Should_AutoLoad_Preferences_On_Initialization()
        {
            // TLDR: Mod should automatically load saved preferences when initialized with a path
            // Arrange
            var tempPath = System.IO.Path.GetTempFileName();

            // Save a preference first
            var firstSession = new ModLoaderIntegration();
            firstSession.SetPreferencesPath(tempPath);
            firstSession.ProcessHotkey(System.Windows.Forms.Keys.F3); // Deep Purple

            // Act - Create new integration with constructor that takes path
            var secondSession = new ModLoaderIntegration(tempPath);

            // Assert - Should automatically have loaded the saved preference
            secondSession.CurrentColorScheme.Should().Be(ColorScheme.DeepPurple);

            // Cleanup
            System.IO.File.Delete(tempPath);
        }

        [Fact]
        public void ModLoaderIntegration_Should_Apply_Loaded_Color_To_FileRedirector()
        {
            // TLDR: When preferences are loaded, FileRedirector should be updated with the color scheme
            // Arrange
            var tempPath = System.IO.Path.GetTempFileName();

            // Save a preference first
            var firstSession = new ModLoaderIntegration();
            firstSession.SetPreferencesPath(tempPath);
            firstSession.ProcessHotkey(System.Windows.Forms.Keys.F1); // White Silver

            // Act - Create new integration that auto-loads
            var secondSession = new ModLoaderIntegration(tempPath);

            // Assert - FileRedirector should have the loaded color scheme
            secondSession.FileRedirector.Should().NotBeNull();
            secondSession.FileRedirector.ActiveScheme.Should().Be(ColorScheme.WhiteSilver);

            // Verify file redirection works with loaded scheme
            var redirectedPath = secondSession.GetRedirectedPath("sprites/ramza.pac");
            redirectedPath.Should().Be("sprites/ramza_white_silver.pac");

            // Cleanup
            System.IO.File.Delete(tempPath);
        }
    }
}