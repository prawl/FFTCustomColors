using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure tests for <see cref="NavigationPathsDescription"/>.
    /// The fail-loud execute_action error uses FormatAvailableActions to
    /// list actions with their purposes; these tests pin the output shape
    /// so a refactor doesn't silently degrade the error message.
    /// </summary>
    public class NavigationPathsDescriptionTests
    {
        [Fact]
        public void GetPathDescription_WorldMap_PartyMenuUnits_ReturnsDesc()
        {
            var desc = NavigationPathsDescription.GetPathDescription(
                "WorldMap", "PartyMenuUnits");
            Assert.NotNull(desc);
            Assert.NotEmpty(desc);
        }

        [Fact]
        public void GetPathDescription_UnknownScreen_ReturnsNull()
        {
            var desc = NavigationPathsDescription.GetPathDescription(
                "NotARealScreen", "Select");
            Assert.Null(desc);
        }

        [Fact]
        public void GetPathDescription_UnknownAction_ReturnsNull()
        {
            var desc = NavigationPathsDescription.GetPathDescription(
                "WorldMap", "TotallyFakeAction");
            Assert.Null(desc);
        }

        [Fact]
        public void GetPathDescription_EmptyArgs_ReturnNull()
        {
            Assert.Null(NavigationPathsDescription.GetPathDescription("", "Select"));
            Assert.Null(NavigationPathsDescription.GetPathDescription("WorldMap", ""));
            Assert.Null(NavigationPathsDescription.GetPathDescription(null!, "Select"));
            Assert.Null(NavigationPathsDescription.GetPathDescription("WorldMap", null!));
        }

        [Fact]
        public void FormatAvailableActions_UnknownScreen_ReturnsNone()
        {
            Assert.Equal("none", NavigationPathsDescription.FormatAvailableActions("NotARealScreen"));
            Assert.Equal("none", NavigationPathsDescription.FormatAvailableActions(null));
            Assert.Equal("none", NavigationPathsDescription.FormatAvailableActions(""));
        }

        [Fact]
        public void FormatAvailableActions_TavernRumors_CoalescesLeaveAndBack()
        {
            // TavernRumors defines "Back"; the alias post-processor adds
            // "Leave". Both should render as a single joined entry
            // "Back/Leave — ...", not two separate entries.
            var text = NavigationPathsDescription.FormatAvailableActions("TavernRumors");
            Assert.Contains("Back/Leave", text);
            // Not two separate "Back ...; Leave ..." entries.
            Assert.DoesNotContain("Leave — Back to Tavern root; Back", text);
        }

        [Fact]
        public void FormatAvailableActions_WorldMap_ContainsPartyMenuEntry()
        {
            var text = NavigationPathsDescription.FormatAvailableActions("WorldMap");
            Assert.Contains("PartyMenuUnits", text);
        }

        [Fact]
        public void FormatAvailableActions_IncludesDescriptionAfterDash()
        {
            // Verify the "Name — Desc" render shape for at least one action
            // that we know has a Desc.
            var text = NavigationPathsDescription.FormatAvailableActions("Cutscene");
            Assert.Contains(" — ", text);
        }

        [Fact]
        public void FormatAvailableActions_Outfitter_LeavePresent_WithDesc()
        {
            // Outfitter explicitly defines Leave (2-key farewell). The
            // aliased Back propagates. Both names should coalesce into
            // one entry with the explicit Desc.
            var text = NavigationPathsDescription.FormatAvailableActions("Outfitter");
            Assert.Contains("Leave", text);
            Assert.Contains("farewell", text, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
