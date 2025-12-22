using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Config class using string-based themes for all characters
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(ReflectionBasedConfigJsonConverter))]
    [JsonConverter(typeof(ReflectionBasedSystemTextJsonConverter))]
    public class Config : Configurable<Config>
    {
        // Dictionary to store all generic job themes
        private Dictionary<string, string> _jobThemes = new();

        // Dictionary to store all story character themes
        private Dictionary<string, string> _storyCharacterThemes = new();

        // Metadata for each job
        private static readonly Dictionary<string, JobMetadata> _jobMetadata = new()
        {
            // Squires
            ["Squire_Male"] = new JobMetadata("Generic Characters", "Male Squire", "Color scheme for all male squires", "SquireMale"),
            ["Squire_Female"] = new JobMetadata("Generic Characters", "Female Squire", "Color scheme for all female squires", "SquireFemale"),

            // Knights
            ["Knight_Male"] = new JobMetadata("Generic Characters", "Male Knight", "Color scheme for all male knights", "KnightMale"),
            ["Knight_Female"] = new JobMetadata("Generic Characters", "Female Knight", "Color scheme for all female knights", "KnightFemale"),

            // Monks
            ["Monk_Male"] = new JobMetadata("Generic Characters", "Male Monk", "Color scheme for all male monks", "MonkMale"),
            ["Monk_Female"] = new JobMetadata("Generic Characters", "Female Monk", "Color scheme for all female monks", "MonkFemale"),

            // Archers
            ["Archer_Male"] = new JobMetadata("Generic Characters", "Male Archer", "Color scheme for all male archers", "ArcherMale"),
            ["Archer_Female"] = new JobMetadata("Generic Characters", "Female Archer", "Color scheme for all female archers", "ArcherFemale"),

            // White Mages
            ["WhiteMage_Male"] = new JobMetadata("Generic Characters", "Male White Mage", "Color scheme for all male white mages", "WhiteMageMale"),
            ["WhiteMage_Female"] = new JobMetadata("Generic Characters", "Female White Mage", "Color scheme for all female white mages", "WhiteMageFemale"),

            // Black Mages
            ["BlackMage_Male"] = new JobMetadata("Generic Characters", "Male Black Mage", "Color scheme for all male black mages", "BlackMageMale"),
            ["BlackMage_Female"] = new JobMetadata("Generic Characters", "Female Black Mage", "Color scheme for all female black mages", "BlackMageFemale"),

            // Time Mages
            ["TimeMage_Male"] = new JobMetadata("Generic Characters", "Male Time Mage", "Color scheme for all male time mages", "TimeMageMale"),
            ["TimeMage_Female"] = new JobMetadata("Generic Characters", "Female Time Mage", "Color scheme for all female time mages", "TimeMageFemale"),

            // Summoners
            ["Summoner_Male"] = new JobMetadata("Generic Characters", "Male Summoner", "Color scheme for all male summoners", "SummonerMale"),
            ["Summoner_Female"] = new JobMetadata("Generic Characters", "Female Summoner", "Color scheme for all female summoners", "SummonerFemale"),

            // Thieves
            ["Thief_Male"] = new JobMetadata("Generic Characters", "Male Thief", "Color scheme for all male thieves", "ThiefMale"),
            ["Thief_Female"] = new JobMetadata("Generic Characters", "Female Thief", "Color scheme for all female thieves", "ThiefFemale"),

            // Mediators
            ["Mediator_Male"] = new JobMetadata("Generic Characters", "Male Mediator", "Color scheme for all male mediators", "MediatorMale"),
            ["Mediator_Female"] = new JobMetadata("Generic Characters", "Female Mediator", "Color scheme for all female mediators", "MediatorFemale"),

            // Mystics
            ["Mystic_Male"] = new JobMetadata("Generic Characters", "Male Mystic", "Color scheme for all male mystics", "MysticMale"),
            ["Mystic_Female"] = new JobMetadata("Generic Characters", "Female Mystic", "Color scheme for all female mystics", "MysticFemale"),

            // Geomancers
            ["Geomancer_Male"] = new JobMetadata("Generic Characters", "Male Geomancer", "Color scheme for all male geomancers", "GeomancerMale"),
            ["Geomancer_Female"] = new JobMetadata("Generic Characters", "Female Geomancer", "Color scheme for all female geomancers", "GeomancerFemale"),

            // Dragoons
            ["Dragoon_Male"] = new JobMetadata("Generic Characters", "Male Dragoon", "Color scheme for all male dragoons", "DragoonMale"),
            ["Dragoon_Female"] = new JobMetadata("Generic Characters", "Female Dragoon", "Color scheme for all female dragoons", "DragoonFemale"),

            // Samurai
            ["Samurai_Male"] = new JobMetadata("Generic Characters", "Male Samurai", "Color scheme for all male samurai", "SamuraiMale"),
            ["Samurai_Female"] = new JobMetadata("Generic Characters", "Female Samurai", "Color scheme for all female samurai", "SamuraiFemale"),

            // Ninjas
            ["Ninja_Male"] = new JobMetadata("Generic Characters", "Male Ninja", "Color scheme for all male ninjas", "NinjaMale"),
            ["Ninja_Female"] = new JobMetadata("Generic Characters", "Female Ninja", "Color scheme for all female ninjas", "NinjaFemale"),

            // Calculators
            ["Calculator_Male"] = new JobMetadata("Generic Characters", "Male Calculator", "Color scheme for all male calculators", "CalculatorMale"),
            ["Calculator_Female"] = new JobMetadata("Generic Characters", "Female Calculator", "Color scheme for all female calculators", "CalculatorFemale"),

            // Bards (Male only)
            ["Bard_Male"] = new JobMetadata("Generic Characters", "Male Bard", "Color scheme for all male bards", "BardMale"),

            // Dancers (Female only)
            ["Dancer_Female"] = new JobMetadata("Generic Characters", "Female Dancer", "Color scheme for all female dancers", "DancerFemale"),

            // Mimes
            ["Mime_Male"] = new JobMetadata("Generic Characters", "Male Mime", "Color scheme for all male mimes", "MimeMale"),
            ["Mime_Female"] = new JobMetadata("Generic Characters", "Female Mime", "Color scheme for all female mimes", "MimeFemale"),

            // Chemists
            ["Chemist_Male"] = new JobMetadata("Generic Characters", "Male Chemist", "Color scheme for all male chemists", "ChemistMale"),
            ["Chemist_Female"] = new JobMetadata("Generic Characters", "Female Chemist", "Color scheme for all female chemists", "ChemistFemale"),
        };

        public Config()
        {
            // Initialize all job themes to original
            foreach (var key in _jobMetadata.Keys)
            {
                _jobThemes[key] = "original";
            }

            // Initialize story character themes
            _storyCharacterThemes["Agrias"] = "original";
            _storyCharacterThemes["Orlandeau"] = "original";
            _storyCharacterThemes["Cloud"] = "original";
            _storyCharacterThemes["Mustadio"] = "original";
            _storyCharacterThemes["Reis"] = "original";
            _storyCharacterThemes["Rapha"] = "original";
            _storyCharacterThemes["Marach"] = "original";
            _storyCharacterThemes["Beowulf"] = "original";
            _storyCharacterThemes["Meliadoul"] = "original";
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

        // Properties for backward compatibility with generic jobs
        [Newtonsoft.Json.JsonIgnore]
        public string Squire_Male
        {
            get => GetJobTheme("Squire_Male");
            set => SetJobTheme("Squire_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Squire_Female
        {
            get => GetJobTheme("Squire_Female");
            set => SetJobTheme("Squire_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Knight_Male
        {
            get => GetJobTheme("Knight_Male");
            set => SetJobTheme("Knight_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Knight_Female
        {
            get => GetJobTheme("Knight_Female");
            set => SetJobTheme("Knight_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Monk_Male
        {
            get => GetJobTheme("Monk_Male");
            set => SetJobTheme("Monk_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Monk_Female
        {
            get => GetJobTheme("Monk_Female");
            set => SetJobTheme("Monk_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Archer_Male
        {
            get => GetJobTheme("Archer_Male");
            set => SetJobTheme("Archer_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Archer_Female
        {
            get => GetJobTheme("Archer_Female");
            set => SetJobTheme("Archer_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string WhiteMage_Male
        {
            get => GetJobTheme("WhiteMage_Male");
            set => SetJobTheme("WhiteMage_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string WhiteMage_Female
        {
            get => GetJobTheme("WhiteMage_Female");
            set => SetJobTheme("WhiteMage_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string BlackMage_Male
        {
            get => GetJobTheme("BlackMage_Male");
            set => SetJobTheme("BlackMage_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string BlackMage_Female
        {
            get => GetJobTheme("BlackMage_Female");
            set => SetJobTheme("BlackMage_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string TimeMage_Male
        {
            get => GetJobTheme("TimeMage_Male");
            set => SetJobTheme("TimeMage_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string TimeMage_Female
        {
            get => GetJobTheme("TimeMage_Female");
            set => SetJobTheme("TimeMage_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Summoner_Male
        {
            get => GetJobTheme("Summoner_Male");
            set => SetJobTheme("Summoner_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Summoner_Female
        {
            get => GetJobTheme("Summoner_Female");
            set => SetJobTheme("Summoner_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Thief_Male
        {
            get => GetJobTheme("Thief_Male");
            set => SetJobTheme("Thief_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Thief_Female
        {
            get => GetJobTheme("Thief_Female");
            set => SetJobTheme("Thief_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Mediator_Male
        {
            get => GetJobTheme("Mediator_Male");
            set => SetJobTheme("Mediator_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Mediator_Female
        {
            get => GetJobTheme("Mediator_Female");
            set => SetJobTheme("Mediator_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Mystic_Male
        {
            get => GetJobTheme("Mystic_Male");
            set => SetJobTheme("Mystic_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Mystic_Female
        {
            get => GetJobTheme("Mystic_Female");
            set => SetJobTheme("Mystic_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Geomancer_Male
        {
            get => GetJobTheme("Geomancer_Male");
            set => SetJobTheme("Geomancer_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Geomancer_Female
        {
            get => GetJobTheme("Geomancer_Female");
            set => SetJobTheme("Geomancer_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Dragoon_Male
        {
            get => GetJobTheme("Dragoon_Male");
            set => SetJobTheme("Dragoon_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Dragoon_Female
        {
            get => GetJobTheme("Dragoon_Female");
            set => SetJobTheme("Dragoon_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Samurai_Male
        {
            get => GetJobTheme("Samurai_Male");
            set => SetJobTheme("Samurai_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Samurai_Female
        {
            get => GetJobTheme("Samurai_Female");
            set => SetJobTheme("Samurai_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Ninja_Male
        {
            get => GetJobTheme("Ninja_Male");
            set => SetJobTheme("Ninja_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Ninja_Female
        {
            get => GetJobTheme("Ninja_Female");
            set => SetJobTheme("Ninja_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Calculator_Male
        {
            get => GetJobTheme("Calculator_Male");
            set => SetJobTheme("Calculator_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Calculator_Female
        {
            get => GetJobTheme("Calculator_Female");
            set => SetJobTheme("Calculator_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Bard_Male
        {
            get => GetJobTheme("Bard_Male");
            set => SetJobTheme("Bard_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Dancer_Female
        {
            get => GetJobTheme("Dancer_Female");
            set => SetJobTheme("Dancer_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Mime_Male
        {
            get => GetJobTheme("Mime_Male");
            set => SetJobTheme("Mime_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Mime_Female
        {
            get => GetJobTheme("Mime_Female");
            set => SetJobTheme("Mime_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Chemist_Male
        {
            get => GetJobTheme("Chemist_Male");
            set => SetJobTheme("Chemist_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Chemist_Female
        {
            get => GetJobTheme("Chemist_Female");
            set => SetJobTheme("Chemist_Female", value);
        }

        // Story Character Properties (string-based themes)
        [Newtonsoft.Json.JsonProperty("RamzaChapter1")]
        public string RamzaChapter1
        {
            get => GetStoryCharacterTheme("RamzaChapter1");
            set => SetStoryCharacterTheme("RamzaChapter1", value);
        }

        [Newtonsoft.Json.JsonProperty("RamzaChapter2")]
        public string RamzaChapter2
        {
            get => GetStoryCharacterTheme("RamzaChapter2");
            set => SetStoryCharacterTheme("RamzaChapter2", value);
        }

        [Newtonsoft.Json.JsonProperty("RamzaChapter34")]
        public string RamzaChapter34
        {
            get => GetStoryCharacterTheme("RamzaChapter34");
            set => SetStoryCharacterTheme("RamzaChapter34", value);
        }

        [Newtonsoft.Json.JsonProperty("Agrias")]
        public string Agrias
        {
            get => GetStoryCharacterTheme("Agrias");
            set => SetStoryCharacterTheme("Agrias", value);
        }

        [Newtonsoft.Json.JsonProperty("Orlandeau")]
        public string Orlandeau
        {
            get => GetStoryCharacterTheme("Orlandeau");
            set => SetStoryCharacterTheme("Orlandeau", value);
        }

        [Newtonsoft.Json.JsonProperty("Cloud")]
        public string Cloud
        {
            get => GetStoryCharacterTheme("Cloud");
            set => SetStoryCharacterTheme("Cloud", value);
        }

        [Newtonsoft.Json.JsonProperty("Mustadio")]
        public string Mustadio
        {
            get => GetStoryCharacterTheme("Mustadio");
            set => SetStoryCharacterTheme("Mustadio", value);
        }

        [Newtonsoft.Json.JsonProperty("Reis")]
        public string Reis
        {
            get => GetStoryCharacterTheme("Reis");
            set => SetStoryCharacterTheme("Reis", value);
        }

        [Newtonsoft.Json.JsonProperty("Rapha")]
        public string Rapha
        {
            get => GetStoryCharacterTheme("Rapha");
            set => SetStoryCharacterTheme("Rapha", value);
        }

        [Newtonsoft.Json.JsonProperty("Marach")]
        public string Marach
        {
            get => GetStoryCharacterTheme("Marach");
            set => SetStoryCharacterTheme("Marach", value);
        }

        [Newtonsoft.Json.JsonProperty("Beowulf")]
        public string Beowulf
        {
            get => GetStoryCharacterTheme("Beowulf");
            set => SetStoryCharacterTheme("Beowulf", value);
        }

        [Newtonsoft.Json.JsonProperty("Meliadoul")]
        public string Meliadoul
        {
            get => GetStoryCharacterTheme("Meliadoul");
            set => SetStoryCharacterTheme("Meliadoul", value);
        }

        // Helper methods
        public JobMetadata GetJobMetadata(string jobKey)
        {
            return _jobMetadata.GetValueOrDefault(jobKey);
        }

        public string GetDisplayName(string characterName)
        {
            switch (characterName)
            {
                case "RamzaChapter1":
                    return "Ramza (Chapter 1)";
                case "RamzaChapter2":
                    return "Ramza (Chapter 2)";
                case "RamzaChapter34":
                    return "Ramza (Chapter 3 & 4)";
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
