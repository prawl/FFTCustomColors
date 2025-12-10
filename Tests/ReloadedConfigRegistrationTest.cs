using System;
using Xunit;
using FFTColorMod;
using FFTColorMod.Configuration;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using Moq;
using System.IO;

namespace FFTColorMod.Tests
{
    public class ReloadedConfigRegistrationTest
    {
        [Fact]
        public void Startup_ShouldRegisterConfiguratorAsController()
        {
            // Arrange
            var mockLoader = new Mock<IModLoaderV1>();
            var mockConfig = new Mock<IModConfigV1>();

            IConfigurable? registeredController = null;
            IModV1? registeredMod = null;

            mockLoader.Setup(l => l.AddOrReplaceController<IConfigurable>(
                It.IsAny<IModV1>(),
                It.IsAny<IConfigurable>()))
                .Callback<IModV1, IConfigurable>((mod, controller) =>
                {
                    registeredMod = mod;
                    registeredController = controller;
                });

            var startup = new Startup();

            // Act
            startup.StartEx(mockLoader.Object, mockConfig.Object);

            // Assert - Verify registration happened
            mockLoader.Verify(l => l.AddOrReplaceController<IConfigurable>(
                It.IsAny<IModV1>(),
                It.IsAny<IConfigurable>()),
                Times.Once);

            // Assert - Verify correct objects were registered
            Assert.NotNull(registeredMod);
            Assert.NotNull(registeredController);
            Assert.Same(startup, registeredMod);
            Assert.IsType<Configurator>(registeredController);

            // Assert - Verify configurator is properly configured
            var configurator = registeredController as Configurator;
            Assert.NotNull(configurator);
            Assert.Equal("FFT Color Mod Configuration", configurator.ConfigName);
            Assert.NotNull(configurator.Save);
        }

        [Fact]
        public void Configurator_ShouldBeDiscoverableByReloadedII()
        {
            // This test verifies the configurator can be accessed through the registration
            var mockLoader = new Mock<IModLoaderV1>();
            var mockConfig = new Mock<IModConfigV1>();

            IConfigurable? capturedController = null;
            mockLoader.Setup(l => l.AddOrReplaceController<IConfigurable>(
                It.IsAny<IModV1>(),
                It.IsAny<IConfigurable>()))
                .Callback<IModV1, IConfigurable>((mod, ctrl) => capturedController = ctrl);

            var startup = new Startup();
            startup.StartEx(mockLoader.Object, mockConfig.Object);

            // Simulate Reloaded-II accessing the configuration
            Assert.NotNull(capturedController);

            // Cast to Configurator to access GetConfiguration
            var configurator = capturedController as Configurator;
            Assert.NotNull(configurator);
            var config = configurator.GetConfiguration<Config>(0);
            Assert.NotNull(config);

            // Verify Save doesn't throw
            var saveAction = capturedController.Save;
            Assert.NotNull(saveAction);
            saveAction.Invoke();
        }
    }
}