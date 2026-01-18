using Xunit;
using FFTColorCustomizer.Services;

namespace Tests.Services
{
    public class RamzaCharclutServiceTests
    {
        [Fact]
        public void Constructor_ShouldAcceptToolsPath()
        {
            // Arrange & Act
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void GetFF16ToolsPath_ShouldReturnCorrectPath()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Act
            var path = service.GetFF16ToolsPath();

            // Assert
            Assert.Equal(@"C:\fake\tools\FF16Tools.CLI.exe", path);
        }

        [Fact]
        public void BuildNxdToSqliteArgs_ShouldReturnCorrectArgs()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Act
            var args = service.BuildNxdToSqliteArgs(@"C:\input\nxd", @"C:\output\db.sqlite");

            // Assert
            Assert.Equal(@"nxd-to-sqlite -i ""C:\input\nxd"" -o ""C:\output\db.sqlite"" -g fft", args);
        }

        [Fact]
        public void BuildSqliteToNxdArgs_ShouldReturnCorrectArgs()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Act
            var args = service.BuildSqliteToNxdArgs(@"C:\input\db.sqlite", @"C:\output\nxd");

            // Assert
            Assert.Equal(@"sqlite-to-nxd -i ""C:\input\db.sqlite"" -o ""C:\output\nxd"" -g fft", args);
        }

        [Fact]
        public void GetTempSqlitePath_ShouldReturnPathInTempDirectory()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Act
            var path = service.GetTempSqlitePath();

            // Assert
            Assert.Contains("charclut.sqlite", path);
            Assert.Contains(System.IO.Path.GetTempPath().TrimEnd('\\'), path);
        }

        [Fact]
        public void GetTempNxdOutputPath_ShouldReturnPathInTempDirectory()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Act
            var path = service.GetTempNxdOutputPath();

            // Assert
            Assert.Contains("charclut_output", path);
            Assert.Contains(System.IO.Path.GetTempPath().TrimEnd('\\'), path);
        }

        [Fact]
        public void GetGeneratedNxdFilePath_ShouldReturnPathToCharclutNxd()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Act
            var path = service.GetGeneratedNxdFilePath();

            // Assert
            Assert.EndsWith("charclut.nxd", path);
            Assert.Contains("charclut_output", path);
        }

        [Fact]
        public void LoadCharclutDatabase_WithNonExistentFile_ShouldReturnFalse()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Act
            var result = service.LoadCharclutDatabase(@"C:\nonexistent\charclut.nxd");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void SaveCharclutNxd_WithNonExistentSqlite_ShouldReturnFalse()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Act
            var result = service.SaveCharclutNxd(@"C:\output\nxd");

            // Assert - should fail because no SQLite database was loaded
            Assert.False(result);
        }

        [Fact]
        public void GetChapterPalette_WhenNotLoaded_ShouldReturnNull()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");

            // Act
            var palette = service.GetChapterPalette(1);

            // Assert - should return null when no database is loaded
            Assert.Null(palette);
        }

        [Fact]
        public void SetChapterPalette_WhenNotLoaded_ShouldReturnFalse()
        {
            // Arrange
            var service = new RamzaCharclutService(@"C:\fake\tools");
            var palette = new int[48];

            // Act
            var result = service.SetChapterPalette(1, palette);

            // Assert - should return false when no database is loaded
            Assert.False(result);
        }
    }
}
