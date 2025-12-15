using System;
using System.Collections.Generic;
using System.Linq;

namespace FFTColorMod.Registry
{
    /// <summary>
    /// Registry for all generic (non-story) characters in FFT
    /// Similar to StoryCharacterRegistry but for generic job classes
    /// </summary>
    public class GenericCharacterRegistry
    {
        private static readonly Lazy<GenericCharacterRegistry> _instance = new(() => new GenericCharacterRegistry());
        private readonly Dictionary<string, GenericCharacterDefinition> _characters = new();
        private readonly Dictionary<string, string> _spriteNameMapping = new();

        public static GenericCharacterRegistry Instance => _instance.Value;

        public GenericCharacterRegistry()
        {
            InitializeCharacters();
            BuildSpriteNameMapping();
        }

        private void InitializeCharacters()
        {
            // Squires
            RegisterCharacter("Squire_Male", "Squire (Male)", "Generic Characters",
                "Color scheme for all male squires", "SquireMale", new[] { "mina_m" });
            RegisterCharacter("Squire_Female", "Squire (Female)", "Generic Characters",
                "Color scheme for all female squires", "SquireFemale", new[] { "mina_w" });

            // Knights
            RegisterCharacter("Knight_Male", "Knight (Male)", "Generic Characters",
                "Color scheme for all male knights", "KnightMale", new[] { "knight_m" });
            RegisterCharacter("Knight_Female", "Knight (Female)", "Generic Characters",
                "Color scheme for all female knights", "KnightFemale", new[] { "knight_w" });

            // Monks
            RegisterCharacter("Monk_Male", "Monk (Male)", "Generic Characters",
                "Color scheme for all male monks", "MonkMale", new[] { "monk_m" });
            RegisterCharacter("Monk_Female", "Monk (Female)", "Generic Characters",
                "Color scheme for all female monks", "MonkFemale", new[] { "monk_w" });

            // Archers (yumi = bow in Japanese)
            RegisterCharacter("Archer_Male", "Archer (Male)", "Generic Characters",
                "Color scheme for all male archers", "ArcherMale", new[] { "yumi_m" });
            RegisterCharacter("Archer_Female", "Archer (Female)", "Generic Characters",
                "Color scheme for all female archers", "ArcherFemale", new[] { "yumi_w" });

            // White Mages (siro = white)
            RegisterCharacter("WhiteMage_Male", "White Mage (Male)", "Generic Characters",
                "Color scheme for all male white mages", "WhiteMageMale", new[] { "siro_m" });
            RegisterCharacter("WhiteMage_Female", "White Mage (Female)", "Generic Characters",
                "Color scheme for all female white mages", "WhiteMageFemale", new[] { "siro_w" });

            // Black Mages (kuro = black)
            RegisterCharacter("BlackMage_Male", "Black Mage (Male)", "Generic Characters",
                "Color scheme for all male black mages", "BlackMageMale", new[] { "kuro_m" });
            RegisterCharacter("BlackMage_Female", "Black Mage (Female)", "Generic Characters",
                "Color scheme for all female black mages", "BlackMageFemale", new[] { "kuro_w" });

            // Time Mages (toki = time)
            RegisterCharacter("TimeMage_Male", "Time Mage (Male)", "Generic Characters",
                "Color scheme for all male time mages", "TimeMageMale", new[] { "toki_m" });
            RegisterCharacter("TimeMage_Female", "Time Mage (Female)", "Generic Characters",
                "Color scheme for all female time mages", "TimeMageFemale", new[] { "toki_w" });

            // Summoners (syou)
            RegisterCharacter("Summoner_Male", "Summoner (Male)", "Generic Characters",
                "Color scheme for all male summoners", "SummonerMale", new[] { "syou_m" });
            RegisterCharacter("Summoner_Female", "Summoner (Female)", "Generic Characters",
                "Color scheme for all female summoners", "SummonerFemale", new[] { "syou_w" });

            // Thieves
            RegisterCharacter("Thief_Male", "Thief (Male)", "Generic Characters",
                "Color scheme for all male thieves", "ThiefMale", new[] { "thief_m" });
            RegisterCharacter("Thief_Female", "Thief (Female)", "Generic Characters",
                "Color scheme for all female thieves", "ThiefFemale", new[] { "thief_w" });

            // Mediators/Orators (waju)
            RegisterCharacter("Mediator_Male", "Mediator (Male)", "Generic Characters",
                "Color scheme for all male mediators", "MediatorMale", new[] { "waju_m" });
            RegisterCharacter("Mediator_Female", "Mediator (Female)", "Generic Characters",
                "Color scheme for all female mediators", "MediatorFemale", new[] { "waju_w" });

            // Mystics/Oracles (onmyo)
            RegisterCharacter("Mystic_Male", "Mystic (Male)", "Generic Characters",
                "Color scheme for all male mystics", "MysticMale", new[] { "onmyo_m" });
            RegisterCharacter("Mystic_Female", "Mystic (Female)", "Generic Characters",
                "Color scheme for all female mystics", "MysticFemale", new[] { "onmyo_w" });

            // Geomancers (fusui)
            RegisterCharacter("Geomancer_Male", "Geomancer (Male)", "Generic Characters",
                "Color scheme for all male geomancers", "GeomancerMale", new[] { "fusui_m" });
            RegisterCharacter("Geomancer_Female", "Geomancer (Female)", "Generic Characters",
                "Color scheme for all female geomancers", "GeomancerFemale", new[] { "fusui_w" });

            // Dragoons (ryu = dragon)
            RegisterCharacter("Dragoon_Male", "Dragoon (Male)", "Generic Characters",
                "Color scheme for all male dragoons", "DragoonMale", new[] { "ryu_m" });
            RegisterCharacter("Dragoon_Female", "Dragoon (Female)", "Generic Characters",
                "Color scheme for all female dragoons", "DragoonFemale", new[] { "ryu_w" });

            // Samurai (samu)
            RegisterCharacter("Samurai_Male", "Samurai (Male)", "Generic Characters",
                "Color scheme for all male samurai", "SamuraiMale", new[] { "samu_m" });
            RegisterCharacter("Samurai_Female", "Samurai (Female)", "Generic Characters",
                "Color scheme for all female samurai", "SamuraiFemale", new[] { "samu_w" });

            // Ninjas
            RegisterCharacter("Ninja_Male", "Ninja (Male)", "Generic Characters",
                "Color scheme for all male ninjas", "NinjaMale", new[] { "ninja_m" });
            RegisterCharacter("Ninja_Female", "Ninja (Female)", "Generic Characters",
                "Color scheme for all female ninjas", "NinjaFemale", new[] { "ninja_w" });

            // Calculators/Arithmeticians (san)
            RegisterCharacter("Calculator_Male", "Calculator (Male)", "Generic Characters",
                "Color scheme for all male calculators", "CalculatorMale", new[] { "san_m" });
            RegisterCharacter("Calculator_Female", "Calculator (Female)", "Generic Characters",
                "Color scheme for all female calculators", "CalculatorFemale", new[] { "san_w" });

            // Bards (gin - male only)
            RegisterCharacter("Bard_Male", "Bard", "Generic Characters",
                "Color scheme for all male bards", "BardMale", new[] { "gin_m" });

            // Dancers (odori - female only)
            RegisterCharacter("Dancer_Female", "Dancer", "Generic Characters",
                "Color scheme for all female dancers", "DancerFemale", new[] { "odori_w" });

            // Mimes (mono)
            RegisterCharacter("Mime_Male", "Mime (Male)", "Generic Characters",
                "Color scheme for all male mimes", "MimeMale", new[] { "mono_m" });
            RegisterCharacter("Mime_Female", "Mime (Female)", "Generic Characters",
                "Color scheme for all female mimes", "MimeFemale", new[] { "mono_w" });

            // Chemists (item)
            RegisterCharacter("Chemist_Male", "Chemist (Male)", "Generic Characters",
                "Color scheme for all male chemists", "ChemistMale", new[] { "item_m" });
            RegisterCharacter("Chemist_Female", "Chemist (Female)", "Generic Characters",
                "Color scheme for all female chemists", "ChemistFemale", new[] { "item_w" });
        }

        private void RegisterCharacter(string key, string displayName, string category,
            string description, string jsonPropertyName, string[] spritePatterns)
        {
            _characters[key] = new GenericCharacterDefinition
            {
                Key = key,
                DisplayName = displayName,
                Category = category,
                Description = description,
                JsonPropertyName = jsonPropertyName,
                SpritePatterns = spritePatterns
            };
        }

        private void BuildSpriteNameMapping()
        {
            foreach (var character in _characters.Values)
            {
                foreach (var pattern in character.SpritePatterns)
                {
                    _spriteNameMapping[pattern] = character.Key;
                }
            }
        }

        public GenericCharacterDefinition GetCharacter(string key)
        {
            return _characters.GetValueOrDefault(key);
        }

        public IReadOnlyCollection<GenericCharacterDefinition> GetAllCharacters()
        {
            return _characters.Values;
        }

        public string GetCharacterBySpriteName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return null;

            // Try to find matching pattern in the sprite name
            foreach (var mapping in _spriteNameMapping)
            {
                if (spriteName.Contains(mapping.Key))
                {
                    return mapping.Value;
                }
            }

            return null;
        }

        public IReadOnlyDictionary<string, GenericCharacterDefinition> GetCharacterDictionary()
        {
            return _characters;
        }
    }

    /// <summary>
    /// Definition for a generic character
    /// </summary>
    public class GenericCharacterDefinition
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string JsonPropertyName { get; set; }
        public string[] SpritePatterns { get; set; }
    }
}