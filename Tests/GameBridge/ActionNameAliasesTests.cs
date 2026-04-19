using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using System.Collections.Generic;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure tests for <see cref="ActionNameAliases.ApplyAliases"/> — the
    /// helper extracted from NavigationPaths.GetPaths in session 47. Covers
    /// every path of the aliasing logic without needing any screen/detection.
    /// </summary>
    public class ActionNameAliasesTests
    {
        private static PathEntry FakeEntry(string desc) =>
            new() { Keys = new[] { new KeyInfo { Vk = 0x1B, Name = "Escape" } }, Desc = desc };

        [Fact]
        public void Null_IsReturnedAsNull()
        {
            Assert.Null(ActionNameAliases.ApplyAliases(null));
        }

        [Fact]
        public void Empty_StaysEmpty()
        {
            var paths = new Dictionary<string, PathEntry>();
            var result = ActionNameAliases.ApplyAliases(paths);
            Assert.Same(paths, result);
            Assert.Empty(result!);
        }

        [Fact]
        public void LeavePresent_BackAdded()
        {
            var leaveEntry = FakeEntry("leave");
            var paths = new Dictionary<string, PathEntry> { ["Leave"] = leaveEntry };
            ActionNameAliases.ApplyAliases(paths);
            Assert.True(paths.ContainsKey("Back"));
            Assert.Same(leaveEntry, paths["Back"]);
        }

        [Fact]
        public void BackPresent_LeaveAdded()
        {
            var backEntry = FakeEntry("back");
            var paths = new Dictionary<string, PathEntry> { ["Back"] = backEntry };
            ActionNameAliases.ApplyAliases(paths);
            Assert.True(paths.ContainsKey("Leave"));
            Assert.Same(backEntry, paths["Leave"]);
        }

        [Fact]
        public void BothPresent_NeitherOverwritten()
        {
            // If a screen explicitly defines different entries for both
            // names (unusual but legal — e.g. Outfitter's 2-key Leave
            // differs from a plain-Escape Back), the post-processor must
            // leave them alone.
            var leaveEntry = FakeEntry("2-key leave");
            var backEntry = FakeEntry("single-key back");
            var paths = new Dictionary<string, PathEntry>
            {
                ["Leave"] = leaveEntry,
                ["Back"] = backEntry
            };
            ActionNameAliases.ApplyAliases(paths);
            Assert.Same(leaveEntry, paths["Leave"]);
            Assert.Same(backEntry, paths["Back"]);
        }

        [Fact]
        public void NeitherPresent_NothingAdded()
        {
            // Screen that has no exit path (e.g. GameOver before detection)
            // shouldn't get phantom Leave/Back aliases.
            var paths = new Dictionary<string, PathEntry>
            {
                ["Advance"] = FakeEntry("advance")
            };
            ActionNameAliases.ApplyAliases(paths);
            Assert.False(paths.ContainsKey("Leave"));
            Assert.False(paths.ContainsKey("Back"));
            Assert.True(paths.ContainsKey("Advance"));
        }

        [Fact]
        public void IntegrationWithGetPaths_LeavePropagatesToBack()
        {
            // End-to-end via NavigationPaths.GetPaths: Outfitter defines
            // Leave explicitly (2-key farewell); Back should be auto-aliased.
            var paths = NavigationPaths.GetPaths(new DetectedScreen { Name = "Outfitter" });
            Assert.NotNull(paths);
            Assert.Contains("Leave", paths!.Keys);
            Assert.Contains("Back", paths.Keys);
            Assert.Same(paths["Leave"], paths["Back"]);
        }

        [Fact]
        public void IntegrationWithGetPaths_BackPropagatesToLeave()
        {
            // End-to-end: TavernRumors defines Back; Leave auto-aliased.
            var paths = NavigationPaths.GetPaths(new DetectedScreen { Name = "TavernRumors" });
            Assert.NotNull(paths);
            Assert.Contains("Back", paths!.Keys);
            Assert.Contains("Leave", paths.Keys);
            Assert.Same(paths["Back"], paths["Leave"]);
        }

        // Session 47: expanded alias groups.

        [Fact]
        public void Exit_Aliased_ToBack_OnTavernScreens()
        {
            // Exit is a natural-language synonym users type reflexively.
            var paths = NavigationPaths.GetPaths(new DetectedScreen { Name = "TavernRumors" });
            Assert.NotNull(paths);
            Assert.Contains("Exit", paths!.Keys);
            Assert.Same(paths["Back"], paths["Exit"]);
        }

        [Fact]
        public void Exit_Aliased_ToLeave_OnShopScreens()
        {
            var paths = NavigationPaths.GetPaths(new DetectedScreen { Name = "Outfitter" });
            Assert.NotNull(paths);
            Assert.Contains("Exit", paths!.Keys);
            Assert.Same(paths["Leave"], paths["Exit"]);
        }

        [Fact]
        public void OK_Aliased_ToConfirm_OnShopConfirmDialog()
        {
            // ShopConfirmDialog defines Confirm (plain Enter); OK auto-aliases.
            // Yes is also already explicitly defined as a safer 2-key commit —
            // that stays as its own distinct entry.
            var paths = NavigationPaths.GetPaths(new DetectedScreen { Name = "ShopConfirmDialog" });
            Assert.NotNull(paths);
            Assert.Contains("Confirm", paths!.Keys);
            Assert.Contains("OK", paths.Keys);
            Assert.Same(paths["Confirm"], paths["OK"]);
        }

        [Fact]
        public void Yes_OnConfirmDialog_StaysDistinctFromConfirm()
        {
            // Yes is the safe-commit (CursorLeft+Enter on horizontal Yes/No);
            // Confirm is raw Enter. The alias post-processor must NOT collapse
            // them onto the same entry, because they really are different.
            var paths = NavigationPaths.GetPaths(new DetectedScreen { Name = "ShopConfirmDialog" });
            Assert.NotNull(paths);
            Assert.NotSame(paths!["Confirm"], paths["Yes"]);
        }

        [Fact]
        public void NonAffirmative_Screen_DoesNotGainOK()
        {
            // WorldMap has no Confirm/OK/Yes — the alias group must not
            // invent phantom entries.
            var paths = NavigationPaths.GetPaths(new DetectedScreen { Name = "WorldMap" });
            Assert.NotNull(paths);
            Assert.DoesNotContain("Confirm", paths!.Keys);
            Assert.DoesNotContain("OK", paths.Keys);
            Assert.DoesNotContain("Yes", paths.Keys);
        }
    }
}
