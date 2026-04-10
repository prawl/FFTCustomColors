using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ClassFingerprintLookupTests
    {
        [Fact]
        public void Lookup_ReturnsSkeletalFiend_ForKnownFingerprint()
        {
            // Empirically verified in Siedge Weald random encounter (2026-04-10):
            // Two Skeletal Fiend enemies shared these exact 11 bytes at struct +0x69.
            var fingerprint = new byte[] { 0x0F, 0x05, 0x65, 0x1E, 0x03, 0x55, 0x66, 0x27, 0x7D, 0x07, 0x58 };

            var jobName = ClassFingerprintLookup.GetJobName(fingerprint);

            Assert.Equal("Skeletal Fiend", jobName);
        }

        [Fact]
        public void Lookup_IgnoresByteZero_WhenMatchingClass()
        {
            // A player Knight and an enemy Knight had fingerprints differing only
            // at byte 0 (02-0A-... vs 03-0A-...). Byte 0 is per-unit/team variation,
            // so the lookup must ignore it to resolve both to "Knight".
            var playerKnight = new byte[] { 0x02, 0x0A, 0x78, 0x0F, 0x50, 0x64, 0x64, 0x28, 0x78, 0x32, 0x50 };
            var enemyKnight  = new byte[] { 0x03, 0x0A, 0x78, 0x0F, 0x50, 0x64, 0x64, 0x28, 0x78, 0x32, 0x50 };

            Assert.Equal("Knight", ClassFingerprintLookup.GetJobName(playerKnight));
            Assert.Equal("Knight", ClassFingerprintLookup.GetJobName(enemyKnight));
        }
    }
}
