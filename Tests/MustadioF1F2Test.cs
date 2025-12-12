using System;
using System.IO;
using Xunit;
using FFTColorMod;
using FFTColorMod.Configuration;

namespace Tests
{
    public class MustadioF1F2Test
    {
        // These tests verify the LOGIC of F1/F2 overriding config-based colors
        // They don't test actual file interception (which requires files to exist)

        [Fact]
        public void GlobalScheme_Should_Override_ConfigScheme()
        {
            // TLDR: When F1/F2 sets a global scheme, it should take priority over config

            // Arrange
            var mod = new Mod(new ModContext(), null);

            // Verify that setting a global scheme changes the current scheme
            mod.SetColorScheme("corpse_brigade");
            Assert.Equal("corpse_brigade", mod.GetCurrentColorScheme());

            // Changing to another scheme
            mod.SetColorScheme("lucavi");
            Assert.Equal("lucavi", mod.GetCurrentColorScheme());

            // Original scheme means no override
            mod.SetColorScheme("original");
            Assert.Equal("original", mod.GetCurrentColorScheme());
        }

        [Fact]
        public void Story_Characters_Are_Recognized_As_JobSprites()
        {
            // TLDR: Verify that story character sprites are recognized with correct names

            var mod = new Mod(new ModContext(), null);
            var method = typeof(Mod).GetMethod("IsJobSprite",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Test story character recognition
            Assert.True((bool)method.Invoke(mod, new[] { "battle_musu_spr.bin" }), "Mustadio");
            Assert.True((bool)method.Invoke(mod, new[] { "battle_aguri_spr.bin" }), "Agrias");
            Assert.True((bool)method.Invoke(mod, new[] { "battle_kanba_spr.bin" }), "Agrias alt");
            Assert.True((bool)method.Invoke(mod, new[] { "battle_oru_spr.bin" }), "Orlandeau (correct)");

            // Wrong Orlandeau name should NOT be recognized
            Assert.False((bool)method.Invoke(mod, new[] { "battle_oran_spr.bin" }), "Orlandeau (wrong)");
        }

    }
}