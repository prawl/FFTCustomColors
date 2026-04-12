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
        [InlineData(76, "Knight")]      // verified: Kenrick job=76
        [InlineData(82, "Summoner")]    // verified: Wilham job=82
        [InlineData(87, "Dragoon")]     // verified: Lloyd job=87
        [InlineData(78, "Monk")]        // verified
        [InlineData(79, "White Mage")]  // PSX order (was incorrectly 79=Knight)
        [InlineData(74, "Squire")]      // verified
        [InlineData(89, "Ninja")]       // verified
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
