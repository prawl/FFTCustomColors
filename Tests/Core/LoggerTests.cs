using System;
using System.Collections.Generic;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using Xunit;

namespace FFTColorCustomizer.Tests.Core
{
    public class LoggerTests
    {
        [Fact]
        public void Logger_ShouldImplementILogger()
        {
            // Arrange
            var logger = new TestLogger();

            // Assert
            Assert.IsAssignableFrom<ILogger>(logger);
        }

        [Fact]
        public void Log_ShouldRecordInfoMessages()
        {
            // Arrange
            var logger = new TestLogger();

            // Act
            logger.Log("Test message");

            // Assert
            Assert.Single(logger.Messages);
            Assert.Contains("[INFO] Test message", logger.Messages[0]);
        }

        [Fact]
        public void LogError_ShouldRecordErrorMessages()
        {
            // Arrange
            var logger = new TestLogger();

            // Act
            logger.LogError("Error occurred");

            // Assert
            Assert.Single(logger.Messages);
            Assert.Contains("[ERROR] Error occurred", logger.Messages[0]);
        }

        [Fact]
        public void LogWarning_ShouldRecordWarningMessages()
        {
            // Arrange
            var logger = new TestLogger();

            // Act
            logger.LogWarning("Warning message");

            // Assert
            Assert.Single(logger.Messages);
            Assert.Contains("[WARNING] Warning message", logger.Messages[0]);
        }

        [Fact]
        public void LogDebug_ShouldRecordDebugMessages()
        {
            // Arrange
            var logger = new TestLogger();

            // Act
            logger.LogDebug("Debug info");

            // Assert
            Assert.Single(logger.Messages);
            Assert.Contains("[DEBUG] Debug info", logger.Messages[0]);
        }

        [Fact]
        public void LogException_ShouldRecordExceptionDetails()
        {
            // Arrange
            var logger = new TestLogger();
            var exception = new InvalidOperationException("Test exception");

            // Act
            logger.LogError("Operation failed", exception);

            // Assert
            Assert.Equal(2, logger.Messages.Count);
            Assert.Contains("[ERROR] Operation failed", logger.Messages[0]);
            Assert.Contains("InvalidOperationException: Test exception", logger.Messages[1]);
        }

        [Fact]
        public void LogLevel_ShouldFilterMessages()
        {
            // Arrange
            var logger = new TestLogger { LogLevel = LogLevel.Warning };

            // Act
            logger.LogDebug("Debug");  // Should be filtered
            logger.Log("Info");        // Should be filtered
            logger.LogWarning("Warn"); // Should be recorded
            logger.LogError("Error");  // Should be recorded

            // Assert
            Assert.Equal(2, logger.Messages.Count);
            Assert.Contains("[WARNING] Warn", logger.Messages[0]);
            Assert.Contains("[ERROR] Error", logger.Messages[1]);
        }

        [Fact]
        public void NullLogger_ShouldNotThrow()
        {
            // Arrange
            var logger = new TestNullLogger();

            // Act & Assert - should not throw
            logger.Log("Test");
            logger.LogError("Error");
            logger.LogWarning("Warning");
            logger.LogDebug("Debug");
            logger.LogError("Error", new Exception("test"));
        }
    }

    // Test implementation for verification
    internal class TestLogger : ILogger
    {
        public List<string> Messages { get; } = new List<string>();
        public LogLevel LogLevel { get; set; } = LogLevel.Debug;

        public void Log(string message)
        {
            if (LogLevel <= LogLevel.Info)
                Messages.Add($"[INFO] {message}");
        }

        public void LogError(string message)
        {
            if (LogLevel <= LogLevel.Error)
                Messages.Add($"[ERROR] {message}");
        }

        public void LogError(string message, Exception exception)
        {
            LogError(message);
            if (exception != null && LogLevel <= LogLevel.Error)
                Messages.Add($"  {exception.GetType().Name}: {exception.Message}");
        }

        public void LogWarning(string message)
        {
            if (LogLevel <= LogLevel.Warning)
                Messages.Add($"[WARNING] {message}");
        }

        public void LogDebug(string message)
        {
            if (LogLevel <= LogLevel.Debug)
                Messages.Add($"[DEBUG] {message}");
        }
    }

    // Null object pattern implementation for testing
    internal class TestNullLogger : ILogger
    {
        public LogLevel LogLevel { get; set; } = LogLevel.None;

        public void Log(string message) { }
        public void LogError(string message) { }
        public void LogError(string message, Exception exception) { }
        public void LogWarning(string message) { }
        public void LogDebug(string message) { }
    }
}
