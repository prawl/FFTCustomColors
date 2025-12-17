using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorMod.Core.ModComponents;

namespace FFTColorMod.Tests.Core.ModComponents
{
    public class ThemeCoordinatorTests : IDisposable
    {
        private readonly string _testPath;
        private readonly string _sourcePath;
        private readonly string _modPath;
        private readonly ThemeCoordinator _coordinator;

        public ThemeCoordinatorTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"ThemeCoordinatorTest_{Guid.NewGuid()}");
            _sourcePath = Path.Combine(_testPath, "source");
            _modPath = Path.Combine(_testPath, "mod");

            Directory.CreateDirectory(_sourcePath);
            Directory.CreateDirectory(_modPath);

            // Create test theme directories
            var unitsPath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitsPath);
            Directory.CreateDirectory(Path.Combine(unitsPath, "sprites_original"));
            Directory.CreateDirectory(Path.Combine(unitsPath, "sprites_lucavi"));
            Directory.CreateDirectory(Path.Combine(unitsPath, "sprites_corpse_brigade"));

            // Create a test sprite file in lucavi theme
            File.WriteAllText(Path.Combine(unitsPath, "sprites_lucavi", "battle_knight_m_spr.bin"), "test");

            _coordinator = new ThemeCoordinator(_sourcePath, _modPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        [Fact]
        public void GetCurrentColorScheme_ShouldReturnOriginalByDefault()
        {
            // Act
            var scheme = _coordinator.GetCurrentColorScheme();

            // Assert
            scheme.Should().Be("original");
        }

        [Fact]
        public void SetColorScheme_ShouldUpdateCurrentScheme()
        {
            // Act
            _coordinator.SetColorScheme("lucavi");
            var scheme = _coordinator.GetCurrentColorScheme();

            // Assert
            scheme.Should().Be("lucavi");
        }

        [Fact]
        public void CycleColorScheme_ShouldCycleToNextScheme()
        {
            // Arrange
            _coordinator.SetColorScheme("original");

            // Act
            _coordinator.CycleColorScheme();
            var scheme = _coordinator.GetCurrentColorScheme();

            // Assert
            scheme.Should().NotBe("original");
        }

        [Fact]
        public void GetAvailableSchemes_ShouldReturnAvailableThemes()
        {
            // Act
            var schemes = _coordinator.GetAvailableSchemes();

            // Assert
            schemes.Should().NotBeNull();
            schemes.Should().Contain("original");
            schemes.Should().Contain("lucavi");
            schemes.Should().Contain("corpse_brigade");
        }

        [Fact]
        public void IsJobSprite_ShouldIdentifyJobSprites()
        {
            // Assert
            _coordinator.IsJobSprite("battle_knight_m_spr.bin").Should().BeTrue();
            _coordinator.IsJobSprite("battle_archer_f_spr.bin").Should().BeTrue();
            _coordinator.IsJobSprite("battle_aguri_spr.bin").Should().BeFalse(); // Story character
            _coordinator.IsJobSprite("random_file.bin").Should().BeFalse();
        }

        [Fact]
        public void InterceptFilePath_WithJobSprite_ShouldModifyPath()
        {
            // Arrange
            _coordinator.SetColorScheme("lucavi");
            var originalPath = @"C:\Game\data\battle_knight_m_spr.bin";

            // Act
            var interceptedPath = _coordinator.InterceptFilePath(originalPath);

            // Assert
            interceptedPath.Should().NotBe(originalPath);
            interceptedPath.Should().Contain("lucavi");
        }

        [Fact]
        public void InterceptFilePath_WithNonJobSprite_ShouldReturnOriginalPath()
        {
            // Arrange
            _coordinator.SetColorScheme("lucavi");
            var originalPath = @"C:\Game\data\random_file.bin";

            // Act
            var interceptedPath = _coordinator.InterceptFilePath(originalPath);

            // Assert
            interceptedPath.Should().Be(originalPath);
        }

        [Fact]
        public void InitializeThemes_ShouldNotThrow()
        {
            // Act & Assert
            _coordinator.Invoking(c => c.InitializeThemes())
                .Should().NotThrow();
        }

        [Fact]
        public void GetThemeManager_ShouldReturnThemeManager()
        {
            // Act
            var themeManager = _coordinator.GetThemeManager();

            // Assert
            themeManager.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullSourcePath_ShouldThrowException()
        {
            // Act & Assert
            var action = () => new ThemeCoordinator(null, _modPath);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("sourcePath");
        }

        [Fact]
        public void Constructor_WithNullModPath_ShouldThrowException()
        {
            // Act & Assert
            var action = () => new ThemeCoordinator(_sourcePath, null);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("modPath");
        }
    }
}