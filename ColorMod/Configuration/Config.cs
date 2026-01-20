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
        // Ramza HSL color settings
        public RamzaHslSettings RamzaColors { get; set; } = new();

        // Dictionary to store all generic job themes
        private Dictionary<string, string> _jobThemes = new();

        // Dictionary to store all story character themes
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

        // WotL Jobs Properties
        [Newtonsoft.Json.JsonIgnore]
        public string DarkKnight_Male
        {
            get => GetJobTheme("DarkKnight_Male");
            set => SetJobTheme("DarkKnight_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string DarkKnight_Female
        {
            get => GetJobTheme("DarkKnight_Female");
            set => SetJobTheme("DarkKnight_Female", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string OnionKnight_Male
        {
            get => GetJobTheme("OnionKnight_Male");
            set => SetJobTheme("OnionKnight_Male", value);
        }

        [Newtonsoft.Json.JsonIgnore]
        public string OnionKnight_Female
        {
            get => GetJobTheme("OnionKnight_Female");
            set => SetJobTheme("OnionKnight_Female", value);
        }

        // Story Character Properties (string-based themes)
        [Newtonsoft.Json.JsonProperty("RamzaChapter1")]
        public string RamzaChapter1
        {
            get => GetStoryCharacterTheme("RamzaChapter1");
            set => SetStoryCharacterTheme("RamzaChapter1", value);
        }

        [Newtonsoft.Json.JsonProperty("RamzaChapter23")]
        public string RamzaChapter23
        {
            get => GetStoryCharacterTheme("RamzaChapter23");
            set => SetStoryCharacterTheme("RamzaChapter23", value);
        }

        [Newtonsoft.Json.JsonProperty("RamzaChapter4")]
        public string RamzaChapter4
        {
            get => GetStoryCharacterTheme("RamzaChapter4");
            set => SetStoryCharacterTheme("RamzaChapter4", value);
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
