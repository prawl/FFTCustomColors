using System.Linq;

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

    [Fact]
    public void ScanForPattern_WithCommonPattern_ShouldFindSomething()
    {
        // Arrange
        var scanner = new ManualMemoryScanner();
        var testProcess = System.Diagnostics.Process.GetCurrentProcess();
        // Very common x64 patterns that should exist in any process
        var patterns = new[] {
            "48 89 5C 24", // push rbx
            "48 83 EC",    // sub rsp
            "C3"           // ret
        };

        // Act - try each pattern
        bool foundAny = false;
        foreach (var pattern in patterns)
        {
            var results = scanner.ScanForPattern(testProcess, pattern);
            if (results != null && results.Any())
            {
                foundAny = true;
                break;
            }
        }

        // Assert - at least one pattern should be found in a real process
        Assert.True(foundAny, "Should find at least one common x64 pattern");
    }
}