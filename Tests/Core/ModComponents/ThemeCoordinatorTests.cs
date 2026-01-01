using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Core.ModComponents;

namespace FFTColorCustomizer.Tests.Core.ModComponents
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

            // Create test theme directories in MOD path (not source path) since we fixed the bug
            var unitsPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
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
            // Assert - both generic jobs and story characters should be recognized
            _coordinator.IsJobSprite("battle_knight_m_spr.bin").Should().BeTrue();
            _coordinator.IsJobSprite("battle_archer_f_spr.bin").Should().BeTrue();
            _coordinator.IsJobSprite("battle_aguri_spr.bin").Should().BeTrue(); // Story character (Agrias)
            _coordinator.IsJobSprite("battle_oru_spr.bin").Should().BeTrue(); // Story character (Orlandeau)
            _coordinator.IsJobSprite("random_file.bin").Should().BeFalse();
            _coordinator.IsJobSprite("not_a_sprite.txt").Should().BeFalse();
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

        [Fact]
        public void IsUserTheme_ShouldReturnTrue_ForSavedUserTheme()
        {
            // Arrange - Create a user theme
            var userThemesPath = Path.Combine(_modPath, "UserThemes", "Knight_Male", "Ocean Blue");
            Directory.CreateDirectory(userThemesPath);
            File.WriteAllBytes(Path.Combine(userThemesPath, "palette.bin"), new byte[512]);

            // Also create the registry
            var registryPath = Path.Combine(_modPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Knight_Male\":[\"Ocean Blue\"]}");

            // Act
            var isUserTheme = _coordinator.IsUserTheme("Knight_Male", "Ocean Blue");

            // Assert
            isUserTheme.Should().BeTrue();
        }

        [Fact]
        public void IsUserTheme_ShouldReturnFalse_ForBuiltInTheme()
        {
            // Act
            var isUserTheme = _coordinator.IsUserTheme("Knight_Male", "lucavi");

            // Assert
            isUserTheme.Should().BeFalse();
        }

        [Fact]
        public void GetUserThemePalettePath_ShouldReturnPath_ForExistingUserTheme()
        {
            // Arrange - Create a user theme
            var userThemesPath = Path.Combine(_modPath, "UserThemes", "Knight_Male", "Ocean Blue");
            Directory.CreateDirectory(userThemesPath);
            File.WriteAllBytes(Path.Combine(userThemesPath, "palette.bin"), new byte[512]);

            // Also create the registry
            var registryPath = Path.Combine(_modPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Knight_Male\":[\"Ocean Blue\"]}");

            // Act
            var palettePath = _coordinator.GetUserThemePalettePath("Knight_Male", "Ocean Blue");

            // Assert
            palettePath.Should().NotBeNull();
            palettePath.Should().EndWith("palette.bin");
            File.Exists(palettePath).Should().BeTrue();
        }

        [Fact]
        public void GetUserThemesForJob_ShouldReturnUserThemes_ForSpecificJob()
        {
            // Arrange - Create user themes for different jobs
            var knightThemesPath = Path.Combine(_modPath, "UserThemes", "Knight_Male", "Ocean Blue");
            Directory.CreateDirectory(knightThemesPath);
            File.WriteAllBytes(Path.Combine(knightThemesPath, "palette.bin"), new byte[512]);

            var archerThemesPath = Path.Combine(_modPath, "UserThemes", "Archer_Female", "Forest Green");
            Directory.CreateDirectory(archerThemesPath);
            File.WriteAllBytes(Path.Combine(archerThemesPath, "palette.bin"), new byte[512]);

            // Create the registry
            var registryPath = Path.Combine(_modPath, "UserThemes.json");
            File.WriteAllText(registryPath, "{\"Knight_Male\":[\"Ocean Blue\"],\"Archer_Female\":[\"Forest Green\"]}");

            // Act
            var knightThemes = _coordinator.GetUserThemesForJob("Knight_Male");

            // Assert
            knightThemes.Should().HaveCount(1);
            knightThemes.Should().Contain("Ocean Blue");
            knightThemes.Should().NotContain("Forest Green");
        }
    }
}
