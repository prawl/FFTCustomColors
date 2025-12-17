using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Refactored configuration form using partial classes and helper components
    /// </summary>
    public partial class ConfigurationForm : Form
    {
        private Config _config;
        private string _configPath;
        private string _modPath;
        private TableLayoutPanel _mainPanel;
        private Button _saveButton;
        private Button _cancelButton;
        private Button _resetAllButton;
        private CustomTitleBar _titleBar;
        private PreviewImageManager _previewManager;

        private bool _isFullyLoaded = false;
        private bool _isInitializing = true;
        private bool _genericCharactersCollapsed = false;
        private List<Control> _genericCharacterControls = new List<Control>();
        private int _genericCharacterStartRow = -1;
        private int _genericCharacterEndRow = -1;
        private bool _storyCharactersCollapsed = false;
        private List<Control> _storyCharacterControls = new List<Control>();

        /// <summary>
        /// Gets the current configuration
        /// </summary>
        public Config Configuration => _config;

        public ConfigurationForm(Config config, string configPath = null, string modPath = null)
        {
            _config = config;
            _configPath = configPath;
            _modPath = modPath;

            ModLogger.Log($"ConfigurationForm created with config - Squire_Male: {config.Squire_Male}");
            if (!string.IsNullOrEmpty(configPath))
                ModLogger.Log($"Config path set to: {configPath}");
            if (!string.IsNullOrEmpty(modPath))
                ModLogger.Log($"Mod path set to: {modPath}");

            _isInitializing = true;
            InitializeForm();
            InitializeRowBuilder();
            LoadConfiguration();
            _isInitializing = false;

            // Defer enabling events until form is fully shown
            this.Shown += (s, e) =>
            {
                _isFullyLoaded = true;
                ModLogger.Log($"Form shown - events now enabled");
                VerifyAllSelections();
            };

            ModLogger.Log($"ConfigurationForm initialized - Squire_Male: {_config.Squire_Male}");
        }

        private void InitializeForm()
        {
            InitializeFormProperties();
            CreateTitleBar();
            InitializePreviewManager();
            CreateMainContentPanel();
            CreateButtonPanel();

            // Force image refresh after form loads
            this.Load += (s, e) => RefreshAllPreviews();
        }

        private void InitializePreviewManager()
        {
            string modPath;

            if (!string.IsNullOrEmpty(_modPath))
            {
                modPath = _modPath;
                ModLogger.Log($"Using explicitly provided mod path: {modPath}");
            }
            else if (!string.IsNullOrEmpty(_configPath))
            {
                var configDir = Path.GetDirectoryName(_configPath);
                modPath = Path.GetDirectoryName(configDir) ?? Environment.CurrentDirectory;
                ModLogger.Log($"Derived mod path from config path: {modPath}");
            }
            else
            {
                modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? Environment.CurrentDirectory;
                ModLogger.Log($"Using assembly location as mod path: {modPath}");
            }

            _previewManager = new PreviewImageManager(modPath);
        }

        private void ToggleGenericCharactersVisibility(Label header)
        {
            _mainPanel.SuspendLayout();
            this.SuspendLayout();

            _genericCharactersCollapsed = !_genericCharactersCollapsed;
            header.Text = _genericCharactersCollapsed ? "▶ Generic Characters" : "▼ Generic Characters";

            SetControlsVisibility(_genericCharacterControls, !_genericCharactersCollapsed);

            _mainPanel.ResumeLayout(true);
            this.ResumeLayout(true);
        }

        private void ToggleStoryCharactersVisibility(Label header)
        {
            _mainPanel.SuspendLayout();
            this.SuspendLayout();

            _storyCharactersCollapsed = !_storyCharactersCollapsed;
            header.Text = _storyCharactersCollapsed ? "▶ Story Characters" : "▼ Story Characters";

            SetControlsVisibility(_storyCharacterControls, !_storyCharactersCollapsed);

            _mainPanel.ResumeLayout(true);
            this.ResumeLayout(true);
        }

        private void SetControlsVisibility(List<Control> controls, bool visible)
        {
            foreach (var control in controls)
            {
                control.Visible = visible;
            }
        }

        private void RefreshAllPreviews()
        {
            foreach (Control control in _mainPanel.Controls)
            {
                if (control is PictureBox pictureBox && pictureBox.Tag != null)
                {
                    var tag = pictureBox.Tag as dynamic;
                    if (tag != null)
                    {
                        var row = _mainPanel.GetRow(pictureBox);
                        var comboBox = _mainPanel.GetControlFromPosition(1, row) as ComboBox;

                        if (comboBox != null && comboBox.SelectedItem != null)
                        {
                            // Refresh based on character type
                            RefreshPreviewForCharacter(pictureBox, comboBox, tag);
                        }
                    }
                }
            }
        }

        private void RefreshPreviewForCharacter(PictureBox pictureBox, ComboBox comboBox, dynamic tag)
        {
            // This would normally update the preview image based on the selection
            // Implementation details depend on the specific preview system
            ModLogger.LogDebug($"Refreshing preview for {tag.JobName}");
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            ModLogger.Log($"Save button clicked");
            ModLogger.Log($"Current config state - Squire_Male: {_config.Squire_Male}");

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ResetAllButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all themes to 'Original'?\n\nThis will save immediately.",
                "Reset All Themes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                ResetAllCharacters();
                ReloadForm();

                ModLogger.Log("All themes reset to original");

                // Auto-save the reset configuration
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void ReloadForm()
        {
            this.SuspendLayout();
            _mainPanel.SuspendLayout();
            _mainPanel.Visible = false;

            var previousCursor = this.Cursor;
            this.Cursor = Cursors.WaitCursor;
            Application.DoEvents();

            try
            {
                _mainPanel.Controls.Clear();
                _genericCharacterControls.Clear();
                _storyCharacterControls.Clear();

                LoadConfiguration();
            }
            finally
            {
                _mainPanel.Visible = true;
                _mainPanel.ResumeLayout(true);
                this.ResumeLayout(true);
                this.Cursor = previousCursor;
            }
        }
    }
}
