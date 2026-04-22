using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Geomancy (Elemental) ability selection is driven by the terrain type
// the caster stands on. The game uses ~16 surface types mapped to
// specific ability names. Pure table lookup; full mapping pinned here
// so ability rendering can surface "Elemental: Wind Slash (Grassland)".
//
// Surface type IDs sourced from FFHacktics wiki map-tile-type table
// (PSX canonical). If IC remaster diverges, live captures will flip
// individual entries; the table is the single source of truth.
public class GeomancySurfaceTableTests
{
    [Theory]
    [InlineData(0, "Local Quake")]          // Natural Surface
    [InlineData(1, "Local Quake")]          // Stone Wall / Stone Floor
    [InlineData(2, "Pitfall")]              // Wasteland
    [InlineData(3, "Water Ball")]           // Swamp
    [InlineData(4, "Hell Ivy")]             // Grassland
    [InlineData(5, "Sand Storm")]           // Bushes (thicket)
    [InlineData(6, "Sand Storm")]           // Tree
    [InlineData(7, "Blizzard")]             // Snow
    [InlineData(8, "Gusty Wind")]           // Rocky Cliff
    [InlineData(9, "Lava Ball")]            // Gravel
    [InlineData(10, "Will-o-the-Wisp")]     // River
    [InlineData(11, "Will-o-the-Wisp")]     // Lake
    [InlineData(12, "Will-o-the-Wisp")]     // Sea
    [InlineData(13, "Sand Storm")]          // Bridge (wood)
    [InlineData(14, "Blizzard")]            // Ice
    public void KnownSurface_MapsToExpectedGeomancyAbility(int surfaceId, string expected)
    {
        Assert.Equal(expected, GeomancySurfaceTable.AbilityFor(surfaceId));
    }

    [Fact]
    public void UnknownSurface_ReturnsNull()
    {
        Assert.Null(GeomancySurfaceTable.AbilityFor(99));
        Assert.Null(GeomancySurfaceTable.AbilityFor(-1));
    }

    [Fact]
    public void Table_CoversAllKnownIds()
    {
        // Sanity: AbilityFor returns non-null for every catalogued id.
        foreach (var id in GeomancySurfaceTable.KnownSurfaceIds)
            Assert.NotNull(GeomancySurfaceTable.AbilityFor(id));
    }

    [Fact]
    public void SurfaceName_KnownId_ReturnsHumanName()
    {
        Assert.Equal("Grassland", GeomancySurfaceTable.SurfaceName(4));
        Assert.Equal("Snow", GeomancySurfaceTable.SurfaceName(7));
    }

    [Fact]
    public void SurfaceName_UnknownId_ReturnsUnknown()
    {
        Assert.Equal("Unknown", GeomancySurfaceTable.SurfaceName(99));
    }
}
