using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.ThemeEditor;
using Xunit;

namespace FFTColorCustomizer.Tests.ThemeEditor
{
    public class MyThemesPanelTests : IDisposable
    {
        private readonly string _testBasePath;

        public MyThemesPanelTests()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), "MyThemesPanelTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testBasePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBasePath))
            {
                Directory.Delete(_testBasePath, true);
            }
        }

        [Fact]
        [STAThread]
        public void MyThemesPanel_Exists_AndIsAPanel()
        {
            // Arrange & Act
            using var panel = new MyThemesPanel(_testBasePath);

            // Assert
            Assert.NotNull(panel);
            Assert.IsAssignableFrom<Panel>(panel);
        }

        [Fact]
        [STAThread]
        public void MyThemesPanel_DisplaysThemesGroupedByJob()
        {
            // Arrange - create user themes
            var userThemeService = new UserThemeService(_testBasePath);
            userThemeService.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]);
            userThemeService.SaveTheme("Knight_Male", "Crimson Red", new byte[512]);
            userThemeService.SaveTheme("Squire_Male", "Forest Green", new byte[512]);

            // Act
            using var panel = new MyThemesPanel(_testBasePath);

            // Assert - should have a TreeView with job groups
            var treeView = panel.Controls.OfType<TreeView>().FirstOrDefault();
            Assert.NotNull(treeView);

            // Should have 2 job nodes (Knight_Male and Squire_Male)
            Assert.Equal(2, treeView.Nodes.Count);

            // Find Knight_Male node and verify it has 2 themes
            var knightNode = treeView.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text.Contains("Knight"));
            Assert.NotNull(knightNode);
            Assert.Equal(2, knightNode.Nodes.Count);

            // Find Squire_Male node and verify it has 1 theme
            var squireNode = treeView.Nodes.Cast<TreeNode>().FirstOrDefault(n => n.Text.Contains("Squire"));
            Assert.NotNull(squireNode);
            Assert.Equal(1, squireNode.Nodes.Count);
        }

        [Fact]
        [STAThread]
        public void MyThemesPanel_HasDeleteButton()
        {
            // Arrange & Act
            using var panel = new MyThemesPanel(_testBasePath);

            // Assert - search recursively through all controls
            var deleteButton = FindControlRecursive<Button>(panel, b => b.Text == "Delete");
            Assert.NotNull(deleteButton);
        }

        [Fact]
        [STAThread]
        public void DeleteButton_WhenThemeSelected_DeletesThemeAndRefreshesTree()
        {
            // Arrange - create user themes
            var userThemeService = new UserThemeService(_testBasePath);
            userThemeService.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]);
            userThemeService.SaveTheme("Knight_Male", "Crimson Red", new byte[512]);

            using var panel = new MyThemesPanel(_testBasePath);
            var treeView = panel.Controls.OfType<TreeView>().FirstOrDefault();
            var deleteButton = FindControlRecursive<Button>(panel, b => b.Text == "Delete");

            // Select the "Ocean Blue" theme node
            var knightNode = treeView.Nodes.Cast<TreeNode>().First(n => n.Text.Contains("Knight"));
            var oceanBlueNode = knightNode.Nodes.Cast<TreeNode>().First(n => n.Text == "Ocean Blue");
            treeView.SelectedNode = oceanBlueNode;

            // Act - click delete
            deleteButton.PerformClick();

            // Assert - theme should be deleted from service
            var remainingThemes = userThemeService.GetUserThemes("Knight_Male");
            Assert.Single(remainingThemes);
            Assert.Equal("Crimson Red", remainingThemes[0]);

            // Assert - tree should be refreshed (Knight node should have 1 child now)
            var updatedKnightNode = treeView.Nodes.Cast<TreeNode>().First(n => n.Text.Contains("Knight"));
            Assert.Single(updatedKnightNode.Nodes.Cast<TreeNode>());
        }

        [Fact]
        [STAThread]
        public void MyThemesPanel_DisplaysThemeCount()
        {
            // Arrange - create some themes
            var userThemeService = new UserThemeService(_testBasePath);
            userThemeService.SaveTheme("Knight_Male", "Theme1", new byte[512]);
            userThemeService.SaveTheme("Knight_Male", "Theme2", new byte[512]);
            userThemeService.SaveTheme("Squire_Male", "Theme3", new byte[512]);

            // Act
            using var panel = new MyThemesPanel(_testBasePath);

            // Assert - should have a label showing theme count
            var countLabel = FindControlRecursive<Label>(panel, l => l.Text.Contains("3"));
            Assert.NotNull(countLabel);
        }

        [Fact]
        [STAThread]
        public void MyThemesPanel_ShowsWarningAt50PlusThemes()
        {
            // Arrange - create 50 themes
            var userThemeService = new UserThemeService(_testBasePath);
            for (int i = 0; i < 50; i++)
            {
                userThemeService.SaveTheme("Knight_Male", $"Theme{i}", new byte[512]);
            }

            // Act
            using var panel = new MyThemesPanel(_testBasePath);

            // Assert - should have a warning indicator in the label
            var countLabel = FindControlRecursive<Label>(panel, l => l.Text.Contains("50"));
            Assert.NotNull(countLabel);
            Assert.Contains("warning", countLabel.Text.ToLower());
        }

        [Fact]
        [STAThread]
        public void DeleteButton_RaisesThemeDeletedEvent_WithJobName()
        {
            // Arrange - create a user theme
            var userThemeService = new UserThemeService(_testBasePath);
            userThemeService.SaveTheme("Knight_Male", "Ocean Blue", new byte[512]);

            using var panel = new MyThemesPanel(_testBasePath);
            var treeView = panel.Controls.OfType<TreeView>().FirstOrDefault();
            var deleteButton = FindControlRecursive<Button>(panel, b => b.Text == "Delete");

            string deletedJobName = null;
            string deletedThemeName = null;
            panel.ThemeDeleted += (sender, args) =>
            {
                deletedJobName = args.JobName;
                deletedThemeName = args.ThemeName;
            };

            // Select the "Ocean Blue" theme node
            var knightNode = treeView.Nodes.Cast<TreeNode>().First(n => n.Text.Contains("Knight"));
            var oceanBlueNode = knightNode.Nodes.Cast<TreeNode>().First(n => n.Text == "Ocean Blue");
            treeView.SelectedNode = oceanBlueNode;

            // Act - click delete
            deleteButton.PerformClick();

            // Assert - event should have been raised with correct job name
            Assert.Equal("Knight_Male", deletedJobName);
            Assert.Equal("Ocean Blue", deletedThemeName);
        }

        [Fact]
        [STAThread]
        public void MyThemesPanel_HasRefreshMethod_ForExternalUpdates()
        {
            // Arrange
            var userThemeService = new UserThemeService(_testBasePath);
            userThemeService.SaveTheme("Knight_Male", "Theme1", new byte[512]);

            using var panel = new MyThemesPanel(_testBasePath);
            var treeView = panel.Controls.OfType<TreeView>().FirstOrDefault();

            // Verify initial state
            Assert.Single(treeView.Nodes);

            // Add another theme externally
            userThemeService.SaveTheme("Squire_Male", "Theme2", new byte[512]);

            // Act - call refresh
            panel.RefreshThemes();

            // Assert - tree should now show both jobs
            Assert.Equal(2, treeView.Nodes.Count);
        }

        private static T FindControlRecursive<T>(Control parent, Func<T, bool> predicate) where T : Control
        {
            foreach (Control control in parent.Controls)
            {
                if (control is T typedControl && predicate(typedControl))
                    return typedControl;

                var found = FindControlRecursive<T>(control, predicate);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
