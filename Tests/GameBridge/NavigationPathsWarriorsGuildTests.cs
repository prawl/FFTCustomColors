using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// WarriorsGuild is a single-item menu at Bervenia (session 44 live find):
    /// only "Recruit" is visible, and CursorDown/Up don't change the menu
    /// cursor. TODO §10 asks for a named `Recruit` validPath + a minimal
    /// set of verbs — `Recruit` (Enter) and `Leave` (Escape). Omit the
    /// CursorUp/Down helpers that were inherited from GetSettlementMenuPaths,
    /// since they're confusing no-ops on a single-item menu.
    /// </summary>
    public class NavigationPathsWarriorsGuildTests
    {
        private static DetectedScreen Screen(string name) => new() { Name = name };

        [Fact]
        public void WarriorsGuild_ExposesRecruitAction()
        {
            var paths = NavigationPaths.GetPaths(Screen("WarriorsGuild"));
            Assert.NotNull(paths);
            Assert.Contains("Recruit", paths!.Keys);
        }

        [Fact]
        public void WarriorsGuild_RecruitIsEnter()
        {
            var paths = NavigationPaths.GetPaths(Screen("WarriorsGuild"));
            var recruit = paths!["Recruit"];
            Assert.NotNull(recruit.Keys);
            Assert.Single(recruit.Keys!);
            Assert.Equal(0x0D, recruit.Keys![0].Vk);
        }

        [Fact]
        public void WarriorsGuild_ExposesLeaveBackToLocationMenu()
        {
            // Escape twice: (1) close the Guild (farewell dialog if any),
            // (2) dismiss the farewell. Matches Outfitter's 2-key Leave.
            // Session 44 wasn't able to confirm whether a farewell fires,
            // so keep it simple with a single Escape — the post-processor
            // alias also makes `Back` work if needed.
            var paths = NavigationPaths.GetPaths(Screen("WarriorsGuild"));
            Assert.NotNull(paths);
            Assert.Contains("Leave", paths!.Keys);
        }

        [Fact]
        public void WarriorsGuild_ExposesBackAlias()
        {
            // Auto-alias from the Leave/Back post-processor still works.
            var paths = NavigationPaths.GetPaths(Screen("WarriorsGuild"));
            Assert.Contains("Back", paths!.Keys);
        }

        [Fact]
        public void WarriorsGuild_DoesNotExposeNoOpCursorUpDown()
        {
            // Session 44 found CursorDown/Up are no-ops at Bervenia (the
            // menu has a single entry). Generic SettlementMenu paths
            // exposed them, misleading callers. WarriorsGuild-specific
            // paths drop them.
            var paths = NavigationPaths.GetPaths(Screen("WarriorsGuild"));
            Assert.NotNull(paths);
            Assert.DoesNotContain("CursorUp", paths!.Keys);
            Assert.DoesNotContain("CursorDown", paths.Keys);
        }

        [Fact]
        public void WarriorsGuild_DoesNotExposeGenericSelect()
        {
            // `Select` was inherited from GetSettlementMenuPaths — it's the
            // generic "enter highlighted sub-action" verb. On a single-item
            // menu it's semantically identical to Recruit; drop it to keep
            // the action surface clean and force callers to use the named
            // Recruit verb.
            var paths = NavigationPaths.GetPaths(Screen("WarriorsGuild"));
            Assert.NotNull(paths);
            Assert.DoesNotContain("Select", paths!.Keys);
        }
    }
}
