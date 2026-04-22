using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure-helper plan-builder for the `search_bytes` bridge action.
// The command has three knobs affecting the underlying
// SearchBytesInAllMemory call: minAddr / maxAddr (range filter) and
// broadSearch (scan RX + private memory instead of private only).
//
// Building the plan here lets us test the full decision table without
// mocking the Windows memory API.
public class SearchBytesPlanTests
{
    [Fact]
    public void NoRange_NoBroadSearch_UsesFullRange()
    {
        var plan = SearchBytesPlan.From(new CommandRequest());
        Assert.Equal(0L, plan.MinAddr);
        Assert.Equal(long.MaxValue, plan.MaxAddr);
        Assert.False(plan.BroadSearch);
    }

    [Fact]
    public void MinAddr_Parsed_FromHexString()
    {
        var plan = SearchBytesPlan.From(new CommandRequest { MinAddr = "0x4000000000" });
        Assert.Equal(0x4000000000L, plan.MinAddr);
    }

    [Fact]
    public void MaxAddr_Parsed_FromHexString()
    {
        var plan = SearchBytesPlan.From(new CommandRequest { MaxAddr = "0x4200000000" });
        Assert.Equal(0x4200000000L, plan.MaxAddr);
    }

    [Fact]
    public void BroadSearch_Defaults_False()
    {
        var plan = SearchBytesPlan.From(new CommandRequest());
        Assert.False(plan.BroadSearch);
    }

    [Fact]
    public void BroadSearch_Forwarded_WhenTrue()
    {
        // TODO §0 session 52: unblocks per-unit-ct hunt — main-module
        // RX pages at 0x14184xxxx were invisible to narrow scans.
        var plan = SearchBytesPlan.From(new CommandRequest { BroadSearch = true });
        Assert.True(plan.BroadSearch);
    }

    [Fact]
    public void BroadSearch_Combines_With_Range()
    {
        var plan = SearchBytesPlan.From(new CommandRequest
        {
            MinAddr = "0x141000000",
            MaxAddr = "0x15C000000",
            BroadSearch = true,
        });
        Assert.Equal(0x141000000L, plan.MinAddr);
        Assert.Equal(0x15C000000L, plan.MaxAddr);
        Assert.True(plan.BroadSearch);
    }
}
