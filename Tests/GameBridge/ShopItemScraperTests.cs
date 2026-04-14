using System.Reflection;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Unit tests for the FString extraction logic. We can't easily test the
    /// full heap walk without a live process, but we CAN test the byte-level
    /// extractor with known FString byte patterns captured from live memory.
    /// </summary>
    public class ShopItemScraperTests
    {
        [Fact]
        public void ExtractFStrings_Weapons_IsFound()
        {
            // Byte layout captured from live memory 2026-04-14 at Outfitter_Buy
            // for the "Weapons" column header.
            byte[] region = new byte[]
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // padding
                0x80, 0x92, 0x03, 0xCE, 0xFD, 0x7F, 0x00, 0x00, // vtable ptr
                0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // length = 14 bytes
                0x57, 0x00, 0x65, 0x00, 0x61, 0x00, 0x70, 0x00,
                0x6F, 0x00, 0x6E, 0x00, 0x73, 0x00, 0x00, 0x00, // "Weapons\0"
            };

            var found = CallExtract(region, baseAddr: 0x10000000);

            Assert.Contains("Weapons", found.Keys);
        }

        [Fact]
        public void ExtractFStrings_OakStaff_IsFound()
        {
            // Synthesised: prepended padding, then FString for "Oak Staff".
            byte[] region = new byte[]
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x80, 0x92, 0x03, 0xCE, 0xFD, 0x7F, 0x00, 0x00,
                0x12, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // length = 18 bytes
                0x4F, 0x00, 0x61, 0x00, 0x6B, 0x00, 0x20, 0x00,
                0x53, 0x00, 0x74, 0x00, 0x61, 0x00, 0x66, 0x00,
                0x66, 0x00, 0x00, 0x00,                         // "Oak Staff\0"
            };

            var found = CallExtract(region, baseAddr: 0x20000000);

            Assert.Contains("Oak Staff", found.Keys);
        }

        [Fact]
        public void ExtractFStrings_RejectsBinaryData()
        {
            // Random bytes — no valid FString should be extracted.
            byte[] region = new byte[128];
            for (int i = 0; i < region.Length; i++) region[i] = (byte)(i * 17 % 256);

            var found = CallExtract(region, baseAddr: 0x30000000);

            Assert.Empty(found);
        }

        // Reflect into the private static extractor.
        private static System.Collections.Generic.Dictionary<string, long> CallExtract(byte[] region, long baseAddr)
        {
            var method = typeof(ShopItemScraper).GetMethod(
                "ExtractFStringsFromRegion",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var dict = new System.Collections.Generic.Dictionary<string, long>(System.StringComparer.Ordinal);
            method!.Invoke(null, new object[] { region, baseAddr, dict });
            return dict;
        }
    }
}
