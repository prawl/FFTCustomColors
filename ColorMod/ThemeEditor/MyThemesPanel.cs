using System;
using System.Windows.Forms;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Event args for when a theme is deleted.
    /// </summary>
    public class ThemeDeletedEventArgs : EventArgs
    {
        public string JobName { get; }
        public string ThemeName { get; }

        public ThemeDeletedEventArgs(string jobName, string themeName)
        {
            JobName = jobName;
            ThemeName = themeName;
        }
    }

    /// <summary>
    /// Panel for managing user-created themes - view, export, and delete.
    /// </summary>
    public class MyThemesPanel : Panel
    {
        /// <summary>
        /// Raised when a theme is deleted, so external components can refresh their theme lists.
        /// </summary>
        public event EventHandler<ThemeDeletedEventArgs> ThemeDeleted;
        private readonly string _basePath;
        private readonly UserThemeService _userThemeService;
        private TreeView _themesTreeView;
        private Button _deleteButton;
        private Label _themeCountLabel;

        public MyThemesPanel(string basePath)
        {
            _basePath = basePath;
            _userThemeService = new UserThemeService(basePath);
            MinimumSize = new System.Drawing.Size(0, 200);
            InitializeComponents();
            LoadThemes();
        }

        private void InitializeComponents()
        {
            // Button panel at bottom
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };

            _deleteButton = new Button
            {
                Text = "Delete",
                Width = 80,
                Height = 30
            };
            _deleteButton.Click += OnDeleteClick;
            buttonPanel.Controls.Add(_deleteButton);

            _themesTreeView = new TreeView
            {
                Name = "ThemesTreeView",
                Dock = DockStyle.Fill
            };

            _themeCountLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 20
            };

            Controls.Add(_themesTreeView);
            Controls.Add(_themeCountLabel);
            Controls.Add(buttonPanel);
        }

        private void LoadThemes()
        {
            _themesTreeView.Nodes.Clear();

            var allThemes = _userThemeService.GetAllUserThemes();
            var totalCount = 0;
            foreach (var jobEntry in allThemes)
            {
                var jobName = jobEntry.Key;
                var themes = jobEntry.Value;

                // Create job node with display name
                var displayName = jobName.Replace("_", " ");
                var jobNode = new TreeNode(displayName);

                // Add theme nodes with job name stored in Tag
                foreach (var themeName in themes)
                {
                    var themeNode = new TreeNode(themeName) { Tag = jobName };
                    jobNode.Nodes.Add(themeNode);
                    totalCount++;
                }

                _themesTreeView.Nodes.Add(jobNode);
            }

            if (totalCount >= 50)
            {
                _themeCountLabel.Text = $"{totalCount} themes (warning: storage limit approaching)";
            }
            else
            {
                _themeCountLabel.Text = $"{totalCount} themes";
            }
            _themesTreeView.ExpandAll();
        }

        private void OnDeleteClick(object sender, EventArgs e)
        {
            ModLogger.Log($"[MY_THEMES] OnDeleteClick called");
            var selectedNode = _themesTreeView.SelectedNode;
            ModLogger.Log($"[MY_THEMES] SelectedNode: {selectedNode?.Text}, Tag: {selectedNode?.Tag}");
            if (selectedNode?.Tag is string jobName)
            {
                var themeName = selectedNode.Text;
                ModLogger.Log($"[MY_THEMES] Deleting theme: {themeName} for job: {jobName}");
                _userThemeService.DeleteTheme(jobName, themeName);
                LoadThemes();
                ModLogger.Log($"[MY_THEMES] Raising ThemeDeleted event, subscribers: {(ThemeDeleted != null ? "yes" : "no")}");
                ThemeDeleted?.Invoke(this, new ThemeDeletedEventArgs(jobName, themeName));
                ModLogger.Log($"[MY_THEMES] ThemeDeleted event raised");
            }
            else
            {
                ModLogger.Log($"[MY_THEMES] No theme node selected (Tag is not a string)");
            }
        }

        /// <summary>
        /// Refreshes the theme list from disk. Call this after themes are added/removed externally.
        /// </summary>
        public void RefreshThemes()
        {
            LoadThemes();
        }
    }
}
