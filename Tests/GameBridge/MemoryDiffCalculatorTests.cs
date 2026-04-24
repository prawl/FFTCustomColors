using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure helper for memory-hunt diffs. Takes two byte snapshots of
    /// equal length and returns a list of offsets where they differ.
    /// Used by the `memory_diff` bridge action to snapshot a region of
    /// game memory at one state (e.g. player turn) and compare to
    /// another state (e.g. enemy turn) to find bytes that cycle with
    /// the state change.
    /// </summary>
    public class MemoryDiffCalculatorTests
    {
        [Fact]
        public void Identical_ReturnsEmpty()
        {
            var a = new byte[] { 0x01, 0x02, 0x03 };
            var b = new byte[] { 0x01, 0x02, 0x03 };
            Assert.Empty(MemoryDiffCalculator.Diff(a, b));
        }

        [Fact]
        public void SingleByteDiff_ReturnsThatOffset()
        {
            var a = new byte[] { 0x01, 0x02, 0x03 };
            var b = new byte[] { 0x01, 0xFF, 0x03 };
            var diffs = MemoryDiffCalculator.Diff(a, b);
            var d = Assert.Single(diffs);
            Assert.Equal(1, d.Offset);
            Assert.Equal(0x02, d.Before);
            Assert.Equal(0xFF, d.After);
        }

        [Fact]
        public void MultipleDiffs_ReturnsEachInOrder()
        {
            var a = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var b = new byte[] { 0xFF, 0x02, 0xEE, 0x04 };
            var diffs = MemoryDiffCalculator.Diff(a, b);
            Assert.Equal(2, diffs.Count);
            Assert.Equal(0, diffs[0].Offset);
            Assert.Equal(0x01, diffs[0].Before);
            Assert.Equal(0xFF, diffs[0].After);
            Assert.Equal(2, diffs[1].Offset);
            Assert.Equal(0x03, diffs[1].Before);
            Assert.Equal(0xEE, diffs[1].After);
        }

        [Fact]
        public void DifferentLengths_Throws()
        {
            var a = new byte[] { 0x01, 0x02 };
            var b = new byte[] { 0x01, 0x02, 0x03 };
            Assert.Throws<System.ArgumentException>(
                () => MemoryDiffCalculator.Diff(a, b));
        }

        [Fact]
        public void NullBefore_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MemoryDiffCalculator.Diff(null!, new byte[] { 0 }));
        }

        [Fact]
        public void NullAfter_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MemoryDiffCalculator.Diff(new byte[] { 0 }, null!));
        }

        [Fact]
        public void EmptyArrays_ReturnEmpty()
        {
            Assert.Empty(MemoryDiffCalculator.Diff(new byte[0], new byte[0]));
        }

        [Fact]
        public void ParseHexString_StripsSpacesAndParses()
        {
            // Real blockData field uses "01 02 03" space-separated;
            // helper must parse it back. Also accept no-space "010203".
            var spaced = MemoryDiffCalculator.ParseHex("01 02 03");
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, spaced);

            var packed = MemoryDiffCalculator.ParseHex("010203");
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, packed);

            // Mixed spacing tolerated.
            var mixed = MemoryDiffCalculator.ParseHex(" 01  02 03 ");
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, mixed);
        }

        [Fact]
        public void ParseHexString_OddLength_Throws()
        {
            Assert.Throws<System.ArgumentException>(
                () => MemoryDiffCalculator.ParseHex("012"));
        }

        [Fact]
        public void ParseHexString_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Empty(MemoryDiffCalculator.ParseHex(null));
            Assert.Empty(MemoryDiffCalculator.ParseHex(""));
            Assert.Empty(MemoryDiffCalculator.ParseHex("   "));
        }

        [Fact]
        public void FormatDiffs_Readable()
        {
            // Output format for the bridge response: one line per diff,
            // "0xNN: XX → YY" where NN is the offset (hex).
            var a = new byte[] { 0x01, 0x02, 0x03 };
            var b = new byte[] { 0x01, 0xFF, 0x03 };
            var formatted = MemoryDiffCalculator.FormatDiffs(
                MemoryDiffCalculator.Diff(a, b));
            Assert.Contains("0x01: 02 -> FF", formatted);
        }
    }
}
