using System;
using System.IO;
using FFTColorCustomizer.Configuration.UI;
using Xunit;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    public class WindowStateServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _configPath;

        public WindowStateServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "WindowStateServiceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _configPath = Path.Combine(_tempDir, "Config.json");
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void Load_ReturnsNull_WhenFileMissing()
        {
            Assert.Null(WindowStateService.Load(_configPath));
        }

        [Fact]
        public void Load_ReturnsNull_WhenConfigPathIsNull()
        {
            Assert.Null(WindowStateService.Load(null));
        }

        [Fact]
        public void Load_ReturnsNull_WhenConfigPathIsEmpty()
        {
            Assert.Null(WindowStateService.Load(string.Empty));
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsDimensions()
        {
            WindowStateService.Save(_configPath, 1234, 567);

            var loaded = WindowStateService.Load(_configPath);

            Assert.NotNull(loaded);
            Assert.Equal(1234, loaded!.Width);
            Assert.Equal(567, loaded.Height);
        }

        [Fact]
        public void Save_WritesToWindowStateJsonNextToConfig()
        {
            WindowStateService.Save(_configPath, 800, 600);

            var expectedPath = Path.Combine(_tempDir, "WindowState.json");
            Assert.True(File.Exists(expectedPath));
        }

        [Fact]
        public void Save_DoesNotThrow_WhenConfigPathIsNull()
        {
            var ex = Record.Exception(() => WindowStateService.Save(null, 800, 600));
            Assert.Null(ex);
        }

        [Fact]
        public void Load_ReturnsNull_WhenFileIsCorrupt()
        {
            var path = Path.Combine(_tempDir, "WindowState.json");
            File.WriteAllText(path, "{ this is not json");

            Assert.Null(WindowStateService.Load(_configPath));
        }

        [Fact]
        public void Load_ReturnsNull_WhenSavedDimensionsAreZero()
        {
            var path = Path.Combine(_tempDir, "WindowState.json");
            File.WriteAllText(path, "{\"Width\":0,\"Height\":0}");

            // Zero-sized state is treated as missing so the form falls back to defaults
            Assert.Null(WindowStateService.Load(_configPath));
        }

        [Fact]
        public void ResolveSize_UsesDefaults_WhenSavedIsNull()
        {
            var (w, h) = WindowStateService.ResolveSize(
                saved: null,
                defaultWidth: 1000, defaultHeight: 900,
                minWidth: 600, minHeight: 400,
                maxWidth: 1920, maxHeight: 1080);

            Assert.Equal(1000, w);
            Assert.Equal(900, h);
        }

        [Fact]
        public void ResolveSize_UsesSavedValues_WhenWithinBounds()
        {
            var saved = new WindowStateService.WindowState { Width = 1200, Height = 800 };
            var (w, h) = WindowStateService.ResolveSize(
                saved,
                defaultWidth: 1000, defaultHeight: 900,
                minWidth: 600, minHeight: 400,
                maxWidth: 1920, maxHeight: 1080);

            Assert.Equal(1200, w);
            Assert.Equal(800, h);
        }

        [Fact]
        public void ResolveSize_ClampsToMinimum_WhenSavedTooSmall()
        {
            var saved = new WindowStateService.WindowState { Width = 100, Height = 50 };
            var (w, h) = WindowStateService.ResolveSize(
                saved,
                defaultWidth: 1000, defaultHeight: 900,
                minWidth: 600, minHeight: 400,
                maxWidth: 1920, maxHeight: 1080);

            Assert.Equal(600, w);
            Assert.Equal(400, h);
        }

        [Fact]
        public void ResolveSize_ClampsToMaximum_WhenSavedTooLarge()
        {
            // Stale entry from a 4K monitor opened on a 1080p screen
            var saved = new WindowStateService.WindowState { Width = 3840, Height = 2160 };
            var (w, h) = WindowStateService.ResolveSize(
                saved,
                defaultWidth: 1000, defaultHeight: 900,
                minWidth: 600, minHeight: 400,
                maxWidth: 1920, maxHeight: 1080);

            Assert.Equal(1920, w);
            Assert.Equal(1080, h);
        }

        [Fact]
        public void GetStatePath_ReturnsNull_ForNullOrEmptyConfigPath()
        {
            Assert.Null(WindowStateService.GetStatePath(null));
            Assert.Null(WindowStateService.GetStatePath(string.Empty));
        }

        [Fact]
        public void GetStatePath_ReturnsSiblingFile()
        {
            var path = WindowStateService.GetStatePath(_configPath);
            Assert.Equal(Path.Combine(_tempDir, "WindowState.json"), path);
        }
    }
}
