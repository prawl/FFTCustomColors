using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.IO;

namespace FFTColorMod.Configuration
{
    public class ConfigurationForm : Form
    {
        private Config _config;
        private TableLayoutPanel _mainPanel;
        private Button _saveButton;
        private Button _cancelButton;

        public ConfigurationForm(Config config)
        {
            _config = config;
            InitializeForm();
            LoadConfiguration();
        }

        private void InitializeForm()
        {
            Text = "FFT Color Mod - Configuration";
            Size = new Size(700, 700);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScroll = true;

            // Force image refresh after form loads
            this.Load += (s, e) => RefreshAllPreviews();

            _mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 50,
                AutoScroll = true,
                Padding = new Padding(10)
            };

            // Add column styles
            _mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            _mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            _mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            // Add header
            var headerLabel = new Label
            {
                Text = "Themes",
                Font = new Font("Arial", 12, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _mainPanel.SetColumnSpan(headerLabel, 3);
            _mainPanel.Controls.Add(headerLabel, 0, 0);

            Controls.Add(_mainPanel);

            // Add buttons panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 40,
                Padding = new Padding(5)
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 30
            };
            _cancelButton.Click += (s, e) => Close();

            _saveButton = new Button
            {
                Text = "Save",
                Width = 80,
                Height = 30
            };
            _saveButton.Click += SaveButton_Click;

            // Add debug button
            var debugButton = new Button
            {
                Text = "Debug Paths",
                Width = 100,
                Height = 30
            };
            debugButton.Click += (s, e) =>
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string previewDir = Path.Combine(baseDir, "Resources", "Previews");
                string testFile = Path.Combine(previewDir, "squire_male_original.png");

                string message = $"Base Directory:\n{baseDir}\n\n" +
                                $"Preview Directory:\n{previewDir}\n\n" +
                                $"Directory Exists: {Directory.Exists(previewDir)}\n\n" +
                                $"Test File:\n{testFile}\n\n" +
                                $"Test File Exists: {File.Exists(testFile)}\n\n";

                // Try to actually load the image
                if (File.Exists(testFile))
                {
                    try
                    {
                        using (var testImage = Image.FromFile(testFile))
                        {
                            message += $"Successfully loaded image: {testImage.Width}x{testImage.Height}\n";
                        }
                    }
                    catch (Exception ex)
                    {
                        message += $"Error loading image: {ex.Message}\n";
                    }
                }

                if (Directory.Exists(previewDir))
                {
                    var files = Directory.GetFiles(previewDir, "*.png").Take(5);
                    message += "\nFirst 5 PNG files found:\n";
                    foreach (var file in files)
                    {
                        message += Path.GetFileName(file) + "\n";
                    }
                }

                // Test what the mapping produces
                message += "\n\nTest name mapping:\n";
                message += $"'Squire (Male)' -> '{GetMappedFileName("Squire (Male)")}_original.png'\n";
                message += $"'Knight (Male)' -> '{GetMappedFileName("Knight (Male)")}_original.png'\n";

                MessageBox.Show(message, "Debug Path Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            string GetMappedFileName(string jobName)
            {
                return jobName.ToLower()
                    .Replace(" (male)", "_male")
                    .Replace(" (female)", "_female")
                    .Replace(" ", "_")
                    .Replace("(", "")
                    .Replace(")", "");
            }

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
                TextAlign = ContentAlignment.MiddleCenter
            };
            _mainPanel.SetColumnSpan(genericHeader, 3);
            _mainPanel.Controls.Add(genericHeader, 0, row++);

            // Squires
            AddJobRow(row++, "Squire (Male)", _config.Squire_Male, v => _config.Squire_Male = v);
            AddJobRow(row++, "Squire (Female)", _config.Squire_Female, v => _config.Squire_Female = v);

            // Knights
            AddJobRow(row++, "Knight (Male)", _config.Knight_Male, v => _config.Knight_Male = v);
            AddJobRow(row++, "Knight (Female)", _config.Knight_Female, v => _config.Knight_Female = v);

            // Monks
            AddJobRow(row++, "Monk (Male)", _config.Monk_Male, v => _config.Monk_Male = v);
            AddJobRow(row++, "Monk (Female)", _config.Monk_Female, v => _config.Monk_Female = v);

            // Archers
            AddJobRow(row++, "Archer (Male)", _config.Archer_Male, v => _config.Archer_Male = v);
            AddJobRow(row++, "Archer (Female)", _config.Archer_Female, v => _config.Archer_Female = v);

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

            // Ninjas
            AddJobRow(row++, "Ninja (Male)", _config.Ninja_Male, v => _config.Ninja_Male = v);
            AddJobRow(row++, "Ninja (Female)", _config.Ninja_Female, v => _config.Ninja_Female = v);

            // Samurai
            AddJobRow(row++, "Samurai (Male)", _config.Samurai_Male, v => _config.Samurai_Male = v);
            AddJobRow(row++, "Samurai (Female)", _config.Samurai_Female, v => _config.Samurai_Female = v);

            // Dragoons
            AddJobRow(row++, "Dragoon (Male)", _config.Dragoon_Male, v => _config.Dragoon_Male = v);
            AddJobRow(row++, "Dragoon (Female)", _config.Dragoon_Female, v => _config.Dragoon_Female = v);

            // Chemists
            AddJobRow(row++, "Chemist (Male)", _config.Chemist_Male, v => _config.Chemist_Male = v);
            AddJobRow(row++, "Chemist (Female)", _config.Chemist_Female, v => _config.Chemist_Female = v);

            // Geomancers
            AddJobRow(row++, "Geomancer (Male)", _config.Geomancer_Male, v => _config.Geomancer_Male = v);
            AddJobRow(row++, "Geomancer (Female)", _config.Geomancer_Female, v => _config.Geomancer_Female = v);

            // Mystics/Oracles
            AddJobRow(row++, "Mystic (Male)", _config.Mystic_Male, v => _config.Mystic_Male = v);
            AddJobRow(row++, "Mystic (Female)", _config.Mystic_Female, v => _config.Mystic_Female = v);

            // Mediators/Orators
            AddJobRow(row++, "Mediator (Male)", _config.Mediator_Male, v => _config.Mediator_Male = v);
            AddJobRow(row++, "Mediator (Female)", _config.Mediator_Female, v => _config.Mediator_Female = v);

            // Dancers (Female only)
            AddJobRow(row++, "Dancer (Female)", _config.Dancer_Female, v => _config.Dancer_Female = v);

            // Bards (Male only)
            AddJobRow(row++, "Bard (Male)", _config.Bard_Male, v => _config.Bard_Male = v);

            // Mimes
            AddJobRow(row++, "Mime (Male)", _config.Mime_Male, v => _config.Mime_Male = v);
            AddJobRow(row++, "Mime (Female)", _config.Mime_Female, v => _config.Mime_Female = v);

            // Calculators/Arithmeticians
            AddJobRow(row++, "Calculator (Male)", _config.Calculator_Male, v => _config.Calculator_Male = v);
            AddJobRow(row++, "Calculator (Female)", _config.Calculator_Female, v => _config.Calculator_Female = v);

            // Add header for story characters
            var storyHeader = new Label
            {
                Text = "=== Story Characters ===",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _mainPanel.SetColumnSpan(storyHeader, 3);
            _mainPanel.Controls.Add(storyHeader, 0, row++);

            // Story characters use different enums, need special handling
            AddStoryCharacterRow(row++, "Agrias", _config.Agrias);
            AddStoryCharacterRow(row++, "Orlandeau", _config.Orlandeau);
        }

        private void AddJobRow(int row, string jobName, ColorScheme currentTheme, Action<ColorScheme> setter)
        {
            var label = new Label
            {
                Text = jobName,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 3, 0, 0)  // Add top padding to align with dropdown text
            };
            _mainPanel.Controls.Add(label, 0, row);

            var comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                DataSource = Enum.GetValues(typeof(ColorScheme)),
                MaxDropDownItems = 16  // Show all themes at once
            };
            comboBox.SelectedItem = currentTheme;

            // Preview image
            var pictureBox = new PictureBox
            {
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // Load initial preview
            UpdatePreviewImage(pictureBox, jobName, currentTheme);

            // Store reference for refresh
            pictureBox.Tag = new { JobName = jobName, Setter = setter };

            comboBox.SelectedIndexChanged += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    var newTheme = (ColorScheme)comboBox.SelectedItem;
                    setter(newTheme);
                    UpdatePreviewImage(pictureBox, jobName, newTheme);
                }
            };

            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(pictureBox, 2, row);
        }

        private void AddStoryCharacterRow(int row, string characterName, object currentTheme)
        {
            var label = new Label
            {
                Text = characterName,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 3, 0, 0)  // Add top padding to align with dropdown text
            };
            _mainPanel.Controls.Add(label, 0, row);

            var comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                MaxDropDownItems = 16  // Show more items at once
            };

            // Preview image for story characters
            var pictureBox = new PictureBox
            {
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                BackColor = Color.White
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
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}