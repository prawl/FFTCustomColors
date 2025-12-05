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
        manager.SavePreferences(ColorScheme.OceanBlue);

        // Assert
        Assert.True(File.Exists(tempPath));

        // Cleanup
        File.Delete(tempPath);
    }

    [Fact]
    public void SavePreferences_ShouldWriteColorSchemeToFile()
    {
        // Arrange
        var tempPath = Path.GetTempFileName();
        var manager = new ColorPreferencesManager(tempPath);

        // Act
        manager.SavePreferences(ColorScheme.WhiteSilver);

        // Assert
        var content = File.ReadAllText(tempPath);
        Assert.Contains("WhiteSilver", content);

        // Cleanup
        File.Delete(tempPath);
    }

    [Fact]
    public void LoadPreferences_ShouldReturnSavedColorScheme()
    {
        // Arrange
        var tempPath = Path.GetTempFileName();
        var manager = new ColorPreferencesManager(tempPath);
        manager.SavePreferences(ColorScheme.OceanBlue);

        // Act
        var loadedScheme = manager.LoadPreferences();

        // Assert
        Assert.Equal(ColorScheme.OceanBlue, loadedScheme);

        // Cleanup
        File.Delete(tempPath);
    }

    [Fact]
    public void LoadPreferences_WhenFileDoesNotExist_ShouldReturnDefaultColorScheme()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var manager = new ColorPreferencesManager(tempPath);

        // Act
        var loadedScheme = manager.LoadPreferences();

        // Assert
        Assert.Equal(ColorScheme.Original, loadedScheme);
    }

    [Fact]
    public void SaveCharacterPreference_ShouldSaveColorSchemeForSpecificCharacter()
    {
        // Arrange
        var tempPath = Path.GetTempFileName();
        var manager = new ColorPreferencesManager(tempPath);

        // Act
        manager.SaveCharacterPreference("Ramza", ColorScheme.OceanBlue);
        manager.SaveCharacterPreference("Agrias", ColorScheme.WhiteSilver);

        // Assert
        var ramzaColor = manager.LoadCharacterPreference("Ramza");
        var agriasColor = manager.LoadCharacterPreference("Agrias");

        Assert.Equal(ColorScheme.OceanBlue, ramzaColor);
        Assert.Equal(ColorScheme.WhiteSilver, agriasColor);

        // Cleanup
        File.Delete(tempPath);
    }

    [Fact]
    public void SaveCharacterPreference_ShouldPersistMultipleCharactersAfterUpdates()
    {
        // Arrange
        var tempPath = Path.GetTempFileName();
        var manager = new ColorPreferencesManager(tempPath);

        // Act - Save initial preferences
        manager.SaveCharacterPreference("Ramza", ColorScheme.OceanBlue);
        manager.SaveCharacterPreference("Agrias", ColorScheme.WhiteSilver);
        manager.SaveCharacterPreference("Delita", ColorScheme.OceanBlue);

        // Update one character's preference
        manager.SaveCharacterPreference("Ramza", ColorScheme.DeepPurple);

        // Assert - All preferences should be correct
        Assert.Equal(ColorScheme.DeepPurple, manager.LoadCharacterPreference("Ramza"));
        Assert.Equal(ColorScheme.WhiteSilver, manager.LoadCharacterPreference("Agrias"));
        Assert.Equal(ColorScheme.OceanBlue, manager.LoadCharacterPreference("Delita"));

        // Create new manager instance to verify persistence
        var newManager = new ColorPreferencesManager(tempPath);
        Assert.Equal(ColorScheme.DeepPurple, newManager.LoadCharacterPreference("Ramza"));
        Assert.Equal(ColorScheme.WhiteSilver, newManager.LoadCharacterPreference("Agrias"));
        Assert.Equal(ColorScheme.OceanBlue, newManager.LoadCharacterPreference("Delita"));

        // Cleanup
        File.Delete(tempPath);
    }

    [Fact]
    public void LoadCharacterPreference_WhenCharacterNotSaved_ShouldReturnDefault()
    {
        // Arrange
        var tempPath = Path.GetTempFileName();
        var manager = new ColorPreferencesManager(tempPath);

        // Save preference for one character
        manager.SaveCharacterPreference("Ramza", ColorScheme.OceanBlue);

        // Act - Load preference for character that was never saved
        var mustadioColor = manager.LoadCharacterPreference("Mustadio");
        var orleanduColor = manager.LoadCharacterPreference("Orleandu");

        // Assert - Should return default (Original) for unsaved characters
        Assert.Equal(ColorScheme.Original, mustadioColor);
        Assert.Equal(ColorScheme.Original, orleanduColor);

        // Ramza should still have his saved preference
        Assert.Equal(ColorScheme.OceanBlue, manager.LoadCharacterPreference("Ramza"));

        // Cleanup
        File.Delete(tempPath);
    }

    [Fact]
    public void Startup_ShouldExposeColorPreferencesManager()
    {
        // Arrange & Act
        var startup = new Startup();

        // Assert - Verify that Startup exposes a ColorPreferencesManager
        Assert.NotNull(startup.ColorPreferencesManager);
    }
}