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
        private const int ZalandLocationId = 10;
        private const int LesaliaLocationId = 0;
        private const int GollundLocationId = 8;

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
            // Any id past 14 (SalGhidos) has no entries yet — should not blow up.
            // (Historical: this test previously used id=0/Lesalia; Lesalia was
            // seeded session 41 so we switched to an id beyond the settlement range.)
            Assert.Null(CityRumors.Lookup(42, 0));
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
            foreach (int city in new[] { DorterLocationId, GarilandLocationId, WarjilisLocationId, YardrowLocationId, GougLocationId, ZalandLocationId, LesaliaLocationId })
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
            foreach (int city in new[] { GarilandLocationId, WarjilisLocationId, YardrowLocationId, GougLocationId, ZalandLocationId, LesaliaLocationId })
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
        public void CitiesFor_CorpusIndex10_ReturnsAllSeededCities()
        {
            // After seeding 8 cities (7 uniform + Gollund), corpus #10 maps
            // to each at row 0. Gollund diverges from the uniform set at
            // row 3 but still has #10 at row 0.
            var result = CityRumors.CitiesFor(10);
            Assert.Equal(8, result.Count);
            Assert.Contains((DorterLocationId, 0), result);
            Assert.Contains((GarilandLocationId, 0), result);
            Assert.Contains((WarjilisLocationId, 0), result);
            Assert.Contains((YardrowLocationId, 0), result);
            Assert.Contains((GougLocationId, 0), result);
            Assert.Contains((ZalandLocationId, 0), result);
            Assert.Contains((LesaliaLocationId, 0), result);
            Assert.Contains((GollundLocationId, 0), result);
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

        // Zaland live-verified session 40: Chapter-1 uniform rumor set (6th city).

        [Fact]
        public void Zaland_Row0_MapsToZodiacBraves()
        {
            Assert.Equal(10, CityRumors.Lookup(ZalandLocationId, 0));
        }

        [Fact]
        public void Zaland_Row1_MapsToZodiacStones()
        {
            Assert.Equal(11, CityRumors.Lookup(ZalandLocationId, 1));
        }

        [Fact]
        public void Zaland_Row2_MapsToHorrorOfRiovanes()
        {
            Assert.Equal(19, CityRumors.Lookup(ZalandLocationId, 2));
        }

        [Fact]
        public void Zaland_Row3_BaelsEnd_ReturnsNull()
        {
            Assert.Null(CityRumors.Lookup(ZalandLocationId, 3));
        }

        // Lesalia live-verified session 41: Chapter-1 uniform rumor set (7th city).

        [Fact]
        public void Lesalia_Row0_MapsToZodiacBraves()
        {
            Assert.Equal(10, CityRumors.Lookup(LesaliaLocationId, 0));
        }

        [Fact]
        public void Lesalia_Row1_MapsToZodiacStones()
        {
            Assert.Equal(11, CityRumors.Lookup(LesaliaLocationId, 1));
        }

        [Fact]
        public void Lesalia_Row2_MapsToHorrorOfRiovanes()
        {
            Assert.Equal(19, CityRumors.Lookup(LesaliaLocationId, 2));
        }

        [Fact]
        public void Lesalia_Row3_BaelsEnd_ReturnsNull()
        {
            Assert.Null(CityRumors.Lookup(LesaliaLocationId, 3));
        }

        // Gollund live-verified session 42: FIRST divergence from the
        // Chapter-1 uniform set. Row 3 is "The Haunted Mine" (corpus #20),
        // a Gollund-specific rumor about monsters in its coal mines. Row 4
        // is the standard unmapped "At Bael's End".

        [Fact]
        public void Gollund_Row0_MapsToZodiacBraves()
        {
            Assert.Equal(10, CityRumors.Lookup(GollundLocationId, 0));
        }

        [Fact]
        public void Gollund_Row1_MapsToZodiacStones()
        {
            Assert.Equal(11, CityRumors.Lookup(GollundLocationId, 1));
        }

        [Fact]
        public void Gollund_Row2_MapsToHorrorOfRiovanes()
        {
            Assert.Equal(19, CityRumors.Lookup(GollundLocationId, 2));
        }

        [Fact]
        public void Gollund_Row3_HauntedMine_MapsToCorpus20()
        {
            // Gollund-specific rumor — the other 7 Chapter-1 cities don't
            // have this row. Body: "Monsters have taken up residence in
            // one of the many coal mines in Gollund..."
            Assert.Equal(20, CityRumors.Lookup(GollundLocationId, 3));
        }

        [Fact]
        public void Gollund_Row4_BaelsEnd_ReturnsNull()
        {
            // Row 4 (not row 3) is "At Bael's End" at Gollund — unmapped.
            Assert.Null(CityRumors.Lookup(GollundLocationId, 4));
        }

        [Fact]
        public void Gollund_IsNotChapter1UniformCity()
        {
            // Pin that Gollund diverges from the uniform set.
            Assert.False(CityRumors.IsChapter1UniformCity(GollundLocationId));
        }

        [Fact]
        public void CitiesFor_CorpusIndex20_ReturnsOnlyGollund()
        {
            // Corpus #20 is the Gollund-specific Haunted Mine body; should
            // reverse-lookup to exactly one (city, row) pair.
            var result = CityRumors.CitiesFor(20);
            Assert.Single(result);
            Assert.Contains((GollundLocationId, 3), result);
        }

        // Session 42: coverage stats for seed progress diagnostics.

        [Fact]
        public void CoverageStats_SettlementProgress_AboveFloor()
        {
            // 15 total settlements (CityId.Lesalia=0 through SalGhidos=14).
            // Floor asserted conservatively to allow back-sliding if a chapter-2
            // visit forces a per-city split that temporarily removes an entry.
            // Raise this floor as coverage grows to lock in progress.
            const int totalSettlements = 15;
            int seededCount = 0;
            for (int cityId = 0; cityId < totalSettlements; cityId++)
            {
                // Any row lookup that returns non-null means the city has
                // at least one seed entry.
                bool hasSeed = false;
                for (int row = 0; row < 10; row++)
                {
                    if (CityRumors.Lookup(cityId, row) != null) { hasSeed = true; break; }
                }
                if (hasSeed) seededCount++;
            }
            double coverage = (double)seededCount / totalSettlements;
            // Current floor: 8/15 (53%) — sessions 33-42 seeded 8 cities.
            // Subtract a safety margin and pin at 40% to tolerate a single
            // divergence-rollback without false-failing.
            Assert.True(coverage >= 0.40,
                $"Settlement rumor coverage has dropped below 40% ({seededCount}/{totalSettlements} = {coverage:P0})");
        }

        [Fact]
        public void CoverageStats_Chapter1UniformCount_AtLeastSix()
        {
            // Floor on how many cities still match the Chapter-1 uniform set.
            // Gollund broke the hypothesis at session 42, leaving 7. Floor at
            // 6 so one more divergence can surface without masking the trend.
            int uniformCount = 0;
            for (int cityId = 0; cityId < 15; cityId++)
            {
                if (CityRumors.IsChapter1UniformCity(cityId)) uniformCount++;
            }
            Assert.True(uniformCount >= 6,
                $"Chapter1UniformCity count dropped below 6 ({uniformCount})");
        }

        // Session 41: IsChapter1UniformCity — true for the 7 cities seeded
        // with the Chapter1UniformRows shared dictionary.

        [Theory]
        [InlineData(0, true)]    // Lesalia
        [InlineData(6, true)]    // Gariland
        [InlineData(7, true)]    // Yardrow
        [InlineData(9, true)]    // Dorter
        [InlineData(10, true)]   // Zaland
        [InlineData(11, true)]   // Goug
        [InlineData(12, true)]   // Warjilis
        [InlineData(1, false)]   // Riovanes — battle location, not seeded
        [InlineData(2, false)]   // Eagrose
        [InlineData(4, false)]   // Limberry — not seeded yet
        [InlineData(14, false)]  // Sal Ghidos — not seeded yet
        [InlineData(-1, false)]  // invalid
        [InlineData(42, false)]  // past settlement range
        public void IsChapter1UniformCity_Sweep(int cityId, bool expected)
        {
            Assert.Equal(expected, CityRumors.IsChapter1UniformCity(cityId));
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
