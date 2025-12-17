using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FFTColorCustomizer.Services
{
    public class CharacterDefinitionService
    {
        private readonly List<CharacterDefinition> _characters = new List<CharacterDefinition>();

        public List<CharacterDefinition> GetAllCharacters()
        {
            // Return a copy to prevent "Collection was modified" errors during iteration
            return new List<CharacterDefinition>(_characters);
        }

        public void AddCharacter(CharacterDefinition character)
        {
            _characters.Add(character);
        }

        public CharacterDefinition? GetCharacterByName(string name)
        {
            return _characters.FirstOrDefault(c => c.Name == name);
        }

        public void LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var container = JsonSerializer.Deserialize<CharacterDefinitionsContainer>(json, options);
            if (container?.Characters != null)
            {
                _characters.Clear();
                _characters.AddRange(container.Characters);
            }
        }

        private class CharacterDefinitionsContainer
        {
            public List<CharacterDefinition> Characters { get; set; } = new List<CharacterDefinition>();
        }
    }
}
