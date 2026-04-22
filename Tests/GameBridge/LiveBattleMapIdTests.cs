using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Session 48 shipped `0x14077D83C` as the authoritative current-battle
// map id byte. This test pins the constant + the validity range so any
// future refactor that shifts either silently breaks the fallback chain.
// The rule in NavigationActions reads a u8 and accepts id in [1..127];
// out-of-range = fall through to locId-based lookup.
public class LiveBattleMapIdTests
{
    [Fact]
    public void Address_IsStable_From_Session48()
    {
        Assert.Equal(0x14077D83CL, LiveBattleMapId.Address);
    }

    [Fact]
    public void Valid_RealMapIds_InRange()
    {
        // Map IDs 1..127 are valid. 0 and 128+ are uninitialized or invalid.
        Assert.True(LiveBattleMapId.IsValid(1));
        Assert.True(LiveBattleMapId.IsValid(74));   // MAP074 — a real map
        Assert.True(LiveBattleMapId.IsValid(86));   // MAP086 — Dugeura
        Assert.True(LiveBattleMapId.IsValid(82));   // MAP082 — Beddha
        Assert.True(LiveBattleMapId.IsValid(127));
    }

    [Fact]
    public void InvalidIds_RejectedByRange()
    {
        Assert.False(LiveBattleMapId.IsValid(0));
        Assert.False(LiveBattleMapId.IsValid(128));
        Assert.False(LiveBattleMapId.IsValid(255));
        Assert.False(LiveBattleMapId.IsValid(-1));
    }
}
