using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using FFTColorMod.Configuration.UI;
using FFTColorMod.Utilities;

namespace FFTColorMod.Configuration
{
    public partial class ConfigurationForm
    {
        private CharacterRowBuilder _rowBuilder;
        private Dictionary<string, StoryCharacterRegistry.StoryCharacterConfig> _storyCharacters;

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
            foreach (var characterConfig in _storyCharacters.Values.OrderBy(c => c.Name))
            {
                _rowBuilder.AddStoryCharacterRow(row++, characterConfig);
            }
        }

        private void AddJobRow(int row, string jobName, ColorScheme currentTheme, Action<ColorScheme> setter)
        {
            _rowBuilder.AddGenericCharacterRow(row, jobName, currentTheme, setter,
                () => _isFullyLoaded);
        }

        private void ResetAllCharacters()
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