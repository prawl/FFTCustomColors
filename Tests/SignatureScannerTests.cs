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

        [Fact]
        public unsafe void ProcessSpriteData_WithRedScheme_ShouldModifyBrownColors()
        {
            // Arrange
            var scanner = new SignatureScanner();
            var detector = new PaletteDetector();
            scanner.SetPaletteDetector(detector);
            scanner.SetColorScheme("red");

            // Create a test palette with Chapter 1 Ramza colors
            byte[] paletteData = new byte[768];
            // Set up Chapter 1 Ramza's main tunic brown color at a known position
            paletteData[0] = 0x17;  // Blue component of brown
            paletteData[1] = 0x2C;  // Green component of brown
            paletteData[2] = 0x4A;  // Red component of brown

            // Add Chapter 1 signature colors for detection
            paletteData[0x20 * 3] = 0x17; paletteData[0x20 * 3 + 1] = 0x2C; paletteData[0x20 * 3 + 2] = 0x4A;
            paletteData[0x21 * 3] = 0x23; paletteData[0x21 * 3 + 1] = 0x3A; paletteData[0x21 * 3 + 2] = 0x62;
            paletteData[0x2B * 3] = 0x38; paletteData[0x2B * 3 + 1] = 0x53; paletteData[0x2B * 3 + 2] = 0x83;

            fixed (byte* ptr = paletteData)
            {
                IntPtr spriteData = new IntPtr(ptr);

                // Act
                scanner.ProcessSpriteData(spriteData, 768);

                // Assert - brown should be changed to red
                Assert.Equal(0x00, paletteData[0]);  // Blue = 0 for red
                Assert.Equal(0x00, paletteData[1]);  // Green = 0 for red
                Assert.Equal(0xFF, paletteData[2]);  // Red = 255 for red
            }
        }

        [Fact]
        public unsafe void ProcessSpriteData_WithOriginalScheme_ShouldNotModifyColors()
        {
            // Arrange
            var scanner = new SignatureScanner();
            var detector = new PaletteDetector();
            scanner.SetPaletteDetector(detector);
            scanner.SetColorScheme("original");

            // Create a test palette with Chapter 1 Ramza colors
            byte[] paletteData = new byte[768];
            // Set up Chapter 1 Ramza's main tunic brown color
            paletteData[0] = 0x17;  // Blue component of brown
            paletteData[1] = 0x2C;  // Green component of brown
            paletteData[2] = 0x4A;  // Red component of brown

            // Add Chapter 1 signature colors for detection
            paletteData[0x20 * 3] = 0x17; paletteData[0x20 * 3 + 1] = 0x2C; paletteData[0x20 * 3 + 2] = 0x4A;
            paletteData[0x21 * 3] = 0x23; paletteData[0x21 * 3 + 1] = 0x3A; paletteData[0x21 * 3 + 2] = 0x62;
            paletteData[0x2B * 3] = 0x38; paletteData[0x2B * 3 + 1] = 0x53; paletteData[0x2B * 3 + 2] = 0x83;

            fixed (byte* ptr = paletteData)
            {
                IntPtr spriteData = new IntPtr(ptr);

                // Act
                scanner.ProcessSpriteData(spriteData, 768);

                // Assert - colors should remain unchanged
                Assert.Equal(0x17, paletteData[0]);
                Assert.Equal(0x2C, paletteData[1]);
                Assert.Equal(0x4A, paletteData[2]);
            }
        }

        [Fact]
        public void ProcessSpriteData_WithNullPointer_ShouldReturnSamePointer()
        {
            // Arrange
            var scanner = new SignatureScanner();
            scanner.SetColorScheme("red");

            // Act
            var result = scanner.ProcessSpriteData(IntPtr.Zero, 768);

            // Assert
            Assert.Equal(IntPtr.Zero, result);
        }

        [Fact]
        public void ProcessSpriteData_WithSmallSize_ShouldNotModify()
        {
            // Arrange
            var scanner = new SignatureScanner();
            scanner.SetColorScheme("red");
            var testPointer = new IntPtr(123456);

            // Act
            var result = scanner.ProcessSpriteData(testPointer, 100); // Too small for palette

            // Assert
            Assert.Equal(testPointer, result);
        }

        [Fact]
        public void ProcessSpriteData_WithoutPaletteDetector_ShouldReturnSamePointer()
        {
            // Arrange
            var scanner = new SignatureScanner();
            scanner.SetColorScheme("red");
            // Don't set PaletteDetector
            var testPointer = new IntPtr(123456);

            // Act
            var result = scanner.ProcessSpriteData(testPointer, 768);

            // Assert
            Assert.Equal(testPointer, result);
        }

        [Fact]
        public void AddMultipleScans_ShouldRegisterAllPatterns()
        {
            // Arrange
            var mockStartupScanner = new Mock<IStartupScanner>();
            var scanner = new SignatureScanner();
            var patterns = new[]
            {
                "48 8B C4 48 89 58 ??",  // Common function prologue
                "48 89 5C 24 ?? 48 89 74 24 ??",  // Stack frame setup
                "40 53 48 83 EC ??"  // Another common pattern
            };

            // Act
            foreach (var pattern in patterns)
            {
                scanner.AddScan(mockStartupScanner.Object, pattern, $"Pattern_{pattern.Substring(0, 5)}", result => { });
            }

            // Assert - should have registered all patterns
            mockStartupScanner.Verify(s => s.AddMainModuleScan(
                It.IsAny<string>(),
                It.IsAny<Action<PatternScanResult>>()
            ), Times.Exactly(3));
        }

        [Fact]
        public void CreateSpriteHook_ShouldHookFunction_WhenPatternFound()
        {
            // TLDR: Test that finding sprite pattern creates actual hook
            // Arrange
            var scanner = new SignatureScanner();
            var mockHooks = new Mock<IReloadedHooks>();
            var mockStartupScanner = new Mock<IStartupScanner>();
            IntPtr testAddress = new IntPtr(0x12345678);

            // Act - simulate finding the sprite loading pattern
            scanner.CreateSpriteLoadHook(mockHooks.Object, testAddress);

            // Assert - should have created a hook for sprite loading
            Assert.True(scanner.IsSpriteHookActive, "Sprite hook should be active after pattern found");
        }

        [Fact]
        public void CreateSpriteHook_ShouldNotBeActive_WhenHooksIsNull()
        {
            // TLDR: Test that hook is not activated when hooks is null
            // Arrange
            var scanner = new SignatureScanner();
            IntPtr testAddress = new IntPtr(0x12345678);

            // Act
            scanner.CreateSpriteLoadHook(null, testAddress);

            // Assert - should NOT be active
            Assert.False(scanner.IsSpriteHookActive, "Hook should not be active when hooks is null");
        }

        [Fact]
        public void LoadSpriteHook_ShouldReturnSamePointer_WhenCalled()
        {
            // TLDR: Test that our hook function returns the sprite data unchanged
            // Arrange
            var scanner = new SignatureScanner();
            IntPtr testData = new IntPtr(0xDEADBEEF);
            int testSize = 256;

            // Act - call the hook method directly
            var result = scanner.TestLoadSpriteHook(testData, testSize);

            // Assert - should return same pointer
            Assert.Equal(testData, result);
        }

        [Fact]
        public void CreateSpriteHook_ShouldCallActivate_WhenHookCreated()
        {
            // TLDR: Test that we call Activate() on the hook
            // Arrange
            var scanner = new SignatureScanner();

            // Act & Assert - check that hook is activated
            Assert.True(scanner.TestIfHookWouldBeActivated(), "Hook should be activated when created");
        }

        [Fact]
        public void SetupExperimentalHooks_ShouldAddMultiplePatterns()
        {
            // Arrange
            var mockStartupScanner = new Mock<IStartupScanner>();
            var mockHooks = new Mock<IReloadedHooks>();
            var scanner = new SignatureScanner();

            // Act
            scanner.SetupExperimentalHooks(mockStartupScanner.Object, mockHooks.Object);

            // Assert - should add at least 3 experimental patterns
            mockStartupScanner.Verify(s => s.AddMainModuleScan(
                It.IsAny<string>(),
                It.IsAny<Action<PatternScanResult>>()
            ), Times.AtLeast(3));
        }

        [Fact]
        public void LogPatternMatch_ShouldNotThrow()
        {
            // Arrange
            var scanner = new SignatureScanner();

            // Act & Assert - just ensure it doesn't throw
            scanner.LogPatternMatch("48 8B C4", 0x1000, true);
            scanner.LogPatternMatch("48 8B C4", 0x2000, false);
        }

        [Fact]
        public void SetupExperimentalHooks_ShouldLogWhenPatternFound()
        {
            // Arrange
            var mockStartupScanner = new Mock<IStartupScanner>();
            var mockHooks = new Mock<IReloadedHooks>();
            var scanner = new SignatureScanner();
            Action<PatternScanResult>? capturedCallback = null;

            mockStartupScanner.Setup(s => s.AddMainModuleScan(It.IsAny<string>(), It.IsAny<Action<PatternScanResult>>()))
                .Callback<string, Action<PatternScanResult>>((pattern, callback) =>
                {
                    if (pattern.StartsWith("48 8B C4"))
                        capturedCallback = callback;
                });

            // Act
            scanner.SetupExperimentalHooks(mockStartupScanner.Object, mockHooks.Object);

            // Simulate pattern found
            if (capturedCallback != null)
            {
                var result = new PatternScanResult(0x1000);
                capturedCallback(result); // Should log without throwing
            }

            // Assert - test passes if no exception
            Assert.NotNull(capturedCallback);
        }
    }
}