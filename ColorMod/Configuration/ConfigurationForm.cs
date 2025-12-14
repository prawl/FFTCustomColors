using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;

namespace FFTColorMod.Configuration
{
    public class ConfigurationForm : Form
    {
        private Config _config;
        private string _configPath;  // Store the actual config path
        private TableLayoutPanel _mainPanel;
        private Button _saveButton;
        private Button _cancelButton;
        private Panel _titleBar;
        private Label _titleLabel;
        private Button _closeButton;
        private Button _minimizeButton;

        private bool _isFullyLoaded = false;
        private bool _isInitializing = true;  // Prevent any changes during initialization

        // For window dragging
        private bool _isDragging = false;
        private Point _dragCursorPoint;
        private Point _dragFormPoint;

        public ConfigurationForm(Config config, string configPath = null)
        {
            _config = config;
            _configPath = configPath;  // Store the path for saving
            Console.WriteLine($"[FFT Color Mod] ConfigurationForm created with config - Squire_Male: {config.Squire_Male}");
            if (!string.IsNullOrEmpty(configPath))
            {
                Console.WriteLine($"[FFT Color Mod] Config path set to: {configPath}");
            }

            _isInitializing = true;  // Block all events during initialization
            InitializeForm();
            LoadConfiguration();
            _isInitializing = false;  // Allow events after everything is loaded

            // Defer enabling events until form is fully shown
            this.Shown += (s, e) =>
            {
                _isFullyLoaded = true;
                Console.WriteLine($"[FFT Color Mod] Form shown - events now enabled");
                // Force refresh all ComboBox selections to ensure they show the right values
                VerifyAllSelections();
            };

            Console.WriteLine($"[FFT Color Mod] ConfigurationForm initialized - Squire_Male: {_config.Squire_Male}");
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
            CreateCustomTitleBar();

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

            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_saveButton);

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
        }

        private void AddJobRow(int row, string jobName, ColorScheme currentTheme, Action<ColorScheme> setter)
        {
            // Debug logging
            Console.WriteLine($"[FFT Color Mod] AddJobRow: {jobName} = {currentTheme} (enum value: {(int)currentTheme})");

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
                    Console.WriteLine($"[FFT Color Mod] Selection changed: {jobName} = {newTheme}");
                    setter(newTheme);
                    Console.WriteLine($"[FFT Color Mod] Config updated via setter for {jobName}");
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
                comboBox.DataSource = Enum.GetValues(typeof(AgriasColorScheme));
                comboBox.SelectedItem = (AgriasColorScheme)currentTheme;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    if (comboBox.SelectedItem != null)
                    {
                        _config.Agrias = (AgriasColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "agrias", _config.Agrias.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "agrias", _config.Agrias.ToString());
                pictureBox.Tag = new { JobName = "agrias" };
            }
            else if (characterName == "Orlandeau")
            {
                comboBox.DataSource = Enum.GetValues(typeof(OrlandeauColorScheme));
                comboBox.SelectedItem = (OrlandeauColorScheme)currentTheme;
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    if (comboBox.SelectedItem != null)
                    {
                        _config.Orlandeau = (OrlandeauColorScheme)comboBox.SelectedItem;
                        UpdateStoryCharacterPreview(pictureBox, "orlandeau", _config.Orlandeau.ToString());
                    }
                };
                UpdateStoryCharacterPreview(pictureBox, "orlandeau", _config.Orlandeau.ToString());
                pictureBox.Tag = new { JobName = "orlandeau" };
            }

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

            Console.WriteLine($"[FFT Color Mod] Looking for embedded resource: {resourceName}");

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
                        Console.WriteLine($"[FFT Color Mod] Successfully loaded embedded preview image");
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
                                    Console.WriteLine($"[FFT Color Mod] Loaded with alt resource name: {matchingResource}");
                                }
                            }
                        }
                        else
                        {
                            pictureBox.Image?.Dispose();
                            pictureBox.Image = null;
                            Console.WriteLine($"[FFT Color Mod] Embedded resource not found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FFT Color Mod] Error loading embedded preview: {ex.Message}");
                pictureBox.Image?.Dispose();
                pictureBox.Image = null;
            }
        }

        private void UpdateStoryCharacterPreview(PictureBox pictureBox, string characterName, string theme)
        {
            string resourceName = $"FFTColorMod.Resources.Previews.{characterName}_{theme.ToLower()}.png";

            Console.WriteLine($"[FFT Color Mod] Looking for embedded story character resource: {resourceName}");

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
                        Console.WriteLine($"[FFT Color Mod] Successfully loaded embedded story character preview");
                    }
                    else
                    {
                        // Try with different resource name pattern
                        var allResources = assembly.GetManifestResourceNames();
                        var matchingResource = allResources.FirstOrDefault(r =>
                            r.EndsWith($"{characterName}_{theme.ToLower()}.png", StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(matchingResource))
                        {
                            using (var altStream = assembly.GetManifestResourceStream(matchingResource))
                            {
                                if (altStream != null)
                                {
                                    pictureBox.Image?.Dispose();
                                    pictureBox.Image = Image.FromStream(altStream);
                                    Console.WriteLine($"[FFT Color Mod] Loaded story character with alt resource name: {matchingResource}");
                                }
                            }
                        }
                        else
                        {
                            pictureBox.Image?.Dispose();
                            pictureBox.Image = null;
                            Console.WriteLine($"[FFT Color Mod] Embedded story character resource not found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FFT Color Mod] Error loading embedded story character preview: {ex.Message}");
                pictureBox.Image?.Dispose();
                pictureBox.Image = null;
            }
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
                        }
                    }
                }
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine($"[FFT Color Mod] Save button clicked");
            Console.WriteLine($"[FFT Color Mod] Current config state - Squire_Male: {_config.Squire_Male}");

            DialogResult = DialogResult.OK;
            Close();
        }

        private void VerifyAllSelections()
        {
            // Go through all ComboBoxes and ensure they have the correct selection
            Console.WriteLine("[FFT Color Mod] Verifying all ComboBox selections...");

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
                            Console.WriteLine($"[FFT Color Mod] Correcting {tag.JobName}: was {currentValue}, setting to {tag.ExpectedValue}");
                            comboBox.SelectedItem = tag.ExpectedValue;
                        }
                        else
                        {
                            Console.WriteLine($"[FFT Color Mod] {tag.JobName} is correctly set to {tag.ExpectedValue}");
                        }
                    }
                }
            }
        }

        private void CreateCustomTitleBar()
        {
            _titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.FromArgb(139, 37, 33),  // RELOADED red color
                ForeColor = Color.White
            };

            // Title label
            _titleLabel = new Label
            {
                Text = "FFT Color Mod - Configuration",
                Location = new Point(10, 5),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };

            // Close button
            _closeButton = new Button
            {
                Text = "✕",
                Size = new Size(30, 30),
                Location = new Point(Width - 30, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(139, 37, 33),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 50, 50);
            _closeButton.Click += (s, e) => Close();

            // Minimize button
            _minimizeButton = new Button
            {
                Text = "—",
                Size = new Size(30, 30),
                Location = new Point(Width - 60, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(139, 37, 33),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };
            _minimizeButton.FlatAppearance.BorderSize = 0;
            _minimizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(160, 40, 40);
            _minimizeButton.Click += (s, e) => WindowState = FormWindowState.Minimized;

            // Add controls to title bar
            _titleBar.Controls.Add(_titleLabel);
            _titleBar.Controls.Add(_closeButton);
            _titleBar.Controls.Add(_minimizeButton);

            // Add dragging functionality
            _titleBar.MouseDown += TitleBar_MouseDown;
            _titleBar.MouseMove += TitleBar_MouseMove;
            _titleBar.MouseUp += TitleBar_MouseUp;
            _titleLabel.MouseDown += TitleBar_MouseDown;
            _titleLabel.MouseMove += TitleBar_MouseMove;
            _titleLabel.MouseUp += TitleBar_MouseUp;

            Controls.Add(_titleBar);
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            _isDragging = true;
            _dragCursorPoint = Cursor.Position;
            _dragFormPoint = this.Location;
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(_dragCursorPoint));
                this.Location = Point.Add(_dragFormPoint, new Size(diff));
            }
        }

        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            _isDragging = false;
        }
    }
}