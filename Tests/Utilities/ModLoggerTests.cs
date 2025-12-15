using System;
using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests.Utilities
{
    [Collection("Sequential")]  // Ensures tests run sequentially, not in parallel
    public class ModLoggerTests
    {
        private string CaptureConsoleOutput(Action action)
        {
            var originalOutput = Console.Out;
            using (var stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);
                try
                {
                    action();
                    return stringWriter.ToString();
                }
                finally
                {
                    Console.SetOut(originalOutput);
                }
            }
        }

        [Fact]
        public void Log_Should_Write_Message_With_Prefix()
        {
            // Arrange
            var message = "Test message";
            var expectedOutput = "[FFT Color Mod] Test message";

            // Act
            var output = CaptureConsoleOutput(() => ModLogger.Log(message));

            // Assert
            output.Should().Contain(expectedOutput);
        }

        [Fact]
        public void LogError_Should_Write_Error_Message_With_Prefix()
        {
            // Arrange
            var message = "Something went wrong";
            var expectedOutput = "[FFT Color Mod] ERROR: Something went wrong";

            // Act
            var output = CaptureConsoleOutput(() => ModLogger.LogError(message));

            // Assert
            output.Should().Contain(expectedOutput);
        }

        [Fact]
        public void LogWarning_Should_Write_Warning_Message_With_Prefix()
        {
            // Arrange
            var message = "This might be a problem";
            var expectedOutput = "[FFT Color Mod] WARNING: This might be a problem";

            // Act
            var output = CaptureConsoleOutput(() => ModLogger.LogWarning(message));

            // Assert
            output.Should().Contain(expectedOutput);
        }

        [Fact]
        public void LogDebug_Should_Write_Debug_Message_When_Debug_Enabled()
        {
            // Arrange
            var originalDebugSetting = ModLogger.EnableDebugLogging;
            var originalLogLevel = ModLogger.LogLevel;
            ModLogger.EnableDebugLogging = true;
            ModLogger.LogLevel = LogLevel.Debug;  // Ensure log level allows debug messages
            var uniqueMessage = $"Debug_Test_Enabled_{Guid.NewGuid()}"; // Use unique message to avoid conflicts
            var expectedOutput = $"[FFT Color Mod] DEBUG: {uniqueMessage}";

            try
            {
                // Act
                var output = CaptureConsoleOutput(() => ModLogger.LogDebug(uniqueMessage));

                // Assert
                output.Should().Contain(expectedOutput);
            }
            finally
            {
                // Restore original settings
                ModLogger.EnableDebugLogging = originalDebugSetting;
                ModLogger.LogLevel = originalLogLevel;
            }
        }

        [Fact]
        public void LogDebug_Should_Not_Write_When_Debug_Disabled()
        {
            // Arrange
            var originalDebugSetting = ModLogger.EnableDebugLogging;
            ModLogger.EnableDebugLogging = false;
            var uniqueMessage = $"Debug_Test_{Guid.NewGuid()}"; // Use unique message to avoid conflicts

            try
            {
                // Act
                var output = CaptureConsoleOutput(() => ModLogger.LogDebug(uniqueMessage));

                // Assert - Check that our specific message is not in the output
                output.Should().NotContain(uniqueMessage, "debug messages should not be logged when debug is disabled");
            }
            finally
            {
                // Restore original setting
                ModLogger.EnableDebugLogging = originalDebugSetting;
            }
        }

        [Fact]
        public void LogException_Should_Format_Exception_With_StackTrace()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            var output = CaptureConsoleOutput(() => ModLogger.LogException("An error occurred", exception));

            // Assert
            output.Should().Contain("[FFT Color Mod] ERROR: An error occurred");
            output.Should().Contain("InvalidOperationException: Test exception");
        }

        [Fact]
        public void Log_With_Format_Should_Support_String_Interpolation()
        {
            // Arrange
            var value = 42;
            var name = "test";
            var expectedOutput = "[FFT Color Mod] Processed 42 items for test";

            // Act
            var output = CaptureConsoleOutput(() => ModLogger.Log($"Processed {value} items for {name}"));

            // Assert
            output.Should().Contain(expectedOutput);
        }

        [Fact]
        public void LogSection_Should_Create_Formatted_Section_Header()
        {
            // Arrange
            var sectionName = "Configuration Loading";

            // Act
            var output = CaptureConsoleOutput(() => ModLogger.LogSection(sectionName));

            // Assert
            output.Should().Contain("========================================");
            output.Should().Contain("Configuration Loading");
        }

        [Fact]
        public void LogSuccess_Should_Write_Success_Message_With_Prefix()
        {
            // Arrange
            var message = "Operation completed successfully";
            var expectedOutput = "[FFT Color Mod] ✓ Operation completed successfully";

            // Act
            var output = CaptureConsoleOutput(() => ModLogger.LogSuccess(message));

            // Assert
            output.Should().Contain(expectedOutput);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Log_Should_Handle_Empty_Or_Null_Messages_Gracefully(string message)
        {
            // Act & Assert - Should not throw
            var exception = Record.Exception(() => ModLogger.Log(message));
            exception.Should().BeNull();
        }

        [Fact]
        public void SetLogLevel_Should_Control_Which_Messages_Are_Logged()
        {
            // Arrange
            var originalLogLevel = ModLogger.LogLevel;
            ModLogger.LogLevel = LogLevel.Warning;

            try
            {
                // Act
                var output = CaptureConsoleOutput(() =>
                {
                    ModLogger.LogDebug("Debug message");
                    ModLogger.Log("Info message");
                    ModLogger.LogWarning("Warning message");
                    ModLogger.LogError("Error message");
                });

                // Assert
                output.Should().NotContain("Debug message");
                output.Should().NotContain("Info message");
                output.Should().Contain("Warning message");
                output.Should().Contain("Error message");
            }
            finally
            {
                // Restore original log level
                ModLogger.LogLevel = originalLogLevel;
            }
        }
    }
}