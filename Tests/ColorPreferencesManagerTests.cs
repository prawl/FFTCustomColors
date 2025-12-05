namespace FFTColorMod.Tests;

public class ColorPreferencesManagerTests
{
    [Fact]
    public void SavePreferences_ShouldCreateConfigFile()
    {
        // Arrange
        var tempPath = Path.GetTempFileName();
        File.Delete(tempPath); // Ensure it doesn't exist
        var manager = new ColorPreferencesManager(tempPath);

        // Act
        manager.SavePreferences(ColorScheme.Blue);

        // Assert
        Assert.True(File.Exists(tempPath));

        // Cleanup
        File.Delete(tempPath);
    }
}