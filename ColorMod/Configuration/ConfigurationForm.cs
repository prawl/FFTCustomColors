using System;
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
        private CustomTitleBar _titleBar;
        private PreviewImageManager _previewManager;

        private bool _isFullyLoaded = false;
        private bool _isInitializing = true;  // Prevent any changes during initialization

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
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(25, 25, 25)  // Darker panel for buttons
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 30,
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

            // Add Debug button
            var debugButton = new Button
            {
                Text = "Debug",
                Width = 80,
                Height = 30,
                BackColor = Color.FromArgb(50, 50, 100),  // Blue accent for debug button
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderColor = Color.FromArgb(100, 100, 200), BorderSize = 1 }
            };
            debugButton.Click += DebugButton_Click;

            // Add hover effect for debug button
            debugButton.MouseEnter += (s, e) => {
                debugButton.BackColor = Color.FromArgb(70, 70, 120);
                debugButton.FlatAppearance.BorderColor = Color.FromArgb(150, 150, 250);
            };
            debugButton.MouseLeave += (s, e) => {
                debugButton.BackColor = Color.FromArgb(50, 50, 100);
                debugButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 200);
            };

            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_saveButton);
            buttonPanel.Controls.Add(debugButton);

            Controls.Add(buttonPanel);
        }

        private void LoadConfiguration()
        {
            int row = 1;

            // Add header for generic characters
            var genericHeader = new Label
            {
                Text = "=== Generic Characters ===",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,  // White text for headers
                BackColor = Color.FromArgb(40, 40, 40),  // Slightly lighter dark background
                Padding = new Padding(0, 5, 0, 5)
            };
            _mainPanel.SetColumnSpan(genericHeader, 3);
            _mainPanel.Controls.Add(genericHeader, 0, row++);

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

            // Add header for story characters
            var storyHeader = new Label
            {
                Text = "=== Story Characters ===",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,  // White text for headers
                BackColor = Color.FromArgb(40, 40, 40),  // Slightly lighter dark background
                Padding = new Padding(0, 5, 0, 5)
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
            AddStoryCharacterRow(row++, "Rafa", _config.Rafa);
            AddStoryCharacterRow(row++, "Delita", _config.Delita);
            AddStoryCharacterRow(row++, "Alma", _config.Alma);
            AddStoryCharacterRow(row++, "Wiegraf", _config.Wiegraf);
            AddStoryCharacterRow(row++, "Celia", _config.Celia);
            AddStoryCharacterRow(row++, "Lettie", _config.Lettie);
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Orlandeau")
            {
                var values = Enum.GetValues(typeof(OrlandeauColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Cloud")
            {
                var values = Enum.GetValues(typeof(CloudColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Mustadio")
            {
                var values = Enum.GetValues(typeof(MustadioColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Reis")
            {
                var values = Enum.GetValues(typeof(ReisColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Malak")
            {
                var values = Enum.GetValues(typeof(MalakColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Rafa")
            {
                var values = Enum.GetValues(typeof(RafaColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Delita")
            {
                var values = Enum.GetValues(typeof(DelitaColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Alma")
            {
                var values = Enum.GetValues(typeof(AlmaColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Wiegraf")
            {
                var values = Enum.GetValues(typeof(WiegrafColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Celia")
            {
                var values = Enum.GetValues(typeof(CeliaColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }
            else if (characterName == "Lettie")
            {
                var values = Enum.GetValues(typeof(LettieColorScheme));
                comboBox.DataSource = values;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
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
                return; // Exit early since we've handled this case
            }

            // Default case - shouldn't normally be reached
            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(pictureBox, 2, row);
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

        private void DebugButton_Click(object sender, EventArgs e)
        {
            // Test preview loading for Agrias and Orlandeau
            // Use the same path logic as in InitializeForm
            string modPath;
            if (!string.IsNullOrEmpty(_modPath))
            {
                modPath = _modPath;
            }
            else if (!string.IsNullOrEmpty(_configPath))
            {
                var configDir = Path.GetDirectoryName(_configPath);
                modPath = Path.GetDirectoryName(configDir) ?? Environment.CurrentDirectory;
            }
            else
            {
                modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
            }
            string previewsPath = Path.Combine(modPath, "Resources", "Previews");

            var debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine("=== FFT Color Mod Debug Info ===\n");
            debugInfo.AppendLine($"Mod Path: {modPath}");
            debugInfo.AppendLine($"Previews Path: {previewsPath}");
            debugInfo.AppendLine($"Previews Directory Exists: {Directory.Exists(previewsPath)}\n");

            // Check for Agrias images
            debugInfo.AppendLine("Agrias Preview Files:");
            string agriasOriginal = Path.Combine(previewsPath, "agrias_original.png");
            string agriasAshDark = Path.Combine(previewsPath, "agrias_ash_dark.png");
            debugInfo.AppendLine($"  agrias_original.png: {(File.Exists(agriasOriginal) ? "EXISTS" : "NOT FOUND")} - {agriasOriginal}");
            debugInfo.AppendLine($"  agrias_ash_dark.png: {(File.Exists(agriasAshDark) ? "EXISTS" : "NOT FOUND")} - {agriasAshDark}");

            // Check for Orlandeau images
            debugInfo.AppendLine("\nOrlandeau Preview Files:");
            string orlandeauOriginal = Path.Combine(previewsPath, "orlandeau_original.png");
            string orlandeauThunderGod = Path.Combine(previewsPath, "orlandeau_thunder_god.png");
            debugInfo.AppendLine($"  orlandeau_original.png: {(File.Exists(orlandeauOriginal) ? "EXISTS" : "NOT FOUND")} - {orlandeauOriginal}");
            debugInfo.AppendLine($"  orlandeau_thunder_god.png: {(File.Exists(orlandeauThunderGod) ? "EXISTS" : "NOT FOUND")} - {orlandeauThunderGod}");

            // Test PreviewImageManager with the corrected mod path
            debugInfo.AppendLine("\nTesting PreviewImageManager:");
            var testManager = new PreviewImageManager(modPath);
            var testPictureBox = new PictureBox();

            // Test Agrias
            testManager.UpdateStoryCharacterPreview(testPictureBox, "Agrias", "original");
            debugInfo.AppendLine($"  Agrias original loaded: {(testPictureBox.Image != null ? "SUCCESS" : "FAILED")}");
            testPictureBox.Image?.Dispose();
            testPictureBox.Image = null;

            testManager.UpdateStoryCharacterPreview(testPictureBox, "Agrias", "ash_dark");
            debugInfo.AppendLine($"  Agrias ash_dark loaded: {(testPictureBox.Image != null ? "SUCCESS" : "FAILED")}");
            testPictureBox.Image?.Dispose();
            testPictureBox.Image = null;

            // Test Orlandeau
            testManager.UpdateStoryCharacterPreview(testPictureBox, "Orlandeau", "original");
            debugInfo.AppendLine($"  Orlandeau original loaded: {(testPictureBox.Image != null ? "SUCCESS" : "FAILED")}");
            testPictureBox.Image?.Dispose();
            testPictureBox.Image = null;

            testManager.UpdateStoryCharacterPreview(testPictureBox, "Orlandeau", "thunder_god");
            debugInfo.AppendLine($"  Orlandeau thunder_god loaded: {(testPictureBox.Image != null ? "SUCCESS" : "FAILED")}");
            testPictureBox.Image?.Dispose();

            testPictureBox.Dispose();

            // Show debug info in a message box
            MessageBox.Show(debugInfo.ToString(), "Debug Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            ModLogger.Log($"Save button clicked");
            ModLogger.Log($"Current config state - Squire_Male: {_config.Squire_Male}");

            DialogResult = DialogResult.OK;
            Close();
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