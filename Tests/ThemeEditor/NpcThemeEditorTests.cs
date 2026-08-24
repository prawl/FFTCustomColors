using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using FFTColorCustomizer.ThemeEditor;
using FluentAssertions;

namespace Tests.ThemeEditor
{
    /// <summary>
    /// NPC characters get their own mappings subdirectory (Data/SectionMappings/NPC/) and
    /// their own separator group in the theme editor's template dropdown.
    /// </summary>
    public class NpcThemeEditorTests : IDisposable
    {
        private readonly string _tempMappingsDir;

        public NpcThemeEditorTests()
        {
            _tempMappingsDir = Path.Combine(Path.GetTempPath(), "NpcMappings_" + Guid.NewGuid());
            Directory.CreateDirectory(Path.Combine(_tempMappingsDir, "NPC"));

            var almaMapping = @"{
  ""job"": ""Alma"",
  ""sprite"": ""battle_aruma_spr.bin"",
  ""sections"": [
    { ""name"": ""Hair"", ""displayName"": ""Hair"", ""indices"": [1, 2], ""roles"": [""shadow"", ""base""] }
  ]
}";
            File.WriteAllText(Path.Combine(_tempMappingsDir, "NPC", "Alma.json"), almaMapping);

            var delitaMapping = @"{
  ""job"": ""DelitaChapter1"",
  ""sprite"": ""battle_dily_spr.bin"",
  ""sections"": [
    { ""name"": ""Underarmor"", ""displayName"": ""Underarmor"", ""indices"": [4, 3], ""roles"": [""highlight"", ""base""] }
  ]
}";
            File.WriteAllText(Path.Combine(_tempMappingsDir, "NPC", "DelitaChapter1.json"), delitaMapping);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempMappingsDir, true); } catch { }
        }

        [Fact]
        public void GetAvailableNpcCharacters_Should_List_Mappings_In_Npc_Directory()
        {
            var npcCharacters = SectionMappingLoader.GetAvailableNpcCharacters(_tempMappingsDir);

            npcCharacters.Should().Contain("Alma");
        }

        [Fact]
        public void GetAvailableNpcCharacters_Should_Return_Empty_When_Directory_Missing()
        {
            var emptyDir = Path.Combine(_tempMappingsDir, "DoesNotExist");

            var npcCharacters = SectionMappingLoader.GetAvailableNpcCharacters(emptyDir);

            npcCharacters.Should().BeEmpty();
        }

        [Fact]
        public void TemplateDropdown_Should_Group_Npc_Characters_Under_Separator()
        {
            using var panel = new ThemeEditorPanel(_tempMappingsDir, null, null);

            var dropdown = panel.Controls.Find("TemplateDropdown", true).OfType<ComboBox>().Single();
            var items = dropdown.Items.Cast<object>().Select(i => i.ToString()).ToList();

            items.Should().Contain("── NPCs ──");
            items.Should().Contain("Alma");

            // Alma is listed inside the NPC group: after the NPC separator, before any later separator
            var npcSeparatorIndex = items.IndexOf("── NPCs ──");
            var almaIndex = items.IndexOf("Alma");
            almaIndex.Should().BeGreaterThan(npcSeparatorIndex, "Alma belongs to the NPC group");
        }

        [Fact]
        public void TemplateDropdown_Should_Show_Delita_Chapters_Like_Ramzas()
        {
            using var panel = new ThemeEditorPanel(_tempMappingsDir, null, null);

            var dropdown = panel.Controls.Find("TemplateDropdown", true).OfType<ComboBox>().Single();
            var items = dropdown.Items.Cast<object>().Select(i => i.ToString()).ToList();

            // Raw registry names are internal keys, not labels; Ramza's chapter entries set
            // the display convention ("Ramza (Chapter 1)") and Delita's must match it.
            items.Should().Contain("Delita (Chapter 1)");
            items.Should().NotContain("DelitaChapter1");
        }
    }
}
