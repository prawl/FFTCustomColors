using System;
using System.IO;
using System.Text;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;

namespace FFTColorCustomizer.Tests.Utilities
{
    /// <summary>
    /// Tests to fix and verify ModLogger functionality
    /// </summary>
    [Collection("ModLogger Tests")] // Ensure tests don't run in parallel
    public class ModLoggerRegressionTests : IDisposable
    {
        private readonly StringBuilder _capturedOutput;
        private readonly StringWriter _stringWriter;
        private readonly TextWriter _originalConsoleOut;
        private readonly ILogger _originalLogger;
        private readonly LogLevel _originalLogLevel;

        public ModLoggerRegressionTests()
        {
            // Save original state
            _originalLogger = ModLogger.Instance;
            _originalLogLevel = ModLogger.LogLevel;

            // Capture console output for testing
            _capturedOutput = new StringBuilder();
            _stringWriter = new StringWriter(_capturedOutput);
            _originalConsoleOut = Console.Out;
            Console.SetOut(_stringWriter);

            // Reset ModLogger to ensure clean state
            ModLogger.Reset();

            // Clear any buffered output from previous tests
            _capturedOutput.Clear();
        }

        [Fact]
        public void ModLogger_Should_Output_When_Enabled()
        {
            // Arrange
            ModLogger.LogLevel = LogLevel.Debug; // Enable all logging

            // Act
            ModLogger.Log("Test info message");
            ModLogger.LogDebug("Test debug message");
            ModLogger.LogWarning("Test warning message");
            ModLogger.LogError("Test error message");

            // Assert
            var output = _capturedOutput.ToString();
            output.Should().Contain("Test info message", "Info messages should be logged");
            output.Should().Contain("Test debug message", "Debug messages should be logged");
            output.Should().Contain("Test warning message", "Warning messages should be logged");
            output.Should().Contain("Test error message", "Error messages should be logged");
        }

        [Fact]
        public void ModLogger_Should_Not_Output_When_Disabled()
        {
            // Arrange
            ModLogger.LogLevel = LogLevel.None; // Disable all logging

            // Act
            ModLogger.Log("Should not appear");
            ModLogger.LogDebug("Should not appear");
            ModLogger.LogWarning("Should not appear");
            ModLogger.LogError("Should not appear");

            // Assert
            var output = _capturedOutput.ToString();
            output.Should().BeEmpty("No messages should be logged when LogLevel is None");
        }

        [Fact]
        public void ModLogger_Should_Use_ConsoleLogger_By_Default()
        {
            // Arrange
            ModLogger.Reset(); // Ensure fresh state

            // Act - First access should create ConsoleLogger
            var instance = ModLogger.Instance;

            // Assert
            instance.Should().BeOfType<ConsoleLogger>("Default logger should be ConsoleLogger, not NullLogger");
            instance.LogLevel.Should().Be(LogLevel.Debug, "Default log level should be Debug");
        }

        [Fact]
        public void ModLogger_EnableDebugLogging_Flag_Should_Work()
        {
            // Arrange
            ModLogger.Reset();

            // Act & Assert - Enable debug logging
            ModLogger.EnableDebugLogging = true;
            ModLogger.LogLevel.Should().Be(LogLevel.Debug, "EnableDebugLogging=true should set LogLevel to Debug");

            // Act & Assert - Disable debug logging
            ModLogger.EnableDebugLogging = false;
            ModLogger.LogLevel.Should().Be(LogLevel.Info, "EnableDebugLogging=false should set LogLevel to Info");
        }

        [Fact]
        public void ModLogger_Should_Respect_LogLevel_Filtering()
        {
            // Arrange
            ModLogger.LogLevel = LogLevel.Warning; // Only warnings and errors

            // Act
            ModLogger.Log("Info - should not appear");
            ModLogger.LogDebug("Debug - should not appear");
            ModLogger.LogWarning("Warning - should appear");
            ModLogger.LogError("Error - should appear");

            // Assert
            var output = _capturedOutput.ToString();
            output.Should().NotContain("Info - should not appear");
            output.Should().NotContain("Debug - should not appear");
            output.Should().Contain("Warning - should appear");
            output.Should().Contain("Error - should appear");
        }

        [Fact]
        public void ModLogger_Should_Work_After_UseNullLogger_Then_Reset()
        {
            // Arrange - Clear any previous output and ensure clean state
            _capturedOutput.Clear();
            ModLogger.Reset();

            // Act - Set to NullLogger
            ModLogger.UseNullLogger();

            // Clear any output that might have been buffered before NullLogger was set
            _capturedOutput.Clear();

            ModLogger.Log("This should not appear");

            var nullOutput = _capturedOutput.ToString();
            nullOutput.Should().BeEmpty("NullLogger should not output anything");

            // Act - Reset to get ConsoleLogger back
            ModLogger.Reset();

            // Clear output before testing ConsoleLogger
            _capturedOutput.Clear();

            ModLogger.Log("This should appear");

            // Assert
            var output = _capturedOutput.ToString();
            output.Should().Contain("This should appear", "After reset, ConsoleLogger should work");
        }

        [Fact]
        public void ModLogger_LogSuccess_Should_Add_Checkmark()
        {
            // Arrange
            ModLogger.LogLevel = LogLevel.Info;

            // Act
            ModLogger.LogSuccess("Operation completed");

            // Assert
            var output = _capturedOutput.ToString();
            output.Should().Contain("✓ Operation completed", "LogSuccess should add a checkmark");
        }

        [Fact]
        public void ModLogger_LogSection_Should_Format_Correctly()
        {
            // Arrange
            ModLogger.LogLevel = LogLevel.Info;

            // Act
            ModLogger.LogSection("Test Section");

            // Assert
            var output = _capturedOutput.ToString();
            output.Should().Contain("========================================");
            output.Should().Contain("Test Section");
        }

        [Fact]
        public void ModLogger_Should_Handle_Null_Messages_Gracefully()
        {
            // Arrange
            ModLogger.LogLevel = LogLevel.Debug;

            // Act & Assert - Should not throw
            Action act = () =>
            {
                ModLogger.Log(null);
                ModLogger.LogError(null);
                ModLogger.LogWarning(null);
                ModLogger.LogDebug(null);
            };

            act.Should().NotThrow("ModLogger should handle null messages gracefully");
        }

        [Fact]
        public void ModLogger_DisableLogging_Should_Stop_All_Output()
        {
            // Arrange
            _capturedOutput.Clear();
            ModLogger.Reset();

            // Act - Disable logging
            ModLogger.DisableLogging();

            ModLogger.Log("Should not appear - info");
            ModLogger.LogDebug("Should not appear - debug");
            ModLogger.LogWarning("Should not appear - warning");
            ModLogger.LogError("Should not appear - error");

            // Assert
            var output = _capturedOutput.ToString();
            output.Should().BeEmpty("DisableLogging should prevent all output");
            ModLogger.LogLevel.Should().Be(LogLevel.None, "DisableLogging should set LogLevel to None");
        }

        [Fact]
        public void ModLogger_EnableLogging_Should_Restore_Output()
        {
            // Arrange
            _capturedOutput.Clear();
            ModLogger.Reset();
            ModLogger.DisableLogging();

            // Verify disabled
            ModLogger.Log("Should not appear");
            _capturedOutput.ToString().Should().BeEmpty();

            // Act - Enable logging
            ModLogger.EnableLogging(LogLevel.Info);
            ModLogger.Log("Should appear after enabling");

            // Assert
            var output = _capturedOutput.ToString();
            output.Should().Contain("Should appear after enabling", "EnableLogging should restore output");
            ModLogger.LogLevel.Should().Be(LogLevel.Info, "EnableLogging should set the specified LogLevel");
        }

        [Fact]
        public void ModLogger_Should_Initialize_With_ConsoleLogger_Not_NullLogger()
        {
            // This is the key fix - ensure ModLogger doesn't accidentally use NullLogger by default

            // Arrange
            _capturedOutput.Clear();
            ModLogger.Reset(); // Force re-initialization

            // Act - First use should create ConsoleLogger
            ModLogger.Log("Test message");

            // Assert
            var output = _capturedOutput.ToString();
            output.Should().Contain("Test message",
                "ModLogger should output by default (not be using NullLogger)");

            ModLogger.Instance.Should().BeOfType<ConsoleLogger>(
                "ModLogger should use ConsoleLogger by default, not NullLogger");
        }

        public void Dispose()
        {
            // Restore original console output
            Console.SetOut(_originalConsoleOut);
            _stringWriter?.Dispose();

            // Restore original logger state
            ModLogger.Instance = _originalLogger;
            ModLogger.LogLevel = _originalLogLevel;
        }
    }
}