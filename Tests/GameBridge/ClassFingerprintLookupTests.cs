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
    }
}
