using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Configuration
{
    public partial class ConfigurationForm
    {
        private CharacterRowBuilder _rowBuilder;
        private Dictionary<string, StoryCharacterRegistry.StoryCharacterConfig> _storyCharacters;
        private MyThemesPanel _myThemesPanel;

        private void InitializeRowBuilder()
        {
            _rowBuilder = new CharacterRowBuilder(
                _mainPanel,
                _previewManager,
                () => _isInitializing,
                _genericCharacterControls,
                _storyCharacterControls
            );
        }

        private void LoadConfiguration()
        {
            int row = 1;

            // Add header for generic characters
            var genericHeader = CreateCollapsibleHeader("Generic Characters", _genericCharactersCollapsed, row++);
            genericHeader.Click += (sender, e) => ToggleGenericCharactersVisibility(genericHeader);

            _genericCharacterStartRow = row;

            // Add all generic character rows
            LoadGenericCharacters(ref row);

            _genericCharacterEndRow = row - 1;

            // Apply initial collapsed state if needed
            if (_genericCharactersCollapsed)
            {
                SetControlsVisibility(_genericCharacterControls, false);
            }

            // Add header for story characters
            var storyHeader = CreateCollapsibleHeader("Story Characters", _storyCharactersCollapsed, row++);
            storyHeader.Click += (sender, e) => ToggleStoryCharactersVisibility(storyHeader);

            // Load story characters using the registry
            LoadStoryCharacters(ref row);

            // Add header for theme editor
            var themeEditorHeader = CreateCollapsibleHeader("Theme Editor", _themeEditorCollapsed, row++);
            themeEditorHeader.Click += (sender, e) => ToggleThemeEditorVisibility(themeEditorHeader);

            // Add theme editor panel with mappings and sprites directories
            string? mappingsDirectory = null;
            string? spritesDirectory = null;
            if (!string.IsNullOrEmpty(_modPath))
            {
                mappingsDirectory = System.IO.Path.Combine(_modPath, "Data", "SectionMappings");
                // Pass the unit/ directory - ThemeEditorPanel will construct full path based on character type
                spritesDirectory = System.IO.Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            }
            var themeEditorPanel = new ThemeEditorPanel(mappingsDirectory, spritesDirectory);
            themeEditorPanel.Dock = DockStyle.Fill;
            _mainPanel.Controls.Add(themeEditorPanel, 0, row);
            _mainPanel.SetColumnSpan(themeEditorPanel, 3);
            _themeEditorControls.Add(themeEditorPanel);

            // Store reference to theme editor panel
            ThemeEditorPanel = themeEditorPanel;

            // Wire up theme saved event
            themeEditorPanel.ThemeSaved += OnThemeSaved;

            // Apply initial collapsed state
            if (_themeEditorCollapsed)
            {
                themeEditorPanel.Visible = false;
            }
            row++;

            // Add header for My Themes
            var myThemesHeader = CreateCollapsibleHeader("My Themes", _myThemesCollapsed, row++);
            myThemesHeader.Click += (sender, e) => ToggleMyThemesVisibility(myThemesHeader);

            // Add My Themes panel
            _myThemesPanel = new MyThemesPanel(_modPath);
            _myThemesPanel.Dock = DockStyle.Fill;
            _mainPanel.Controls.Add(_myThemesPanel, 0, row);
            _mainPanel.SetColumnSpan(_myThemesPanel, 3);
            _myThemesControls.Add(_myThemesPanel);

            // Wire up theme deleted event to refresh dropdowns
            _myThemesPanel.ThemeDeleted += OnThemeDeleted;

            // Apply initial collapsed state
            if (_myThemesCollapsed)
            {
                _myThemesPanel.Visible = false;
            }
            row++;
        }

        private void OnThemeDeleted(object? sender, ThemeDeletedEventArgs e)
        {
            Utilities.ModLogger.Log($"[CONFIG_FORM] OnThemeDeleted received: Job={e.JobName}, Theme={e.ThemeName}");
            // Refresh dropdowns for the job whose theme was deleted
            RefreshDropdownsForJob(e.JobName);
        }

        private void OnThemeSaved(object? sender, EventArgs e)
        {
            if (e is not ThemeSavedEventArgs args)
                return;

            try
            {
                var userThemeService = new UserThemeService(_modPath!);
                userThemeService.SaveTheme(args.JobName, args.ThemeName, args.PaletteData);

                // Refresh dropdowns for the job that was saved
                RefreshDropdownsForJob(args.JobName);

                // Refresh the My Themes panel
                _myThemesPanel?.RefreshThemes();

                // Format job name for display (e.g., "Squire_Male" -> "Squire (Male)")
                var displayJobName = args.JobName.Replace("_", " (") + ")";
                if (!args.JobName.Contains("_"))
                {
                    displayJobName = args.JobName;
                }

                // Determine if this is a story character or generic character
                var isStoryCharacter = _storyCharacters != null && _storyCharacters.ContainsKey(args.JobName);
                var sectionName = isStoryCharacter ? "Story Characters" : "Generic Characters";

                MessageBox.Show($"Theme '{args.ThemeName}' saved successfully!\n\nYou can select it under \"{displayJobName}\" in the \"{sectionName}\" section above.", "Theme Saved",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
            {
                MessageBox.Show(ex.Message, "Theme Already Exists",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save theme: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshDropdownsForJob(string jobName)
        {
            Utilities.ModLogger.Log($"[REFRESH] RefreshDropdownsForJob called for: {jobName}");
            var userThemeService = new UserThemeService(_modPath!);
            var userThemes = userThemeService.GetUserThemes(jobName);
            Utilities.ModLogger.Log($"[REFRESH] User themes for {jobName}: [{string.Join(", ", userThemes)}]");

            int comboBoxCount = 0;
            int matchCount = 0;

            // Find all ThemeComboBox controls for this job and refresh them
            foreach (Control control in _mainPanel.Controls)
            {
                if (control is ThemeComboBox comboBox && comboBox.Tag != null)
                {
                    comboBoxCount++;
                    try
                    {
                        dynamic tag = comboBox.Tag;
                        if (tag.JobName != null)
                        {
                            var comboJobName = CharacterRowBuilder.ConvertJobNameToPropertyFormat(tag.JobName.ToString());
                            Utilities.ModLogger.Log($"[REFRESH] Checking comboBox: tag.JobName={tag.JobName}, converted={comboJobName}, target={jobName}");
                            if (comboJobName == jobName)
                            {
                                matchCount++;
                                Utilities.ModLogger.Log($"[REFRESH] MATCH! Refreshing comboBox for {jobName}");
                                comboBox.RefreshUserThemes(userThemes);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Utilities.ModLogger.Log($"[REFRESH] Error checking tag: {ex.Message}");
                    }
                }
            }

            Utilities.ModLogger.Log($"[REFRESH] Found {comboBoxCount} ThemeComboBox controls, {matchCount} matched {jobName}");
        }

        private void LoadGenericCharacters(ref int row)
        {
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
        }

        private void LoadStoryCharacters(ref int row)
        {
            _storyCharacters = StoryCharacterRegistry.GetStoryCharacters(_config);

            // Load all available story characters from the registry
            // The registry automatically loads from StoryCharacters.json
            // Put Ramza characters first (in chapter order), then sort the rest alphabetically
            var sortedCharacters = _storyCharacters.Values
                .OrderBy(c =>
                {
                    if (c.Name == "RamzaChapter1") return "0_1";
                    if (c.Name == "RamzaChapter23") return "0_2";
                    if (c.Name == "RamzaChapter4") return "0_3";
                    return c.Name;
                });

            foreach (var characterConfig in sortedCharacters)
            {
                _rowBuilder.AddStoryCharacterRow(row++, characterConfig);
            }
        }

        private void AddJobRow(int row, string jobName, string currentTheme, Action<string> setter)
        {
            _rowBuilder.AddGenericCharacterRow(row, jobName, currentTheme, setter,
                () => _isFullyLoaded);
        }

        private void ResetAllCharacters()
        {
            // Reset all generic characters
            _config.Squire_Male = "original";
            _config.Squire_Female = "original";
            _config.Chemist_Male = "original";
            _config.Chemist_Female = "original";
            _config.Knight_Male = "original";
            _config.Knight_Female = "original";
            _config.Archer_Male = "original";
            _config.Archer_Female = "original";
            _config.Monk_Male = "original";
            _config.Monk_Female = "original";
            _config.WhiteMage_Male = "original";
            _config.WhiteMage_Female = "original";
            _config.BlackMage_Male = "original";
            _config.BlackMage_Female = "original";
            _config.TimeMage_Male = "original";
            _config.TimeMage_Female = "original";
            _config.Summoner_Male = "original";
            _config.Summoner_Female = "original";
            _config.Thief_Male = "original";
            _config.Thief_Female = "original";
            _config.Mediator_Male = "original";
            _config.Mediator_Female = "original";
            _config.Mystic_Male = "original";
            _config.Mystic_Female = "original";
            _config.Geomancer_Male = "original";
            _config.Geomancer_Female = "original";
            _config.Dragoon_Male = "original";
            _config.Dragoon_Female = "original";
            _config.Samurai_Male = "original";
            _config.Samurai_Female = "original";
            _config.Ninja_Male = "original";
            _config.Ninja_Female = "original";
            _config.Calculator_Male = "original";
            _config.Calculator_Female = "original";
            _config.Bard_Male = "original";
            _config.Dancer_Female = "original";
            _config.Mime_Male = "original";
            _config.Mime_Female = "original";

            // Reset all story characters using the registry
            StoryCharacterRegistry.ResetAllStoryCharacters(_config);
        }

        private void VerifyAllSelections()
        {
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
