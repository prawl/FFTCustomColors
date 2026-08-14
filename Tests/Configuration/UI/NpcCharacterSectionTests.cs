using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Tests.Helpers;
using FluentAssertions;

namespace Tests.Configuration.UI
{
    /// <summary>
    /// NPC characters (Alma is the pilot) live in their own collapsible config-window
    /// section, separate from Story Characters, while reusing the same lazy-loaded
    /// preview row machinery.
    /// </summary>
    public class NpcCharacterSectionTests
    {
        [Fact]
        public void Registry_Should_Pass_Category_Through_To_UI_Config()
        {
            // Arrange
            var service = new CharacterDefinitionService();
            service.AddCharacter(new CharacterDefinition
            {
                Name = "Alma",
                Category = "NPC",
                SpriteNames = new[] { "aruma" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original" }
            });
            service.AddCharacter(new CharacterDefinition
            {
                Name = "Agrias",
                SpriteNames = new[] { "aguri" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original" }
            });
            var config = new Config();

            // Act
            var characters = StoryCharacterRegistry.GetStoryCharactersFromService(config, service);

            // Assert
            characters["Alma"].Category.Should().Be("NPC");
            characters["Agrias"].Category.Should().Be("Story");
        }

        [Fact]
        public void ConfigurationForm_Should_Render_Npc_Section_With_Alma_Row()
        {
            // Arrange - the form loads characters from the repo StoryCharacters.json
            CharacterServiceSingleton.Reset();
            var config = new Config();
            using var form = new TestConfigurationForm(config);
            var handle = form.Handle; // force handle creation so LoadConfiguration ran

            // Assert - an NPC Characters header exists
            var mainPanel = GetPrivateField<TableLayoutPanel>(form, "_mainPanel");
            var headerTexts = mainPanel.Controls.OfType<Label>().Select(l => l.Text).ToList();
            headerTexts.Should().Contain(t => t.Contains("NPCs"),
                "the config window needs an NPCs section header");

            // Assert - Alma's dropdown is tracked by the NPC section, not the Story section
            var npcControls = GetPrivateField<List<Control>>(form, "_npcCharacterControls");
            var storyControls = GetPrivateField<List<Control>>(form, "_storyCharacterControls");

            FindComboTagged(npcControls, "Alma").Should().NotBeNull(
                "Alma's dropdown should collapse with the NPC section");
            FindComboTagged(storyControls, "Alma").Should().BeNull(
                "Alma should not be duplicated into the Story section");
            FindComboTagged(storyControls, "Agrias").Should().NotBeNull(
                "existing story characters must stay in the Story section");
        }

        private static T GetPrivateField<T>(object target, string name) where T : class
        {
            var field = target.GetType().BaseType == typeof(ConfigurationForm)
                ? typeof(ConfigurationForm).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                : target.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.Should().NotBeNull($"field {name} should exist on ConfigurationForm");
            return field!.GetValue(target) as T;
        }

        private static Control? FindComboTagged(List<Control> controls, string jobName)
        {
            return controls?.FirstOrDefault(c =>
            {
                if (c is not ComboBox || c.Tag == null) return false;
                var prop = c.Tag.GetType().GetProperty("JobName");
                return prop?.GetValue(c.Tag)?.ToString() == jobName;
            });
        }
    }
}
