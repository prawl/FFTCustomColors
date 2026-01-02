using Xunit;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Configuration.UI;

namespace Tests.ThemeEditor;

public class ThemeEditorUnsavedChangesTests
{
    [Fact]
    public void HasUnsavedChanges_InitiallyFalse()
    {
        // Arrange
        var panel = new ThemeEditorPanel();

        // Act
        var hasChanges = panel.HasUnsavedChanges;

        // Assert
        Assert.False(hasChanges);
    }

    [Fact]
    public void MarkAsModified_SetsHasUnsavedChangesToTrue()
    {
        // Arrange
        var panel = new ThemeEditorPanel();

        // Act
        panel.MarkAsModified();

        // Assert
        Assert.True(panel.HasUnsavedChanges);
    }

    [Fact]
    public void ClearModified_SetsHasUnsavedChangesToFalse()
    {
        // Arrange
        var panel = new ThemeEditorPanel();
        panel.MarkAsModified();

        // Act
        panel.ClearModified();

        // Assert
        Assert.False(panel.HasUnsavedChanges);
    }
}
