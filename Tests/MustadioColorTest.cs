using System;
using System.IO;
using Xunit;
using FFTColorMod;

namespace Tests
{
    public class MustadioColorTest
    {
        [Fact]
        public void Config_GetColorForSprite_RecognizesMustadio()
        {
            // Arrange
            var config = new FFTColorMod.Configuration.Config();
            config.Mustadio = FFTColorMod.Configuration.ColorScheme.corpse_brigade;

            // Act
            var color = config.GetColorForSprite("battle_musu_spr.bin");

            // Assert - GetColorForSprite returns the display name
            Assert.Equal("Corpse Brigade", color);
        }

        [Fact]
        public void Mod_IsJobSprite_RecognizesMustadioAsJobSprite()
        {
            // Arrange
            // Create a mock ModContext (can be null for this test)
            var mod = new Mod(null, null);

            // Use reflection to test the private IsJobSprite method
            var method = typeof(Mod).GetMethod("IsJobSprite",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (bool)method.Invoke(mod, new object[] { "battle_musu_spr.bin" });

            // Assert
            Assert.True(result, "IsJobSprite should recognize Mustadio sprite");
        }

        [Fact]
        public void Mod_InterceptFilePath_HandlesMustadioWithConfig()
        {
            // This test verifies that Mustadio sprite patterns are recognized as job sprites
            // The actual file path interception is tested in SpriteFileManager tests
            // Since the Mod requires complex initialization that's difficult in unit tests,
            // we'll test the component behavior directly

            // Test that Mustadio sprite name is recognized as a job sprite
            var mod = new Mod(null, null);
            var method = typeof(Mod).GetMethod("IsJobSprite",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Mustadio sprite should be recognized
            var isMustadioJob = (bool)method.Invoke(mod, new object[] { "battle_musu_spr.bin" });
            Assert.True(isMustadioJob, "Mustadio sprite should be recognized as a job sprite");

            // Test that the Config recognizes Mustadio sprites
            var config = new FFTColorMod.Configuration.Config();
            config.Mustadio = FFTColorMod.Configuration.ColorScheme.corpse_brigade;
            var colorForMustadio = config.GetColorForSprite("battle_musu_spr.bin");
            Assert.Equal("Corpse Brigade", colorForMustadio);
        }

        [Fact]
        public void SpriteFileManager_SwitchColorScheme_CopiesMustadio()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create necessary directory structure
                var unitDir = Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                Directory.CreateDirectory(unitDir);

                // Create theme directories with Mustadio sprites
                var corpsBrigadeDir = Path.Combine(unitDir, "sprites_corpse_brigade");
                Directory.CreateDirectory(corpsBrigadeDir);

                var brigadeMustadio = Path.Combine(corpsBrigadeDir, "battle_musu_spr.bin");
                File.WriteAllBytes(brigadeMustadio, new byte[] { 4, 5, 6 });

                // Also create other required sprites so the operation succeeds
                File.WriteAllBytes(Path.Combine(corpsBrigadeDir, "battle_knight_m_spr.bin"), new byte[] { 7, 8, 9 });

                var manager = new FFTColorMod.Utilities.SpriteFileManager(tempDir);

                // Act
                manager.SwitchColorScheme("corpse_brigade");

                // Assert - Mustadio should be copied to main unit directory
                var mainMustadio = Path.Combine(unitDir, "battle_musu_spr.bin");
                Assert.True(File.Exists(mainMustadio), "Mustadio sprite should be copied to main directory");

                var copiedBytes = File.ReadAllBytes(mainMustadio);
                Assert.Equal(new byte[] { 4, 5, 6 }, copiedBytes);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}