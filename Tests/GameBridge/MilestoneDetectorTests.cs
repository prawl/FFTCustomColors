using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure helper: given a unit's lifetime stats, what milestones have they
// just crossed? "Just crossed" is the key — we compare before and after
// to emit one-shot callouts ("Ramza reached 100 kills!") without
// repeating them every battle.
//
// Milestones are multiples of round numbers: kills at 10, 50, 100, 500;
// damage at 1000, 5000, 10000; battles at 10, 50, 100. Full battery
// so a long playthrough surfaces interesting moments without spamming.
public class MilestoneDetectorTests
{
    [Fact]
    public void NoChange_NoMilestones()
    {
        var before = new UnitLifetimeStats { Name = "Ramza", TotalKills = 50, TotalDamageDealt = 5000 };
        var after = new UnitLifetimeStats { Name = "Ramza", TotalKills = 50, TotalDamageDealt = 5000 };
        Assert.Empty(MilestoneDetector.Detect(before, after));
    }

    [Fact]
    public void Kill_Milestone_10_Crossed()
    {
        var before = new UnitLifetimeStats { Name = "Ramza", TotalKills = 9 };
        var after = new UnitLifetimeStats { Name = "Ramza", TotalKills = 10 };
        var milestones = MilestoneDetector.Detect(before, after);
        Assert.Contains(milestones, m => m.Contains("10 kills"));
        Assert.Contains(milestones, m => m.Contains("Ramza"));
    }

    [Fact]
    public void Kill_Milestone_100_Crossed()
    {
        var before = new UnitLifetimeStats { Name = "Lloyd", TotalKills = 99 };
        var after = new UnitLifetimeStats { Name = "Lloyd", TotalKills = 100 };
        var milestones = MilestoneDetector.Detect(before, after);
        Assert.Contains(milestones, m => m.Contains("100 kills") && m.Contains("Lloyd"));
    }

    [Fact]
    public void FirstKill_EmitsCallout()
    {
        var before = new UnitLifetimeStats { Name = "Wilham", TotalKills = 0 };
        var after = new UnitLifetimeStats { Name = "Wilham", TotalKills = 1 };
        var milestones = MilestoneDetector.Detect(before, after);
        Assert.Contains(milestones, m => m.Contains("first kill") && m.Contains("Wilham"));
    }

    [Fact]
    public void NonMilestoneKills_Silent()
    {
        // 23 → 24 is not a milestone. No output.
        var before = new UnitLifetimeStats { Name = "Ramza", TotalKills = 23 };
        var after = new UnitLifetimeStats { Name = "Ramza", TotalKills = 24 };
        Assert.Empty(MilestoneDetector.Detect(before, after));
    }

    [Fact]
    public void Damage_Milestone_1000_Crossed()
    {
        var before = new UnitLifetimeStats { Name = "Ramza", TotalDamageDealt = 980 };
        var after = new UnitLifetimeStats { Name = "Ramza", TotalDamageDealt = 1200 };
        var milestones = MilestoneDetector.Detect(before, after);
        Assert.Contains(milestones, m => m.Contains("1,000") || m.Contains("1000"));
    }

    [Fact]
    public void Damage_MultipleThresholdsInOneBattle_AllFire()
    {
        // Edge case: a big single battle can cross multiple thresholds.
        // 950 + 4100 damage = crosses both 1000 and 5000 in one swing.
        var before = new UnitLifetimeStats { Name = "Ramza", TotalDamageDealt = 950 };
        var after = new UnitLifetimeStats { Name = "Ramza", TotalDamageDealt = 5050 };
        var milestones = MilestoneDetector.Detect(before, after);
        Assert.Equal(2, milestones.Count);
    }

    [Fact]
    public void Battles_Milestone_10_Crossed()
    {
        var before = new UnitLifetimeStats { Name = "Ramza", TotalBattles = 9 };
        var after = new UnitLifetimeStats { Name = "Ramza", TotalBattles = 10 };
        var milestones = MilestoneDetector.Detect(before, after);
        Assert.Contains(milestones, m => m.Contains("10 battles"));
    }

    [Fact]
    public void Multiple_Units_IterateAll()
    {
        // Integration: DetectAll takes before/after lifetime-wide snapshots
        // and emits milestones for every unit that crossed one.
        var before = new LifetimeStats();
        before.Units["Ramza"] = new UnitLifetimeStats { Name = "Ramza", TotalKills = 99 };
        before.Units["Lloyd"] = new UnitLifetimeStats { Name = "Lloyd", TotalKills = 9 };
        before.Units["Kenrick"] = new UnitLifetimeStats { Name = "Kenrick", TotalKills = 5 };

        var after = new LifetimeStats();
        after.Units["Ramza"] = new UnitLifetimeStats { Name = "Ramza", TotalKills = 100 };
        after.Units["Lloyd"] = new UnitLifetimeStats { Name = "Lloyd", TotalKills = 10 };
        after.Units["Kenrick"] = new UnitLifetimeStats { Name = "Kenrick", TotalKills = 5 }; // no change

        var milestones = MilestoneDetector.DetectAll(before, after);
        Assert.Equal(2, milestones.Count);
        Assert.Contains(milestones, m => m.Contains("Ramza") && m.Contains("100"));
        Assert.Contains(milestones, m => m.Contains("Lloyd") && m.Contains("10"));
    }

    [Fact]
    public void NewUnit_InAfterOnly_HandledGracefully()
    {
        // Recruited mid-playthrough: no "before" entry.
        var before = new LifetimeStats();
        var after = new LifetimeStats();
        after.Units["Agrias"] = new UnitLifetimeStats { Name = "Agrias", TotalKills = 1 };

        var milestones = MilestoneDetector.DetectAll(before, after);
        Assert.Contains(milestones, m => m.Contains("first kill") && m.Contains("Agrias"));
    }
}
