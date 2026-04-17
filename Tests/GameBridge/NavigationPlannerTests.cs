using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;
using static FFTColorCustomizer.GameBridge.NavigationPlanner;

namespace FFTColorCustomizer.Tests.GameBridge;

/// <summary>
/// Tests for the pure planning logic behind NavigateToCharacterStatus.
/// Primary goals: (a) catch regressions in the key sequence without a
/// live game, (b) enable dry-run validation of the plan before the
/// live nav runs, (c) lock in the specific wait times (session 23
/// found 500ms was too fast; 1000ms is the settled value).
/// </summary>
public class NavigationPlannerTests
{
    [Fact]
    public void Plan_RejectsEmptyRoster()
    {
        var p = PlanNavigateToCharacterStatus("WorldMap", 0, 0);
        Assert.False(p.Ok);
        Assert.Contains("rosterCount", p.Error);
    }

    [Fact]
    public void Plan_RejectsTargetBeyondRoster()
    {
        var p = PlanNavigateToCharacterStatus("WorldMap", 15, 14);
        Assert.False(p.Ok);
        Assert.Contains("out of range", p.Error);
    }

    [Fact]
    public void Plan_FromWorldMap_ToRamza_SingleEscapePlusEnter()
    {
        // WorldMap → Escape opens PartyMenu at (0,0). Ramza is at
        // displayOrder 0, so no nav keys needed after the open.
        var p = PlanNavigateToCharacterStatus("WorldMap", 0, 14);
        Assert.True(p.Ok);
        Assert.Equal(2, p.Steps.Count);
        Assert.Equal(VK_ESCAPE, p.Steps[0].VkCode);
        Assert.Equal(1000, p.Steps[0].SettleMs);
        Assert.Equal(VK_ENTER, p.Steps[1].VkCode);
    }

    [Fact]
    public void Plan_FromWorldMap_ToKenrick_OneRight()
    {
        // Kenrick at displayOrder 1 → (0, 1). Escape + Right + Enter.
        var p = PlanNavigateToCharacterStatus("WorldMap", 1, 14);
        Assert.True(p.Ok);
        Assert.Equal(3, p.Steps.Count);
        Assert.Equal(VK_ESCAPE, p.Steps[0].VkCode);
        Assert.Equal(VK_RIGHT, p.Steps[1].VkCode);
        Assert.Equal(VK_ENTER, p.Steps[2].VkCode);
    }

    [Fact]
    public void Plan_FromWorldMap_ToSecondRow_DownRight()
    {
        // displayOrder 7 → (1, 2). Escape + Down + Right + Right + Enter.
        var p = PlanNavigateToCharacterStatus("WorldMap", 7, 14);
        Assert.True(p.Ok);
        Assert.Equal(5, p.Steps.Count);
        Assert.Equal(VK_ESCAPE, p.Steps[0].VkCode);
        Assert.Equal(VK_DOWN, p.Steps[1].VkCode);
        Assert.Equal(VK_RIGHT, p.Steps[2].VkCode);
        Assert.Equal(VK_RIGHT, p.Steps[3].VkCode);
        Assert.Equal(VK_ENTER, p.Steps[4].VkCode);
    }

    [Fact]
    public void Plan_FromPartyMenu_IncludesWrapToOrigin()
    {
        // Already on PartyMenu — cursor position unknown, wrap to (0, 0)
        // with gridRows Ups + 5 Lefts, then nav to target.
        // rosterCount=14 → gridRows = (14+4)/5 = 3.
        var p = PlanNavigateToCharacterStatus("PartyMenuUnits", 1, 14);
        Assert.True(p.Ok);
        // 3 UPs + 5 LEFTs = 8 wrap keys + 1 RIGHT to col 1 + 1 ENTER = 10.
        Assert.Equal(10, p.Steps.Count);
        Assert.Equal(3, p.Steps.Count(s => s.VkCode == VK_UP));
        Assert.Equal(5, p.Steps.Count(s => s.VkCode == VK_LEFT));
        Assert.Equal(1, p.Steps.Count(s => s.VkCode == VK_RIGHT));
        Assert.Equal(1, p.Steps.Count(s => s.VkCode == VK_ENTER));
        // No ESCAPE when already on PartyMenu.
        Assert.Equal(0, p.Steps.Count(s => s.VkCode == VK_ESCAPE));
    }

    [Fact]
    public void Plan_FromDeepTree_StormsEscapes()
    {
        // From EquipmentAndAbilities or another nested screen, the planner
        // emits 8 escapes + 1 final escape to open PartyMenu + nav.
        // This matches the live code which stops early on first WorldMap
        // detection — the planner emits the upper bound because it can't
        // call DetectScreen.
        var p = PlanNavigateToCharacterStatus("EquipmentAndAbilities", 0, 14);
        Assert.True(p.Ok);
        Assert.Equal(9, p.Steps.Count(s => s.VkCode == VK_ESCAPE));
        Assert.Equal(1, p.Steps.Count(s => s.VkCode == VK_ENTER));
    }

    [Fact]
    public void Plan_SettleTimesMatchLiveCode()
    {
        // Lock in the specific wait values that session 23 found via
        // live testing. Changing these risks key-drop regressions.
        var p = PlanNavigateToCharacterStatus("WorldMap", 1, 14);
        var escape = p.Steps.First(s => s.VkCode == VK_ESCAPE);
        var right = p.Steps.First(s => s.VkCode == VK_RIGHT);
        var enter = p.Steps.First(s => s.VkCode == VK_ENTER);
        Assert.Equal(1000, escape.SettleMs); // PartyMenu open animation
        Assert.Equal(200, right.SettleMs);   // nav key
        Assert.Equal(300, enter.SettleMs);   // CharacterStatus open
    }

    [Fact]
    public void Render_FormatsHumanReadableOutput()
    {
        var p = PlanNavigateToCharacterStatus("WorldMap", 0, 14);
        var rendered = p.Render();
        Assert.Contains("ESC", rendered);
        Assert.Contains("ENTER", rendered);
        Assert.Contains("total", rendered);
    }

    [Fact]
    public void Render_RenderedFailsSurfaceError()
    {
        var p = PlanNavigateToCharacterStatus("WorldMap", -1, 14);
        Assert.False(p.Ok);
        var rendered = p.Render();
        Assert.Contains("FAILED", rendered);
    }
}
