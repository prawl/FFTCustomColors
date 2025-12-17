using System;
using System.Reflection;
using Xunit;
using FFTColorCustomizer;
using FFTColorCustomizer.Utilities;

namespace Tests
{
    public class StoryCharacterSpriteNamesTest
    {
        [Theory]
        [InlineData("battle_musu_spr.bin", true)]      // Mustadio
        [InlineData("battle_aguri_spr.bin", true)]     // Agrias
        [InlineData("battle_kanba_spr.bin", true)]     // Agrias second sprite
        [InlineData("battle_oru_spr.bin", true)]       // Orlandeau (correct name)
        [InlineData("battle_oran_spr.bin", false)]     // Orlandeau (wrong name - should NOT match)
        [InlineData("battle_dily_spr.bin", true)]      // Delita
        [InlineData("battle_dily2_spr.bin", true)]     // Delita chapter 2
        [InlineData("battle_dily3_spr.bin", true)]     // Delita chapter 3
        [InlineData("battle_hime_spr.bin", false)]     // Ovelia (removed - not recruitable)
        [InlineData("battle_aruma_spr.bin", true)]     // Alma
        [InlineData("battle_rafa_spr.bin", true)]      // Rafa
        [InlineData("battle_mara_spr.bin", true)]      // Malak
        [InlineData("battle_cloud_spr.bin", true)]     // Cloud
        [InlineData("battle_reze_spr.bin", true)]      // Reis human
        [InlineData("battle_reze_d_spr.bin", true)]    // Reis dragon
        [InlineData("battle_knight_m_spr.bin", true)]  // Generic knight (baseline)
        [InlineData("battle_random_spr.bin", false)]   // Non-existent sprite
        public void IsJobSprite_RecognizesCorrectStoryCharacterNames(string fileName, bool expectedResult)
        {
            // Arrange
            var mod = new Mod(new ModContext(), null, new NullHotkeyHandler());
            var method = typeof(Mod).GetMethod("IsJobSprite",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (bool)method.Invoke(mod, new object[] { fileName });

            // Assert
            if (expectedResult)
            {
                Assert.True(result, $"IsJobSprite should recognize {fileName}");
            }
            else
            {
                Assert.False(result, $"IsJobSprite should not recognize {fileName}");
            }
        }

        [Fact]
        public void Orlandeau_Uses_Oru_Not_Oran()
        {
            // This test specifically verifies that we're using the correct Orlandeau sprite name
            // The game uses battle_oru_spr.bin, NOT battle_oran_spr.bin

            var mod = new Mod(new ModContext(), null, new NullHotkeyHandler());
            var method = typeof(Mod).GetMethod("IsJobSprite",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // The correct sprite name should be recognized
            var correctName = (bool)method.Invoke(mod, new object[] { "battle_oru_spr.bin" });
            Assert.True(correctName, "battle_oru_spr.bin should be recognized (correct Orlandeau sprite)");

            // The incorrect sprite name should NOT be recognized
            var incorrectName = (bool)method.Invoke(mod, new object[] { "battle_oran_spr.bin" });
            Assert.False(incorrectName, "battle_oran_spr.bin should NOT be recognized (wrong Orlandeau sprite)");
        }

        [Fact]
        public void Agrias_Has_Two_Sprites()
        {
            // Agrias has both battle_aguri_spr.bin and battle_kanba_spr.bin

            var mod = new Mod(new ModContext(), null, new NullHotkeyHandler());
            var method = typeof(Mod).GetMethod("IsJobSprite",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var aguri = (bool)method.Invoke(mod, new object[] { "battle_aguri_spr.bin" });
            var kanba = (bool)method.Invoke(mod, new object[] { "battle_kanba_spr.bin" });

            Assert.True(aguri, "battle_aguri_spr.bin should be recognized (Agrias main sprite)");
            Assert.True(kanba, "battle_kanba_spr.bin should be recognized (Agrias second sprite)");
        }

    }
}
