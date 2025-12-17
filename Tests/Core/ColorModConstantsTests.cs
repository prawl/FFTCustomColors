using FFTColorCustomizer.Core;
using Xunit;

namespace FFTColorCustomizer.Tests.Core
{
    public class ColorModConstantsTests
    {
        [Fact]
        public void DefaultTheme_ShouldBeOriginal()
        {
            // Assert
            Assert.Equal("original", ColorModConstants.DefaultTheme);
        }

        [Fact]
        public void ConfigFileName_ShouldBeConfigJson()
        {
            // Assert
            Assert.Equal("Config.json", ColorModConstants.ConfigFileName);
        }

        [Fact]
        public void SpritesRelativePath_ShouldBeCorrect()
        {
            // Assert
            Assert.Equal(@"FFTIVC\data\enhanced\fftpack\unit", ColorModConstants.SpritesRelativePath);
        }

        [Fact]
        public void BattlePrefix_ShouldBeBattleUnderscore()
        {
            // Assert
            Assert.Equal("battle_", ColorModConstants.BattlePrefix);
        }

        [Fact]
        public void PropertySuffixes_ShouldBeCorrect()
        {
            // Assert
            Assert.Equal("_Male", ColorModConstants.MaleSuffix);
            Assert.Equal("_Female", ColorModConstants.FemaleSuffix);
        }

        [Fact]
        public void ModMetadata_ShouldBeCorrect()
        {
            // Assert
            Assert.Equal("FFTColorCustomizer", ColorModConstants.ModId);
            Assert.Equal("FFT Color Customizer", ColorModConstants.ModName);
            Assert.Equal("ptyra.fft.colorcustomizer", ColorModConstants.ModNamespace);
        }

        [Fact]
        public void JsonFileNames_ShouldBeCorrect()
        {
            // Assert
            Assert.Equal("StoryCharacters.json", ColorModConstants.StoryCharactersFile);
            Assert.Equal("JobClasses.json", ColorModConstants.JobClassesFile);
        }

        [Fact]
        public void UIConstants_ShouldHaveReasonableValues()
        {
            // Assert
            Assert.True(ColorModConstants.PreviewImageWidth > 0);
            Assert.True(ColorModConstants.PreviewImageHeight > 0);
            Assert.True(ColorModConstants.RowHeight > 0);
            Assert.True(ColorModConstants.LabelWidth > 0);
            Assert.True(ColorModConstants.DropdownWidth > 0);
        }

        [Fact]
        public void CommonThemeNames_ShouldBeCorrect()
        {
            // Assert
            Assert.Equal("original", ColorModConstants.OriginalTheme);
            Assert.Equal("lucavi", ColorModConstants.LucaviTheme);
            Assert.Equal("corpse_brigade", ColorModConstants.CorpseBrigadeTheme);
            Assert.Equal("vampyre", ColorModConstants.VampyreTheme);
        }

        [Fact]
        public void ErrorMessages_ShouldExist()
        {
            // Assert
            Assert.NotNull(ColorModConstants.ConfigNotFoundError);
            Assert.NotNull(ColorModConstants.InvalidThemeError);
            Assert.NotNull(ColorModConstants.SpriteNotFoundError);
            Assert.NotNull(ColorModConstants.DirectoryNotFoundError);
            Assert.NotEmpty(ColorModConstants.ConfigNotFoundError);
            Assert.NotEmpty(ColorModConstants.InvalidThemeError);
        }

        [Fact]
        public void SuccessMessages_ShouldExist()
        {
            // Assert
            Assert.NotNull(ColorModConstants.ConfigSavedSuccess);
            Assert.NotNull(ColorModConstants.ThemeAppliedSuccess);
            Assert.NotNull(ColorModConstants.ModLoadedSuccess);
            Assert.NotEmpty(ColorModConstants.ConfigSavedSuccess);
            Assert.NotEmpty(ColorModConstants.ThemeAppliedSuccess);
        }

        [Fact]
        public void FileExtensions_ShouldBeCorrect()
        {
            // Assert
            Assert.Equal(".bmp", ColorModConstants.BitmapExtension);
            Assert.Equal(".png", ColorModConstants.PngExtension);
            Assert.Equal(".json", ColorModConstants.JsonExtension);
        }

        [Fact]
        public void DirectoryNames_ShouldBeCorrect()
        {
            // Assert
            Assert.Equal("Data", ColorModConstants.DataDirectory);
            Assert.Equal("User", ColorModConstants.UserDirectory);
            Assert.Equal("Mods", ColorModConstants.ModsDirectory);
            Assert.Equal("FFTIVC", ColorModConstants.FFTIVCPath);
            Assert.Equal("unit", ColorModConstants.UnitPath);
        }

        [Fact]
        public void LogPrefix_ShouldBeCorrect()
        {
            // Assert
            Assert.Equal("[FFT Color Mod]", ColorModConstants.LogPrefix);
        }
    }
}
