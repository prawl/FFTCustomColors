using System;
using Xunit;
using Moq;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Memory.Sigscan.Definitions.Structs;
using Reloaded.Hooks.Definitions;

namespace FFTColorMod.Tests
{
    public class SignatureScannerTests
    {
        [Fact]
        public void Constructor_ShouldInitializeSuccessfully()
        {
            // Arrange & Act
            var scanner = new SignatureScanner();

            // Assert
            Assert.NotNull(scanner);
        }

        [Fact]
        public void AddScan_ShouldRegisterPatternWithStartupScanner()
        {
            // Arrange
            var mockStartupScanner = new Mock<IStartupScanner>();
            var scanner = new SignatureScanner();
            var pattern = "48 8B C4 48 89 58 ?? 48 89 70 ??"; // Example sprite loading pattern
            var patternName = "LoadSprite";

            // Act
            scanner.AddScan(mockStartupScanner.Object, pattern, patternName, result => { });

            // Assert
            mockStartupScanner.Verify(s => s.AddMainModuleScan(
                It.Is<string>(p => p == pattern),
                It.IsAny<Action<PatternScanResult>>()
            ), Times.Once);
        }

        [Fact]
        public void SetupHooks_ShouldInitializeWithStartupScanner()
        {
            // Arrange
            var mockStartupScanner = new Mock<IStartupScanner>();
            var scanner = new SignatureScanner();

            // Act
            scanner.SetupHooks(mockStartupScanner.Object);

            // Assert - should not throw
            Assert.NotNull(scanner);
        }

        [Fact]
        public void SetupHooks_ShouldAddSpriteLoadingPattern()
        {
            // Arrange
            var mockStartupScanner = new Mock<IStartupScanner>();
            var scanner = new SignatureScanner();

            // Act
            scanner.SetupHooks(mockStartupScanner.Object);

            // Assert - verify that AddMainModuleScan was called at least once
            mockStartupScanner.Verify(s => s.AddMainModuleScan(
                It.IsAny<string>(),
                It.IsAny<Action<PatternScanResult>>()
            ), Times.AtLeastOnce);
        }

        [Fact]
        public void SetupHooks_ShouldAcceptHooksParameter()
        {
            // Arrange
            var mockStartupScanner = new Mock<IStartupScanner>();
            var mockHooks = new Mock<IReloadedHooks>();
            var scanner = new SignatureScanner();

            // Act
            scanner.SetupHooks(mockStartupScanner.Object, mockHooks.Object);

            // Assert - should not throw
            Assert.NotNull(scanner);
        }

        [Fact]
        public void SetupHooks_ShouldStoreHooksReference()
        {
            // Arrange
            var mockStartupScanner = new Mock<IStartupScanner>();
            var mockHooks = new Mock<IReloadedHooks>();
            var scanner = new SignatureScanner();

            // Act
            scanner.SetupHooks(mockStartupScanner.Object, mockHooks.Object);

            // Assert - verify hooks were stored
            Assert.NotNull(scanner.Hooks);
        }

        [Fact]
        public void ProcessSpriteData_ShouldAcceptPointerAndSize()
        {
            // Arrange
            var scanner = new SignatureScanner();
            var testPointer = IntPtr.Zero;
            var testSize = 256;

            // Act
            var result = scanner.ProcessSpriteData(testPointer, testSize);

            // Assert - should return the same pointer (pass-through for now)
            Assert.Equal(testPointer, result);
        }

        [Fact]
        public void SetPaletteDetector_ShouldStorePaletteDetector()
        {
            // Arrange
            var scanner = new SignatureScanner();
            var detector = new PaletteDetector();

            // Act
            scanner.SetPaletteDetector(detector);

            // Assert
            Assert.NotNull(scanner.PaletteDetector);
            Assert.Same(detector, scanner.PaletteDetector);
        }

        [Fact]
        public void SetColorScheme_ShouldStoreColorScheme()
        {
            // Arrange
            var scanner = new SignatureScanner();
            var scheme = "red";

            // Act
            scanner.SetColorScheme(scheme);

            // Assert
            Assert.Equal(scheme, scanner.ColorScheme);
        }
    }
}