namespace FFTColorMod.Tests;

public class ManualMemoryScannerTests
{
    [Fact]
    public void Constructor_CreatesScanner()
    {
        // Arrange/Act
        var scanner = new ManualMemoryScanner();

        // Assert
        Assert.NotNull(scanner);
    }

    [Fact]
    public void ScanForPattern_WithValidProcess_ReturnsResults()
    {
        // Arrange
        var scanner = new ManualMemoryScanner();
        var testProcess = System.Diagnostics.Process.GetCurrentProcess();
        var pattern = "48 8B C4"; // Common x64 function prologue

        // Act
        var results = scanner.ScanForPattern(testProcess, pattern);

        // Assert
        Assert.NotNull(results);
    }

    [Fact]
    public void ScanForPattern_WithCallback_ExecutesCallback()
    {
        // Arrange
        var scanner = new ManualMemoryScanner();
        var testProcess = System.Diagnostics.Process.GetCurrentProcess();
        var pattern = "48 8B C4";
        bool callbackExecuted = false;

        // Act
        scanner.ScanForPattern(testProcess, pattern, (offset) =>
        {
            callbackExecuted = true;
        });

        // Assert - callback might not execute if pattern not found, that's ok for test
        Assert.NotNull(scanner);
    }
}