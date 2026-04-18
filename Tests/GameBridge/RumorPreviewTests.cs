using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for RumorLookup.FirstSentence — a diagnostic helper that returns
    /// the leading sentence of a rumor body. Supports the title-map expansion
    /// workflow: when a new city's Tavern is visited, Claude can dump the corpus
    /// with `list_rumors` and cross-reference UI titles against the short preview
    /// instead of scanning 200-400 character body paragraphs.
    ///
    /// The extraction rule: everything up to and including the first sentence
    /// terminator (. ! ?) at a word boundary, OR the first 120 characters,
    /// whichever comes first.
    /// </summary>
    public class RumorPreviewTests
    {
        [Fact]
        public void FirstSentence_SimpleSentence_ReturnsUpToPeriod()
        {
            string body = "The king is dead. Long live the king.";
            Assert.Equal("The king is dead.", RumorLookup.FirstSentence(body));
        }

        [Fact]
        public void FirstSentence_Exclamation_RecognizedAsTerminator()
        {
            Assert.Equal("Fire!", RumorLookup.FirstSentence("Fire! The city burns."));
        }

        [Fact]
        public void FirstSentence_Question_RecognizedAsTerminator()
        {
            Assert.Equal("Who goes there?", RumorLookup.FirstSentence("Who goes there? I am the guard."));
        }

        [Fact]
        public void FirstSentence_NoTerminator_ReturnsWholeBody_IfShort()
        {
            Assert.Equal("No terminator here", RumorLookup.FirstSentence("No terminator here"));
        }

        [Fact]
        public void FirstSentence_LongSentence_NoEarlyTerminator_TruncatedWithEllipsis()
        {
            // A body with no terminator in the first 120 chars should truncate
            // to 120 chars + "…" so the caller still gets a usable preview.
            string body = new string('a', 150);
            string preview = RumorLookup.FirstSentence(body);
            Assert.Equal(121, preview.Length); // 120 chars + 1 ellipsis character
            Assert.EndsWith("…", preview);
        }

        [Fact]
        public void FirstSentence_TerminatorBeyond120Chars_Truncated()
        {
            // Terminator exists but past the 120-char cap. Must truncate rather
            // than return a 200-char "sentence."
            string prefix = new string('x', 130);
            string body = prefix + ". Next sentence.";
            string preview = RumorLookup.FirstSentence(body);
            Assert.EndsWith("…", preview);
            Assert.True(preview.Length <= 121, $"Preview too long: {preview.Length}");
        }

        [Fact]
        public void FirstSentence_Null_ReturnsEmpty()
        {
            Assert.Equal("", RumorLookup.FirstSentence(null));
        }

        [Fact]
        public void FirstSentence_Empty_ReturnsEmpty()
        {
            Assert.Equal("", RumorLookup.FirstSentence(""));
        }

        [Fact]
        public void FirstSentence_EveryCorpusEntry_ProducesNonEmptyPreview()
        {
            // The 26 hardcoded rumors should all yield a usable preview — the
            // diagnostic path breaks if any returns empty. Sweep the whole corpus.
            for (int i = 0; i < RumorCorpus.Bodies.Length; i++)
            {
                string preview = RumorLookup.FirstSentence(RumorCorpus.Bodies[i]);
                Assert.False(string.IsNullOrWhiteSpace(preview),
                    $"Corpus entry #{i} produced an empty preview.");
                Assert.True(preview.Length <= 121,
                    $"Corpus entry #{i} preview exceeded 121 chars: {preview.Length}");
            }
        }

        [Fact]
        public void FirstSentence_ZodiacBraves_MatchesOpeningLine()
        {
            // Spot-check a known entry: corpus #10 is the Zodiac Braves legend
            // opening "Long ago, before Ivalice was united as it is today, the
            // land was divided into seven kingdoms: ..."
            string preview = RumorLookup.FirstSentence(RumorCorpus.Bodies[10]);
            Assert.StartsWith("Long ago", preview);
        }

        [Fact]
        public void GetPreview_ValidIndex_ReturnsFirstSentenceOfCorpus()
        {
            var lookup = new RumorLookup();
            string preview = lookup.GetPreview(10);
            Assert.StartsWith("Long ago", preview);
        }

        [Fact]
        public void GetPreview_NegativeIndex_ReturnsEmpty()
        {
            var lookup = new RumorLookup();
            Assert.Equal("", lookup.GetPreview(-1));
        }

        [Fact]
        public void GetPreview_IndexPastCorpus_ReturnsEmpty()
        {
            var lookup = new RumorLookup();
            Assert.Equal("", lookup.GetPreview(lookup.Count));
            Assert.Equal("", lookup.GetPreview(9999));
        }

        [Fact]
        public void GetPreview_RoundTripsWithGetByIndex()
        {
            // For every valid index, GetPreview should equal FirstSentence(GetByIndex(i).Body).
            var lookup = new RumorLookup();
            for (int i = 0; i < lookup.Count; i++)
            {
                string preview = lookup.GetPreview(i);
                string expected = RumorLookup.FirstSentence(lookup.GetByIndex(i)!.Body);
                Assert.Equal(expected, preview);
            }
        }

        // Session 36: ordering + count contract for RumorLookup.All. Callers
        // (list_rumors, reverse lookups, the title-map expansion workflow)
        // assume All[i].Index == i and Count == corpus length. Pin both.

        [Fact]
        public void All_Count_Matches_RumorCorpusBodies()
        {
            var lookup = new RumorLookup();
            Assert.Equal(RumorCorpus.Bodies.Length, lookup.Count);
            Assert.Equal(RumorCorpus.Bodies.Length, lookup.All.Count);
        }

        [Fact]
        public void All_IndexProperty_MatchesPosition()
        {
            // Contract: the Index property of each rumor must equal its
            // position in the All collection. Callers use this invariant
            // to correlate titles with corpus positions.
            var lookup = new RumorLookup();
            for (int i = 0; i < lookup.Count; i++)
            {
                Assert.Equal(i, lookup.All[i].Index);
            }
        }

        [Fact]
        public void All_BodyAtIndex_MatchesRumorCorpus()
        {
            // All[i].Body should equal RumorCorpus.Bodies[i]. Pins the
            // construction path in the RumorLookup constructor.
            var lookup = new RumorLookup();
            for (int i = 0; i < lookup.Count; i++)
            {
                Assert.Equal(RumorCorpus.Bodies[i], lookup.All[i].Body);
            }
        }

        [Fact]
        public void Count_IsExactly26_CurrentCorpusSize()
        {
            // Absolute pin on the current corpus size. If world_wldmes.bin
            // decoder changes and the corpus grows/shrinks, this test fails
            // loudly so the session-33 emitter workflow is re-run.
            var lookup = new RumorLookup();
            Assert.Equal(26, lookup.Count);
        }
    }
}
