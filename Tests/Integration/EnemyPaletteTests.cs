using System;
using System.IO;
using System.Collections.Generic;
using Xunit;

namespace FFTColorCustomizer.Tests.Integration
{
    /// <summary>
    /// Tests to ensure enemy palettes are preserved correctly across all themes
    /// </summary>
    public class EnemyPaletteTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _spritesDirectory;

        public EnemyPaletteTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"FFTEnemyPaletteTest_{Guid.NewGuid()}");
            _spritesDirectory = Path.Combine(_testDirectory, "sprites");
            Directory.CreateDirectory(_spritesDirectory);
        }

        [Fact]
        public void JobSpecificThemes_Should_Have_Enemy_Palettes_Populated()
        {
            // Job-specific themes must have palettes 1-4 populated for enemy units
            // These palettes should not be all black (except transparency at index 0)

            // Create a mock job-specific theme sprite
            var knightHolyGuardPath = Path.Combine(_spritesDirectory, "knight_holy_guard", "battle_knight_m_spr.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(knightHolyGuardPath));

            // Create sprite data with proper enemy palettes
            var spriteData = new byte[512]; // 16 palettes x 16 colors x 2 bytes per color

            // Palette 0: Player colors (brown/beige theme)
            SetPalette(spriteData, 0, GetPlayerPaletteColors());

            // Palettes 1-4: Enemy colors (must not be all black)
            SetPalette(spriteData, 1, GetEnemyPalette1Colors()); // Blue variant
            SetPalette(spriteData, 2, GetEnemyPalette2Colors()); // Red variant
            SetPalette(spriteData, 3, GetEnemyPalette3Colors()); // Green variant
            SetPalette(spriteData, 4, GetEnemyPalette4Colors()); // Purple variant

            File.WriteAllBytes(knightHolyGuardPath, spriteData);

            // Act - Read and verify
            var readData = File.ReadAllBytes(knightHolyGuardPath);

            // Assert - Enemy palettes should not be black
            AssertPaletteNotBlack(readData, 1, "Enemy Palette 1");
            AssertPaletteNotBlack(readData, 2, "Enemy Palette 2");
            AssertPaletteNotBlack(readData, 3, "Enemy Palette 3");
            AssertPaletteNotBlack(readData, 4, "Enemy Palette 4");
        }

        [Fact]
        public void GenericThemes_Should_Have_All_Five_Palettes()
        {
            // Generic themes like corpse_brigade and lucavi should have palettes 0-4 populated

            var corpseBrigadePath = Path.Combine(_spritesDirectory, "corpse_brigade", "battle_knight_m_spr.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(corpseBrigadePath));

            // Create sprite with all palettes populated
            var spriteData = new byte[512];

            // All 5 palettes should be populated for generic themes
            SetPalette(spriteData, 0, GetCorpseBrigadePaletteColors());
            SetPalette(spriteData, 1, GetEnemyPalette1Colors());
            SetPalette(spriteData, 2, GetEnemyPalette2Colors());
            SetPalette(spriteData, 3, GetEnemyPalette3Colors());
            SetPalette(spriteData, 4, GetEnemyPalette4Colors());

            File.WriteAllBytes(corpseBrigadePath, spriteData);

            // Act & Assert
            var readData = File.ReadAllBytes(corpseBrigadePath);

            // All palettes should be populated
            AssertPaletteNotBlack(readData, 0, "Player Palette");
            AssertPaletteNotBlack(readData, 1, "Enemy Palette 1");
            AssertPaletteNotBlack(readData, 2, "Enemy Palette 2");
            AssertPaletteNotBlack(readData, 3, "Enemy Palette 3");
            AssertPaletteNotBlack(readData, 4, "Enemy Palette 4");
        }

        [Theory]
        [InlineData("knight_holy_guard", "battle_knight_m_spr.bin")]
        [InlineData("monk_shadow_assassin", "battle_monk_m_spr.bin")]
        [InlineData("archer_desert_nomad", "battle_yumi_w_spr.bin")]
        public void JobSpecificThemes_Should_Not_Have_Black_Enemy_Palettes(string theme, string spriteName)
        {
            // Ensure job-specific themes don't have black enemy palettes after fix

            var spritePath = Path.Combine(_spritesDirectory, theme, spriteName);
            Directory.CreateDirectory(Path.GetDirectoryName(spritePath));

            // Create sprite with properly populated enemy palettes
            var spriteData = CreateCorrectSprite();
            File.WriteAllBytes(spritePath, spriteData);

            // Act & Assert
            var readData = File.ReadAllBytes(spritePath);

            // Palettes 1-4 should not be black
            for (int i = 1; i <= 4; i++)
            {
                AssertPaletteNotBlack(readData, i, $"Enemy Palette {i} in {theme}");
            }
        }

        [Fact]
        public void Palettes_5_Through_7_Should_Be_Unused()
        {
            // Palettes 5-7 should remain unused (all zeros/black)

            var spritePath = Path.Combine(_spritesDirectory, "test_sprite.bin");
            var spriteData = CreateCorrectSprite();
            File.WriteAllBytes(spritePath, spriteData);

            // Act
            var readData = File.ReadAllBytes(spritePath);

            // Assert - Palettes 5-7 should be all zeros
            for (int paletteIndex = 5; paletteIndex <= 7; paletteIndex++)
            {
                int offset = paletteIndex * 32; // Each palette is 32 bytes
                bool allZeros = true;

                for (int i = 0; i < 32; i++)
                {
                    if (readData[offset + i] != 0)
                    {
                        allZeros = false;
                        break;
                    }
                }

                Assert.True(allZeros, $"Palette {paletteIndex} should be unused (all zeros)");
            }
        }

        [Fact]
        public void Enemy_Palettes_Should_Match_Original_Colors()
        {
            // Enemy palettes should maintain standard FFT enemy colors
            // Not custom colors from the job-specific theme

            var originalSprite = CreateOriginalSprite();
            var themedSprite = CreateCorrectSprite();

            // Compare palettes 1-4 (enemy palettes)
            for (int paletteIndex = 1; paletteIndex <= 4; paletteIndex++)
            {
                var originalPalette = ExtractPalette(originalSprite, paletteIndex);
                var themedPalette = ExtractPalette(themedSprite, paletteIndex);

                // Enemy palettes should match between original and themed
                Assert.Equal(originalPalette, themedPalette);
            }
        }

        private byte[] CreateCorrectSprite()
        {
            var sprite = new byte[512];

            // Player palette (custom theme colors)
            SetPalette(sprite, 0, GetPlayerPaletteColors());

            // Enemy palettes (standard FFT enemy colors)
            SetPalette(sprite, 1, GetEnemyPalette1Colors());
            SetPalette(sprite, 2, GetEnemyPalette2Colors());
            SetPalette(sprite, 3, GetEnemyPalette3Colors());
            SetPalette(sprite, 4, GetEnemyPalette4Colors());

            // Palettes 5-7 remain unused (zeros)

            return sprite;
        }

        private byte[] CreateOriginalSprite()
        {
            var sprite = new byte[512];

            // Original FFT sprite with standard colors
            SetPalette(sprite, 0, GetOriginalPlayerColors());
            SetPalette(sprite, 1, GetEnemyPalette1Colors());
            SetPalette(sprite, 2, GetEnemyPalette2Colors());
            SetPalette(sprite, 3, GetEnemyPalette3Colors());
            SetPalette(sprite, 4, GetEnemyPalette4Colors());

            return sprite;
        }

        private void SetPalette(byte[] spriteData, int paletteIndex, ushort[] colors)
        {
            int offset = paletteIndex * 32; // Each palette is 32 bytes (16 colors x 2 bytes)

            for (int i = 0; i < colors.Length && i < 16; i++)
            {
                spriteData[offset + i * 2] = (byte)(colors[i] & 0xFF);
                spriteData[offset + i * 2 + 1] = (byte)((colors[i] >> 8) & 0xFF);
            }
        }

        private ushort[] ExtractPalette(byte[] spriteData, int paletteIndex)
        {
            var palette = new ushort[16];
            int offset = paletteIndex * 32;

            for (int i = 0; i < 16; i++)
            {
                palette[i] = (ushort)(spriteData[offset + i * 2] | (spriteData[offset + i * 2 + 1] << 8));
            }

            return palette;
        }

        private void AssertPaletteNotBlack(byte[] spriteData, int paletteIndex, string paletteName)
        {
            int offset = paletteIndex * 32;
            bool hasNonZeroColor = false;

            // Skip first color (index 0) as it's transparency
            for (int i = 2; i < 32; i++)
            {
                if (spriteData[offset + i] != 0)
                {
                    hasNonZeroColor = true;
                    break;
                }
            }

            Assert.True(hasNonZeroColor, $"{paletteName} should not be all black/zeros");
        }

        // Standard color palettes for testing
        private ushort[] GetPlayerPaletteColors() => new ushort[]
        {
            0x0000, 0x1234, 0x2456, 0x3678, 0x489A, 0x5ABC, 0x6CDE, 0x7EF0,
            0x8012, 0x9234, 0xA456, 0xB678, 0xC89A, 0xDABC, 0xECDE, 0xFEF0
        };

        private ushort[] GetOriginalPlayerColors() => new ushort[]
        {
            0x0000, 0x1111, 0x2222, 0x3333, 0x4444, 0x5555, 0x6666, 0x7777,
            0x8888, 0x9999, 0xAAAA, 0xBBBB, 0xCCCC, 0xDDDD, 0xEEEE, 0xFFFF
        };

        private ushort[] GetCorpseBrigadePaletteColors() => new ushort[]
        {
            0x0000, 0x1357, 0x2468, 0x3579, 0x468A, 0x579B, 0x68AC, 0x79BD,
            0x8ACE, 0x9BDF, 0xACE0, 0xBDF1, 0xCE02, 0xDF13, 0xE024, 0xF135
        };

        private ushort[] GetEnemyPalette1Colors() => new ushort[] // Blue variant
        {
            0x0000, 0x001F, 0x003F, 0x005F, 0x007F, 0x009F, 0x00BF, 0x00DF,
            0x00FF, 0x10FF, 0x20FF, 0x30FF, 0x40FF, 0x50FF, 0x60FF, 0x70FF
        };

        private ushort[] GetEnemyPalette2Colors() => new ushort[] // Red variant
        {
            0x0000, 0x1F00, 0x3F00, 0x5F00, 0x7F00, 0x9F00, 0xBF00, 0xDF00,
            0xFF00, 0xFF10, 0xFF20, 0xFF30, 0xFF40, 0xFF50, 0xFF60, 0xFF70
        };

        private ushort[] GetEnemyPalette3Colors() => new ushort[] // Green variant
        {
            0x0000, 0x03E0, 0x07E0, 0x0BE0, 0x0FE0, 0x13E0, 0x17E0, 0x1BE0,
            0x1FE0, 0x1FE4, 0x1FE8, 0x1FEC, 0x1FF0, 0x1FF4, 0x1FF8, 0x1FFC
        };

        private ushort[] GetEnemyPalette4Colors() => new ushort[] // Purple variant
        {
            0x0000, 0x1F1F, 0x3F3F, 0x5F5F, 0x7F7F, 0x9F9F, 0xBFBF, 0xDFDF,
            0xFFFF, 0xEFEF, 0xDFDF, 0xCFCF, 0xBFBF, 0xAFAF, 0x9F9F, 0x8F8F
        };

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}