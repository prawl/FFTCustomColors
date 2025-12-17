using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorMod.Core.ModComponents;
using FFTColorMod.Services;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests.Core.ModComponents
{
    public class HotkeyManagerTests : IDisposable
    {
        private const int VK_F1 = 0x70;
        private const int VK_F3 = 0x72;
        private const int VK_F4 = 0x73;
        private const int VK_F5 = 0x74;
        private const int VK_F10 = 0x79;
        private const int VK_F11 = 0x7A;

        private readonly string _testPath;
        private readonly ThemeManager _themeManager;
        private bool _configUIOpened;
        private bool _colorsReset;
        private int _f3PressCount;
        private int _f4PressCount;
        private int _f5PressCount;
        private readonly HotkeyManager _hotkeyManager;

        public HotkeyManagerTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"HotkeyManagerTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);

            // Create a test theme manager
            _themeManager = new TestThemeManager(_testPath);

            _configUIOpened = false;
            _colorsReset = false;
            _f3PressCount = 0;
            _f4PressCount = 0;
            _f5PressCount = 0;

            _hotkeyManager = new HotkeyManager(
                null,
                _themeManager,
                () => _configUIOpened = true,
                () => _colorsReset = true
            );
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        // Test implementation of ThemeManager for testing purposes
        private class TestThemeManager : ThemeManager
        {
            public int CycleOrlandeauCount { get; private set; }
            public int CycleAgriasCount { get; private set; }
            public int CycleCloudCount { get; private set; }

            public TestThemeManager(string path) : base(path, path)
            {
            }

            public override void CycleOrlandeauTheme()
            {
                CycleOrlandeauCount++;
            }

            public override void CycleAgriasTheme()
            {
                CycleAgriasCount++;
            }

            public override void CycleCloudTheme()
            {
                CycleCloudCount++;
            }
        }

        [Fact]
        public void ProcessHotkeyPress_F1_ShouldOpenConfigUI()
        {
            // F1 now opens the configuration UI
            // Act
            _hotkeyManager.ProcessHotkeyPress(VK_F1);

            // Assert
            _configUIOpened.Should().BeTrue();
        }

        [Fact]
        public void ProcessHotkeyPress_F3_ShouldDoNothing_NoLongerRegistered()
        {
            // F3 is no longer registered
            // Arrange
            var testManager = (TestThemeManager)_themeManager;

            // Act
            _hotkeyManager.ProcessHotkeyPress(VK_F3);

            // Assert - Should do nothing
            testManager.CycleOrlandeauCount.Should().Be(0);
        }

        [Fact]
        public void ProcessHotkeyPress_F4_ShouldDoNothing_NoLongerRegistered()
        {
            // F4 is no longer registered
            // Arrange
            var testManager = (TestThemeManager)_themeManager;

            // Act
            _hotkeyManager.ProcessHotkeyPress(VK_F4);

            // Assert - Should do nothing
            testManager.CycleAgriasCount.Should().Be(0);
        }

        [Fact]
        public void ProcessHotkeyPress_F5_ShouldDoNothing_NoLongerRegistered()
        {
            // F5 is no longer registered
            // Arrange
            var testManager = (TestThemeManager)_themeManager;

            // Act
            _hotkeyManager.ProcessHotkeyPress(VK_F5);

            // Assert - Should do nothing
            testManager.CycleCloudCount.Should().Be(0);
        }

        [Fact]
        public void ProcessHotkeyPress_F10_ShouldDoNothing_NoLongerRegistered()
        {
            // F10 is no longer registered
            // Act
            _hotkeyManager.ProcessHotkeyPress(VK_F10);

            // Assert - Should do nothing
            _configUIOpened.Should().BeFalse();
        }

        [Fact]
        public void ProcessHotkeyPress_F11_ShouldDoNothing_NoLongerRegistered()
        {
            // F11 is no longer registered
            // Act
            _hotkeyManager.ProcessHotkeyPress(VK_F11);

            // Assert - Should do nothing
            _colorsReset.Should().BeFalse();
        }

        [Fact]
        public void ProcessHotkeyPress_UnregisteredKey_ShouldDoNothing()
        {
            // Arrange
            var unregisteredKey = 0xFF;

            // Act & Assert - Should not throw
            _hotkeyManager.Invoking(h => h.ProcessHotkeyPress(unregisteredKey))
                .Should().NotThrow();
        }

        [Fact]
        public void RegisterHotkey_ShouldAddNewAction()
        {
            // Arrange
            var customKey = 0x99;
            var actionExecuted = false;

            // Act
            _hotkeyManager.RegisterHotkey(customKey, () => actionExecuted = true);
            _hotkeyManager.ProcessHotkeyPress(customKey);

            // Assert
            actionExecuted.Should().BeTrue();
        }

        [Fact]
        public void UnregisterHotkey_ShouldRemoveAction()
        {
            // Arrange
            var customKey = 0x99;
            var actionExecuted = false;
            _hotkeyManager.RegisterHotkey(customKey, () => actionExecuted = true);

            // Act
            _hotkeyManager.UnregisterHotkey(customKey);
            _hotkeyManager.ProcessHotkeyPress(customKey);

            // Assert
            actionExecuted.Should().BeFalse();
        }

        [Fact]
        public void IsHotkeyRegistered_ShouldReturnCorrectValue()
        {
            // Assert - F1 is the only registered key
            _hotkeyManager.IsHotkeyRegistered(VK_F1).Should().BeTrue();

            // Assert - F10 is no longer registered
            _hotkeyManager.IsHotkeyRegistered(VK_F10).Should().BeFalse();

            // Assert - Random key is not registered
            _hotkeyManager.IsHotkeyRegistered(0x99).Should().BeFalse();
        }

        [Fact]
        public void Constructor_WithNullOpenConfigUI_ShouldThrowException()
        {
            // Act & Assert
            var action = () => new HotkeyManager(null, _themeManager, null, () => { });

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("openConfigUI");
        }

        [Fact]
        public void Constructor_WithNullResetColors_ShouldThrowException()
        {
            // Act & Assert
            var action = () => new HotkeyManager(null, _themeManager, () => { }, null);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("resetColors");
        }

        [Fact]
        public void RegisterHotkey_WithNullAction_ShouldThrowException()
        {
            // Act & Assert
            var action = () => _hotkeyManager.RegisterHotkey(0x99, null);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("action");
        }
    }
}