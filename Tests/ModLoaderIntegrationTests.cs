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

            integration.SetColorScheme(ColorScheme.Blue);

            integration.CurrentColorScheme.Should().Be(ColorScheme.Blue);
        }

        [Fact]
        public void ProcessHotkey_F1_Should_Set_Blue_ColorScheme()
        {
            // TLDR: F1 hotkey switches to blue color scheme
            var integration = new ModLoaderIntegration();

            integration.ProcessHotkey(System.Windows.Forms.Keys.F1);

            integration.CurrentColorScheme.Should().Be(ColorScheme.Blue);
        }

        [Fact]
        public void ProcessHotkey_F2_Should_Set_Red_ColorScheme()
        {
            // TLDR: F2 hotkey switches to red color scheme
            var integration = new ModLoaderIntegration();

            integration.ProcessHotkey(System.Windows.Forms.Keys.F2);

            integration.CurrentColorScheme.Should().Be(ColorScheme.Red);
        }

        [Fact]
        public void ProcessHotkey_F4_Should_Set_Purple_ColorScheme()
        {
            // TLDR: F4 hotkey switches to purple color scheme
            var integration = new ModLoaderIntegration();

            integration.ProcessHotkey(System.Windows.Forms.Keys.F4);

            integration.CurrentColorScheme.Should().Be(ColorScheme.Purple);
        }

        [Fact]
        public void ProcessHotkey_F7_Should_Set_Green_ColorScheme()
        {
            // TLDR: F7 hotkey switches to green color scheme
            var integration = new ModLoaderIntegration();

            integration.ProcessHotkey(System.Windows.Forms.Keys.F7);

            integration.CurrentColorScheme.Should().Be(ColorScheme.Green);
        }

        [Fact]
        public void ProcessHotkey_F8_Should_Set_Original_ColorScheme()
        {
            // TLDR: F8 hotkey switches to original color scheme
            var integration = new ModLoaderIntegration();
            integration.SetColorScheme(ColorScheme.Blue); // Start with non-original

            integration.ProcessHotkey(System.Windows.Forms.Keys.F8);

            integration.CurrentColorScheme.Should().Be(ColorScheme.Original);
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
            integration.SetColorScheme(ColorScheme.Blue);

            integration.ProcessHotkey(System.Windows.Forms.Keys.F10);

            integration.CurrentColorScheme.Should().Be(ColorScheme.Blue);
        }

        [Fact]
        public void SetColorScheme_Should_Update_FileRedirector()
        {
            // TLDR: Setting color scheme updates connected FileRedirector
            var integration = new ModLoaderIntegration();

            integration.SetColorScheme(ColorScheme.Red);

            integration.FileRedirector.Should().NotBeNull();
            integration.FileRedirector.ActiveScheme.Should().Be(ColorScheme.Red);
        }

        [Fact]
        public void RegisterFileRedirect_Should_Create_Mapping_For_Active_Color()
        {
            // TLDR: Registering redirects creates appropriate color variant mapping
            var integration = new ModLoaderIntegration();
            integration.SetColorScheme(ColorScheme.Blue);
            var originalPath = "sprites/ramza.pac";

            var result = integration.RegisterFileRedirect(originalPath, null);

            result.Should().BeTrue();
            integration.GetRedirectedPath(originalPath).Should().Be("sprites/ramza_blue.pac");
        }
    }
}