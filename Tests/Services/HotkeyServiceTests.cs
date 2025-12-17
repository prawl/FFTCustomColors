using System;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using FluentAssertions;
using Xunit;

namespace FFTColorCustomizer.Tests.Services
{
    public class HotkeyServiceTests
    {
        [Fact]
        public void HandleF1Press_Should_Always_Call_OpenConfigUI_Action()
        {
            // Arrange
            bool openConfigUICalled = false;
            bool eventHandlerCalled = false;

            // Create a mock action that doesn't open actual UI
            Action mockOpenConfigUI = () => { openConfigUICalled = true; };

            var hotkeyService = new HotkeyService(
                setColorScheme: _ => { },
                cycleNextScheme: () => "test",
                cyclePreviousScheme: () => "test",
                cycleOrlandeauTheme: () => { },
                cycleAgriasTheme: () => { },
                cycleCloudTheme: () => { },
                openConfigUI: mockOpenConfigUI,
                inputSimulator: null);

            // Subscribe to the event
            hotkeyService.ConfigUIRequested += () => { eventHandlerCalled = true; };

            // Act
            hotkeyService.ProcessHotkeyPress(0x70); // F1 key

            // Assert
            openConfigUICalled.Should().BeTrue("OpenConfigUI action should always be called when F1 is pressed");
            eventHandlerCalled.Should().BeTrue("Event handlers should also be invoked if present");
        }

        [Fact]
        public void HandleF1Press_Should_Call_OpenConfigUI_Even_Without_EventHandlers()
        {
            // Arrange
            bool openConfigUICalled = false;

            var hotkeyService = new HotkeyService(
                setColorScheme: _ => { },
                cycleNextScheme: () => "test",
                cyclePreviousScheme: () => "test",
                cycleOrlandeauTheme: () => { },
                cycleAgriasTheme: () => { },
                cycleCloudTheme: () => { },
                openConfigUI: () => { openConfigUICalled = true; },
                inputSimulator: null);

            // Don't subscribe any event handlers

            // Act
            hotkeyService.ProcessHotkeyPress(0x70); // F1 key

            // Assert
            openConfigUICalled.Should().BeTrue("OpenConfigUI action should be called even without event handlers");
        }

        [Fact]
        public void HandleF1Press_Should_Not_Skip_OpenConfigUI_When_Event_Has_Handlers()
        {
            // This test ensures the bug doesn't regress - where having event handlers
            // would cause the OpenConfigUI action to be skipped

            // Arrange
            int openConfigUICallCount = 0;
            int eventHandlerCallCount = 0;

            var hotkeyService = new HotkeyService(
                setColorScheme: _ => { },
                cycleNextScheme: () => "test",
                cyclePreviousScheme: () => "test",
                cycleOrlandeauTheme: () => { },
                cycleAgriasTheme: () => { },
                cycleCloudTheme: () => { },
                openConfigUI: () => { openConfigUICallCount++; },
                inputSimulator: null);

            // Add multiple event handlers to ensure it still works
            hotkeyService.ConfigUIRequested += () => { eventHandlerCallCount++; };
            hotkeyService.ConfigUIRequested += () => { eventHandlerCallCount++; };

            // Act
            hotkeyService.ProcessHotkeyPress(0x70); // F1 key

            // Assert
            openConfigUICallCount.Should().Be(1, "OpenConfigUI should be called exactly once");
            eventHandlerCallCount.Should().Be(2, "Both event handlers should be called");
        }
    }
}
