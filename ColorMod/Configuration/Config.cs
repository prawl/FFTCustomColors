using System.Collections.Generic;
using System.Text.Json.Serialization;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Config class using string-based themes for all characters.
    /// Uses dictionary-based storage with data-driven initialization from StoryCharacters.json.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(ReflectionBasedConfigJsonConverter))]
    [JsonConverter(typeof(ReflectionBasedSystemTextJsonConverter))]
    public class Config : Configurable<Config>
    {
        // Ramza HSL color settings
        public RamzaHslSettings RamzaColors { get; set; } = new();

        // Dictionary to store all generic job themes
        private Dictionary<string, string> _jobThemes = new();

        // Dictionary to store all story character themes (initialized from StoryCharacters.json)
        private Dictionary<string, string> _storyCharacterThemes = new();

        // Metadata for each job
        private static readonly Dictionary<string, JobMetadata> _jobMetadata = new()
        {
            // Squires
            ["Squire_Male"] = new JobMetadata("Generic Characters", "Squire (Male)", "Color scheme for all male squires", "SquireMale"),
            ["Squire_Female"] = new JobMetadata("Generic Characters", "Squire (Female)", "Color scheme for all female squires", "SquireFemale"),

            // Knights
            ["Knight_Male"] = new JobMetadata("Generic Characters", "Knight (Male)", "Color scheme for all male knights", "KnightMale"),
            ["Knight_Female"] = new JobMetadata("Generic Characters", "Knight (Female)", "Color scheme for all female knights", "KnightFemale"),

            // Monks
            ["Monk_Male"] = new JobMetadata("Generic Characters", "Monk (Male)", "Color scheme for all male monks", "MonkMale"),
            ["Monk_Female"] = new JobMetadata("Generic Characters", "Monk (Female)", "Color scheme for all female monks", "MonkFemale"),

            // Archers
            ["Archer_Male"] = new JobMetadata("Generic Characters", "Archer (Male)", "Color scheme for all male archers", "ArcherMale"),
            ["Archer_Female"] = new JobMetadata("Generic Characters", "Archer (Female)", "Color scheme for all female archers", "ArcherFemale"),

            // White Mages
            ["WhiteMage_Male"] = new JobMetadata("Generic Characters", "White Mage (Male)", "Color scheme for all male white mages", "WhiteMageMale"),
            ["WhiteMage_Female"] = new JobMetadata("Generic Characters", "White Mage (Female)", "Color scheme for all female white mages", "WhiteMageFemale"),

            // Black Mages
            ["BlackMage_Male"] = new JobMetadata("Generic Characters", "Black Mage (Male)", "Color scheme for all male black mages", "BlackMageMale"),
            ["BlackMage_Female"] = new JobMetadata("Generic Characters", "Black Mage (Female)", "Color scheme for all female black mages", "BlackMageFemale"),

            // Time Mages
            ["TimeMage_Male"] = new JobMetadata("Generic Characters", "Time Mage (Male)", "Color scheme for all male time mages", "TimeMageMale"),
            ["TimeMage_Female"] = new JobMetadata("Generic Characters", "Time Mage (Female)", "Color scheme for all female time mages", "TimeMageFemale"),

            // Summoners
            ["Summoner_Male"] = new JobMetadata("Generic Characters", "Summoner (Male)", "Color scheme for all male summoners", "SummonerMale"),
            ["Summoner_Female"] = new JobMetadata("Generic Characters", "Summoner (Female)", "Color scheme for all female summoners", "SummonerFemale"),

            // Thieves
            ["Thief_Male"] = new JobMetadata("Generic Characters", "Thief (Male)", "Color scheme for all male thieves", "ThiefMale"),
            ["Thief_Female"] = new JobMetadata("Generic Characters", "Thief (Female)", "Color scheme for all female thieves", "ThiefFemale"),

            // Mediators
            ["Mediator_Male"] = new JobMetadata("Generic Characters", "Mediator (Male)", "Color scheme for all male mediators", "MediatorMale"),
            ["Mediator_Female"] = new JobMetadata("Generic Characters", "Mediator (Female)", "Color scheme for all female mediators", "MediatorFemale"),

            // Mystics
            ["Mystic_Male"] = new JobMetadata("Generic Characters", "Mystic (Male)", "Color scheme for all male mystics", "MysticMale"),
            ["Mystic_Female"] = new JobMetadata("Generic Characters", "Mystic (Female)", "Color scheme for all female mystics", "MysticFemale"),

            // Geomancers
            ["Geomancer_Male"] = new JobMetadata("Generic Characters", "Geomancer (Male)", "Color scheme for all male geomancers", "GeomancerMale"),
            ["Geomancer_Female"] = new JobMetadata("Generic Characters", "Geomancer (Female)", "Color scheme for all female geomancers", "GeomancerFemale"),

            // Dragoons
            ["Dragoon_Male"] = new JobMetadata("Generic Characters", "Dragoon (Male)", "Color scheme for all male dragoons", "DragoonMale"),
            ["Dragoon_Female"] = new JobMetadata("Generic Characters", "Dragoon (Female)", "Color scheme for all female dragoons", "DragoonFemale"),

            // Samurai
            ["Samurai_Male"] = new JobMetadata("Generic Characters", "Samurai (Male)", "Color scheme for all male samurai", "SamuraiMale"),
            ["Samurai_Female"] = new JobMetadata("Generic Characters", "Samurai (Female)", "Color scheme for all female samurai", "SamuraiFemale"),

            // Ninjas
            ["Ninja_Male"] = new JobMetadata("Generic Characters", "Ninja (Male)", "Color scheme for all male ninjas", "NinjaMale"),
            ["Ninja_Female"] = new JobMetadata("Generic Characters", "Ninja (Female)", "Color scheme for all female ninjas", "NinjaFemale"),

            // Calculators
            ["Calculator_Male"] = new JobMetadata("Generic Characters", "Calculator (Male)", "Color scheme for all male calculators", "CalculatorMale"),
            ["Calculator_Female"] = new JobMetadata("Generic Characters", "Calculator (Female)", "Color scheme for all female calculators", "CalculatorFemale"),

            // Bards (Male only)
            ["Bard_Male"] = new JobMetadata("Generic Characters", "Bard (Male)", "Color scheme for all male bards", "BardMale"),

            // Dancers (Female only)
            ["Dancer_Female"] = new JobMetadata("Generic Characters", "Dancer (Female)", "Color scheme for all female dancers", "DancerFemale"),

            // Mimes
            ["Mime_Male"] = new JobMetadata("Generic Characters", "Mime (Male)", "Color scheme for all male mimes", "MimeMale"),
            ["Mime_Female"] = new JobMetadata("Generic Characters", "Mime (Female)", "Color scheme for all female mimes", "MimeFemale"),

            // Chemists
            ["Chemist_Male"] = new JobMetadata("Generic Characters", "Chemist (Male)", "Color scheme for all male chemists", "ChemistMale"),
            ["Chemist_Female"] = new JobMetadata("Generic Characters", "Chemist (Female)", "Color scheme for all female chemists", "ChemistFemale"),

            // WotL Jobs - Dark Knight
            ["DarkKnight_Male"] = new JobMetadata("WotL Jobs", "Dark Knight (Male)", "Color scheme for all male dark knights", "DarkKnightMale"),
            ["DarkKnight_Female"] = new JobMetadata("WotL Jobs", "Dark Knight (Female)", "Color scheme for all female dark knights", "DarkKnightFemale"),

            // WotL Jobs - Onion Knight
            ["OnionKnight_Male"] = new JobMetadata("WotL Jobs", "Onion Knight (Male)", "Color scheme for all male onion knights", "OnionKnightMale"),
            ["OnionKnight_Female"] = new JobMetadata("WotL Jobs", "Onion Knight (Female)", "Color scheme for all female onion knights", "OnionKnightFemale"),
        };

        public Config()
        {
            // Initialize all job themes to original
            foreach (var key in _jobMetadata.Keys)
            {
                _jobThemes[key] = "original";
            }

            // Initialize story character themes from CharacterDefinitionService (StoryCharacters.json)
            InitializeStoryCharacterThemes();
        }

        /// <summary>
        /// Initialize story character themes from CharacterDefinitionService.
        /// Falls back to hardcoded list if service is not available.
        /// </summary>
        private void InitializeStoryCharacterThemes()
        {
            try
            {
                var characters = CharacterServiceSingleton.Instance.GetAllCharacters();
                if (characters.Count > 0)
                {
                    foreach (var character in characters)
                    {
                        _storyCharacterThemes[character.Name] = character.DefaultTheme ?? "original";
                    }
                    return;
                }
            }
            catch
            {
                // Service not available, fall through to fallback
            }

            // Fallback: hardcoded story characters (for testing or when service unavailable)
            var fallbackCharacters = new[]
            {
                "RamzaChapter1", "RamzaChapter23", "RamzaChapter4",
                "Agrias", "Orlandeau", "Cloud", "Mustadio", "Reis",
                "Rapha", "Marach", "Beowulf", "Meliadoul"
            };
            foreach (var name in fallbackCharacters)
            {
                _storyCharacterThemes[name] = "original";
            }
        }

        // Generic job theme accessors
        public string GetJobTheme(string jobKey)
        {
            return _jobThemes.GetValueOrDefault(jobKey, "original");
        }

        public void SetJobTheme(string jobKey, string theme)
        {
            _jobThemes[jobKey] = theme;
        }

        // Story character theme accessors
        public string GetStoryCharacterTheme(string characterName)
        {
            return _storyCharacterThemes.GetValueOrDefault(characterName, "original");
        }

        public void SetStoryCharacterTheme(string characterName, string theme)
        {
            _storyCharacterThemes[characterName] = theme;
        }

        /// <summary>
        /// Indexer for unified access to both job and story character themes.
        /// Allows config["Knight_Male"] = "dark" syntax.
        /// </summary>
        public string this[string key]
        {
            get
            {
                if (_jobMetadata.ContainsKey(key))
                    return GetJobTheme(key);
                if (_storyCharacterThemes.ContainsKey(key))
                    return GetStoryCharacterTheme(key);
                return "original";
            }
            set
            {
                if (_jobMetadata.ContainsKey(key))
                    SetJobTheme(key, value);
                else
                    SetStoryCharacterTheme(key, value);
            }
        }

        // Helper methods
        public JobMetadata GetJobMetadata(string jobKey)
        {
            return _jobMetadata.GetValueOrDefault(jobKey);
        }

        public RamzaChapterHslSettings GetRamzaChapterSettings(int chapter)
        {
            return chapter switch
            {
                1 => RamzaColors.Chapter1,
                2 => RamzaColors.Chapter2,
                4 => RamzaColors.Chapter4,
                _ => RamzaColors.Chapter1
            };
        }

        public void SetRamzaChapterSettings(int chapter, RamzaChapterHslSettings settings)
        {
            switch (chapter)
            {
                case 1:
                    RamzaColors.Chapter1 = settings;
                    break;
                case 2:
                    RamzaColors.Chapter2 = settings;
                    break;
                case 4:
                    RamzaColors.Chapter4 = settings;
                    break;
            }
        }

        public string GetDisplayName(string characterName)
        {
            switch (characterName)
            {
                case "RamzaChapter1":
                    return "Ramza (Chapter 1)";
                case "RamzaChapter23":
                    return "Ramza (Chapter 2 & 3)";
                case "RamzaChapter4":
                    return "Ramza (Chapter 4)";
                default:
                    return characterName;
            }
        }

        public IEnumerable<string> GetAllJobKeys()
        {
            return _jobMetadata.Keys;
        }

        public IEnumerable<string> GetAllStoryCharacters()
        {
            return _storyCharacterThemes.Keys;
        }

        // Custom JSON serialization to maintain compatibility
        public Dictionary<string, object> ToJsonDictionary()
        {
            var result = new Dictionary<string, object>();

            // Add all generic character themes
            foreach (var kvp in _jobMetadata)
            {
                var jsonPropertyName = kvp.Value.JsonPropertyName;
                result[jsonPropertyName] = _jobThemes[kvp.Key];
            }

            // Add all story character themes
            foreach (var kvp in _storyCharacterThemes)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result;
        }
    }
}
