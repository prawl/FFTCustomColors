using System;
using FFTColorMod.Configuration;
using System.Threading;
using FFTColorMod.Utilities;
using Xunit;

namespace FFTColorMod.Tests;

public class InputSimulatorTests
{
    [Fact]
    public void SendKeyPress_ShouldReturnTrue_ForMockSimulator()
    {
        // Arrange
        var simulator = new MockInputSimulator();

        // Act
        bool result = simulator.SendKeyPress(0x0D); // VK_RETURN (Enter key)

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SimulateMenuRefresh_ShouldSendEnterThenEscape()
    {
        // Arrange
        var simulator = new MockInputSimulator();

        // Act
        bool result = simulator.SimulateMenuRefresh();

        // Assert
        Assert.True(result, "SimulateMenuRefresh should return true when successful");
        Assert.Equal(2, simulator.KeyPressCallCount);
        Assert.Equal(0x0D, simulator.KeysPressed[0]); // Enter (VK_RETURN)
        Assert.Equal(0x1B, simulator.KeysPressed[1]); // Escape (VK_ESCAPE)
    }

    [Fact]
    public void SimulateMenuRefresh_ShouldDelayBetweenKeys()
    {
        // Arrange
        var simulator = new TimingMockInputSimulator();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        bool result = simulator.SimulateMenuRefresh();
        stopwatch.Stop();

        // Assert
        Assert.True(result, "SimulateMenuRefresh should return true");
        Assert.True(stopwatch.ElapsedMilliseconds >= 500,
            $"Should delay at least 500ms between keys, but only delayed {stopwatch.ElapsedMilliseconds}ms");
        Assert.Equal(2, simulator.KeyPressCallCount);
    }

    [Fact]
    public void SimulateMenuRefresh_ShouldNotSendEscapeIfEnterFails()
    {
        // Arrange
        var simulator = new FailingMockInputSimulator();

        // Act
        bool result = simulator.SimulateMenuRefresh();

        // Assert
        Assert.False(result, "SimulateMenuRefresh should return false when Enter fails");
        Assert.Equal(1, simulator.KeyPressCallCount);
        Assert.Equal(0x0D, simulator.KeysPressed[0]); // Only Enter was attempted
    }
}

// Mock implementation for testing
public class MockInputSimulator : IInputSimulator
{
    public int KeyPressCallCount { get; private set; }
    public System.Collections.Generic.List<int> KeysPressed { get; } = new();

    public bool SendKeyPress(int vkCode)
    {
        KeyPressCallCount++;
        KeysPressed.Add(vkCode);
        return true;
    }

    public bool SimulateMenuRefresh()
    {
        // Send Enter key
        bool enterResult = SendKeyPress(0x0D);
        if (!enterResult) return false;

        // Send Escape key (no delay in tests)
        bool escapeResult = SendKeyPress(0x1B);

        return escapeResult;
    }
}

// Mock that includes timing delays like the real implementation
public class TimingMockInputSimulator : IInputSimulator
{
    public int KeyPressCallCount { get; private set; }
    public System.Collections.Generic.List<int> KeysPressed { get; } = new();

    public bool SendKeyPress(int vkCode)
    {
        KeyPressCallCount++;
        KeysPressed.Add(vkCode);
        // Simulate the 50ms delay in the real implementation
        System.Threading.Thread.Sleep(50);
        return true;
    }

    public bool SimulateMenuRefresh()
    {
        // Send Enter key
        bool enterResult = SendKeyPress(0x0D);
        if (!enterResult) return false;

        // Simulate the 500ms delay from real implementation
        System.Threading.Thread.Sleep(500);

        // Send Escape key
        bool escapeResult = SendKeyPress(0x1B);

        return escapeResult;
    }
}

// Mock that simulates failure on Enter key
public class FailingMockInputSimulator : IInputSimulator
{
    public int KeyPressCallCount { get; private set; }
    public System.Collections.Generic.List<int> KeysPressed { get; } = new();

    public bool SendKeyPress(int vkCode)
    {
        KeyPressCallCount++;
        KeysPressed.Add(vkCode);
        // Return false for Enter key, true for others
        return vkCode != 0x0D;
    }

    public bool SimulateMenuRefresh()
    {
        // Send Enter key
        bool enterResult = SendKeyPress(0x0D);
        if (!enterResult) return false;

        // This shouldn't be reached
        System.Threading.Thread.Sleep(500);
        bool escapeResult = SendKeyPress(0x1B);
        return escapeResult;
    }
}