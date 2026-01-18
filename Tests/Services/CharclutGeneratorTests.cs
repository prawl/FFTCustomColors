using Xunit;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Configuration;

namespace Tests.Services
{
    public class CharclutGeneratorTests
    {
        [Fact]
        public void Constructor_ShouldAcceptDependencies()
        {
            // Arrange
            var charclutService = new RamzaCharclutService(@"C:\fake\tools");
            var paletteTransformer = new RamzaPaletteTransformer();

            // Act
            var generator = new CharclutGenerator(charclutService, paletteTransformer);

            // Assert
            Assert.NotNull(generator);
        }

        [Fact]
        public void SetBaseNxdPath_ShouldStorePathForLaterUse()
        {
            // Arrange
            var charclutService = new RamzaCharclutService(@"C:\fake\tools");
            var paletteTransformer = new RamzaPaletteTransformer();
            var generator = new CharclutGenerator(charclutService, paletteTransformer);

            // Act
            generator.SetBaseNxdPath(@"C:\game\data\charclut.nxd");

            // Assert
            Assert.Equal(@"C:\game\data\charclut.nxd", generator.BaseNxdPath);
        }

        [Fact]
        public void IsConfigured_ShouldReturnFalseWhenBaseNxdPathNotSet()
        {
            // Arrange
            var charclutService = new RamzaCharclutService(@"C:\fake\tools");
            var paletteTransformer = new RamzaPaletteTransformer();
            var generator = new CharclutGenerator(charclutService, paletteTransformer);

            // Act & Assert
            Assert.False(generator.IsConfigured);
        }

        [Fact]
        public void IsConfigured_ShouldReturnTrueWhenBaseNxdPathIsSet()
        {
            // Arrange
            var charclutService = new RamzaCharclutService(@"C:\fake\tools");
            var paletteTransformer = new RamzaPaletteTransformer();
            var generator = new CharclutGenerator(charclutService, paletteTransformer);

            // Act
            generator.SetBaseNxdPath(@"C:\game\data\charclut.nxd");

            // Assert
            Assert.True(generator.IsConfigured);
        }

        [Fact]
        public void ShouldGenerate_WithNoChaptersEnabled_ShouldReturnFalse()
        {
            // Arrange
            var charclutService = new RamzaCharclutService(@"C:\fake\tools");
            var paletteTransformer = new RamzaPaletteTransformer();
            var generator = new CharclutGenerator(charclutService, paletteTransformer);
            var settings = new RamzaHslSettings(); // All chapters disabled by default

            // Act
            var result = generator.ShouldGenerate(settings);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        public void ShouldGenerate_WithChapterEnabled_ShouldReturnTrue(int chapter)
        {
            // Arrange
            var charclutService = new RamzaCharclutService(@"C:\fake\tools");
            var paletteTransformer = new RamzaPaletteTransformer();
            var generator = new CharclutGenerator(charclutService, paletteTransformer);
            var settings = new RamzaHslSettings();

            // Enable specific chapter
            switch (chapter)
            {
                case 1: settings.Chapter1.Enabled = true; break;
                case 2: settings.Chapter2.Enabled = true; break;
                case 4: settings.Chapter4.Enabled = true; break;
            }

            // Act
            var result = generator.ShouldGenerate(settings);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ShouldGenerate_WithAllChaptersEnabled_ShouldReturnTrue()
        {
            // Arrange
            var charclutService = new RamzaCharclutService(@"C:\fake\tools");
            var paletteTransformer = new RamzaPaletteTransformer();
            var generator = new CharclutGenerator(charclutService, paletteTransformer);
            var settings = new RamzaHslSettings();
            settings.Chapter1.Enabled = true;
            settings.Chapter2.Enabled = true;
            settings.Chapter4.Enabled = true;

            // Act
            var result = generator.ShouldGenerate(settings);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ShouldGenerate_WithNullSettings_ShouldReturnFalse()
        {
            // Arrange
            var charclutService = new RamzaCharclutService(@"C:\fake\tools");
            var paletteTransformer = new RamzaPaletteTransformer();
            var generator = new CharclutGenerator(charclutService, paletteTransformer);

            // Act
            var result = generator.ShouldGenerate(null);

            // Assert
            Assert.False(result);
        }
    }
}
