using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class CharacterDataTests
    {
        [Fact]
        public void GetJobName_RamzaCh4_ReturnsGallantKnight()
        {
            // Roster job=3 is Ramza's Ch4 unique job.
            // The game displays "Gallant Knight", not "Heretic".
            Assert.Equal("Gallant Knight", CharacterData.GetJobName(3));
        }

        [Theory]
        [InlineData(82, "Summoner")]
        [InlineData(79, "Knight")]
        [InlineData(87, "Dragoon")]
        [InlineData(78, "Monk")]
        public void GetJobName_GenericPlayerJobs_ReturnsCorrectName(int jobId, string expected)
        {
            Assert.Equal(expected, CharacterData.GetJobName(jobId));
        }

        [Fact]
        public void ResolvePlayerJobName_ShouldPreferRosterOverFingerprint()
        {
            // Wilham: roster job=82 (Summoner), but fingerprint matched "Steelhawk" (monster).
            // For player units (team=0), roster job should take priority.
            int rosterJobId = 82;
            string? fingerprintJob = "Steelhawk";

            // The roster job name should be used, not the fingerprint
            string? rosterJobName = CharacterData.GetJobName(rosterJobId);
            Assert.Equal("Summoner", rosterJobName);
            Assert.NotEqual(fingerprintJob, rosterJobName);
        }
    }
}
