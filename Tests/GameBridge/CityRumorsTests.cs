using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for CityRumors — maps (cityId, row) → corpus index so get_rumor can
    /// resolve rumor bodies from the game's in-scene cursor position rather than
    /// requiring Claude to know the title or a distinctive phrase.
    ///
    /// Seed data is session 33's live Dorter mapping: row 0 → #10 (Zodiac Braves),
    /// row 1 → #11 (Zodiac Stones), row 2 → #19 (Riovanes). Row 3 at Dorter is
    /// "At Bael's End" which is NOT in the corpus — lookup must return null for it.
    ///
    /// Future sessions expand the table by visiting each of the 15 settlements
    /// with a Tavern per the workflow in FFTHandsFree/TavernRumorTitleMap.md.
    /// </summary>
    public class CityRumorsTests
    {
        // Named city IDs — also pinned below that these match the constants
        // in CityRumors.CityId (which is the canonical source).
        private const int DorterLocationId = 9;
        private const int GarilandLocationId = 6;
        private const int WarjilisLocationId = 12;
        private const int YardrowLocationId = 7;
        private const int GougLocationId = 11;

        [Fact]
        public void Dorter_Row0_MapsToCorpusIndex10()
        {
            Assert.Equal(10, CityRumors.Lookup(DorterLocationId, 0));
        }

        [Fact]
        public void Dorter_Row1_MapsToCorpusIndex11()
        {
            Assert.Equal(11, CityRumors.Lookup(DorterLocationId, 1));
        }

        [Fact]
        public void Dorter_Row2_MapsToCorpusIndex19()
        {
            Assert.Equal(19, CityRumors.Lookup(DorterLocationId, 2));
        }

        [Fact]
        public void Dorter_Row3_ReturnsNull_BaelsEnd_NotInCorpus()
        {
            // "At Bael's End" body is not in world_wldmes.bin — the lookup must
            // return null rather than a spurious corpus index so callers can
            // fall through to a "not decoded" error message.
            Assert.Null(CityRumors.Lookup(DorterLocationId, 3));
        }

        [Fact]
        public void UnknownCity_ReturnsNull()
        {
            // Location id 0 (Lesalia) has no entries yet — should not blow up.
            Assert.Null(CityRumors.Lookup(0, 0));
        }

        [Fact]
        public void NegativeCityId_ReturnsNull()
        {
            // -1 is the "unset" sentinel on CommandRequest.LocationId.
            Assert.Null(CityRumors.Lookup(-1, 0));
        }

        [Fact]
        public void NegativeRow_ReturnsNull()
        {
            Assert.Null(CityRumors.Lookup(DorterLocationId, -1));
        }

        [Fact]
        public void RowOutOfRange_ReturnsNull()
        {
            // Dorter only has rows 0-3 populated (3 maps to null by design).
            // Any row beyond the table must also return null.
            Assert.Null(CityRumors.Lookup(DorterLocationId, 99));
        }

        // Session 35: reverse lookup — given a corpus index, return every
        // (cityId, row) pair that maps to it. Supports the title-map workflow
        // by surfacing which cities' rumor lists contain a given corpus body.

        [Fact]
        public void CitiesFor_CorpusIndex10_ReturnsDorterRow0()
        {
            var result = CityRumors.CitiesFor(10);
            Assert.Contains((DorterLocationId, 0), result);
        }

        [Fact]
        public void CitiesFor_CorpusIndex11_ReturnsDorterRow1()
        {
            var result = CityRumors.CitiesFor(11);
            Assert.Contains((DorterLocationId, 1), result);
        }

        [Fact]
        public void CitiesFor_CorpusIndex19_ReturnsDorterRow2()
        {
            var result = CityRumors.CitiesFor(19);
            Assert.Contains((DorterLocationId, 2), result);
        }

        [Fact]
        public void CitiesFor_UnmappedCorpusIndex_ReturnsEmpty()
        {
            // Corpus index 0 isn't mapped to any city yet.
            var result = CityRumors.CitiesFor(0);
            Assert.Empty(result);
        }

        [Fact]
        public void CitiesFor_NegativeIndex_ReturnsEmpty()
        {
            Assert.Empty(CityRumors.CitiesFor(-1));
        }

        [Fact]
        public void CitiesFor_IndexBeyondCorpus_ReturnsEmpty()
        {
            Assert.Empty(CityRumors.CitiesFor(9999));
        }

        [Fact]
        public void CitiesFor_IsConsistentWithForwardLookup()
        {
            // Round-trip: for every (city, row) in the forward table,
            // CitiesFor(lookupIndex) must include that (city, row) pair.
            foreach (int city in new[] { DorterLocationId, GarilandLocationId, WarjilisLocationId, YardrowLocationId, GougLocationId })
            {
                for (int row = 0; row < 10; row++)
                {
                    int? idx = CityRumors.Lookup(city, row);
                    if (idx == null) continue;
                    var reverse = CityRumors.CitiesFor(idx.Value);
                    Assert.Contains((city, row), reverse);
                }
            }
        }

        // Gariland live-verified session 36: Chapter 1 rumor set matches Dorter.

        [Fact]
        public void Gariland_Row0_MapsToZodiacBraves()
        {
            Assert.Equal(10, CityRumors.Lookup(GarilandLocationId, 0));
        }

        [Fact]
        public void Gariland_Row1_MapsToZodiacStones()
        {
            Assert.Equal(11, CityRumors.Lookup(GarilandLocationId, 1));
        }

        [Fact]
        public void Gariland_Row2_MapsToHorrorOfRiovanes()
        {
            Assert.Equal(19, CityRumors.Lookup(GarilandLocationId, 2));
        }

        [Fact]
        public void Gariland_Row3_BaelsEnd_ReturnsNull()
        {
            // Same unmappable title as Dorter row 3.
            Assert.Null(CityRumors.Lookup(GarilandLocationId, 3));
        }

        [Fact]
        public void Gariland_AndDorter_HaveIdenticalChapter1RumorSet()
        {
            // Pin the observed invariant: every mapped row at Gariland resolves
            // to the same corpus entry as the same row at Dorter. If this
            // breaks, either the game changed between chapters OR a typo
            // slipped into one of the tables.
            for (int row = 0; row < 4; row++)
            {
                Assert.Equal(
                    CityRumors.Lookup(DorterLocationId, row),
                    CityRumors.Lookup(GarilandLocationId, row));
            }
        }

        [Fact]
        public void CitiesFor_CorpusIndex10_ReturnsBothDorterAndGariland()
        {
            // After seeding Gariland, corpus #10 is reachable from two cities.
            var result = CityRumors.CitiesFor(10);
            Assert.Contains((DorterLocationId, 0), result);
            Assert.Contains((GarilandLocationId, 0), result);
        }

        // Warjilis live-verified session 37: same Chapter-1 rumor set.

        [Fact]
        public void Warjilis_Row0_MapsToZodiacBraves()
        {
            Assert.Equal(10, CityRumors.Lookup(WarjilisLocationId, 0));
        }

        [Fact]
        public void Warjilis_Row1_MapsToZodiacStones()
        {
            Assert.Equal(11, CityRumors.Lookup(WarjilisLocationId, 1));
        }

        [Fact]
        public void Warjilis_Row2_MapsToHorrorOfRiovanes()
        {
            Assert.Equal(19, CityRumors.Lookup(WarjilisLocationId, 2));
        }

        [Fact]
        public void Warjilis_Row3_BaelsEnd_ReturnsNull()
        {
            // Same unmappable title as Dorter and Gariland row 3.
            Assert.Null(CityRumors.Lookup(WarjilisLocationId, 3));
        }

        [Fact]
        public void Chapter1Cities_AllMatchDorter()
        {
            // Pin the invariant: all seeded Chapter-1 cities resolve row-by-row
            // to the same corpus index as Dorter. Divergence breaks this test,
            // prompting a per-chapter split.
            foreach (int city in new[] { GarilandLocationId, WarjilisLocationId, YardrowLocationId, GougLocationId })
            {
                for (int row = 0; row < 3; row++)
                {
                    Assert.Equal(
                        CityRumors.Lookup(DorterLocationId, row),
                        CityRumors.Lookup(city, row));
                }
            }
        }

        [Fact]
        public void CitiesFor_CorpusIndex10_ReturnsAllSeededChapter1Cities()
        {
            // After seeding all five Chapter-1 cities, corpus #10 maps to
            // (Dorter, 0), (Gariland, 0), (Warjilis, 0), (Yardrow, 0), (Goug, 0).
            var result = CityRumors.CitiesFor(10);
            Assert.Equal(5, result.Count);
            Assert.Contains((DorterLocationId, 0), result);
            Assert.Contains((GarilandLocationId, 0), result);
            Assert.Contains((WarjilisLocationId, 0), result);
            Assert.Contains((YardrowLocationId, 0), result);
            Assert.Contains((GougLocationId, 0), result);
        }

        // Yardrow live-verified session 38: Chapter-1 uniform rumor set.

        [Fact]
        public void Yardrow_Row0_MapsToZodiacBraves()
        {
            Assert.Equal(10, CityRumors.Lookup(YardrowLocationId, 0));
        }

        [Fact]
        public void Yardrow_Row1_MapsToZodiacStones()
        {
            Assert.Equal(11, CityRumors.Lookup(YardrowLocationId, 1));
        }

        [Fact]
        public void Yardrow_Row2_MapsToHorrorOfRiovanes()
        {
            Assert.Equal(19, CityRumors.Lookup(YardrowLocationId, 2));
        }

        [Fact]
        public void Yardrow_Row3_BaelsEnd_ReturnsNull()
        {
            Assert.Null(CityRumors.Lookup(YardrowLocationId, 3));
        }

        // Goug live-verified session 39: Chapter-1 uniform rumor set (5th city).

        [Fact]
        public void Goug_Row0_MapsToZodiacBraves()
        {
            Assert.Equal(10, CityRumors.Lookup(GougLocationId, 0));
        }

        [Fact]
        public void Goug_Row1_MapsToZodiacStones()
        {
            Assert.Equal(11, CityRumors.Lookup(GougLocationId, 1));
        }

        [Fact]
        public void Goug_Row2_MapsToHorrorOfRiovanes()
        {
            Assert.Equal(19, CityRumors.Lookup(GougLocationId, 2));
        }

        [Fact]
        public void Goug_Row3_BaelsEnd_ReturnsNull()
        {
            Assert.Null(CityRumors.Lookup(GougLocationId, 3));
        }

        // Session 36: defensive validity guards on the CityRumors.Table
        // contents so future seed additions surface typos at test time.

        [Fact]
        public void AllMappings_CityIds_AreWithinValidWorldMapRange()
        {
            // Location IDs are [0, 42] inclusive per LocationSaveLogic.
            // A typoed city id (e.g. 99 or -1) would silently never fire.
            foreach (var (city, _, _) in CityRumors.AllMappings)
            {
                Assert.InRange(city, 0, 42);
            }
        }

        [Fact]
        public void AllMappings_Rows_AreNonNegative()
        {
            // Row indices correspond to UI cursor positions — must be 0+.
            foreach (var (_, row, _) in CityRumors.AllMappings)
            {
                Assert.True(row >= 0, $"Row {row} is negative");
            }
        }

        [Fact]
        public void AllMappings_CorpusIndices_ResolveToRealRumor()
        {
            // Every mapped corpus index must point to a valid RumorCorpus entry.
            // Catches off-by-one errors in the seed tables.
            int corpusSize = RumorCorpus.Bodies.Length;
            foreach (var (city, row, idx) in CityRumors.AllMappings)
            {
                Assert.InRange(idx, 0, corpusSize - 1);
            }
        }

        [Fact]
        public void CityIdConstants_MatchWorldMapTravelIds()
        {
            // Canonical source: Shopping.md settlement list.
            Assert.Equal(0, CityRumors.CityId.Lesalia);
            Assert.Equal(1, CityRumors.CityId.Riovanes);
            Assert.Equal(2, CityRumors.CityId.Eagrose);
            Assert.Equal(3, CityRumors.CityId.Lionel);
            Assert.Equal(4, CityRumors.CityId.Limberry);
            Assert.Equal(5, CityRumors.CityId.Zeltennia);
            Assert.Equal(6, CityRumors.CityId.Gariland);
            Assert.Equal(7, CityRumors.CityId.Yardrow);
            Assert.Equal(8, CityRumors.CityId.Gollund);
            Assert.Equal(9, CityRumors.CityId.Dorter);
            Assert.Equal(10, CityRumors.CityId.Zaland);
            Assert.Equal(11, CityRumors.CityId.Goug);
            Assert.Equal(12, CityRumors.CityId.Warjilis);
            Assert.Equal(13, CityRumors.CityId.Bervenia);
            Assert.Equal(14, CityRumors.CityId.SalGhidos);
        }

        [Fact]
        public void CityIdConstants_MatchLocalTestConstants()
        {
            // Ensure the local test constants match the canonical CityId constants.
            // If the canonical values ever change, this test surfaces the drift.
            Assert.Equal(CityRumors.CityId.Dorter, DorterLocationId);
            Assert.Equal(CityRumors.CityId.Gariland, GarilandLocationId);
        }

        // Session 37: CityId name↔id round-trip helpers (for friendly error
        // messages and diagnostic dumps).

        [Theory]
        [InlineData(0, "Lesalia")]
        [InlineData(6, "Gariland")]
        [InlineData(9, "Dorter")]
        [InlineData(12, "Warjilis")]
        [InlineData(14, "SalGhidos")]
        public void CityId_NameForId_RoundTrips(int id, string expectedName)
        {
            Assert.Equal(expectedName, CityRumors.CityId.NameFor(id));
        }

        [Fact]
        public void CityId_NameForId_UnknownReturnsNull()
        {
            Assert.Null(CityRumors.CityId.NameFor(99));
            Assert.Null(CityRumors.CityId.NameFor(-1));
            Assert.Null(CityRumors.CityId.NameFor(15));  // just past the last settlement
        }

        [Theory]
        [InlineData("Lesalia", 0)]
        [InlineData("Dorter", 9)]
        [InlineData("Warjilis", 12)]
        public void CityId_IdForName_RoundTrips(string name, int expectedId)
        {
            Assert.Equal(expectedId, CityRumors.CityId.IdFor(name));
        }

        [Fact]
        public void CityId_IdForName_CaseInsensitive()
        {
            Assert.Equal(9, CityRumors.CityId.IdFor("dorter"));
            Assert.Equal(9, CityRumors.CityId.IdFor("DORTER"));
        }

        [Fact]
        public void CityId_IdForName_UnknownReturnsMinus1()
        {
            Assert.Equal(-1, CityRumors.CityId.IdFor("Atlantis"));
            Assert.Equal(-1, CityRumors.CityId.IdFor(""));
            Assert.Equal(-1, CityRumors.CityId.IdFor(null!));
        }

        [Fact]
        public void CityId_AllKnownIds_RoundTrip_NameThenId()
        {
            // Every canonical id resolves to a name that resolves back to the id.
            for (int id = 0; id <= 14; id++)
            {
                string? name = CityRumors.CityId.NameFor(id);
                Assert.NotNull(name);
                Assert.Equal(id, CityRumors.CityId.IdFor(name!));
            }
        }

        [Fact]
        public void AllMappings_NoDuplicateCityRowPairs()
        {
            // A (city, row) pair should appear at most once (otherwise the
            // table has conflicting mappings for the same UI position).
            var seen = new System.Collections.Generic.HashSet<(int, int)>();
            foreach (var (city, row, _) in CityRumors.AllMappings)
            {
                Assert.True(seen.Add((city, row)),
                    $"Duplicate mapping for city={city} row={row}");
            }
        }
    }
}
