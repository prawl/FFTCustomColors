using System;
using System.Reflection;
using Xunit;
using FFTColorMod;

namespace Tests
{
    public class SpriteNameMappingTest
    {
        [Fact]
        public void VerifyCorrectStoryCharacterSpriteNames()
        {
            // This test documents the CORRECT sprite names discovered from the game files
            // These are the actual filenames the game uses (not what we might expect)

            // Correct sprite names from game directory
            var correctSpriteNames = new[]
            {
                "battle_musu_spr.bin",    // Mustadio (correct)
                "battle_aguri_spr.bin",   // Agrias (correct)
                "battle_kanba_spr.bin",   // Agrias second sprite (correct)
                "battle_oru_spr.bin",     // Orlandeau (NOT oran!)
                "battle_dily_spr.bin",    // Delita chapter 1
                "battle_dily2_spr.bin",   // Delita chapter 2
                "battle_dily3_spr.bin",   // Delita chapter 3
                "battle_aruma_spr.bin",   // Alma
                "battle_rafa_spr.bin",    // Rafa
                "battle_mara_spr.bin",    // Malak
                "battle_cloud_spr.bin",   // Cloud
                "battle_reze_spr.bin",    // Reis human
                "battle_reze_d_spr.bin"   // Reis dragon
            };

            var mod = new Mod(new ModContext(), null);
            var method = typeof(Mod).GetMethod("IsJobSprite",
                BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var spriteName in correctSpriteNames)
            {
                var result = (bool)method.Invoke(mod, new object[] { spriteName });
                Assert.True(result, $"IsJobSprite should recognize {spriteName}");
            }
        }

        [Fact]
        public void DocumentSpriteNameDiscrepancies()
        {
            // This test documents the discrepancies between expected and actual sprite names

            var discrepancies = new[]
            {
                new { Expected = "battle_oran_spr.bin", Actual = "battle_oru_spr.bin", Character = "Orlandeau" }
                // Add more discrepancies as we discover them
            };

            // Just document these - no assertions needed
            foreach (var d in discrepancies)
            {
                Console.WriteLine($"{d.Character}: Expected '{d.Expected}' but game uses '{d.Actual}'");
            }

            Assert.True(true, "Documentation test");
        }

        [Fact]
        public void StoryCharacters_ShouldBeRecognized_WhenUsingCorrectNames()
        {
            // Test that our IsJobSprite method recognizes all story characters with correct names

            var mod = new Mod(new ModContext(), null);
            var method = typeof(Mod).GetMethod("IsJobSprite",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Test Orlandeau with CORRECT name
            var orlandeauCorrect = (bool)method.Invoke(mod, new object[] { "battle_oru_spr.bin" });
            Assert.True(orlandeauCorrect, "Should recognize Orlandeau with correct name (oru)");

            // Test Agrias's second sprite
            var kanba = (bool)method.Invoke(mod, new object[] { "battle_kanba_spr.bin" });
            Assert.True(kanba, "Should recognize Agrias's second sprite (kanba)");

            // Test Mustadio
            var mustadio = (bool)method.Invoke(mod, new object[] { "battle_musu_spr.bin" });
            Assert.True(mustadio, "Should recognize Mustadio");
        }
    }
}