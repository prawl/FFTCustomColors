using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Stat-tracker hooks record attacker/target by unit name. Enemy names
// aren't readable from memory (Instructions/BattleTurns.md), so a scan
// returns Name=null + JobNameOverride="Minotaur" for enemies. Without a
// fallback, stat rows attribute every enemy-side KO to "(unknown)"
// instead of at least saying "Minotaur KO'd Wilham".
//
// Pure helper resolves the best available label: Name > JobName > default.
public class UnitDisplayNameTests
{
    [Fact]
    public void Name_WhenSet_IsUsed()
    {
        Assert.Equal("Ramza", UnitDisplayName.For(name: "Ramza", jobName: "Gallant Knight"));
    }

    [Fact]
    public void Name_NullButJobNamePresent_FallsBackToJobName()
    {
        Assert.Equal("Minotaur", UnitDisplayName.For(name: null, jobName: "Minotaur"));
    }

    [Fact]
    public void BothNull_ReturnsUnknownDefault()
    {
        Assert.Equal("(unknown)", UnitDisplayName.For(name: null, jobName: null));
    }

    [Fact]
    public void BothEmpty_ReturnsUnknownDefault()
    {
        Assert.Equal("(unknown)", UnitDisplayName.For(name: "", jobName: ""));
    }

    [Fact]
    public void Name_EmptyFallsThroughToJobName()
    {
        // Empty string shouldn't outrank a valid job name.
        Assert.Equal("Goblin", UnitDisplayName.For(name: "", jobName: "Goblin"));
    }

    [Fact]
    public void Name_WhitespaceFallsThroughToJobName()
    {
        Assert.Equal("Knight", UnitDisplayName.For(name: "   ", jobName: "Knight"));
    }
}
