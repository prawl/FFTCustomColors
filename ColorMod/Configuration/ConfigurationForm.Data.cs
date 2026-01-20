using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;
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

            // Add header for WotL Jobs
            var wotlHeader = CreateCollapsibleHeader("WotL Jobs", _wotlJobsCollapsed, row++);
            wotlHeader.Click += (sender, e) => ToggleWotLJobsVisibility(wotlHeader);

            // Load WotL jobs
            LoadWotLJobs(ref row);

            // Apply initial collapsed state for WotL jobs
            if (_wotlJobsCollapsed)
            {
                SetControlsVisibility(_wotlJobsControls, false);
            }

            // Add header for theme editor
            var themeEditorHeader = CreateCollapsibleHeader("Theme Editor", _themeEditorCollapsed, row++);
            themeEditorHeader.Click += (sender, e) => ToggleThemeEditorVisibility(themeEditorHeader);

            // Add theme editor panel with mappings and sprites directories
            string? mappingsDirectory = null;
            string? spritesDirectory = null;
            string? modsDirectory = null;
            if (!string.IsNullOrEmpty(_modPath))
            {
                mappingsDirectory = System.IO.Path.Combine(_modPath, "Data", "SectionMappings");
                // Pass the unit/ directory - ThemeEditorPanel will construct full path based on character type
                spritesDirectory = System.IO.Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                // Mods directory is the parent of the mod path (for GenericJobs detection)
                modsDirectory = System.IO.Path.GetDirectoryName(_modPath);
            }
            var themeEditorPanel = new ThemeEditorPanel(mappingsDirectory, spritesDirectory, modsDirectory);
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
                // Check if this is a Ramza chapter - use NXD-based saving
                var ramzaThemeSaver = new Services.RamzaThemeSaver();
                if (ramzaThemeSaver.IsRamzaChapter(args.JobName))
                {
                    SaveRamzaTheme(args, ramzaThemeSaver);
                    return;
                }

                // Standard theme saving for non-Ramza characters
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

                // Determine which section this job belongs to
                var wotlJobs = new HashSet<string> { "DarkKnight_Male", "DarkKnight_Female", "OnionKnight_Male", "OnionKnight_Female" };
                var isWotlJob = wotlJobs.Contains(args.JobName);
                var isStoryCharacter = _storyCharacters != null && _storyCharacters.ContainsKey(args.JobName);
                var sectionName = isWotlJob ? "WotL Jobs" : (isStoryCharacter ? "Story Characters" : "Generic Characters");

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

        private void SaveRamzaTheme(ThemeSavedEventArgs args, Services.RamzaThemeSaver ramzaThemeSaver)
        {
            var chapter = ramzaThemeSaver.GetChapterFromJobName(args.JobName);
            var chapterDisplay = chapter == 2 ? "Chapter 2/3" : $"Chapter {chapter}";

            // Normalize job name to canonical format for consistent storage
            var normalizedJobName = ramzaThemeSaver.NormalizeJobName(args.JobName);

            // Save the theme to UserThemes registry first (so it appears in dropdown)
            var userThemeService = new UserThemeService(_modPath!);
            userThemeService.SaveTheme(normalizedJobName, args.ThemeName, args.PaletteData);

            // Save the theme using the NXD patcher (updates SQLite and patches NXD)
            var success = ramzaThemeSaver.SaveTheme(args.JobName, args.PaletteData, _modPath!);

            if (success)
            {
                // Refresh dropdowns for the job that was saved (use normalized name)
                RefreshDropdownsForJob(normalizedJobName);

                // Refresh the My Themes panel
                _myThemesPanel?.RefreshThemes();

                var message = $"Ramza {chapterDisplay} theme '{args.ThemeName}' saved successfully!\n\n" +
                              $"You can select it under \"Ramza {chapterDisplay}\" in the Story Characters section.";

                MessageBox.Show(message, "Ramza Theme Saved",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                Utilities.ModLogger.Log($"[RAMZA_THEME] Saved theme for {args.JobName}");
            }
            else
            {
                MessageBox.Show($"Failed to save Ramza {chapterDisplay} theme.\n\nCheck the mod logs for details.",
                    "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);

                Utilities.ModLogger.Log($"[RAMZA_THEME] Failed to save theme for {args.JobName}");
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
            // Load all generic jobs from metadata (category = "Generic Characters")
            foreach (var jobKey in _config.GetAllJobKeys())
            {
                var metadata = _config.GetJobMetadata(jobKey);
                if (metadata?.Category == "Generic Characters")
                {
                    var key = jobKey; // Capture for closure
                    AddJobRow(row++, metadata.DisplayName, _config[key], v => _config[key] = v);
                }
            }
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

        private void LoadWotLJobs(ref int row)
        {
            // Detect GenericJobs mod state
            var modsDirectory = System.IO.Path.GetDirectoryName(_modPath);
            var modState = GenericJobsState.NotInstalled;
            if (!string.IsNullOrEmpty(modsDirectory))
            {
                var detector = new GenericJobsDetector(modsDirectory);
                modState = detector.State;
            }

            // Add download link
            var downloadLink = new LinkLabel
            {
                Text = "GenericJobs Mod",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                LinkColor = Color.FromArgb(100, 150, 255),
                ActiveLinkColor = Color.FromArgb(150, 200, 255),
                VisitedLinkColor = Color.FromArgb(100, 150, 255),
                Padding = new Padding(5, 5, 0, 0)
            };
            downloadLink.LinkClicked += (s, e) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://www.nexusmods.com/finalfantasytacticstheivalicechronicles/mods/34",
                    UseShellExecute = true
                });
            };
            _mainPanel.Controls.Add(downloadLink, 0, row);
            _mainPanel.SetColumnSpan(downloadLink, 3);
            _wotlJobsControls.Add(downloadLink);
            row++;

            // Determine status text and color based on state
            string statusText;
            Color statusColor;
            switch (modState)
            {
                case GenericJobsState.InstalledAndEnabled:
                    statusText = "✓ Generic Jobs Mod Enabled";
                    statusColor = Color.FromArgb(100, 200, 100);  // Green
                    break;
                case GenericJobsState.InstalledButDisabled:
                    statusText = "⚠ Generic Jobs Mod is installed but disabled - enable it in Reloaded-II";
                    statusColor = Color.FromArgb(200, 180, 80);   // Yellow/Orange
                    break;
                default:
                    statusText = "✗ Generic Jobs Mod not installed - these themes will not work";
                    statusColor = Color.FromArgb(200, 100, 100);  // Red
                    break;
            }

            // Add status label
            var statusLabel = new Label
            {
                Text = statusText,
                ForeColor = statusColor,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Padding = new Padding(5, 0, 0, 5)
            };
            _mainPanel.Controls.Add(statusLabel, 0, row);
            _mainPanel.SetColumnSpan(statusLabel, 3);
            _wotlJobsControls.Add(statusLabel);
            row++;

            // Load all WotL jobs from metadata (category = "WotL Jobs")
            foreach (var jobKey in _config.GetAllJobKeys())
            {
                var metadata = _config.GetJobMetadata(jobKey);
                if (metadata?.Category == "WotL Jobs")
                {
                    var key = jobKey; // Capture for closure
                    AddWotLJobRow(row++, metadata.DisplayName, _config[key], v => _config[key] = v);
                }
            }
        }

        private void AddWotLJobRow(int row, string jobName, string currentTheme, Action<string> setter)
        {
            _rowBuilder.AddGenericCharacterRow(row, jobName, currentTheme, setter,
                () => _isFullyLoaded, _wotlJobsControls);
        }

        private void AddJobRow(int row, string jobName, string currentTheme, Action<string> setter)
        {
            _rowBuilder.AddGenericCharacterRow(row, jobName, currentTheme, setter,
                () => _isFullyLoaded);
        }

        private void ResetAllCharacters()
        {
            // Reset all job themes (generic characters and WotL jobs)
            foreach (var jobKey in _config.GetAllJobKeys())
            {
                _config[jobKey] = "original";
            }

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
