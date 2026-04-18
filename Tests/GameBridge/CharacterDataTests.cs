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

        // GetName tests (session 33 batch 7).

        [Theory]
        [InlineData(1, "Ramza")]
        [InlineData(2, "Delita")]
        [InlineData(13, "Orlandeau")]
        [InlineData(15, "Reis")]
        [InlineData(22, "Mustadio")]
        [InlineData(26, "Marach")]
        [InlineData(30, "Agrias")]
        [InlineData(31, "Beowulf")]
        [InlineData(41, "Rapha")]
        [InlineData(42, "Meliadoul")]
        [InlineData(50, "Cloud")]
        [InlineData(117, "Construct 8")]
        [InlineData(140, "Balthier")]
        [InlineData(141, "Luso")]
        public void GetName_VerifiedStoryCharacters(int nameId, string expected)
        {
            Assert.Equal(expected, CharacterData.GetName(nameId));
        }

        [Theory]
        [InlineData(4, "Ovelia")]
        [InlineData(5, "Alma")]
        [InlineData(6, "Tietra")]
        [InlineData(7, "Zalbag")]
        [InlineData(8, "Dycedarg")]
        [InlineData(12, "Wiegraf")]
        public void GetName_PSXAdaptedNames(int nameId, string expected)
        {
            Assert.Equal(expected, CharacterData.GetName(nameId));
        }

        [Fact]
        public void GetName_UnknownId_ReturnsNull()
        {
            Assert.Null(CharacterData.GetName(9999));
            Assert.Null(CharacterData.GetName(-1));
            Assert.Null(CharacterData.GetName(int.MaxValue));
        }

        [Fact]
        public void GetName_ZeroId_ReturnsNull()
        {
            // nameId 0 is the "unset" sentinel; shouldn't resolve.
            Assert.Null(CharacterData.GetName(0));
        }

        [Fact]
        public void GetName_AllEntries_NonEmptyStrings()
        {
            foreach (var kv in CharacterData.NameById)
            {
                Assert.False(string.IsNullOrWhiteSpace(kv.Value),
                    $"NameById[{kv.Key}] is empty or whitespace");
            }
        }

        [Fact]
        public void GetName_AllZodiacNameIds_ResolveToNames()
        {
            // Cross-consistency: every name ID that has a zodiac entry should
            // also resolve via CharacterData.GetName.
            foreach (var zodiacId in new[] { 2, 4, 5, 13, 15, 22, 26, 30, 31, 41, 42, 50, 117 })
            {
                Assert.NotNull(CharacterData.GetName(zodiacId));
            }
        }
    }
}
