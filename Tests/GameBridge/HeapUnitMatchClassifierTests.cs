using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the confidence scoring for heap unit-struct candidates. When a
    /// unit's (HP, MaxHP) pattern is common (e.g. HP=4, MaxHP=452) the
    /// heap search returns many false positives, each of which may have a
    /// non-zero fingerprint that decodes to a plausible-but-wrong class.
    /// The level byte (at struct+0x09, i.e. at byte index 0x09 within a
    /// read that started at struct-base) provides additional
    /// disambiguation: real unit structs store the unit's actual level,
    /// false positives land on arbitrary bytes.
    /// </summary>
    public class HeapUnitMatchClassifierTests
    {
        [Fact]
        public void LevelMatch_Prefers_OverLevelMismatch()
        {
            int scoreMatch = HeapUnitMatchClassifier.Score(candidateLevel: 15, expectedLevel: 15);
            int scoreMismatch = HeapUnitMatchClassifier.Score(candidateLevel: 32, expectedLevel: 15);
            Assert.True(scoreMatch > scoreMismatch);
        }

        [Fact]
        public void UnknownExpectedLevel_AllCandidatesEqual()
        {
            // expectedLevel==0 is the "we don't know" sentinel (condensed
            // struct occasionally reads 0 pre-scan). Don't penalize either
            // candidate; fall back to first-match behavior.
            int scoreA = HeapUnitMatchClassifier.Score(candidateLevel: 10, expectedLevel: 0);
            int scoreB = HeapUnitMatchClassifier.Score(candidateLevel: 50, expectedLevel: 0);
            Assert.Equal(scoreA, scoreB);
        }

        [Fact]
        public void OutOfRangeCandidateLevel_ScoresLow()
        {
            // Valid unit levels are 1..99 in FFT. Candidate level of 0, 255
            // or 100+ indicates a false-positive heap slot.
            int scoreValid = HeapUnitMatchClassifier.Score(candidateLevel: 50, expectedLevel: 50);
            int scoreZero = HeapUnitMatchClassifier.Score(candidateLevel: 0, expectedLevel: 50);
            int score255 = HeapUnitMatchClassifier.Score(candidateLevel: 255, expectedLevel: 50);
            int score200 = HeapUnitMatchClassifier.Score(candidateLevel: 200, expectedLevel: 50);

            Assert.True(scoreValid > scoreZero);
            Assert.True(scoreValid > score255);
            Assert.True(scoreValid > score200);
        }

        [Fact]
        public void InRangeMismatch_ScoresBetterThanOutOfRange()
        {
            // A candidate with a plausible level (even if wrong) is
            // slightly more trustworthy than one with an out-of-range
            // level — a real dead-unit slot can keep its level byte.
            int scoreInRangeMismatch = HeapUnitMatchClassifier.Score(candidateLevel: 20, expectedLevel: 50);
            int scoreOutOfRange = HeapUnitMatchClassifier.Score(candidateLevel: 0, expectedLevel: 50);
            Assert.True(scoreInRangeMismatch > scoreOutOfRange);
        }

        [Fact]
        public void ExactMatch_ProducesHighestScore()
        {
            int scoreMatch = HeapUnitMatchClassifier.Score(candidateLevel: 50, expectedLevel: 50);
            int scoreOff1 = HeapUnitMatchClassifier.Score(candidateLevel: 51, expectedLevel: 50);
            int scoreOff10 = HeapUnitMatchClassifier.Score(candidateLevel: 60, expectedLevel: 50);

            Assert.True(scoreMatch >= scoreOff1);
            Assert.True(scoreMatch > scoreOff10);
        }
    }
}
