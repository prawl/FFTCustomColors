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

        // Session 42: priority-ambiguity characterization. When searchLabel
        // matches BOTH the title map AND a body substring in a different
        // corpus entry, the title map wins (per the documented priority).

        [Fact]
        public void Resolve_TitleOverSubstring_WhenBothMatch()
        {
            // "Zodiac Stones" is in the title map (→ #11) AND appears as a
            // substring in corpus #10's body ("...engraved with a sign of
            // the zodiac, and so became known as the Zodiac Braves" plus
            // references to the Stones). Title map wins → #11.
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: "Zodiac Stones", locationId: -1, unitIndex: 0);
            Assert.True(result.Ok);
            Assert.Equal(11, result.Rumor!.Index);
        }

        [Fact]
        public void Resolve_SubstringOnly_WhenNoTitleMatch()
        {
            // "Zodiac Braves" IS in the title map ("The Legend of the Zodiac
            // Braves"), but if we pass just "Braves" that doesn't match a
            // title key — falls through to body-substring (corpus #10).
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: "Braves", locationId: -1, unitIndex: 0);
            Assert.True(result.Ok);
            // Whichever corpus contains "Braves" first — corpus #10 is the
            // Zodiac Braves body so that's the expected hit.
            Assert.Equal(10, result.Rumor!.Index);
        }

        [Fact]
        public void Resolve_CityRowPath_SkippedWhenSearchLabelSet()
        {
            // Even if locationId AND searchLabel are both set, searchLabel
            // wins. City+row resolution only fires when searchLabel is empty.
            // Gollund row 3 = corpus #20 (Haunted Mine); "Riovanes" title/
            // substring = corpus #19. searchLabel should win → #19.
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: "Riovanes", locationId: 8, unitIndex: 3);
            Assert.True(result.Ok);
            Assert.Equal(19, result.Rumor!.Index);
        }

        [Fact]
        public void Resolve_CityRowPath_FiresWhenSearchLabelEmpty()
        {
            // Mirror: with searchLabel empty, city+row wins.
            // Gollund row 3 → corpus #20.
            var result = RumorResolver.Resolve(_lookup,
                searchLabel: null, locationId: 8, unitIndex: 3);
            Assert.True(result.Ok);
            Assert.Equal(20, result.Rumor!.Index);
        }
    }
}
