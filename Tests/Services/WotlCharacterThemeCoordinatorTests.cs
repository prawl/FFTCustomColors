using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Core.ModComponents;
using System;
using System.IO;

namespace FFTColorCustomizer.Tests.Services
{
    /// <summary>
    /// Tests for ThemeCoordinator's handling of WotL character sprites (Balthier, Luso).
    /// </summary>
    public class WotlCharacterThemeCoordinatorTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly ThemeCoordinator _coordinator;

        public WotlCharacterThemeCoordinatorTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), $"WotlThemeCoordTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testModPath);

            var unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);

            _coordinator = new ThemeCoordinator(_testModPath, _testModPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }

        [Theory]
        [InlineData("spr_dst_bchr_bulechange_m_spr.bin")]
        [InlineData("spr_dst_bchr_kaito_m_spr.bin")]
        public void IsJobSprite_Should_Recognize_WotL_Character_Sprites(string fileName)
        {
            _coordinator.IsJobSprite(fileName).Should().BeTrue();
        }

        [Theory]
        [InlineData("spr_dst_bchr_ankoku_m_spr.bin")]
        [InlineData("spr_dst_bchr_tama_w_spr.bin")]
        public void IsJobSprite_Should_Recognize_WotL_Job_Sprites(string fileName)
        {
            _coordinator.IsJobSprite(fileName).Should().BeTrue();
        }

        [Theory]
        [InlineData("battle_knight_m_spr.bin")]
        [InlineData("battle_monk_w_spr.bin")]
        public void IsJobSprite_Should_Recognize_Regular_Battle_Sprites(string fileName)
        {
            _coordinator.IsJobSprite(fileName).Should().BeTrue();
        }

        [Theory]
        [InlineData("some_random_file.bin")]
        [InlineData("texture_file.tex")]
        [InlineData("")]
        [InlineData(null)]
        public void IsJobSprite_Should_Reject_Non_Sprite_Files(string fileName)
        {
            _coordinator.IsJobSprite(fileName).Should().BeFalse();
        }
    }
}
