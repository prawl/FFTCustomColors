using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Services;
using System;
using System.IO;

namespace FFTColorCustomizer.Tests.Services
{
    public class GenericJobsDetectorTests : IDisposable
    {
        private readonly string _testReloadedRoot;
        private readonly string _testModsPath;
        private readonly string _testAppsPath;

        public GenericJobsDetectorTests()
        {
            _testReloadedRoot = Path.Combine(Path.GetTempPath(), $"GenericJobsDetectorTest_{Guid.NewGuid()}");
            _testModsPath = Path.Combine(_testReloadedRoot, "Mods");
            _testAppsPath = Path.Combine(_testReloadedRoot, "Apps");
            Directory.CreateDirectory(_testModsPath);
            Directory.CreateDirectory(_testAppsPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testReloadedRoot))
            {
                Directory.Delete(_testReloadedRoot, true);
            }
        }

        [Fact]
        public void State_Should_Return_NotInstalled_When_No_GenericJobs_Folder_Exists()
        {
            // Arrange
            var detector = new GenericJobsDetector(_testModsPath);

            // Act
            var result = detector.State;

            // Assert
            result.Should().Be(GenericJobsState.NotInstalled);
        }

        [Fact]
        public void State_Should_Return_NotInstalled_When_ModsPath_Does_Not_Exist()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testReloadedRoot, "NonExistent");
            var detector = new GenericJobsDetector(nonExistentPath);

            // Act
            var result = detector.State;

            // Assert
            result.Should().Be(GenericJobsState.NotInstalled);
        }

        [Fact]
        public void State_Should_Return_InstalledButDisabled_When_Folder_Exists_But_No_AppConfig()
        {
            // Arrange
            var genericJobsDir = Path.Combine(_testModsPath, "GenericJobs");
            Directory.CreateDirectory(genericJobsDir);
            var detector = new GenericJobsDetector(_testModsPath);

            // Act
            var result = detector.State;

            // Assert
            result.Should().Be(GenericJobsState.InstalledButDisabled);
        }

        [Fact]
        public void State_Should_Return_InstalledButDisabled_When_Mod_Not_In_EnabledMods()
        {
            // Arrange
            var genericJobsDir = Path.Combine(_testModsPath, "GenericJobs");
            Directory.CreateDirectory(genericJobsDir);

            // Create FFT app config without GenericJobs enabled
            var fftAppDir = Path.Combine(_testAppsPath, "fft_enhanced.exe");
            Directory.CreateDirectory(fftAppDir);
            var appConfig = @"{
                ""EnabledMods"": [
                    ""fftivc.utility.modloader"",
                    ""paxtrick.fft.colorcustomizer""
                ]
            }";
            File.WriteAllText(Path.Combine(fftAppDir, "AppConfig.json"), appConfig);

            var detector = new GenericJobsDetector(_testModsPath);

            // Act
            var result = detector.State;

            // Assert
            result.Should().Be(GenericJobsState.InstalledButDisabled);
        }

        [Fact]
        public void State_Should_Return_InstalledAndEnabled_When_Mod_In_EnabledMods()
        {
            // Arrange
            var genericJobsDir = Path.Combine(_testModsPath, "GenericJobs");
            Directory.CreateDirectory(genericJobsDir);

            // Create FFT app config with GenericJobs enabled
            var fftAppDir = Path.Combine(_testAppsPath, "fft_enhanced.exe");
            Directory.CreateDirectory(fftAppDir);
            var appConfig = @"{
                ""EnabledMods"": [
                    ""fftivc.utility.modloader"",
                    ""ffttic.jobs.genericjobs"",
                    ""paxtrick.fft.colorcustomizer""
                ]
            }";
            File.WriteAllText(Path.Combine(fftAppDir, "AppConfig.json"), appConfig);

            var detector = new GenericJobsDetector(_testModsPath);

            // Act
            var result = detector.State;

            // Assert
            result.Should().Be(GenericJobsState.InstalledAndEnabled);
        }

        [Fact]
        public void State_Should_Handle_Versioned_GenericJobs_Folder()
        {
            // Arrange
            var genericJobsDir = Path.Combine(_testModsPath, "GenericJobs.v1.0.0");
            Directory.CreateDirectory(genericJobsDir);

            // Create FFT app config with GenericJobs enabled
            var fftAppDir = Path.Combine(_testAppsPath, "fft_enhanced.exe");
            Directory.CreateDirectory(fftAppDir);
            var appConfig = @"{
                ""EnabledMods"": [
                    ""ffttic.jobs.genericjobs""
                ]
            }";
            File.WriteAllText(Path.Combine(fftAppDir, "AppConfig.json"), appConfig);

            var detector = new GenericJobsDetector(_testModsPath);

            // Act
            var result = detector.State;

            // Assert
            result.Should().Be(GenericJobsState.InstalledAndEnabled);
        }

        [Fact]
        public void IsGenericJobsInstalled_Should_Return_True_When_Folder_Exists()
        {
            // Arrange - backwards compatibility test
            var genericJobsDir = Path.Combine(_testModsPath, "GenericJobs");
            Directory.CreateDirectory(genericJobsDir);
            var detector = new GenericJobsDetector(_testModsPath);

            // Act
            var result = detector.IsGenericJobsInstalled;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsGenericJobsInstalled_Should_Return_False_When_No_Folder()
        {
            // Arrange - backwards compatibility test
            var detector = new GenericJobsDetector(_testModsPath);

            // Act
            var result = detector.IsGenericJobsInstalled;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsGenericJobsEnabled_Should_Return_True_Only_When_Enabled()
        {
            // Arrange
            var genericJobsDir = Path.Combine(_testModsPath, "GenericJobs");
            Directory.CreateDirectory(genericJobsDir);

            var fftAppDir = Path.Combine(_testAppsPath, "fft_enhanced.exe");
            Directory.CreateDirectory(fftAppDir);
            var appConfig = @"{
                ""EnabledMods"": [""ffttic.jobs.genericjobs""]
            }";
            File.WriteAllText(Path.Combine(fftAppDir, "AppConfig.json"), appConfig);

            var detector = new GenericJobsDetector(_testModsPath);

            // Act & Assert
            detector.IsGenericJobsEnabled.Should().BeTrue();
        }

        [Fact]
        public void IsGenericJobsEnabled_Should_Return_False_When_Only_Installed()
        {
            // Arrange
            var genericJobsDir = Path.Combine(_testModsPath, "GenericJobs");
            Directory.CreateDirectory(genericJobsDir);
            var detector = new GenericJobsDetector(_testModsPath);

            // Act & Assert
            detector.IsGenericJobsEnabled.Should().BeFalse();
        }
    }
}
