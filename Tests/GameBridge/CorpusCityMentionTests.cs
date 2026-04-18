using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Diagnostic tests: scan the 26-entry corpus for city-name mentions.
    /// Rumors that name a specific city in their body are candidates for
    /// that city's Tavern list — e.g. corpus #20 ("Monsters have taken up
    /// residence in one of the many coal mines in Gollund...") is Gollund's
    /// row 3 (confirmed session 42).
    ///
    /// These tests don't prove a mapping — only surface hints. Future
    /// sessions visiting a named city should check whether the hinted
    /// rumor appears in that city's list. Tests characterize current
    /// mappings + flag unexplored candidates.
    /// </summary>
    public class CorpusCityMentionTests
    {
        // Canonical city names to scan for. Order matches CityRumors.CityId.
        private static readonly (int Id, string Name)[] CityNames = {
            (CityRumors.CityId.Lesalia,   "Lesalia"),
            (CityRumors.CityId.Riovanes,  "Riovanes"),
            (CityRumors.CityId.Eagrose,   "Eagrose"),
            (CityRumors.CityId.Lionel,    "Lionel"),
            (CityRumors.CityId.Limberry,  "Limberry"),
            (CityRumors.CityId.Zeltennia, "Zeltennia"),
            (CityRumors.CityId.Gariland,  "Gariland"),
            (CityRumors.CityId.Yardrow,   "Yardrow"),
            (CityRumors.CityId.Gollund,   "Gollund"),
            (CityRumors.CityId.Dorter,    "Dorter"),
            (CityRumors.CityId.Zaland,    "Zaland"),
            (CityRumors.CityId.Goug,      "Goug"),
            (CityRumors.CityId.Warjilis,  "Warjilis"),
            (CityRumors.CityId.Bervenia,  "Bervenia"),
        };

        /// <summary>
        /// Returns (corpusIndex, list-of-cities-mentioned) for every corpus
        /// entry that mentions at least one canonical city name.
        /// </summary>
        private static List<(int Index, List<string> Cities)> FindMentions()
        {
            var result = new List<(int, List<string>)>();
            for (int i = 0; i < RumorCorpus.Bodies.Length; i++)
            {
                var body = RumorCorpus.Bodies[i];
                var mentioned = new List<string>();
                foreach (var (_, name) in CityNames)
                {
                    if (body.Contains(name)) mentioned.Add(name);
                }
                if (mentioned.Count > 0) result.Add((i, mentioned));
            }
            return result;
        }

        [Fact]
        public void CorpusContainsAtLeastOneCityMention()
        {
            // Sanity — every corpus entry mentioning a city is a potential
            // city-specific rumor. Assert at least one mention exists so a
            // scan regression (e.g. decoder bug strips city names) fires here.
            var mentions = FindMentions();
            Assert.NotEmpty(mentions);
        }

        [Fact]
        public void Corpus20_MentionsGollund_MatchingKnownMapping()
        {
            // Session 42 confirmed corpus #20 is Gollund's row 3. Pin the
            // body-mention-Gollund invariant so a future decoder change
            // that breaks the mention is visible.
            Assert.Contains("Gollund", RumorCorpus.Bodies[20]);
        }

        [Fact]
        public void Corpus23_MentionsBerveniaAndDorter_WailingOrte()
        {
            // Corpus #23 body: "The city of Bervenia has donated a treasure
            // known as the Wailing Orte to the city of Dorter..." — mentions
            // BOTH cities. Session 43 confirmed it does NOT appear at either
            // Bervenia OR Dorter's Chapter-1 Tavern list (both cities show
            // the uniform 4-rumor set). Candidate for Chapter-2+ reappearance.
            var body = RumorCorpus.Bodies[23];
            Assert.Contains("Bervenia", body);
            Assert.Contains("Dorter", body);
        }

        [Fact]
        public void Corpus12_MentionsWarjilis_BaertTradingCompany()
        {
            // Corpus #12: "The Baert Trading Company, a successful trading
            // company based in the merchant city of Warjilis..."
            // Session 37 confirmed it does NOT appear at Warjilis Chapter-1
            // Tavern. Another Chapter-2+ candidate.
            Assert.Contains("Warjilis", RumorCorpus.Bodies[12]);
        }

        [Fact]
        public void Corpus15_MentionsLionel_CardinalDelacroix()
        {
            // Corpus #15: "It has been three months since the death of
            // Cardinal Delacroix, liege lord of Lionel..."
            // Lionel has story battles (not a normal tavern city in
            // Chapter 1). Candidate rumor if Lionel becomes accessible.
            Assert.Contains("Lionel", RumorCorpus.Bodies[15]);
        }

        [Fact]
        public void Corpus19_MentionsRiovanes_HorrorOfRiovanes()
        {
            // Corpus #19 is the Horror of Riovanes rumor that appears at
            // every Chapter-1 tavern row 2. It mentions Riovanes by name.
            // This is the base-case for "mentioned city" → "does appear
            // in corpus" but it's uniform, not Riovanes-specific.
            Assert.Contains("Riovanes", RumorCorpus.Bodies[19]);
        }

        [Fact]
        public void DiagnosticDump_CityMentionsSummary()
        {
            // Non-failing diagnostic: enumerate every corpus entry with any
            // city mention. Used to spot-check the scan output via
            // --logger:console;verbosity=detailed. Never fails.
            var mentions = FindMentions();
            Assert.NotEmpty(mentions);
            // If this grows significantly, some mentions may have been added
            // by a corpus regeneration — the regression test above catches
            // unintended deletions, this one caps unintended insertions.
            Assert.True(mentions.Count <= 26,
                $"Unexpectedly many corpus entries have city mentions: {mentions.Count}");
        }

        [Fact]
        public void DuplicateCityMentions_AreAllowed_NoInvariant()
        {
            // Pin that a single corpus entry CAN mention multiple cities
            // (e.g. #23 mentions both Bervenia and Dorter). Callers must
            // not assume one-to-one mapping.
            var multi = FindMentions().Where(m => m.Cities.Count > 1).ToList();
            // At least one multi-mention entry exists (corpus #23).
            Assert.NotEmpty(multi);
        }
    }
}
