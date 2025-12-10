using System;
using System.IO;
using Xunit;
using FFTColorMod;
using FFTColorMod.Configuration;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using Moq;

namespace FFTColorMod.Tests
{
    public class StartupConfigurationTests : IDisposable
    {
        private readonly string _testPath;

        public StartupConfigurationTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"FFTColorModStartupTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        [Fact]
        public void Startup_ShouldRegisterConfiguratorWithReloadedAPI()
        {
            // Arrange
            var mockLoader = new Mock<IModLoaderV1>();
            var mockConfig = new Mock<IModConfigV1>();

            IConfigurable? registeredConfigurable = null;
            mockLoader.Setup(l => l.AddOrReplaceController<IConfigurable>(
                It.IsAny<IModV1>(),
                It.IsAny<IConfigurable>()))
                .Callback<IModV1, IConfigurable>((mod, config) => registeredConfigurable = config);

            var startup = new Startup();

            // Act
            startup.StartEx(mockLoader.Object, mockConfig.Object);

            // Assert
            mockLoader.Verify(l => l.AddOrReplaceController<IConfigurable>(
                startup,
                It.IsAny<IConfigurable>()),
                Times.Once);

            Assert.NotNull(registeredConfigurable);
            Assert.IsType<Configurator>(registeredConfigurable);
            Assert.Equal("FFT Color Mod Configuration", registeredConfigurable.ConfigName);
        }

        [Fact]
        public void Startup_ShouldWireConfiguratorEventsToMod()
        {
            // Arrange
            var mockLoader = new Mock<IModLoaderV1>();
            var mockConfig = new Mock<IModConfigV1>();

            Configurator? capturedConfigurator = null;
            mockLoader.Setup(l => l.AddOrReplaceController<IConfigurable>(
                It.IsAny<IModV1>(),
                It.IsAny<IConfigurable>()))
                .Callback<IModV1, IConfigurable>((mod, config) => capturedConfigurator = config as Configurator);

            var startup = new Startup();
            startup.StartEx(mockLoader.Object, mockConfig.Object);

            // Get the mod instance via reflection
            var modField = typeof(Startup).GetField("_mod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var modInstance = modField?.GetValue(startup) as Mod;

            // Act - Update configuration through the configurator
            var testConfig = new Config
            {
                KnightMale = "test_color",
                ArcherFemale = "another_color"
            };
            capturedConfigurator?.SetConfiguration(0, testConfig);

            // Assert - The mod should have received the configuration
            Assert.NotNull(modInstance);
            Assert.Equal("test_color", modInstance.GetJobColor("KnightMale"));
            Assert.Equal("another_color", modInstance.GetJobColor("ArcherFemale"));
        }
    }
}