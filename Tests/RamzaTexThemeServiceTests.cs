using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Tests
{
    public class RamzaTexThemeServiceTests : IDisposable
    {
        private readonly string _testBasePath;

        public RamzaTexThemeServiceTests()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testBasePath);
        }

        [Fact]
        public void Constructor_ShouldInitializeWithBasePath()
        {
            // Act
            var service = new RamzaTexThemeService(_testBasePath);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void HasOriginalBackup_ShouldReturnFalse_WhenBackupDirectoryDoesNotExist()
        {
            // Arrange
            var service = new RamzaTexThemeService(_testBasePath);

            // Act
            bool hasBackup = service.HasOriginalBackup();

            // Assert
            Assert.False(hasBackup);
        }

        [Fact]
        public void HasOriginalBackup_ShouldReturnTrue_WhenAllBackupFilesExist()
        {
            // Arrange
            var service = new RamzaTexThemeService(_testBasePath);
            string backupPath = Path.Combine(_testBasePath, "original_backup");
            Directory.CreateDirectory(backupPath);

            // Create all required tex backup files (830-835)
            for (int i = 830; i <= 835; i++)
            {
                File.WriteAllBytes(Path.Combine(backupPath, $"tex_{i}.bin"), new byte[100]);
            }

            // Act
            bool hasBackup = service.HasOriginalBackup();

            // Assert
            Assert.True(hasBackup);
        }

        [Fact]
        public void ApplyThemeToRamzaTexFiles_ShouldThrowException_WhenBackupDirectoryDoesNotExist()
        {
            // Arrange
            var service = new RamzaTexThemeService(_testBasePath);

            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() =>
                service.ApplyThemeToRamzaTexFiles("white_heretic"));
        }

        [Fact]
        public void ApplyThemeToRamzaTexFiles_ShouldCreateModifiedTexFiles()
        {
            // Arrange
            var service = new RamzaTexThemeService(_testBasePath);
            string backupPath = Path.Combine(_testBasePath, "original_backup");
            Directory.CreateDirectory(backupPath);

            // Create a test tex file with known brown color data
            byte[] testData = new byte[131072];
            // Add brown color at specific offset (RGB555: R=72, G=64, B=16)
            ushort brownColor = (ushort)(((72 >> 3) & 0x1F) |
                                        (((64 >> 3) & 0x1F) << 5) |
                                        (((16 >> 3) & 0x1F) << 10));
            testData[0x0E50] = (byte)(brownColor & 0xFF);
            testData[0x0E51] = (byte)((brownColor >> 8) & 0xFF);

            File.WriteAllBytes(Path.Combine(backupPath, "tex_830.bin"), testData);

            // Act
            service.ApplyThemeToRamzaTexFiles("white_heretic");

            // Assert
            string outputFile = Path.Combine(_testBasePath, "tex_830.bin");
            Assert.True(File.Exists(outputFile));

            // Verify the color was changed
            byte[] modifiedData = File.ReadAllBytes(outputFile);
            ushort modifiedColor = (ushort)(modifiedData[0x0E50] | (modifiedData[0x0E51] << 8));

            // Should not be the same as the original brown color
            Assert.NotEqual(brownColor, modifiedColor);
        }

        [Fact]
        public void RestoreOriginalTexFiles_ShouldRestoreBackupFiles()
        {
            // Arrange
            var service = new RamzaTexThemeService(_testBasePath);
            string backupPath = Path.Combine(_testBasePath, "original_backup");
            Directory.CreateDirectory(backupPath);

            // Create original backup files with specific data
            byte[] originalData = new byte[131072];
            originalData[0] = 0xFF; // Mark to identify original

            for (int i = 830; i <= 835; i++)
            {
                File.WriteAllBytes(Path.Combine(backupPath, $"tex_{i}.bin"), originalData);
            }

            // Create modified files in base path
            byte[] modifiedData = new byte[131072];
            modifiedData[0] = 0xAA; // Different marker

            for (int i = 830; i <= 835; i++)
            {
                File.WriteAllBytes(Path.Combine(_testBasePath, $"tex_{i}.bin"), modifiedData);
            }

            // Act
            service.RestoreOriginalTexFiles();

            // Assert
            for (int i = 830; i <= 835; i++)
            {
                byte[] restoredData = File.ReadAllBytes(Path.Combine(_testBasePath, $"tex_{i}.bin"));
                Assert.Equal(0xFF, restoredData[0]); // Should have original marker
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBasePath))
            {
                Directory.Delete(_testBasePath, true);
            }
        }
    }
}