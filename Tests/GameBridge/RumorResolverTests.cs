using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for RumorResolver — pure resolution logic extracted from the
    /// get_rumor bridge action in CommandWatcher.
    ///
    /// Resolution order (priority ranked):
    ///   1. Non-empty searchLabel → title map or body substring
    ///   2. locationId &gt;= 0 → CityRumors (cityId, unitIndex → corpusIdx)
    ///   3. fallback → unitIndex as raw corpus index
    ///
    /// The tests pin the priority so a future refactor that reorders the
    /// branches surfaces immediately (e.g. if someone makes locationId
    /// preempt searchLabel, the SearchLabelBeatsLocationId test fires).
    /// </summary>
    public class RumorResolverTests
    {
        private readonly RumorLookup _lookup = new();

        [Fact]
        public void Resolve_SearchLabelTitle_ResolvesViaTitle()
        {
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: "Zodiac Stones", locationId: -1, unitIndex: 0);
            Assert.True(result.Ok);
            Assert.Equal(11, result.Rumor!.Index);
        }

        [Fact]
        public void Resolve_SearchLabelSubstring_ResolvesViaBodyMatch()
        {
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: "Riovanes", locationId: -1, unitIndex: 0);
            Assert.True(result.Ok);
            Assert.Equal(19, result.Rumor!.Index);
        }

        [Fact]
        public void Resolve_SearchLabelBeatsLocationId()
        {
            // Priority: when searchLabel is set AND locationId is valid,
            // searchLabel wins. Pin this so a refactor doesn't silently
            // flip the branches.
            // Dorter row 0 = corpus #10, but "Zodiac Stones" = corpus #11.
            // The call should resolve to #11 (via title), not #10 (via city+row).
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: "Zodiac Stones",
                locationId: 9, unitIndex: 0);
            Assert.True(result.Ok);
            Assert.Equal(11, result.Rumor!.Index);
        }

        [Fact]
        public void Resolve_LocationIdFiresWhenSearchLabelEmpty()
        {
            // With empty searchLabel and valid locationId, city+row path fires.
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: null, locationId: 9, unitIndex: 0);
            Assert.True(result.Ok);
            Assert.Equal(10, result.Rumor!.Index);
        }

        [Fact]
        public void Resolve_LocationIdFiresWhenSearchLabelWhitespace()
        {
            // Whitespace-only searchLabel is treated as empty.
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: "   ", locationId: 9, unitIndex: 0);
            Assert.True(result.Ok);
            Assert.Equal(10, result.Rumor!.Index);
        }

        [Fact]
        public void Resolve_FallbackToRawIndex_WhenNeitherSearchLabelNorLocation()
        {
            // No searchLabel, no locationId (or negative) → unitIndex is raw
            // corpus index. Pass-through for direct-access callers.
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: null, locationId: -1, unitIndex: 5);
            Assert.True(result.Ok);
            Assert.Equal(5, result.Rumor!.Index);
        }

        [Fact]
        public void Resolve_SearchLabelNoMatch_ReturnsError()
        {
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: "Not A Real Rumor", locationId: -1, unitIndex: 0);
            Assert.False(result.Ok);
            Assert.Contains("No rumor matches", result.Error);
        }

        [Fact]
        public void Resolve_UnmappedCityRow_ReturnsError()
        {
            // Dorter row 3 ("At Bael's End") is unmapped — clean error.
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: null, locationId: 9, unitIndex: 3);
            Assert.False(result.Ok);
            Assert.Contains("No rumor mapped for city 9 row 3", result.Error);
        }

        [Fact]
        public void Resolve_RawIndexOutOfRange_ReturnsError()
        {
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: null, locationId: -1, unitIndex: 9999);
            Assert.False(result.Ok);
            Assert.Contains("out of range", result.Error);
        }

        [Fact]
        public void Resolve_TitleMapCaseInsensitive()
        {
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: "ZODIAC STONES", locationId: -1, unitIndex: 0);
            Assert.True(result.Ok);
            Assert.Equal(11, result.Rumor!.Index);
        }
    }
}
