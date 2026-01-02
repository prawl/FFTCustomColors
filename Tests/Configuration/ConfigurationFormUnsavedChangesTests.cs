using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.ThemeEditor;

namespace Tests.Configuration;

public class ConfigurationFormUnsavedChangesTests
{
    [Fact]
    public void CanCheckThemeEditorForUnsavedChanges()
    {
        // Arrange
        var themeEditorPanel = new ThemeEditorPanel();
        themeEditorPanel.MarkAsModified();

        // Act
        var hasChanges = themeEditorPanel.HasUnsavedChanges;

        // Assert
        Assert.True(hasChanges);
    }

    [Fact]
    public void ConfigurationForm_HasThemeEditorPanelProperty()
    {
        // Arrange
        var config = new Config();

        // Act
        var form = new ConfigurationForm(config);

        // Assert
        Assert.NotNull(form.ThemeEditorPanel);
    }

    [Fact]
    public void FormClosing_ShowsWarning_WhenThemeEditorHasUnsavedChanges()
    {
        // Arrange
        var config = new Config();
        var form = new ConfigurationForm(config);
        form.ThemeEditorPanel?.MarkAsModified();

        // Act
        var shouldWarn = form.ShouldWarnAboutUnsavedChanges();

        // Assert
        Assert.True(shouldWarn);
    }
}
