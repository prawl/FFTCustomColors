using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using FFTColorMod.Configuration.UI;
using FFTColorMod.Utilities;

namespace FFTColorMod.Configuration
{
    public class ConfigurationForm : Form
    {
        private Config _config;
        private string _configPath;  // Store the actual config path
        private string _modPath;     // Store the mod installation path for resources
        private TableLayoutPanel _mainPanel;
        private Button _saveButton;
        private Button _cancelButton;
        private Button _resetAllButton;
        private CustomTitleBar _titleBar;
        private PreviewImageManager _previewManager;

        private bool _isFullyLoaded = false;
        private bool _isInitializing = true;  // Prevent any changes during initialization
        private bool _genericCharactersCollapsed = false;
        private List<Control> _genericCharacterControls = new List<Control>();
        private int _genericCharacterStartRow = -1;
        private int _genericCharacterEndRow = -1;
        private bool _storyCharactersCollapsed = false;
        private List<Control> _storyCharacterControls = new List<Control>();

        public ConfigurationForm(Config config, string configPath = null, string modPath = null)
        {
            _config = config;
            _configPath = configPath;  // Store the path for saving
            _modPath = modPath;        // Store the mod path for resources
            ModLogger.Log($"ConfigurationForm created with config - Squire_Male: {config.Squire_Male}");
            if (!string.IsNullOrEmpty(configPath))
            {
                ModLogger.Log($"Config path set to: {configPath}");
            }
            if (!string.IsNullOrEmpty(modPath))
            {
                ModLogger.Log($"Mod path set to: {modPath}");
            }

            _isInitializing = true;  // Block all events during initialization
            InitializeForm();
            LoadConfiguration();
            _isInitializing = false;  // Allow events after everything is loaded

            // Defer enabling events until form is fully shown
            this.Shown += (s, e) =>
            {
                _isFullyLoaded = true;
                ModLogger.Log($"Form shown - events now enabled");
                // Force refresh all ComboBox selections to ensure they show the right values
                VerifyAllSelections();
            };

            ModLogger.Log($"ConfigurationForm initialized - Squire_Male: {_config.Squire_Male}");
        }

        private void InitializeForm()
        {
            Text = "FFT Color Mod - Configuration";
            Size = new Size(700, 730);  // Slightly taller to accommodate custom title bar
            StartPosition = FormStartPosition.CenterScreen;
            AutoScroll = false;  // We'll manage scrolling in the content area
            TopMost = true;  // Always show above other windows including the game
            FormBorderStyle = FormBorderStyle.None;  // Remove default title bar

            // Apply RELOADED dark theme colors
            BackColor = Color.FromArgb(30, 30, 30);  // Dark background like RELOADED
            ForeColor = Color.White;  // White text

            // Create custom title bar
            _titleBar = new CustomTitleBar(this, "FFT Color Mod - Configuration");
            Controls.Add(_titleBar);

            // Initialize preview manager
            string modPath;

            // Priority: Use explicitly provided mod path, then derive from config path, then use assembly location
            if (!string.IsNullOrEmpty(_modPath))
            {
                modPath = _modPath;
                ModLogger.Log($"Using explicitly provided mod path: {modPath}");
            }
            else if (!string.IsNullOrEmpty(_configPath))
            {
                // Config path is like "C:\path\to\mod\Config\Config.json"
                // We need to get "C:\path\to\mod"
                var configDir = Path.GetDirectoryName(_configPath);
                modPath = Path.GetDirectoryName(configDir) ?? Environment.CurrentDirectory;
                ModLogger.Log($"Derived mod path from config path: {modPath}");
            }
            else
            {
                modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
                ModLogger.Log($"Using assembly location as mod path: {modPath}");
            }
            _previewManager = new PreviewImageManager(modPath);

            // Force image refresh after form loads
            this.Load += (s, e) => RefreshAllPreviews();

            // Create main content panel that will be below the title bar
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                AutoScroll = true
            };

            _mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 50,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(30, 30, 30),  // Dark background
                ForeColor = Color.White  // White text
            };

            // Add column styles
            _mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            _mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            _mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            // Add header with white text
            var headerLabel = new Label
            {
                Text = "Themes",
                Font = new Font("Arial", 12, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,  // White text for better readability
                BackColor = Color.FromArgb(40, 40, 40)  // Slightly lighter dark background
            };
            _mainPanel.SetColumnSpan(headerLabel, 3);
            _mainPanel.Controls.Add(headerLabel, 0, 0);

            contentPanel.Controls.Add(_mainPanel);
            Controls.Add(contentPanel);

            // Add buttons panel with dark theme
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(25, 25, 25)  // Darker panel for buttons
            };

            // Position buttons using absolute positioning
            _cancelButton = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 30,
                Location = new Point(buttonPanel.Width - 95, 5),  // Right side
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(50, 50, 50),  // Dark button
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderColor = Color.FromArgb(100, 100, 100), BorderSize = 1 }
            };
            _cancelButton.Click += (s, e) => Close();

            // Add hover effect for cancel button
            _cancelButton.MouseEnter += (s, e) => {
                _cancelButton.BackColor = Color.FromArgb(70, 70, 70);
                _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(150, 150, 150);
            };
            _cancelButton.MouseLeave += (s, e) => {
                _cancelButton.BackColor = Color.FromArgb(50, 50, 50);
                _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            };

            _saveButton = new Button
            {
                Text = "Save",
                Width = 80,
                Height = 30,
                Location = new Point(buttonPanel.Width - 180, 5),  // Right side, before Cancel
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(150, 30, 30),  // Red accent for save button like RELOADED
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderColor = Color.FromArgb(220, 50, 50), BorderSize = 1 }
            };
            _saveButton.Click += SaveButton_Click;

            // Add hover effect for save button
            _saveButton.MouseEnter += (s, e) => {
                _saveButton.BackColor = Color.FromArgb(180, 40, 40);
                _saveButton.FlatAppearance.BorderColor = Color.FromArgb(255, 60, 60);
            };
            _saveButton.MouseLeave += (s, e) => {
                _saveButton.BackColor = Color.FromArgb(150, 30, 30);
                _saveButton.FlatAppearance.BorderColor = Color.FromArgb(220, 50, 50);
            };

            // Add Reset All button on the left side
            _resetAllButton = new Button
            {
                Text = "Reset All",
                Width = 80,
                Height = 30,
                Location = new Point(10, 5),  // Left side with padding
                BackColor = Color.FromArgb(80, 80, 30),  // Yellow accent for reset button
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderColor = Color.FromArgb(150, 150, 50), BorderSize = 1 }
            };
            _resetAllButton.Click += ResetAllButton_Click;

            // Add hover effect for reset all button
            _resetAllButton.MouseEnter += (s, e) => {
                _resetAllButton.BackColor = Color.FromArgb(100, 100, 40);
                _resetAllButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 60);
            };
            _resetAllButton.MouseLeave += (s, e) => {
                _resetAllButton.BackColor = Color.FromArgb(80, 80, 30);
                _resetAllButton.FlatAppearance.BorderColor = Color.FromArgb(150, 150, 50);
            };

            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_saveButton);
            buttonPanel.Controls.Add(_resetAllButton);

            Controls.Add(buttonPanel);
        }

        private void LoadConfiguration()
        {
            int row = 1;

            // Add header for generic characters (collapsible)
            var genericHeader = new Label
            {
                Text = _genericCharactersCollapsed ? "▶ Generic Characters" : "▼ Generic Characters",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,  // White text for headers
                BackColor = Color.FromArgb(40, 40, 40),  // Slightly lighter dark background
                Padding = new Padding(0, 5, 0, 5),
                Cursor = Cursors.Hand  // Show hand cursor on hover
            };

            // Make header clickable
            genericHeader.Click += (sender, e) => {
                // Suspend layout to prevent multiple redraws
                _mainPanel.SuspendLayout();
                this.SuspendLayout();

                _genericCharactersCollapsed = !_genericCharactersCollapsed;
                genericHeader.Text = _genericCharactersCollapsed ? "▶ Generic Characters" : "▼ Generic Characters";

                // Toggle visibility of generic character controls in batch
                bool newVisibility = !_genericCharactersCollapsed;
                foreach (var control in _genericCharacterControls)
                {
                    control.Visible = newVisibility;
                }

                // Resume layout and force single recalculation
                _mainPanel.ResumeLayout(true);
                this.ResumeLayout(true);
            };

            _mainPanel.SetColumnSpan(genericHeader, 3);
            _mainPanel.Controls.Add(genericHeader, 0, row++);

            // Store the starting row for generic characters
            _genericCharacterStartRow = row;

            // Squires
            AddJobRow(row++, "Squire (Male)", _config.Squire_Male, v => _config.Squire_Male = v);
            AddJobRow(row++, "Squire (Female)", _config.Squire_Female, v => _config.Squire_Female = v);

            // Chemists
            AddJobRow(row++, "Chemist (Male)", _config.Chemist_Male, v => _config.Chemist_Male = v);
            AddJobRow(row++, "Chemist (Female)", _config.Chemist_Female, v => _config.Chemist_Female = v);

            // Knights
            AddJobRow(row++, "Knight (Male)", _config.Knight_Male, v => _config.Knight_Male = v);
            AddJobRow(row++, "Knight (Female)", _config.Knight_Female, v => _config.Knight_Female = v);

            // Archers
            AddJobRow(row++, "Archer (Male)", _config.Archer_Male, v => _config.Archer_Male = v);
            AddJobRow(row++, "Archer (Female)", _config.Archer_Female, v => _config.Archer_Female = v);

            // Monks
            AddJobRow(row++, "Monk (Male)", _config.Monk_Male, v => _config.Monk_Male = v);
            AddJobRow(row++, "Monk (Female)", _config.Monk_Female, v => _config.Monk_Female = v);

            // White Mages
            AddJobRow(row++, "White Mage (Male)", _config.WhiteMage_Male, v => _config.WhiteMage_Male = v);
            AddJobRow(row++, "White Mage (Female)", _config.WhiteMage_Female, v => _config.WhiteMage_Female = v);

            // Black Mages
            AddJobRow(row++, "Black Mage (Male)", _config.BlackMage_Male, v => _config.BlackMage_Male = v);
            AddJobRow(row++, "Black Mage (Female)", _config.BlackMage_Female, v => _config.BlackMage_Female = v);

            // Time Mages
            AddJobRow(row++, "Time Mage (Male)", _config.TimeMage_Male, v => _config.TimeMage_Male = v);
            AddJobRow(row++, "Time Mage (Female)", _config.TimeMage_Female, v => _config.TimeMage_Female = v);

            // Summoners
            AddJobRow(row++, "Summoner (Male)", _config.Summoner_Male, v => _config.Summoner_Male = v);
            AddJobRow(row++, "Summoner (Female)", _config.Summoner_Female, v => _config.Summoner_Female = v);

            // Thieves
            AddJobRow(row++, "Thief (Male)", _config.Thief_Male, v => _config.Thief_Male = v);
            AddJobRow(row++, "Thief (Female)", _config.Thief_Female, v => _config.Thief_Female = v);

            // Mediators/Orators
            AddJobRow(row++, "Mediator (Male)", _config.Mediator_Male, v => _config.Mediator_Male = v);
            AddJobRow(row++, "Mediator (Female)", _config.Mediator_Female, v => _config.Mediator_Female = v);

            // Mystics/Oracles
            AddJobRow(row++, "Mystic (Male)", _config.Mystic_Male, v => _config.Mystic_Male = v);
            AddJobRow(row++, "Mystic (Female)", _config.Mystic_Female, v => _config.Mystic_Female = v);

            // Geomancers
            AddJobRow(row++, "Geomancer (Male)", _config.Geomancer_Male, v => _config.Geomancer_Male = v);
            AddJobRow(row++, "Geomancer (Female)", _config.Geomancer_Female, v => _config.Geomancer_Female = v);

            // Dragoons
            AddJobRow(row++, "Dragoon (Male)", _config.Dragoon_Male, v => _config.Dragoon_Male = v);
            AddJobRow(row++, "Dragoon (Female)", _config.Dragoon_Female, v => _config.Dragoon_Female = v);

            // Samurai
            AddJobRow(row++, "Samurai (Male)", _config.Samurai_Male, v => _config.Samurai_Male = v);
            AddJobRow(row++, "Samurai (Female)", _config.Samurai_Female, v => _config.Samurai_Female = v);

            // Ninjas
            AddJobRow(row++, "Ninja (Male)", _config.Ninja_Male, v => _config.Ninja_Male = v);
            AddJobRow(row++, "Ninja (Female)", _config.Ninja_Female, v => _config.Ninja_Female = v);

            // Calculators/Arithmeticians
            AddJobRow(row++, "Calculator (Male)", _config.Calculator_Male, v => _config.Calculator_Male = v);
            AddJobRow(row++, "Calculator (Female)", _config.Calculator_Female, v => _config.Calculator_Female = v);

            // Bards (Male only)
            AddJobRow(row++, "Bard (Male)", _config.Bard_Male, v => _config.Bard_Male = v);

            // Dancers (Female only)
            AddJobRow(row++, "Dancer (Female)", _config.Dancer_Female, v => _config.Dancer_Female = v);

            // Mimes
            AddJobRow(row++, "Mime (Male)", _config.Mime_Male, v => _config.Mime_Male = v);
            AddJobRow(row++, "Mime (Female)", _config.Mime_Female, v => _config.Mime_Female = v);

            // Mark the end of generic characters section
            _genericCharacterEndRow = row - 1;

            // Apply initial collapsed state if needed (batch operation)
            if (_genericCharactersCollapsed)
            {
                _mainPanel.SuspendLayout();
                foreach (var control in _genericCharacterControls)
                {
                    control.Visible = false;
                }
                _mainPanel.ResumeLayout(false);  // Don't perform layout yet
            }

            // Add header for story characters (collapsible)
            var storyHeader = new Label
            {
                Text = _storyCharactersCollapsed ? "▶ Story Characters" : "▼ Story Characters",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,  // White text for headers
                BackColor = Color.FromArgb(40, 40, 40),  // Slightly lighter dark background
                Padding = new Padding(0, 5, 0, 5),
                Cursor = Cursors.Hand  // Show hand cursor on hover
            };

            // Make header clickable
            storyHeader.Click += (sender, e) => {
                // Suspend layout to prevent multiple redraws
                _mainPanel.SuspendLayout();
                this.SuspendLayout();

                _storyCharactersCollapsed = !_storyCharactersCollapsed;
                storyHeader.Text = _storyCharactersCollapsed ? "▶ Story Characters" : "▼ Story Characters";

                // Toggle visibility of story character controls in batch
                bool newVisibility = !_storyCharactersCollapsed;
                foreach (var control in _storyCharacterControls)
                {
                    control.Visible = newVisibility;
                }

                // Resume layout and force single recalculation
                _mainPanel.ResumeLayout(true);
                this.ResumeLayout(true);
            };

            _mainPanel.SetColumnSpan(storyHeader, 3);
            _mainPanel.Controls.Add(storyHeader, 0, row++);

            // Story characters use different enums, need special handling
            AddStoryCharacterRow(row++, "Agrias", _config.Agrias);
            AddStoryCharacterRow(row++, "Orlandeau", _config.Orlandeau);
            AddStoryCharacterRow(row++, "Cloud", _config.Cloud);
            AddStoryCharacterRow(row++, "Mustadio", _config.Mustadio);
            AddStoryCharacterRow(row++, "Reis", _config.Reis);
            AddStoryCharacterRow(row++, "Malak", _config.Malak);
            AddStoryCharacterRow(row++, "Rapha", _config.Rafa);
            AddStoryCharacterRow(row++, "Delita", _config.Delita);
            AddStoryCharacterRow(row++, "Alma", _config.Alma);
            AddStoryCharacterRow(row++, "Wiegraf", _config.Wiegraf);
            AddStoryCharacterRow(row++, "Celia", _config.Celia);
            AddStoryCharacterRow(row++, "Lettie", _config.Lettie);

            // Additional Story Characters
            AddStoryCharacterRow(row++, "Ovelia", _config.Ovelia);
            AddStoryCharacterRow(row++, "Simon", _config.Simon);
            AddStoryCharacterRow(row++, "Gaffgarion", _config.Gaffgarion);
            AddStoryCharacterRow(row++, "Dycedarg", _config.Dycedarg);
            AddStoryCharacterRow(row++, "Elmdore", _config.Elmdore);
            AddStoryCharacterRow(row++, "Vormav", _config.Vormav);
            AddStoryCharacterRow(row++, "Zalbag", _config.Zalbag);
            AddStoryCharacterRow(row++, "Zalmo", _config.Zalmo);
        }

        private void AddJobRow(int row, string jobName, ColorScheme currentTheme, Action<ColorScheme> setter)
        {
            // Debug logging
            ModLogger.Log($"AddJobRow: {jobName} = {currentTheme} (enum value: {(int)currentTheme})");

            var label = new Label
            {
                Text = jobName,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 3, 0, 0),  // Add top padding to align with dropdown text
                ForeColor = Color.White  // White text for labels
            };
            _mainPanel.Controls.Add(label, 0, row);

            // Track this control as part of generic characters
            _genericCharacterControls.Add(label);

            var comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                MaxDropDownItems = 30,  // Show all themes at once
                BackColor = Color.FromArgb(45, 45, 45),  // Dark combo box
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            // Set DataSource first, then SelectedItem (order matters!)
            comboBox.DataSource = Enum.GetValues(typeof(ColorScheme));
            comboBox.SelectedItem = currentTheme;

            // Preview image
            var pictureBox = new PictureBox
            {
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50)  // Dark background for preview boxes
            };

            // Load initial preview
            UpdatePreviewImage(pictureBox, jobName, currentTheme);

            // Store reference for refresh
            pictureBox.Tag = new { JobName = jobName, Setter = setter };

            // Add controls to panel BEFORE setting up event handler
            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(pictureBox, 2, row);

            // Track these controls as part of generic characters
            _genericCharacterControls.Add(comboBox);
            _genericCharacterControls.Add(pictureBox);

            // Store the combo box reference for later verification
            comboBox.Tag = new { JobName = jobName, ExpectedValue = currentTheme, Setter = setter };

            // Setup event handler AFTER controls are added and initial value is set
            comboBox.SelectedIndexChanged += (s, e) =>
            {
                // Block all events during initialization
                if (_isInitializing)
                    return;

                // Only process events after form is fully loaded
                if (_isFullyLoaded && comboBox.SelectedItem != null)
                {
                    var newTheme = (ColorScheme)comboBox.SelectedItem;
                    ModLogger.Log($"Selection changed: {jobName} = {newTheme}");
                    setter(newTheme);
                    ModLogger.Log($"Config updated via setter for {jobName}");
                    UpdatePreviewImage(pictureBox, jobName, newTheme);
                }
            };

        }

        private void AddStoryCharacterRow(int row, string characterName, object currentTheme)
        {
            var label = new Label
            {
                Text = characterName,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 3, 0, 0),  // Add top padding to align with dropdown text
                ForeColor = Color.White  // White text for labels
            };
            _mainPanel.Controls.Add(label, 0, row);

            // Track this control as part of story characters
            _storyCharacterControls.Add(label);

            var comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                MaxDropDownItems = 30,  // Show all items at once
                BackColor = Color.FromArgb(45, 45, 45),  // Dark combo box
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            // Preview image for story characters
            var pictureBox = new PictureBox
            {
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50)  // Dark background for preview boxes
            };

            // Handle different enum types for story characters
            if (characterName == "Agrias")
            {
                var values = Enum.GetValues(typeof(AgriasColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Agrias = (AgriasColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Agrias", _config.Agrias.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Agrias", _config.Agrias.ToString());
                pictureBox.Tag = new { JobName = "Agrias" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Orlandeau")
            {
                var values = Enum.GetValues(typeof(OrlandeauColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Orlandeau = (OrlandeauColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Orlandeau", _config.Orlandeau.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Orlandeau", _config.Orlandeau.ToString());
                pictureBox.Tag = new { JobName = "Orlandeau" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Cloud")
            {
                var values = Enum.GetValues(typeof(CloudColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Cloud = (CloudColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Cloud", _config.Cloud.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Cloud", _config.Cloud.ToString());
                pictureBox.Tag = new { JobName = "Cloud" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Mustadio")
            {
                var values = Enum.GetValues(typeof(MustadioColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Mustadio = (MustadioColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Mustadio", _config.Mustadio.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Mustadio", _config.Mustadio.ToString());
                pictureBox.Tag = new { JobName = "Mustadio" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Reis")
            {
                var values = Enum.GetValues(typeof(ReisColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Reis = (ReisColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Reis", _config.Reis.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Reis", _config.Reis.ToString());
                pictureBox.Tag = new { JobName = "Reis" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Malak")
            {
                var values = Enum.GetValues(typeof(MalakColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Malak = (MalakColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Malak", _config.Malak.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Malak", _config.Malak.ToString());
                pictureBox.Tag = new { JobName = "Malak" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Rapha")
            {
                var values = Enum.GetValues(typeof(RafaColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Rafa = (RafaColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Rafa", _config.Rafa.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Rafa", _config.Rafa.ToString());
                pictureBox.Tag = new { JobName = "Rafa" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Delita")
            {
                var values = Enum.GetValues(typeof(DelitaColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Delita = (DelitaColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Delita", _config.Delita.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Delita", _config.Delita.ToString());
                pictureBox.Tag = new { JobName = "Delita" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Alma")
            {
                var values = Enum.GetValues(typeof(AlmaColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Alma = (AlmaColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Alma", _config.Alma.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Alma", _config.Alma.ToString());
                pictureBox.Tag = new { JobName = "Alma" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Wiegraf")
            {
                var values = Enum.GetValues(typeof(WiegrafColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Wiegraf = (WiegrafColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Wiegraf", _config.Wiegraf.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Wiegraf", _config.Wiegraf.ToString());
                pictureBox.Tag = new { JobName = "Wiegraf" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Celia")
            {
                var values = Enum.GetValues(typeof(CeliaColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Celia = (CeliaColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Celia", _config.Celia.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Celia", _config.Celia.ToString());
                pictureBox.Tag = new { JobName = "Celia" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Lettie")
            {
                var values = Enum.GetValues(typeof(LettieColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Lettie = (LettieColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "Lettie", _config.Lettie.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "Lettie", _config.Lettie.ToString());
                pictureBox.Tag = new { JobName = "Lettie" };

                // Add to panel first to ensure Handle creation
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);

                // Force Handle creation - This is critical!
                var handle = comboBox.Handle;

                // Wait for Items to be populated after Handle creation
                if (comboBox.Items.Count == 0)
                {
                    // If Items aren't populated yet, force refresh
                    comboBox.DataSource = null;
                    comboBox.DataSource = values;
                    handle = comboBox.Handle; // Ensure Handle still exists
                }

                // Now set the selection using index after Handle is created
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Ovelia")
            {
                var values = Enum.GetValues(typeof(OveliaColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Ovelia = (OveliaColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "ovelia", _config.Ovelia.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "ovelia", _config.Ovelia.ToString());
                pictureBox.Tag = new { JobName = "ovelia" };
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);
                var handle = comboBox.Handle;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return;
            }
            else if (characterName == "Simon")
            {
                var values = Enum.GetValues(typeof(SimonColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Simon = (SimonColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "simon", _config.Simon.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "simon", _config.Simon.ToString());
                pictureBox.Tag = new { JobName = "simon" };
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);
                var handle = comboBox.Handle;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return;
            }
            else if (characterName == "Gaffgarion")
            {
                var values = Enum.GetValues(typeof(GaffgarionColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Gaffgarion = (GaffgarionColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "gaffgarion", _config.Gaffgarion.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "gaffgarion", _config.Gaffgarion.ToString());
                pictureBox.Tag = new { JobName = "gaffgarion" };
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);
                var handle = comboBox.Handle;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return;
            }
            else if (characterName == "Dycedarg")
            {
                var values = Enum.GetValues(typeof(DycedargColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Dycedarg = (DycedargColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "dycedarg", _config.Dycedarg.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "dycedarg", _config.Dycedarg.ToString());
                pictureBox.Tag = new { JobName = "dycedarg" };
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);
                var handle = comboBox.Handle;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return;
            }
            else if (characterName == "Elmdore")
            {
                var values = Enum.GetValues(typeof(ElmdoreColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Elmdore = (ElmdoreColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "elmdore", _config.Elmdore.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "elmdore", _config.Elmdore.ToString());
                pictureBox.Tag = new { JobName = "elmdore" };
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);
                var handle = comboBox.Handle;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return;
            }
            else if (characterName == "Vormav")
            {
                var values = Enum.GetValues(typeof(VormavColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Vormav = (VormavColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "vormav", _config.Vormav.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "vormav", _config.Vormav.ToString());
                pictureBox.Tag = new { JobName = "vormav" };
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);
                var handle = comboBox.Handle;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return;
            }
            else if (characterName == "Zalbag")
            {
                var values = Enum.GetValues(typeof(ZalbagColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Zalbag = (ZalbagColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "zalbag", _config.Zalbag.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "zalbag", _config.Zalbag.ToString());
                pictureBox.Tag = new { JobName = "zalbag" };
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);
                var handle = comboBox.Handle;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return;
            }
            else if (characterName == "Zalmo")
            {
                var values = Enum.GetValues(typeof(ZalmoColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    // Check if we're initializing to prevent events during reset
                    if (_isInitializing)
                        return;

                    if (comboBox.SelectedItem != null)
                    {
                        _config.Zalmo = (ZalmoColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "zalmo", _config.Zalmo.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "zalmo", _config.Zalmo.ToString());
                pictureBox.Tag = new { JobName = "zalmo" };
                _mainPanel.Controls.Add(comboBox, 1, row);
                _mainPanel.Controls.Add(pictureBox, 2, row);
                var handle = comboBox.Handle;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).ToString() == currentTheme.ToString())
                    {
                        comboBox.SelectedIndex = i;
                        break;
                    }
                }

                // Track these controls as part of story characters
                _storyCharacterControls.Add(comboBox);
                _storyCharacterControls.Add(pictureBox);
                return;
            }

            // Default case - shouldn't normally be reached
            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(pictureBox, 2, row);

            // Track these controls as part of story characters (for all cases)
            _storyCharacterControls.Add(comboBox);
            _storyCharacterControls.Add(pictureBox);
        }

        private void UpdatePreviewImage(PictureBox pictureBox, string jobName, ColorScheme theme)
        {
            // Map display names to file names
            string fileName = jobName.ToLower()
                .Replace(" (male)", "_male")
                .Replace(" (female)", "_female")
                .Replace(" ", "_")
                .Replace("(", "")
                .Replace(")", "");

            string resourceName = $"FFTColorMod.Resources.Previews.{fileName}_{theme.ToString().ToLower()}.png";

            ModLogger.LogDebug($"Looking for embedded resource: {resourceName}");

            try
            {
                // Load from embedded resources
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        // Dispose old image if exists
                        pictureBox.Image?.Dispose();
                        // Load new image from stream
                        pictureBox.Image = Image.FromStream(stream);
                        ModLogger.LogSuccess($"Successfully loaded embedded preview image");
                    }
                    else
                    {
                        // Try with different resource name pattern
                        var allResources = assembly.GetManifestResourceNames();
                        var matchingResource = allResources.FirstOrDefault(r =>
                            r.EndsWith($"{fileName}_{theme.ToString().ToLower()}.png", StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(matchingResource))
                        {
                            using (var altStream = assembly.GetManifestResourceStream(matchingResource))
                            {
                                if (altStream != null)
                                {
                                    pictureBox.Image?.Dispose();
                                    pictureBox.Image = Image.FromStream(altStream);
                                    ModLogger.Log($"Loaded with alt resource name: {matchingResource}");
                                }
                            }
                        }
                        else
                        {
                            pictureBox.Image?.Dispose();
                            pictureBox.Image = null;
                            ModLogger.Log($"Embedded resource not found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"loading embedded preview: {ex.Message}");
                pictureBox.Image?.Dispose();
                pictureBox.Image = null;
            }
        }

        private void UpdateStoryCharacterPreview(PictureBox pictureBox, string characterName, string theme)
        {
            _previewManager?.UpdateStoryCharacterPreview(pictureBox, characterName, theme);
        }

        private void RefreshAllPreviews()
        {
            // Refresh all preview images after form loads
            foreach (Control control in _mainPanel.Controls)
            {
                if (control is PictureBox pictureBox && pictureBox.Tag != null)
                {
                    // Re-load the image
                    var tag = pictureBox.Tag as dynamic;
                    if (tag != null)
                    {
                        // Find the associated ComboBox (should be in previous column, same row)
                        var row = _mainPanel.GetRow(pictureBox);
                        var comboBox = _mainPanel.GetControlFromPosition(1, row) as ComboBox;

                        if (comboBox != null && comboBox.SelectedItem != null)
                        {
                            if (comboBox.SelectedItem is ColorScheme scheme)
                            {
                                UpdatePreviewImage(pictureBox, tag.JobName, scheme);
                            }
                            else if (tag.JobName == "agrias")
                            {
                                UpdateStoryCharacterPreview(pictureBox, "agrias", _config.Agrias.ToString());
                            }
                            else if (tag.JobName == "orlandeau")
                            {
                                UpdateStoryCharacterPreview(pictureBox, "orlandeau", _config.Orlandeau.ToString());
                            }
                            else if (tag.JobName == "cloud")
                            {
                                UpdateStoryCharacterPreview(pictureBox, "cloud", _config.Cloud.ToString());
                            }
                        }
                    }
                }
            }
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
            var result = MessageBox.Show("Are you sure you want to reset all themes to 'Original'?",
                "Reset All Themes", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                // Reset all generic characters
                _config.Squire_Male = ColorScheme.original;
                _config.Squire_Female = ColorScheme.original;
                _config.Chemist_Male = ColorScheme.original;
                _config.Chemist_Female = ColorScheme.original;
                _config.Knight_Male = ColorScheme.original;
                _config.Knight_Female = ColorScheme.original;
                _config.Archer_Male = ColorScheme.original;
                _config.Archer_Female = ColorScheme.original;
                _config.Monk_Male = ColorScheme.original;
                _config.Monk_Female = ColorScheme.original;
                _config.WhiteMage_Male = ColorScheme.original;
                _config.WhiteMage_Female = ColorScheme.original;
                _config.BlackMage_Male = ColorScheme.original;
                _config.BlackMage_Female = ColorScheme.original;
                _config.TimeMage_Male = ColorScheme.original;
                _config.TimeMage_Female = ColorScheme.original;
                _config.Summoner_Male = ColorScheme.original;
                _config.Summoner_Female = ColorScheme.original;
                _config.Thief_Male = ColorScheme.original;
                _config.Thief_Female = ColorScheme.original;
                _config.Mediator_Male = ColorScheme.original;
                _config.Mediator_Female = ColorScheme.original;
                _config.Mystic_Male = ColorScheme.original;
                _config.Mystic_Female = ColorScheme.original;
                _config.Geomancer_Male = ColorScheme.original;
                _config.Geomancer_Female = ColorScheme.original;
                _config.Dragoon_Male = ColorScheme.original;
                _config.Dragoon_Female = ColorScheme.original;
                _config.Samurai_Male = ColorScheme.original;
                _config.Samurai_Female = ColorScheme.original;
                _config.Ninja_Male = ColorScheme.original;
                _config.Ninja_Female = ColorScheme.original;
                _config.Calculator_Male = ColorScheme.original;
                _config.Calculator_Female = ColorScheme.original;
                _config.Bard_Male = ColorScheme.original;
                _config.Dancer_Female = ColorScheme.original;
                _config.Mime_Male = ColorScheme.original;
                _config.Mime_Female = ColorScheme.original;

                // Reset all story characters to their original themes
                _config.Agrias = AgriasColorScheme.original;
                _config.Alma = AlmaColorScheme.original;
                _config.Celia = CeliaColorScheme.original;
                _config.Cloud = CloudColorScheme.original;
                _config.Delita = DelitaColorScheme.original;
                _config.Dycedarg = DycedargColorScheme.original;
                _config.Elmdore = ElmdoreColorScheme.original;
                _config.Gaffgarion = GaffgarionColorScheme.original;
                _config.Lettie = LettieColorScheme.original;
                _config.Malak = MalakColorScheme.original;
                _config.Mustadio = MustadioColorScheme.original;
                _config.Orlandeau = OrlandeauColorScheme.original;
                _config.Ovelia = OveliaColorScheme.original;
                _config.Rafa = RafaColorScheme.original;
                _config.Reis = ReisColorScheme.original;
                _config.Simon = SimonColorScheme.original;
                _config.Vormav = VormavColorScheme.original;
                _config.Wiegraf = WiegrafColorScheme.original;
                _config.Zalbag = ZalbagColorScheme.original;
                _config.Zalmo = ZalmoColorScheme.original;

                // Suspend layout to prevent flicker during reload
                this.SuspendLayout();
                _mainPanel.SuspendLayout();

                // Disable the panel to prevent interaction during reload
                _mainPanel.Visible = false;

                // Show a loading cursor
                var previousCursor = this.Cursor;
                this.Cursor = Cursors.WaitCursor;

                // Use Application.DoEvents to keep UI responsive
                Application.DoEvents();

                try
                {
                    // Clear and reload the entire form to ensure proper initialization
                    _mainPanel.Controls.Clear();
                    _genericCharacterControls.Clear();
                    _storyCharacterControls.Clear();

                    // Reload the configuration UI
                    LoadConfiguration();
                }
                finally
                {
                    // Make panel visible again
                    _mainPanel.Visible = true;

                    // Resume layout and restore cursor
                    _mainPanel.ResumeLayout(true);
                    this.ResumeLayout(true);
                    this.Cursor = previousCursor;
                }

                ModLogger.Log("All themes reset to original");
            }
        }

        private void VerifyAllSelections()
        {
            // Go through all ComboBoxes and ensure they have the correct selection
            ModLogger.Log("Verifying all ComboBox selections...");

            foreach (Control control in _mainPanel.Controls)
            {
                if (control is ComboBox comboBox && comboBox.Tag != null)
                {
                    dynamic tag = comboBox.Tag;
                    if (tag.ExpectedValue != null)
                    {
                        var currentValue = comboBox.SelectedItem;
                        if (currentValue == null || !currentValue.Equals(tag.ExpectedValue))
                        {
                            ModLogger.Log($"Correcting {tag.JobName}: was {currentValue}, setting to {tag.ExpectedValue}");
                            comboBox.SelectedItem = tag.ExpectedValue;
                        }
                        else
                        {
                            ModLogger.Log($"{tag.JobName} is correctly set to {tag.ExpectedValue}");
                        }
                    }
                }
            }
        }

    }
}