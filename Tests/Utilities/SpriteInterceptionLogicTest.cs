using System;
using System.IO;
using System.Reflection;
using Xunit;
using FFTColorMod;
using FFTColorMod.Utilities;

namespace Tests
{
    public class SpriteInterceptionLogicTest
    {
        [Fact]
        public void SpriteFileManager_WouldRedirect_IfFileExisted()
        {
            // This test verifies the LOGIC of sprite interception without requiring actual files
            // It tests what WOULD happen if the files existed

            var tempDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());

            try
            {
                // Create the expected directory structure
                var unitDir = Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                var corpsBrigadeDir = Path.Combine(unitDir, "sprites_corpse_brigade");
                Directory.CreateDirectory(corpsBrigadeDir);

                // Create dummy sprite files
                File.WriteAllBytes(Path.Combine(corpsBrigadeDir, "battle_mina_m_spr.bin"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(corpsBrigadeDir, "battle_musu_spr.bin"), new byte[] { 2 });
                File.WriteAllBytes(Path.Combine(corpsBrigadeDir, "battle_oru_spr.bin"), new byte[] { 3 });

                var manager = new SpriteFileManager(tempDir);

                // Test generic unit sprite
                var squirePath = manager.InterceptFilePath("data\\sprites\\battle_mina_m_spr.bin", "corpse_brigade");
                Assert.Contains("sprites_corpse_brigade", squirePath);
                Assert.Contains("battle_mina_m_spr.bin", squirePath);

                // Test story character sprites
                var mustadioPath = manager.InterceptFilePath("data\\sprites\\battle_musu_spr.bin", "corpse_brigade");
                Assert.Contains("sprites_corpse_brigade", mustadioPath);
                Assert.Contains("battle_musu_spr.bin", mustadioPath);

                var orlandeauPath = manager.InterceptFilePath("data\\sprites\\battle_oru_spr.bin", "corpse_brigade");
                Assert.Contains("sprites_corpse_brigade", orlandeauPath);
                Assert.Contains("battle_oru_spr.bin", orlandeauPath);

                // Test that original scheme doesn't redirect
                var originalPath = manager.InterceptFilePath("data\\sprites\\battle_mina_m_spr.bin", "original");
                Assert.Equal("data\\sprites\\battle_mina_m_spr.bin", originalPath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }


        [Theory]
        [InlineData("battle_oran_spr.bin", false)]  // Wrong Orlandeau name - should NOT work
        [InlineData("battle_oru_spr.bin", true)]    // Correct Orlandeau name - should work
        public void Orlandeau_OnlyWorksWithCorrectName(string fileName, bool shouldBeRecognized)
        {
            var mod = new Mod(new ModContext(), null, new NullHotkeyHandler());
            var method = typeof(Mod).GetMethod("IsJobSprite",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var result = (bool)method.Invoke(mod, new object[] { fileName });

            if (shouldBeRecognized)
            {
                Assert.True(result, $"{fileName} should be recognized");
            }
            else
            {
                Assert.False(result, $"{fileName} should NOT be recognized");
            }
        }
    }
}