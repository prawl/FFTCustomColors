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
    /// Properly isolated tests for ModLogger functionality.
    /// These tests demonstrate that the ModLogger works correctly when used properly.
    /// </summary>
    [Collection("Isolated ModLogger Tests")]
    public class ModLoggerFixedTests
    {
        [Fact]
        public void ModLogger_DisableLogging_Prevents_All_Output()
        {
            // This test demonstrates the proper way to disable ModLogger output

            // Arrange - Capture output in isolated context
            var output = CaptureOutput(() =>
            {
                // Save current state
                var originalLevel = ModLogger.LogLevel;
                var originalInstance = ModLogger.Instance;

                try
                {
                    // Set up a fresh logger instance for this test
                    ModLogger.Instance = new ConsoleLogger("[FFT Color Mod]");

                    // Act - Disable logging
                    ModLogger.DisableLogging();

                    // These should not produce output
                    ModLogger.Log("Should not appear - info");
                    ModLogger.LogDebug("Should not appear - debug");
                    ModLogger.LogWarning("Should not appear - warning");
                    ModLogger.LogError("Should not appear - error");
                }
                finally
                {
                    // Restore original state
                    ModLogger.Instance = originalInstance;
                    ModLogger.LogLevel = originalLevel;
                }
            });

            // Assert - check that our specific messages don't appear
            output.Should().NotContain("Should not appear", "DisableLogging should prevent all output");
        }

        [Fact]
        public void ModLogger_EnableLogging_Restores_Output()
        {
            // This test demonstrates enabling logging after it was disabled

            // Arrange & Act
            var output = CaptureOutput(() =>
            {
                var originalLevel = ModLogger.LogLevel;

                try
                {
                    // First disable
                    ModLogger.DisableLogging();
                    ModLogger.Log("This should not appear");

                    // Then enable
                    ModLogger.EnableLogging(LogLevel.Info);
                    ModLogger.Log("This should appear");
                }
                finally
                {
                    ModLogger.LogLevel = originalLevel;
                }
            });

            // Assert
            output.Should().NotContain("This should not appear");
            output.Should().Contain("This should appear");
        }

        [Fact]
        public void ModLogger_Respects_LogLevel_Settings()
        {
            // This test demonstrates LogLevel filtering

            var output = CaptureOutput(() =>
            {
                var originalLevel = ModLogger.LogLevel;

                try
                {
                    // Set to Warning level - only warnings and errors should show
                    ModLogger.LogLevel = LogLevel.Warning;

                    ModLogger.LogDebug("Debug - should not appear");
                    ModLogger.Log("Info - should not appear");
                    ModLogger.LogWarning("Warning - should appear");
                    ModLogger.LogError("Error - should appear");
                }
                finally
                {
                    ModLogger.LogLevel = originalLevel;
                }
            });

            // Assert
            output.Should().NotContain("Debug - should not appear");
            output.Should().NotContain("Info - should not appear");
            output.Should().Contain("Warning - should appear");
            output.Should().Contain("Error - should appear");
        }

        [Fact]
        public void ModLogger_Works_By_Default()
        {
            // This test verifies that ModLogger outputs by default

            var output = CaptureOutput(() =>
            {
                // ModLogger should work out of the box
                ModLogger.Log("Default test message");
            });

            // Assert
            output.Should().Contain("Default test message",
                "ModLogger should output by default without any configuration");
        }

        [Fact]
        public void ModLogger_EnableDebugLogging_Flag_Works()
        {
            // Test the EnableDebugLogging convenience property

            var originalLevel = ModLogger.LogLevel;
            var originalDebugFlag = ModLogger.EnableDebugLogging;

            try
            {
                // Enable debug logging
                ModLogger.EnableDebugLogging = true;
                ModLogger.LogLevel.Should().Be(LogLevel.Debug);

                // Disable debug logging
                ModLogger.EnableDebugLogging = false;
                ModLogger.LogLevel.Should().Be(LogLevel.Info);
            }
            finally
            {
                // Restore original state
                ModLogger.EnableDebugLogging = originalDebugFlag;
                ModLogger.LogLevel = originalLevel;
            }
        }

        [Fact]
        public void ModLogger_NullLogger_Integration()
        {
            // Test using NullLogger explicitly

            var originalInstance = ModLogger.Instance;

            try
            {
                // Set to NullLogger
                ModLogger.UseNullLogger();

                var output = CaptureOutput(() =>
                {
                    ModLogger.Log("Should not appear with NullLogger");
                });

                output.Should().BeEmpty("NullLogger should not produce any output");
            }
            finally
            {
                // Restore original logger
                ModLogger.Instance = originalInstance;
            }
        }

        // Helper method to capture console output in an isolated way
        private string CaptureOutput(Action action)
        {
            var originalOut = Console.Out;
            var stringBuilder = new StringBuilder();

            try
            {
                using (var stringWriter = new StringWriter(stringBuilder))
                {
                    Console.SetOut(stringWriter);
                    action();
                    stringWriter.Flush();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            return stringBuilder.ToString();
        }
    }

    /// <summary>
    /// Collection definition to ensure these tests run in isolation
    /// </summary>
    [CollectionDefinition("Isolated ModLogger Tests")]
    public class IsolatedModLoggerTestsCollection : ICollectionFixture<ModLoggerTestFixture>
    {
    }

    /// <summary>
    /// Fixture to ensure ModLogger state is properly managed across tests
    /// </summary>
    public class ModLoggerTestFixture : IDisposable
    {
        private readonly ILogger _originalInstance;
        private readonly LogLevel _originalLevel;

        public ModLoggerTestFixture()
        {
            // Save original state
            _originalInstance = ModLogger.Instance;
            _originalLevel = ModLogger.LogLevel;

            // Ensure clean state for tests
            ModLogger.Reset();
        }

        public void Dispose()
        {
            // Restore original state
            ModLogger.Instance = _originalInstance;
            ModLogger.LogLevel = _originalLevel;
        }
    }
}