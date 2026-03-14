using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Services;
using System;
using System.IO;

namespace FFTColorCustomizer.Tests.Services
{
    public class WotlCharactersDetectorTests : IDisposable
    {
        private readonly string _testReloadedRoot;
        private readonly string _testModsPath;
        private readonly string _testAppsPath;

        public WotlCharactersDetectorTests()
        {
            _testReloadedRoot = Path.Combine(Path.GetTempPath(), $"WotlCharactersDetectorTest_{Guid.NewGuid()}");
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
        public void State_Should_Return_NotInstalled_When_No_WotLCharacters_Folder_Exists()
        {
            var detector = new WotlCharactersDetector(_testModsPath);
            detector.State.Should().Be(WotlCharactersState.NotInstalled);
        }

        [Fact]
        public void State_Should_Return_NotInstalled_When_ModsPath_Does_Not_Exist()
        {
            var nonExistentPath = Path.Combine(_testReloadedRoot, "NonExistent");
            var detector = new WotlCharactersDetector(nonExistentPath);
            detector.State.Should().Be(WotlCharactersState.NotInstalled);
        }

        [Fact]
        public void State_Should_Return_NotInstalled_When_ModsPath_Is_Null()
        {
            var detector = new WotlCharactersDetector(null);
            detector.State.Should().Be(WotlCharactersState.NotInstalled);
        }

        [Fact]
        public void State_Should_Return_InstalledButDisabled_When_Folder_Exists_But_No_AppConfig()
        {
            Directory.CreateDirectory(Path.Combine(_testModsPath, "WotLCharacters"));
            var detector = new WotlCharactersDetector(_testModsPath);
            detector.State.Should().Be(WotlCharactersState.InstalledButDisabled);
        }

        [Fact]
        public void State_Should_Return_InstalledButDisabled_When_Mod_Not_In_EnabledMods()
        {
            Directory.CreateDirectory(Path.Combine(_testModsPath, "WotLCharacters"));

            var fftAppDir = Path.Combine(_testAppsPath, "fft_enhanced.exe");
            Directory.CreateDirectory(fftAppDir);
            var appConfig = @"{
                ""EnabledMods"": [
                    ""fftivc.utility.modloader"",
                    ""paxtrick.fft.colorcustomizer""
                ]
            }";
            File.WriteAllText(Path.Combine(fftAppDir, "AppConfig.json"), appConfig);

            var detector = new WotlCharactersDetector(_testModsPath);
            detector.State.Should().Be(WotlCharactersState.InstalledButDisabled);
        }

        [Fact]
        public void State_Should_Return_InstalledAndEnabled_When_Mod_In_EnabledMods()
        {
            Directory.CreateDirectory(Path.Combine(_testModsPath, "WotLCharacters"));

            var fftAppDir = Path.Combine(_testAppsPath, "fft_enhanced.exe");
            Directory.CreateDirectory(fftAppDir);
            var appConfig = @"{
                ""EnabledMods"": [
                    ""fftivc.utility.modloader"",
                    ""ffttic.wotlcharacters"",
                    ""paxtrick.fft.colorcustomizer""
                ]
            }";
            File.WriteAllText(Path.Combine(fftAppDir, "AppConfig.json"), appConfig);

            var detector = new WotlCharactersDetector(_testModsPath);
            detector.State.Should().Be(WotlCharactersState.InstalledAndEnabled);
        }

        [Fact]
        public void State_Should_Handle_Versioned_WotLCharacters_Folder()
        {
            Directory.CreateDirectory(Path.Combine(_testModsPath, "WotLCharacters.v1.0.0"));

            var fftAppDir = Path.Combine(_testAppsPath, "fft_enhanced.exe");
            Directory.CreateDirectory(fftAppDir);
            var appConfig = @"{
                ""EnabledMods"": [""ffttic.wotlcharacters""]
            }";
            File.WriteAllText(Path.Combine(fftAppDir, "AppConfig.json"), appConfig);

            var detector = new WotlCharactersDetector(_testModsPath);
            detector.State.Should().Be(WotlCharactersState.InstalledAndEnabled);
        }

        [Fact]
        public void IsWotlCharactersInstalled_Should_Return_True_When_Folder_Exists()
        {
            Directory.CreateDirectory(Path.Combine(_testModsPath, "WotLCharacters"));
            var detector = new WotlCharactersDetector(_testModsPath);
            detector.IsWotlCharactersInstalled.Should().BeTrue();
        }

        [Fact]
        public void IsWotlCharactersInstalled_Should_Return_False_When_No_Folder()
        {
            var detector = new WotlCharactersDetector(_testModsPath);
            detector.IsWotlCharactersInstalled.Should().BeFalse();
        }

        [Fact]
        public void IsWotlCharactersEnabled_Should_Return_True_Only_When_Enabled()
        {
            Directory.CreateDirectory(Path.Combine(_testModsPath, "WotLCharacters"));

            var fftAppDir = Path.Combine(_testAppsPath, "fft_enhanced.exe");
            Directory.CreateDirectory(fftAppDir);
            var appConfig = @"{
                ""EnabledMods"": [""ffttic.wotlcharacters""]
            }";
            File.WriteAllText(Path.Combine(fftAppDir, "AppConfig.json"), appConfig);

            var detector = new WotlCharactersDetector(_testModsPath);
            detector.IsWotlCharactersEnabled.Should().BeTrue();
        }

        [Fact]
        public void IsWotlCharactersEnabled_Should_Return_False_When_Only_Installed()
        {
            Directory.CreateDirectory(Path.Combine(_testModsPath, "WotLCharacters"));
            var detector = new WotlCharactersDetector(_testModsPath);
            detector.IsWotlCharactersEnabled.Should().BeFalse();
        }

        [Fact]
        public void State_Should_Be_Cached_After_First_Check()
        {
            var detector = new WotlCharactersDetector(_testModsPath);

            var firstResult = detector.State;
            // Create folder after first check
            Directory.CreateDirectory(Path.Combine(_testModsPath, "WotLCharacters"));
            var secondResult = detector.State;

            // Should still return the cached value
            secondResult.Should().Be(firstResult);
        }
    }
}
